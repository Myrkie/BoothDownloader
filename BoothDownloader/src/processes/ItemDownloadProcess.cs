using System.Collections.Concurrent;
using System.IO.Compression;
using BoothDownloader.booth;
using BoothDownloader.config;
using BoothDownloader.log;
using BoothDownloader.web;
using Microsoft.Extensions.Logging;

namespace BoothDownloader.processes;

public static class ItemDownloadProcess
{
    public static void Run(JsonConfig config, string boothId, string outputDirectory, bool enableFilePathOutput)
    {
        var logger = Log.Factory.CreateLogger("ItemDownloadProcess");

        using (logger.BeginScope(boothId))
        {

            #region Prep Folders

            var outputDir = Directory.CreateDirectory(outputDirectory);
            if (Directory.Exists(outputDir + "/" + boothId))
            {
                logger.Log(LogLevel.Information, "Directory already exists. Deleting...");
                Directory.Delete(outputDir + "/" + boothId, true);
            }

            var entryDir = Directory.CreateDirectory(outputDir + "/" + boothId);
            var binaryDir = Directory.CreateDirectory(entryDir + "/" + "Binary");

            #endregion

            #region Prep Booth Client

            var client = new BoothClient(config.Config);
            var hasValidCookie = client.IsCookieValid();

            if (hasValidCookie)
            {
                logger.Log(LogLevel.Information, "Valid Cookie. File downloads will function");
            }
            else
            {
                logger.Log(LogLevel.Warning,
                    "Invalid Cookie. File downloads will not function! Image downloads will still function. Update your cookie in the config file.");
                config.Config.Cookie = "";
                config.Save();
            }

            var boothItemPage = new BoothItemPage(client, boothId);

            // Saving Tags for the entry
            var tagsFile = File.CreateText(entryDir + "/tags.txt");
            tagsFile.Write(string.Join('\n', boothItemPage.Tags));
            tagsFile.Close();

            #endregion

            #region Start Image Downloads

            var imageTasks = boothItemPage.ResizedImages.Distinct()
                .Select(url => client.StartDownloadImageTask(url, entryDir)).ToArray();
            var gifTasks = boothItemPage.Gifs.Distinct().Select(url => client.StartDownloadImageTask(url, entryDir))
                .ToArray();

            #endregion

            #region Parse Downloads

            var downloadCollection = hasValidCookie
                ? boothItemPage.Downloads.ToArray()
                : Array.Empty<string>();
            var ordersCollection = hasValidCookie
                ? boothItemPage.OrderIds.ToArray()
                : Array.Empty<string>();

            // Thread safe container for collecting download urls
            var downloadBag = new ConcurrentBag<string>(downloadCollection);

            if (ordersCollection.Length > 0)
            {
                var boothOrderPages =
                    ordersCollection.Distinct().Select(orderId => new BoothOrderPage(client, orderId));
                foreach (var boothOrderPage in boothOrderPages)
                {
                    foreach (var downloadUrl in boothOrderPage.ItemDownloads[boothId])
                    {
                        downloadBag.Add(downloadUrl);
                    }
                }
            }

            #endregion

            #region Download Processing

            var downloadTasks = downloadBag.Distinct().Select(url => client.StartDownloadBinaryTask(url, binaryDir))
                .ToArray();

            if (imageTasks.Length > 0)
            {
                Task.WaitAll(imageTasks);
                logger.Log(LogLevel.Information, "Image downloads completed");
            }
            else logger.Log(LogLevel.Information, "Image downloads skipped - no images");

            if (gifTasks.Length > 0)
            {
                Task.WaitAll(gifTasks);
                logger.Log(LogLevel.Information, "Gif downloads completed");
            }
            else logger.Log(LogLevel.Information, "Gif downloads skipped - no gifs");

            if (downloadTasks.Length > 0)
            {
                Task.WaitAll(downloadTasks);
                logger.Log(LogLevel.Information, "File downloads completed");
            }
            else logger.Log(LogLevel.Information, "File downloads skipped - no files");

            #endregion

            #region Compression

            Thread.Sleep(1500);

            if (config.Config.AutoZip)
            {
                if (File.Exists(entryDir + ".zip"))
                {
                    logger.Log(LogLevel.Information, "File already exists. Deleting...");
                    File.Delete(entryDir + ".zip");
                }

                ZipFile.CreateFromDirectory(entryDir.ToString(), entryDir + ".zip");
                Directory.Delete(entryDir.ToString(), true);
                logger.Log(LogLevel.Information, "Zipped!");
            }

            #endregion

            #region Exit Successfully

            logger.Log(LogLevel.Information, "Done!");

            if (enableFilePathOutput && config.Config.AutoZip)
            {
                // used for standard output redirection for path to zip file with another process
                logger.Log(LogLevel.Information, "ENVFilePATH: {0}.zip", entryDir);
            }

            #endregion
        }
    }
}