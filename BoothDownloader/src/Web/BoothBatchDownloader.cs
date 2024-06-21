using System.Collections.Concurrent;
using System.IO.Compression;
using BoothDownloader.Configuration;
using BoothDownloader.Miscellaneous;
using Discord.Common.Helpers;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ShellProgressBar;
using Unidecode.NET;

namespace BoothDownloader.Web;

public static class BoothBatchDownloader
{
    private const string BoothPageJson = "_BoothPage.json";
    private const string BoothInnerHtmlList = "_BoothInnerHtmlList.json";
    private const string BatchDownloadDebug = "_Debug_BatchDownloadDebug.json";

    public static async Task DownloadAsync(Dictionary<string, BoothItemAssets> boothItems, string outputDirectory, int maxRetries, bool debug = false, CancellationToken cancellationToken = default)
    {
        var outputDir = Directory.CreateDirectory(outputDirectory).ToString();

        if (debug)
        {
            LoggerHelper.GlobalLogger.LogInformation("Writing {fileName}", BatchDownloadDebug);
            File.WriteAllText(Path.Combine(outputDir, "_Debug_BatchDownloadDebug.json"), JsonConvert.SerializeObject(boothItems, Formatting.Indented));
        }

        var options = BoothProgressBarOptions.Layer1;
        options.CollapseWhenFinished = false;

        var parentOptions = BoothProgressBarOptions.Layer2;
        parentOptions.CollapseWhenFinished = false;

        var childOptions = BoothProgressBarOptions.Layer3;
        parentOptions.CollapseWhenFinished = true;

        foreach (var boothItem in boothItems)
        {
            LoggerHelper.GlobalLogger.LogInformation("Downloading {boothId}", boothItem.Key);

            if (Directory.Exists(Path.Combine(outputDir, boothItem.Key)))
            {
                LoggerHelper.GlobalLogger.LogInformation("Directory already exists, deleting: {directoryName}", Path.Combine(outputDir, boothItem.Key));
                if (!Utils.TryDeleteDirectoryWithRetry(Path.Combine(outputDir, boothItem.Key), out var exception))
                {
                    LoggerHelper.GlobalLogger.LogError(exception, "Failed to delete directory: {directoryName}", Path.Combine(outputDir, boothItem.Key));
                    continue;
                }
            }

            var entryDir = Directory.CreateDirectory(Path.Combine(outputDir, boothItem.Key)).ToString();
            ConcurrentBag<string> entryDirFiles = [BoothPageJson, BoothInnerHtmlList];

            if (!string.IsNullOrWhiteSpace(boothItem.Value.BoothPageJson))
            {
                LoggerHelper.GlobalLogger.LogInformation("Writing {fileName}", BoothPageJson);
                File.WriteAllText(Path.Combine(entryDir, BoothPageJson), boothItem.Value.BoothPageJson);
            }
            else if (boothItem.Value.InnerHtml.Count > 0)
            {
                LoggerHelper.GlobalLogger.LogInformation("Writing {fileName}", BoothInnerHtmlList);
                File.WriteAllText(Path.Combine(entryDir, BoothInnerHtmlList), JsonConvert.SerializeObject(boothItem.Value.InnerHtml));
            }

            var totalCount = boothItem.Value.Downloadables.Count + boothItem.Value.Images.Count;

            using (var progressBar = new ProgressBar(totalCount, "Overall Progress", options))
            {
                var allTasks = new List<Task>();

                int remainingImages = boothItem.Value.Images.Count;
                var imageTaskBar = progressBar.Spawn(boothItem.Value.Images.Count, $"Images ({remainingImages}/{boothItem.Value.Images.Count} Left)", parentOptions);
                allTasks.AddRange(boothItem.Value.Images.Select(url => Task.Run(async () =>
                {
                    var filename = new Uri(url).Segments.Last();

                    string uniqueFilename;
                    lock (entryDirFiles)
                    {
                        uniqueFilename = Utils.GetUniqueFilename(entryDir, filename, entryDirFiles, progressBar);
                        entryDirFiles.Add(uniqueFilename);
                    }

                    var child = imageTaskBar.Spawn(10000, uniqueFilename, childOptions);
                    var childProgress = new ChildProgressBarProgress(child);

                    await Utils.DownloadFileAsync(url, Path.Combine(entryDir, uniqueFilename), childProgress, cancellationToken);

                    imageTaskBar.Tick();
                    progressBar.Tick();

                    Interlocked.Decrement(ref remainingImages);
                    imageTaskBar.Message = $"Images ({remainingImages}/{boothItem.Value.Images.Count} Left)";
                })));

                if (boothItem.Value.Images.Count == 0)
                {
                    imageTaskBar.Tick();
                }

                if (boothItem.Value.Downloadables.Count > 0 && BoothHttpClientManager.IsAnonymous)
                {
                    progressBar.WriteLine("Skipping downloads as anonymous user.");
                }
                else
                {
                    var binaryDir = Directory.CreateDirectory(Path.Combine(entryDir, "Binary")).ToString();
                    ConcurrentBag<string> binaryDirFiles = [];

                    int remainingDownloads = boothItem.Value.Downloadables.Count;
                    var downloadTaskBar = progressBar.Spawn(boothItem.Value.Downloadables.Count, $"Downloads ({remainingDownloads}/{boothItem.Value.Downloadables.Count} Left)", parentOptions);
                    allTasks.AddRange(boothItem.Value.Downloadables.Select(url => Task.Run(async () =>
                    {
                        var resp = await BoothHttpClientManager.HttpClient.GetAsync(url, cancellationToken);
                        var redirectUrl = resp.Headers.Location!.ToString();
                        var encodedFilename = new Uri(redirectUrl).Segments.Last();
                        var filename = Uri.UnescapeDataString(encodedFilename);
                        
                        string uniqueFilename;
                        lock (binaryDirFiles)
                        {
                            uniqueFilename = Utils.GetUniqueFilename(binaryDir, filename, binaryDirFiles, progressBar);
                            binaryDirFiles.Add(uniqueFilename);
                        }

                        var success = false;
                        var retryCount = 0;
                        var child = downloadTaskBar.Spawn(10000, uniqueFilename.Unidecode(), childOptions);
                        var childProgress = new ChildProgressBarProgress(child);
                        while (!success && retryCount < maxRetries)
                        {
                            try
                            {
                                await Utils.DownloadFileAsync(redirectUrl, Path.Combine(binaryDir.ToString(), uniqueFilename), childProgress, cancellationToken);
                                Interlocked.Decrement(ref remainingDownloads);
                                downloadTaskBar.Message = $"Downloads ({remainingDownloads}/{boothItem.Value.Downloadables.Count} Left)";
                                success = true;
                            }
                            catch (Exception ex)
                            {
                                retryCount++;
                                child.Message = $"Failed to download {url}. Retry attempt {retryCount}/{maxRetries}. Error: {ex.Message}";
                                await Task.Delay(5000, cancellationToken);
                            }
                        }

                        downloadTaskBar.Tick();
                        progressBar.Tick();

                        if (success) return;

                        child.Message = $"Failed to download {url} after {maxRetries} attempts.";
                        progressBar.WriteErrorLine($"Failed to download {url} after {maxRetries} attempts.");
                    })));

                    if (boothItem.Value.Downloadables.Count == 0)
                    {
                        downloadTaskBar.Tick();
                    }
                }

                if(allTasks.Count > 0)
                {
                    await Task.WhenAll(allTasks);
                }
            }

            Console.WriteLine();

            if (BoothConfig.Instance.AutoZip)
            {
                var zipFileName = entryDir + ".zip";
                if (File.Exists(zipFileName))
                {
                    LoggerHelper.GlobalLogger.LogInformation("File already exists, deleting: {fileName}", zipFileName);
                    if (!Utils.TryDeleteFileWithRetry(zipFileName, out var exception))
                    {
                        LoggerHelper.GlobalLogger.LogError(exception, "Failed to delete file: {fileName}", zipFileName);
                        continue;
                    }
                }

                LoggerHelper.GlobalLogger.LogInformation("Zipping");
                ZipFile.CreateFromDirectory(entryDir, zipFileName);
                LoggerHelper.GlobalLogger.LogInformation("Zipped");


                if (!Utils.TryDeleteDirectoryWithRetry(entryDir, out var dirException))
                {
                    LoggerHelper.GlobalLogger.LogError(dirException, "Failed to delete directory after zipping: {directoryName}", entryDir);
                    LoggerHelper.GlobalLogger.LogInformation("ENVFileDIR: {directoryPath}", entryDir);
                    continue;
                }

                LoggerHelper.GlobalLogger.LogInformation("ENVFilePATH: {filePath}", zipFileName);
            }
            else
            {
                LoggerHelper.GlobalLogger.LogInformation("ENVFileDIR: {directoryPath}", entryDir);
            }
        }
    }
}
