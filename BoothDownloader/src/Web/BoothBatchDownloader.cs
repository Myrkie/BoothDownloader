using System.Collections.Concurrent;
using System.IO.Compression;
using BoothDownloader.Configuration;
using BoothDownloader.Miscellaneous;
using Discord.Common.Helpers;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ShellProgressBar;
using Unidecode.NET;

namespace BoothDownloader.Web;

public static class BoothBatchDownloader
{
    private const string BoothPageJson = "_BoothPage.json";
    private const string BoothInnerHtmlList = "_BoothInnerHtmlList.json";
    private const string BatchDownloadDebug = "_Debug_BatchDownloadDebug.json";

    public static async Task DownloadAsync(
        Dictionary<string, BoothItemAssets> boothItems,
        string outputDirectory,
        int maxRetries,
        bool debug = false,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        var outputDir = Directory.CreateDirectory(outputDirectory).ToString();
        var cache = BoothDownloadCache.Load(outputDir);

        if (debug)
        {
            LoggerHelper.GlobalLogger.LogInformation("Writing {fileName}", BatchDownloadDebug);
            File.WriteAllText(
                Path.Combine(outputDir, BatchDownloadDebug),
                JsonConvert.SerializeObject(boothItems, Formatting.Indented));
        }

        if (force)
        {
            LoggerHelper.GlobalLogger.LogWarning(
                "Forced download enabled. Every selected item will be downloaded again; avoid this for large libraries.");
        }

        foreach (var boothItem in boothItems)
        {
            var boothId = boothItem.Key;
            cache.Items.TryGetValue(boothId, out var cachedItem);

            var currentItem = CreateCurrentSnapshot(boothItem.Value, cachedItem);
            var change = GetChange(outputDir, boothId, currentItem, cachedItem, force);

            if (change == DownloadChange.Baseline)
            {
                PreserveExistingMetadata(outputDir, boothId, currentItem);
                LoggerHelper.GlobalLogger.LogWarning(
                    "Existing item {boothId} has no download cache. Adopting it without downloading; use --force with this item if you know it is outdated.",
                    boothId);
                cache.Items[boothId] = currentItem;
                cache.Save(outputDir);
                continue;
            }

            if (change == DownloadChange.None)
            {
                LoggerHelper.GlobalLogger.LogInformation("Skipping unchanged item {boothId}", boothId);
                cache.Items[boothId] = currentItem;
                cache.Save(outputDir);
                continue;
            }

            LoggerHelper.GlobalLogger.LogInformation(
                "Updating {boothId}: {change}", boothId, GetChangeDescription(change));

            var entryDir = PrepareEntryDirectory(outputDir, boothId, change);
            var operationSucceeded = await ApplyChangeAsync(
                currentItem,
                cachedItem,
                entryDir,
                maxRetries,
                change,
                cancellationToken);

            if (!operationSucceeded)
            {
                LoggerHelper.GlobalLogger.LogError(
                    "Update for {boothId} was incomplete. Its cache was not changed, so it will be retried next time.",
                    boothId);
                continue;
            }

            if (BoothConfig.Instance.AutoZip)
            {
                Utils.AutoZip(entryDir);
            }
            else
            {
                LoggerHelper.GlobalLogger.LogInformation("ENVFileDIR: {directoryPath}", entryDir);
            }

            cache.Items[boothId] = currentItem;
            cache.Save(outputDir);
        }
    }

    private static BoothDownloadCacheItem CreateCurrentSnapshot(
        BoothItemAssets item,
        BoothDownloadCacheItem? cachedItem)
    {
        var currentItem = BoothDownloadCacheItem.From(item);

        if (cachedItem != null)
        {
            currentItem.ImageFiles = CopyKnownFiles(currentItem.Images, cachedItem.ImageFiles);
            currentItem.DownloadableFiles = CopyKnownFiles(currentItem.Downloadables, cachedItem.DownloadableFiles);
        }

        if (cachedItem == null
            || !string.IsNullOrWhiteSpace(item.BoothPageJson)
            || string.IsNullOrWhiteSpace(cachedItem.BoothPageJson))
        {
            return currentItem;
        }

        // A delisted or temporarily unavailable page only exposes its library preview. Keep the
        // last complete metadata and image set instead of replacing it with that partial response.
        currentItem.BoothPageJson = cachedItem.BoothPageJson;
        currentItem.InnerHtml = cachedItem.InnerHtml;
        currentItem.Images = cachedItem.Images;
        currentItem.ImageFiles = new Dictionary<string, string>(cachedItem.ImageFiles, StringComparer.Ordinal);
        currentItem.ItemPageUnavailable = true;

        return currentItem;
    }

    private static Dictionary<string, string> CopyKnownFiles(
        IEnumerable<string> currentUrls,
        Dictionary<string, string> cachedFiles)
    {
        var currentUrlSet = currentUrls.ToHashSet(StringComparer.Ordinal);
        return cachedFiles
            .Where(file => currentUrlSet.Contains(file.Key))
            .ToDictionary(file => file.Key, file => file.Value, StringComparer.Ordinal);
    }

    private static DownloadChange GetChange(
        string outputDirectory,
        string boothId,
        BoothDownloadCacheItem currentItem,
        BoothDownloadCacheItem? cachedItem,
        bool force)
    {
        if (force || !OutputExists(outputDirectory, boothId))
        {
            return DownloadChange.Full;
        }

        if (cachedItem == null)
        {
            return DownloadChange.Baseline;
        }

        if (!SetEquals(currentItem.Downloadables, cachedItem.Downloadables))
        {
            return DownloadChange.Full;
        }

        if (!SetEquals(currentItem.Images, cachedItem.Images))
        {
            return DownloadChange.ImagesAndMetadata;
        }

        return MetadataEquals(currentItem, cachedItem)
            ? DownloadChange.None
            : DownloadChange.Metadata;
    }

    private static bool OutputExists(string outputDirectory, string boothId)
    {
        return BoothConfig.Instance.AutoZip
            ? File.Exists(Path.Combine(outputDirectory, boothId + ".zip"))
            : Directory.Exists(Path.Combine(outputDirectory, boothId));
    }

    private static void PreserveExistingMetadata(
        string outputDirectory,
        string boothId,
        BoothDownloadCacheItem currentItem)
    {
        if (!currentItem.ItemPageUnavailable)
        {
            return;
        }

        string? existingJson;
        if (BoothConfig.Instance.AutoZip)
        {
            using var archive = ZipFile.OpenRead(Path.Combine(outputDirectory, boothId + ".zip"));
            var entry = archive.GetEntry(BoothPageJson);
            if (entry == null)
            {
                return;
            }

            using var reader = new StreamReader(entry.Open());
            existingJson = reader.ReadToEnd();
        }
        else
        {
            var path = Path.Combine(outputDirectory, boothId, BoothPageJson);
            if (!File.Exists(path))
            {
                return;
            }

            existingJson = File.ReadAllText(path);
        }

        if (string.IsNullOrWhiteSpace(existingJson))
        {
            return;
        }

        currentItem.BoothPageJson = existingJson;
        var item = JsonConvert.DeserializeObject<BoothJsonItem>(existingJson);
        if (item?.Images == null)
        {
            return;
        }

        currentItem.Images = item.Images
            .Select(image => !string.IsNullOrWhiteSpace(image.Original) ? image.Original : image.Resized)
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => url!)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    private static bool SetEquals(IEnumerable<string> left, IEnumerable<string> right)
    {
        return left.ToHashSet(StringComparer.Ordinal).SetEquals(right);
    }

    private static bool MetadataEquals(BoothDownloadCacheItem left, BoothDownloadCacheItem right)
    {
        if (!string.IsNullOrWhiteSpace(left.BoothPageJson)
            || !string.IsNullOrWhiteSpace(right.BoothPageJson))
        {
            return JsonEquals(left.BoothPageJson, right.BoothPageJson);
        }

        return SetEquals(left.InnerHtml, right.InnerHtml);
    }

    private static bool JsonEquals(string left, string right)
    {
        if (string.Equals(left, right, StringComparison.Ordinal))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        try
        {
            return JToken.DeepEquals(JToken.Parse(left), JToken.Parse(right));
        }
        catch (JsonReaderException)
        {
            return false;
        }
    }

    private static string PrepareEntryDirectory(
        string outputDirectory,
        string boothId,
        DownloadChange change)
    {
        var entryPath = Path.Combine(outputDirectory, boothId);

        if (change == DownloadChange.Full)
        {
            if (Directory.Exists(entryPath)
                && !Utils.TryDeleteDirectoryWithRetry(entryPath, out var exception))
            {
                throw new IOException($"Failed to prepare item directory: {entryPath}", exception);
            }

            return Directory.CreateDirectory(entryPath).ToString();
        }

        if (BoothConfig.Instance.AutoZip)
        {
            if (Directory.Exists(entryPath)
                && !Utils.TryDeleteDirectoryWithRetry(entryPath, out var exception))
            {
                throw new IOException($"Failed to prepare item directory: {entryPath}", exception);
            }

            ZipFile.ExtractToDirectory(Path.Combine(outputDirectory, boothId + ".zip"), entryPath);
            return entryPath;
        }

        return Directory.CreateDirectory(entryPath).ToString();
    }

    private static async Task<bool> ApplyChangeAsync(
        BoothDownloadCacheItem currentItem,
        BoothDownloadCacheItem? cachedItem,
        string entryDir,
        int maxRetries,
        DownloadChange change,
        CancellationToken cancellationToken)
    {
        WriteMetadata(entryDir, currentItem);

        List<string> imageUrls = change is DownloadChange.Full or DownloadChange.ImagesAndMetadata
            ? currentItem.Images
            : [];
        List<string> downloadUrls = change == DownloadChange.Full
            ? currentItem.Downloadables
            : [];

        if (change == DownloadChange.ImagesAndMetadata)
        {
            RemovePreviousImages(entryDir, cachedItem);
            currentItem.ImageFiles.Clear();
        }
        else if (change == DownloadChange.Full)
        {
            currentItem.ImageFiles.Clear();
            currentItem.DownloadableFiles.Clear();
        }

        if (imageUrls.Count == 0 && downloadUrls.Count == 0)
        {
            return true;
        }

        var options = BoothProgressBarOptions.Layer1;
        options.CollapseWhenFinished = false;

        var parentOptions = BoothProgressBarOptions.Layer2;
        parentOptions.CollapseWhenFinished = false;

        var childOptions = BoothProgressBarOptions.Layer3;
        childOptions.CollapseWhenFinished = true;

        using var progressBar = new ProgressBar(
            imageUrls.Count + downloadUrls.Count,
            "Overall Progress",
            options);

        var allTasks = new List<Task>();
        var failedDownloads = 0;

        var entryDirFiles = new ConcurrentBag<string>(
            Directory.EnumerateFiles(entryDir).Select(Path.GetFileName)!);
        var remainingImages = imageUrls.Count;
        var imageTaskBar = progressBar.Spawn(
            imageUrls.Count,
            $"Images ({remainingImages}/{imageUrls.Count} Left)",
            parentOptions);

        allTasks.AddRange(imageUrls.Select(url => Task.Run(async () =>
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

            await Utils.DownloadFileAsync(
                url,
                Path.Combine(entryDir, uniqueFilename),
                childProgress,
                cancellationToken);

            lock (currentItem.ImageFiles)
            {
                currentItem.ImageFiles[url] = uniqueFilename;
            }

            imageTaskBar.Tick();
            progressBar.Tick();

            Interlocked.Decrement(ref remainingImages);
            imageTaskBar.Message = $"Images ({remainingImages}/{imageUrls.Count} Left)";
        }, cancellationToken)));

        if (downloadUrls.Count > 0 && BoothHttpClientManager.IsAnonymous)
        {
            progressBar.WriteErrorLine("Skipping purchased downloads because the configured cookie is invalid.");
            failedDownloads = downloadUrls.Count;
        }
        else if (downloadUrls.Count > 0)
        {
            var binaryDir = Directory.CreateDirectory(Path.Combine(entryDir, "Binary")).ToString();
            var binaryDirFiles = new ConcurrentBag<string>(
                Directory.EnumerateFiles(binaryDir).Select(Path.GetFileName)!);
            var remainingDownloads = downloadUrls.Count;
            var downloadTaskBar = progressBar.Spawn(
                downloadUrls.Count,
                $"Downloads ({remainingDownloads}/{downloadUrls.Count} Left)",
                parentOptions);

            allTasks.AddRange(downloadUrls.Select(url => Task.Run(async () =>
            {
                using var response = await BoothHttpClientManager.HttpClient.GetAsync(url, cancellationToken);
                var location = response.Headers.Location
                    ?? throw new HttpRequestException($"BOOTH did not return a download redirect for {url}.");
                var redirectUrl = location.IsAbsoluteUri
                    ? location
                    : new Uri(response.RequestMessage!.RequestUri!, location);
                var filename = Uri.UnescapeDataString(redirectUrl.Segments.Last());

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
                        await Utils.DownloadFileAsync(
                            redirectUrl.ToString(),
                            Path.Combine(binaryDir, uniqueFilename),
                            childProgress,
                            cancellationToken);
                        success = true;
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        retryCount++;
                        child.Message = $"Failed to download {url}. Retry attempt {retryCount}/{maxRetries}. Error: {ex.Message}";
                        if (retryCount < maxRetries)
                        {
                            await Task.Delay(5000, cancellationToken);
                        }
                    }
                }

                downloadTaskBar.Tick();
                progressBar.Tick();

                if (!success)
                {
                    Interlocked.Increment(ref failedDownloads);
                    child.Message = $"Failed to download {url} after {maxRetries} attempts.";
                    progressBar.WriteErrorLine($"Failed to download {url} after {maxRetries} attempts.");
                    return;
                }

                lock (currentItem.DownloadableFiles)
                {
                    currentItem.DownloadableFiles[url] = uniqueFilename;
                }

                Interlocked.Decrement(ref remainingDownloads);
                downloadTaskBar.Message = $"Downloads ({remainingDownloads}/{downloadUrls.Count} Left)";
            }, cancellationToken)));
        }

        await Task.WhenAll(allTasks);
        return failedDownloads == 0;
    }

    private static void WriteMetadata(string entryDir, BoothDownloadCacheItem currentItem)
    {
        var pageJsonPath = Path.Combine(entryDir, BoothPageJson);
        var innerHtmlPath = Path.Combine(entryDir, BoothInnerHtmlList);

        if (!string.IsNullOrWhiteSpace(currentItem.BoothPageJson))
        {
            File.Delete(innerHtmlPath);
            File.WriteAllText(pageJsonPath, currentItem.BoothPageJson);
            return;
        }

        File.Delete(pageJsonPath);
        if (currentItem.InnerHtml.Count > 0)
        {
            File.WriteAllText(innerHtmlPath, JsonConvert.SerializeObject(currentItem.InnerHtml));
        }
        else
        {
            File.Delete(innerHtmlPath);
        }
    }

    private static void RemovePreviousImages(string entryDir, BoothDownloadCacheItem? cachedItem)
    {
        if (cachedItem?.ImageFiles.Count > 0)
        {
            foreach (var filename in cachedItem.ImageFiles.Values.Distinct(StringComparer.Ordinal))
            {
                File.Delete(Path.Combine(entryDir, filename));
            }

            return;
        }

        // Legacy debug caches did not record output filenames. Item root files other than the two
        // metadata files are images managed by this downloader.
        foreach (var file in Directory.EnumerateFiles(entryDir))
        {
            var filename = Path.GetFileName(file);
            if (filename is not BoothPageJson and not BoothInnerHtmlList)
            {
                File.Delete(file);
            }
        }
    }

    private static string GetChangeDescription(DownloadChange change)
    {
        return change switch
        {
            DownloadChange.Full => "download links changed or no cache exists; downloading the complete item",
            DownloadChange.ImagesAndMetadata => "images changed; refreshing images and metadata",
            DownloadChange.Metadata => "metadata changed; refreshing metadata only",
            _ => "no changes"
        };
    }

    private enum DownloadChange
    {
        None,
        Baseline,
        Metadata,
        ImagesAndMetadata,
        Full
    }
}
