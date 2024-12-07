using System.Collections.Concurrent;
using System.IO.Compression;
using System.Diagnostics;
using ShellProgressBar;
using BoothDownloader.Web;
using Discord.Common.Helpers;
using Microsoft.Extensions.Logging;

namespace BoothDownloader.Miscellaneous;

public static class Utils
{
    public static async Task DownloadFileAsync(string url, string destinationPath, IProgress<double> progress, CancellationToken cancellationToken = default)
    {
        var response = await BoothHttpClientManager.AnonymousHttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        var contentLength = response.Content.Headers.ContentLength ?? -1;

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var buffer = new byte[81920];
        var totalBytesRead = 0L;
        long bytesRead;
        await using var fileStream = File.Create(destinationPath);
        do
        {
            bytesRead = await stream.ReadAsync(buffer, cancellationToken);
            if (bytesRead <= 0) continue;
            await fileStream.WriteAsync(buffer.AsMemory(0, (int)bytesRead), cancellationToken);
            totalBytesRead += bytesRead;
            progress.Report((double)totalBytesRead / contentLength);
        } while (bytesRead > 0);
    }

    public static string GetUniqueFilename(string binaryDir, string filename, ConcurrentBag<string> files, ProgressBar progressBar)
    {
        int counter = 0;
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filename);
        string extension = Path.GetExtension(filename);

        string newFilename = $"{fileNameWithoutExtension}{extension}";

        while (File.Exists(Path.Combine(binaryDir, newFilename)) || files.Contains(newFilename))
        {
            counter++;
            newFilename = $"{fileNameWithoutExtension} ({counter}){extension}";
            progressBar.WriteLine($"Duplicate file found, extending name {filename} => {newFilename}");
        }

        return newFilename;
    }

    public static bool TryDeleteDirectoryWithRetry(string path, out Exception? exception)
    {
        return TryActionWithRetry(actionDescription: $"delete directory: {path}",
            action: () =>
            {
                if(Directory.Exists(path))
                    Directory.Delete(path, recursive: true);
            },
            exception: out exception);
    }

    private static bool TryDeleteFileWithRetry(string path, out Exception? exception)
    {
        return TryActionWithRetry(actionDescription: $"delete file: {path}",
            action: () =>
            {
                if(File.Exists(path))
                    File.Delete(path);
            },
            exception: out exception);
    }

    private static bool TryActionWithRetry(string actionDescription, Action action, out Exception? exception)
    {
        exception = null;
        while (true)
        {
            try
            {
                action();
                return true;
            }
            catch (Exception ex)
            {
                exception = ex;
                var userDecision = GetUserDecision($"Failed to {actionDescription}. Error: {ex.Message}");
                if (userDecision == UserDecision.Retry)
                {
                    continue;
                }

                if (userDecision == UserDecision.Skip)
                {
                    return false;
                }

                if (userDecision == UserDecision.Quit)
                {
                    Environment.Exit(0);
                }
            }
        }
    }


    private static UserDecision GetUserDecision(string message)
    {
        Console.WriteLine($"{message}\nChoose an option: Retry (r), Skip (s), Quit (q)");
        while (true)
        {
            var userInput = Console.ReadKey(intercept: true).Key;
            switch (userInput)
            {
                case ConsoleKey.R:
                    return UserDecision.Retry;
                case ConsoleKey.S:
                    return UserDecision.Skip;
                case ConsoleKey.Q:
                    return UserDecision.Quit;
                default:
                    Console.WriteLine("Invalid option. Please choose: Retry (r), Skip (s), Quit (q)");
                    break;
            }
        }
    }

    private enum UserDecision
    {
        Retry,
        Skip,
        Quit
    }
    [Conditional("WINDOWS_BUILD")]
    internal static void OpenDownloadFolder(string folderPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, folderPath),
            UseShellExecute = true,
            Verb = "runas"
        };
        try
        {
            Process.Start(startInfo);
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            LoggerHelper.GlobalLogger.LogError("Error: " + ex.Message);
        }
    }

    internal static void AutoZip(string entryDirectory)
    {
        var zipFileName = entryDirectory + ".zip";
        if (File.Exists(zipFileName))
        {
            LoggerHelper.GlobalLogger.LogInformation("File already exists, deleting: {fileName}", zipFileName);
            if (!TryDeleteFileWithRetry(zipFileName, out var exception))
            {
                LoggerHelper.GlobalLogger.LogError(exception, "Failed to delete file: {fileName}", zipFileName);
            }
        }

        LoggerHelper.GlobalLogger.LogInformation("Zipping");
        ZipFile.CreateFromDirectory(entryDirectory, zipFileName);
        LoggerHelper.GlobalLogger.LogInformation("Zipped");


        if (!TryDeleteDirectoryWithRetry(entryDirectory, out var dirException))
        {
            LoggerHelper.GlobalLogger.LogError(dirException, "Failed to delete directory after zipping: {directoryName}", entryDirectory);
            LoggerHelper.GlobalLogger.LogInformation("ENVFileDIR: {directoryPath}", entryDirectory);
        }

        LoggerHelper.GlobalLogger.LogInformation("ENVFilePATH: {filePath}", zipFileName);
    }
}
