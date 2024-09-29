using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Threading;

class Program
{
    static List<string[]> prevBandwidthData = new List<string[]>();
    static List<string[]> prevEarningsData = new List<string[]>();
    static List<string[]> prevWorkloadsData = new List<string[]>();
    static List<string[]> prevWalletData = new List<string[]>();
    static string directoryPath = @"C:\ProgramData\Salad\logs"; // Path to logs folder

    static void Main(string[] args)
    {
        // Welcome Message
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("Welcome to USLR! \nPlease Wait a moment while we get things ready.");
        Console.ResetColor();

        Console.ForegroundColor = ConsoleColor.Blue;
        Console.Write("Checking for logs at: ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(directoryPath);
        Console.ResetColor();

        // Check for logs
        if (!Directory.Exists(directoryPath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Error: Log directory not found!");
            Console.ResetColor();
            return;
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Log directory found!");
            Console.ResetColor();
        }

        Console.WriteLine("Press Enter to begin processing logs...");
        Console.ReadLine();

        while (true)
        {
            // Get the latest log file
            string latestLogFilePath = FindLatestLogFile(directoryPath);
            (string latestBandwidthLog, string oldestBandwidthLog) = FindLatestAndOldestBandwidthLog(directoryPath);
            string latestNDMLog = FindLatestNDMLog(directoryPath);

            // Extract information from logs
            var bandwidthData = latestBandwidthLog != null ? ExtractInfoFromBandwidthLog(latestBandwidthLog) : null;
            var (earningsData, workloadsData, walletData) = ExtractInfoFromLogFile(latestLogFilePath);

            // Extract gateway from the oldest bandwidth log
            var gateway = oldestBandwidthLog != null ? ExtractGatewayFromOldestBandwidthLog(oldestBandwidthLog) : null;

            // Extract speeds from the NDM log
            var (uploadSpeed, downloadSpeed) = ExtractSpeedsFromNDMLog(latestNDMLog);

            Console.Clear();

            // Display NDM Speeds
            DisplayNDMSpeeds(uploadSpeed, downloadSpeed);

            // Display Bandwidth Data
            DisplayBandwidthData(bandwidthData);

            // Display Workloads Data
            DisplayWorkloadsData(workloadsData, gateway);

            // Display Earnings Data
            DisplayEarningsData(earningsData);

            // Display Wallet Data
            DisplayWalletData(walletData);

            // Display Current Log File
            DisplayCurrentLog(latestLogFilePath);

            // Update console title with latest predicted earnings and balance
            if (earningsData.Count > 0 && walletData.Count > 0)
            {
                var latestEarnings = earningsData[0][1];
                var latestBalance = walletData[0][1];
                Console.Title = $"Predicted Earnings: {latestEarnings} - Current Balance: {latestBalance}";
            }
            Thread.Sleep(10000);
        }
    }

    static string FindLatestLogFile(string directory)
    {
        var logFiles = Directory.GetFiles(directory, "log-*.txt");
        return logFiles.OrderByDescending(File.GetLastWriteTime).FirstOrDefault();
    }

    static (string, string) FindLatestAndOldestBandwidthLog(string directory)
    {
        var bandwidthFolders = Directory.GetDirectories(directory, "Bandwidth*");
        if (bandwidthFolders.Length == 0) return (null, null);

        var latestFolder = bandwidthFolders.OrderByDescending(Directory.GetLastWriteTime).FirstOrDefault();
        var bandwidthLogFiles = Directory.GetFiles(latestFolder, "bandwidth*");

        var latestBandwidthLogFile = bandwidthLogFiles.OrderByDescending(File.GetLastWriteTime).FirstOrDefault();
        var oldestBandwidthLogFile = bandwidthLogFiles.OrderBy(File.GetLastWriteTime).FirstOrDefault();

        return (latestBandwidthLogFile, oldestBandwidthLogFile);
    }

    static string FindLatestNDMLog(string directory)
    {
        var ndmFolder = Directory.GetDirectories(directory, "ndm");
        if (ndmFolder.Length == 0)
        {
            Console.WriteLine("No NDM folder found.");
            return null;
        }

        var ndmLogFiles = Directory.GetFiles(ndmFolder[0], "ndm-*.log");
        return ndmLogFiles.OrderByDescending(File.GetLastWriteTime).FirstOrDefault();
    }

    static (List<string[]>, List<string[]>, List<string[]>) ExtractInfoFromLogFile(string logFilePath)
    {
        var predictedEarningsMatches = new List<string[]>();
        var workloadManagerMatches = new List<string[]>();
        var walletMatches = new List<string[]>();

        if (logFilePath != null)
        {
            // Open the file in read-only mode due to Salad-Bowl.Service writing to it
            using (var stream = new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream))
            {
                var lines = reader.ReadToEnd().Split(new[] { Environment.NewLine }, StringSplitOptions.None).Reverse();
                foreach (var line in lines)
                {
                    var predictedEarningsMatch = Regex.Match(line, @"(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}).*?Predicted Earnings Report: (.+)");
                    if (predictedEarningsMatch.Success)
                        predictedEarningsMatches.Add(new[] { predictedEarningsMatch.Groups[1].Value, predictedEarningsMatch.Groups[2].Value });

                    var workloadManagerMatch = Regex.Match(line, @"(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}).*?WorkloadManager: Added.*?Name = (.*?), Image = (.*?)(?:,|\})");
                    if (workloadManagerMatch.Success)
                        workloadManagerMatches.Add(new[] { workloadManagerMatch.Groups[1].Value, workloadManagerMatch.Groups[2].Value, workloadManagerMatch.Groups[3].Value });

                    var walletMatch = Regex.Match(line, @"(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}).*?Wallet: Current\(([\d.]+)\), Predicted\(([-\d.]+)\)");
                    if (walletMatch.Success)
                        walletMatches.Add(new[] { walletMatch.Groups[1].Value, walletMatch.Groups[2].Value, walletMatch.Groups[3].Value });
                }
            }
        }

        return (predictedEarningsMatches, workloadManagerMatches, walletMatches);
    }

    static List<string[]> ExtractInfoFromBandwidthLog(string bandwidthLog)
    {
        var bandwidthLogData = new List<string[]>();
        if (bandwidthLog != null)
        {
            var lines = File.ReadAllLines(bandwidthLog).Reverse();
            foreach (var line in lines)
            {
                var bandwidthLogMatch = Regex.Match(line, @"(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}).*?""BidirThroughput"":(-?\d+).*?""ErrorRate"":(-?\d+)");
                if (bandwidthLogMatch.Success)
                    bandwidthLogData.Add(new[] { bandwidthLogMatch.Groups[1].Value, bandwidthLogMatch.Groups[2].Value, bandwidthLogMatch.Groups[3].Value });
            }
        }

        return bandwidthLogData;
    }

    static string ExtractGatewayFromOldestBandwidthLog(string oldestBandwidthLog)
    {
        var lines = File.ReadAllLines(oldestBandwidthLog);
        foreach (var line in lines)
        {
            var gatewayMatch = Regex.Match(line, @"-server_host_port (.*?):443");
            if (gatewayMatch.Success)
            {
                return gatewayMatch.Groups[1].Value;
            }
        }

        return null;
    }
    static (string, string) ExtractSpeedsFromNDMLog(string ndmLogFile)
    {
        string latestUploadSpeed = "N/A";
        string latestDownloadSpeed = "N/A";

        if (ndmLogFile != null)
        {
            var lines = File.ReadAllLines(ndmLogFile).Reverse();

            foreach (var line in lines)
            {
                var downloadSpeedMatch = Regex.Match(line, @"(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} [+-]\d{2}:\d{2}) \[INF\] Download rate:\s+(\d+\.\d+) Mbps");
                if (downloadSpeedMatch.Success)
                {
                    latestDownloadSpeed = downloadSpeedMatch.Groups[2].Value;
                }

                var uploadSpeedMatch = Regex.Match(line, @"(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} [+-]\d{2}:\d{2}) \[INF\] Upload rate:\s+(\d+\.\d+) Mbps");
                if (uploadSpeedMatch.Success)
                {
                    latestUploadSpeed = uploadSpeedMatch.Groups[2].Value;
                }

                if (latestDownloadSpeed != "N/A" && latestUploadSpeed != "N/A")
                {
                    break;
                }
            }
        }

        return (latestUploadSpeed, latestDownloadSpeed);
    }

    static void DisplayBandwidthData(List<string[]> bandwidthData)
    {
        if (bandwidthData == null || bandwidthData.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\nSGS Bandwidth Usage/Errors (No Data Found):");
            Console.ResetColor();
        }
        else if (!bandwidthData.SequenceEqual(prevBandwidthData))
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\nSGS Bandwidth Usage/Errors (Updated Data):");
            prevBandwidthData = bandwidthData;
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\nSGS Bandwidth Usage/Errors (Old Data, Not Updated):");
            Console.ResetColor();
        }

        var table = bandwidthData.Take(5).Select((entry, index) =>
            $"{index + 1}. {entry[0]} | Throughput: {int.Parse(entry[1]) / 250000.0:F2} Mbps | ErrorRate: {entry[2]}").ToArray();
        foreach (var row in table)
        {
            Console.WriteLine(row);
        }
    }

    static void DisplayWorkloadsData(List<string[]> workloadsData, string gateway)
    {
        if (!workloadsData.SequenceEqual(prevWorkloadsData))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\nWorkloads Information (Updated Data):");
            prevWorkloadsData = workloadsData;
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\nWorkloads Information (Old Data, Not Updated):");
            Console.ResetColor();
        }

        var table = workloadsData.Take(5).Select((entry, index) =>
            $"{index + 1}. {entry[0]} | {entry[1]} | {(entry[1].ToLower().Contains("bandwidth") ? gateway : entry[2])}").ToArray();
        foreach (var row in table)
        {
            Console.WriteLine(row);
        }
    }

    static void DisplayEarningsData(List<string[]> earningsData)
    {
        if (!earningsData.SequenceEqual(prevEarningsData))
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\nContainer Earnings Report (Updated Data):");
            prevEarningsData = earningsData;
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\nContainer Earnings Report (Old Data, Not Updated):");
            Console.ResetColor();
        }

        var table = earningsData.Take(5).Select((entry, index) =>
            $"{index + 1}. {entry[0]} | Predicted Earnings: {entry[1]}").ToArray();
        foreach (var row in table)
        {
            Console.WriteLine(row);
        }
    }

    static void DisplayWalletData(List<string[]> walletData)
    {
        if (!walletData.SequenceEqual(prevWalletData))
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("\nWallet Information (Updated Data):");
            prevWalletData = walletData;
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\nWallet Information (Old Data, Not Updated):");
            Console.ResetColor();
        }

        var table = walletData.Take(5).Select((entry, index) =>
            $"{index + 1}. {entry[0]} | Current: {entry[1]} | Predicted: {entry[2]}").ToArray();
        foreach (var row in table)
        {
            Console.WriteLine(row);
        }
    }
    static void DisplayCurrentLog(string latestLogFilePath)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\nCurrent log file:");
        Console.ResetColor();
        Console.WriteLine(latestLogFilePath);
    }

    static void DisplayNDMSpeeds(string uploadSpeed, string downloadSpeed)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\nNDM Speeds:");
        Console.ResetColor();
        Console.WriteLine($"Upload: {uploadSpeed} Mbps | Download: {downloadSpeed} Mbps");
    }
}
