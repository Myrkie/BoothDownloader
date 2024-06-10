using System.Collections.Concurrent;
using System.IO.Compression;
using BoothDownloader.Configuration;
using BoothDownloader.Miscellaneous;
using Newtonsoft.Json;
using ShellProgressBar;

namespace BoothDownloader.Web
{
    public static class BoothBatchDownloader
    {
        private const string BoothPageJson = "_BoothPage.json";
        private const string BoothInnerHtmlList = "_BoothInnerHtmlList.json";

        public static async Task DownloadAsync(Dictionary<string, BoothItemAssets> boothItems, string outputDirectory, int maxRetries, CancellationToken cancellationToken = default)
        {
            var outputDir = Directory.CreateDirectory(outputDirectory).ToString();

            var options = BoothProgressBarOptions.Layer1;
            options.CollapseWhenFinished = false;

            var parentOptions = BoothProgressBarOptions.Layer2;
            parentOptions.CollapseWhenFinished = false;

            var childOptions = BoothProgressBarOptions.Layer3;

            foreach (var boothItem in boothItems)
            {
                Console.WriteLine($"Downloading {boothItem.Key}");

                if (Directory.Exists(Path.Combine(outputDir, boothItem.Key)))
                {
                    Console.WriteLine("Directory already exists. Deleting...");
                    Directory.Delete(Path.Combine(outputDir, boothItem.Key), true);
                }

                var entryDir = Directory.CreateDirectory(Path.Combine(outputDir, boothItem.Key)).ToString();
                var binaryDir = Directory.CreateDirectory(Path.Combine(entryDir, "Binary")).ToString();
                ConcurrentBag<string> entryDirFiles = [BoothPageJson, BoothInnerHtmlList];
                ConcurrentBag<string> binaryDirFiles = [];

                if (!string.IsNullOrWhiteSpace(boothItem.Value.BoothPageJson))
                {
                    Console.WriteLine($"Writing {BoothPageJson}...");
                    File.WriteAllText(Path.Combine(entryDir, BoothPageJson), boothItem.Value.BoothPageJson);
                }
                else if (boothItem.Value.InnerHtml.Count > 0)
                {
                    Console.WriteLine($"Writing {BoothInnerHtmlList}...");
                    File.WriteAllText(Path.Combine(entryDir, BoothInnerHtmlList), JsonConvert.SerializeObject(boothItem.Value.InnerHtml));
                }

                var totalCount = boothItem.Value.Downloadables.Count + boothItem.Value.Images.Count;

                using(var progressBar = new ProgressBar(totalCount, "Overall Progress", options))
                {
                    int remainingImages = boothItem.Value.Images.Count;
                    var imageTaskBar = progressBar.Spawn(boothItem.Value.Images.Count, $"Images ({remainingImages}/{boothItem.Value.Images.Count} Left)", parentOptions);
                    var imageTasks = boothItem.Value.Images.Select(url => Task.Run(async () =>
                    {
                        var filename = new Uri(url).Segments.Last();

                        string uniqueFilename;
                        lock (entryDirFiles)
                        {
                            uniqueFilename = Utils.GetUniqueFilename(entryDir, filename, entryDirFiles);
                            entryDirFiles.Add(uniqueFilename);
                        }

                        var child = imageTaskBar.Spawn(10000, uniqueFilename, childOptions);
                        var childProgress = new ChildProgressBarProgress(child);

                        await Utils.DownloadFileAsync(url, Path.Combine(entryDir, uniqueFilename), childProgress, cancellationToken);

                        imageTaskBar.Tick();
                        progressBar.Tick();

                        Interlocked.Decrement(ref remainingImages);
                        imageTaskBar.Message = $"Images ({remainingImages}/{boothItem.Value.Images.Count} Left)";
                    })).ToArray();

                    if (boothItem.Value.Images.Count == 0)
                    {
                        imageTaskBar.Tick();
                    }


                    int remainingDownloads = boothItem.Value.Downloadables.Count;
                    var downloadTaskBar = progressBar.Spawn(boothItem.Value.Downloadables.Count, $"Downloads ({remainingDownloads}/{boothItem.Value.Downloadables.Count} Left)", parentOptions);
                    var downloadTasks = boothItem.Value.Downloadables.Select(url => Task.Run(async () =>
                    {
                        var resp = await BoothHttpClientManager.HttpClient.GetAsync(url, cancellationToken);
                        var redirectUrl = resp.Headers.Location!.ToString();
                        var filename = new Uri(redirectUrl).Segments.Last();

                        string uniqueFilename;
                        lock (binaryDirFiles)
                        {
                            uniqueFilename = Utils.GetUniqueFilename(binaryDir, filename, binaryDirFiles);
                            binaryDirFiles.Add(uniqueFilename);
                        }

                        var success = false;
                        var retryCount = 0;
                        var child = downloadTaskBar.Spawn(10000, uniqueFilename, childOptions);
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
                    })).ToArray();

                    if (boothItem.Value.Downloadables.Count == 0)
                    {
                        downloadTaskBar.Tick();
                    }

                    var allTasks = imageTasks.Concat(downloadTasks).ToArray();
                    await Task.WhenAll(allTasks);

                }

                if (BoothConfig.Instance.AutoZip)
                {
                    if (File.Exists(entryDir + ".zip"))
                    {
                        Console.WriteLine("File already exists. Deleting...");
                        File.Delete(entryDir + ".zip");
                    }

                    Console.WriteLine("Zipping!");
                    ZipFile.CreateFromDirectory(entryDir, entryDir + ".zip");
                    Console.WriteLine("Zipped!");

                    Directory.Delete(entryDir, true);

                    Console.WriteLine("ENV-Completed-DL-Zip: " + entryDir + ".zip");
                }
                else
                {
                    Console.WriteLine("ENV-Completed-DL-Dir: " + entryDir);
                }
            }
        }
    }
}
