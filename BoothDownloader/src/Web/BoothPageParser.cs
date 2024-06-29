using System.Collections.Concurrent;
using BoothDownloader.Miscellaneous;
using Discord.Common.Helpers;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ShellProgressBar;

namespace BoothDownloader.Web;

public class BoothPageParser
{
    public static async Task<Dictionary<string, BoothItemAssets>> GetPageItemsAsync(string path, Dictionary<string, BoothItemAssets>? incomingItems = null, bool getJsons = true, CancellationToken cancellationToken = default)
    {
        Dictionary<string, BoothItemAssets> items = incomingItems ?? [];

        int pageNumber = 1;
        var htmlDoc = new HtmlDocument();

        var options = BoothProgressBarOptions.Layer1;
        options.CollapseWhenFinished = false;

        using (var pageProgressBar = new ProgressBar(1, $"Going through pages (Page: {pageNumber})", options))
        {
            pageProgressBar.WriteLine($"Getting items from {path}");
            while (true)
            {
                pageProgressBar.Message = $"Going through pages (Page: {pageNumber})";

                string url = $"https://accounts.booth.pm/{path}?page={pageNumber}";
                HttpResponseMessage? response = await BoothHttpClientManager.HttpClient.GetAsync(url, cancellationToken);
                string content = await response.Content.ReadAsStringAsync(cancellationToken);

                htmlDoc.LoadHtml(content);

                var itemsList = htmlDoc.DocumentNode.Descendants("div")
                    .Where(node => node.GetAttributeValue("class", string.Empty).Contains("mb-16"));

                if (itemsList?.Any() == true)
                {
                    foreach (var item in itemsList)
                    {
                        var itemMatch = RegexStore.ItemRegex.Match(item.InnerHtml);
                        if (itemMatch.Success)
                        {
                            var boothId = itemMatch.Groups[1].Value;
                            if (!items.ContainsKey(boothId))
                            {
                                items.Add(boothId, new BoothItemAssets());
                            }

                            if (!items[boothId].InnerHtml.Contains(item.InnerHtml))
                            {
                                items[boothId].InnerHtml.Add(item.InnerHtml);
                            }

                            var downloadCollection = RegexStore.DownloadRegex.Matches(item.InnerHtml).Select(match => match.Value);

                            foreach (var download in downloadCollection)
                            {
                                if (!string.IsNullOrWhiteSpace(download) && !items[boothId].Downloadables.Contains(download))
                                {
                                    items[boothId].Downloadables.Add(download);
                                }
                            }
                        }
                    }
                }
                else
                {
                    break;
                }

                pageNumber++;
            }

            pageProgressBar.Tick();
        }

        Console.WriteLine();

        if (getJsons)
        {
            var itemsToGetJsonsOf = items.Where(x => !x.Value.TriedToGetJson);
            int remainingItems = itemsToGetJsonsOf.Count();
            using (var jsonProgressBar = new ProgressBar(remainingItems, $"Getting booth jsons ({remainingItems}/{itemsToGetJsonsOf.Count()} Left)", options))
            {
                jsonProgressBar.WriteLine("Getting booth jsons");
                if (!itemsToGetJsonsOf.Any())
                {
                    jsonProgressBar.Tick();
                }
                else
                {
                    await Task.WhenAll(itemsToGetJsonsOf.Select(item => Task.Run(async () =>
                    {
                        try
                        {
                            var itemPage = await BoothHttpClientManager.GetItemJsonAsync(item.Key, true, cancellationToken);
                            items[item.Key].BoothPageJson = itemPage;
                            AddItemsFromJson(items[item.Key].BoothPageJson, item.Key, ref items);
                        }
                        catch (HttpRequestException ex)
                        {
                            jsonProgressBar.WriteLine($"Failed to get page for {item.Key} (Status Code: {((int?)ex.StatusCode)}). Possibly deleted. Grabbing image/information from preview.");

                            var htmlDoc = new HtmlDocument();

                            foreach (var innerHtml in item.Value.InnerHtml)
                            {
                                htmlDoc.LoadHtml(innerHtml);

                                var imageNodes = htmlDoc.DocumentNode.SelectNodes("//img[contains(@class, 'l-library-item-thumbnail')]");

                                if (imageNodes?.Count > 0)
                                {
                                    foreach (var imageNode in imageNodes)
                                    {
                                        var imageUrl = imageNode.GetAttributeValue("src", string.Empty);
                                        if (!string.IsNullOrWhiteSpace(imageUrl) && !items[item.Key].Images.Contains(imageUrl))
                                        {
                                            items[item.Key].Images.Add(imageUrl);
                                        }
                                    }
                                }
                            }
                        }
                        finally
                        {
                            items[item.Key].TriedToGetJson = true;
                            lock (jsonProgressBar)
                            {
                                jsonProgressBar.Tick();
                                remainingItems--;
                                jsonProgressBar.Message = $"Getting booth jsons ({remainingItems}/{items.Count} Left)";
                            }
                        }
                    })));
                }
            }

            Console.WriteLine();
        }
        return items;
    }


    public static async Task<Dictionary<string, BoothItemAssets>> GetItemsAsync(IEnumerable<string> boothIds, Dictionary<string, BoothItemAssets>? incomingItems = null, bool gotGiftPage = false, CancellationToken cancellationToken = default)
    {
        Dictionary<string, BoothItemAssets> items = incomingItems ?? [];

        if (!boothIds.Any())
        {
            return items;
        }

        foreach (var boothId in boothIds)
        {
            if (!items.ContainsKey(boothId))
            {
                items.Add(boothId, new BoothItemAssets());
            }
        }

        var options = BoothProgressBarOptions.Layer1;
        options.CollapseWhenFinished = false;

        ConcurrentBag<string> orders = [];

        bool hasAGiftedItem = false;
        int remainingItems = boothIds.Count();
        using (var progressBar = new ProgressBar(remainingItems, $"Getting booth items ({remainingItems}/{boothIds.Count()} Left)", options))
        {
            await Task.WhenAll(boothIds.Select(boothId => Task.Run(async () =>
            {
                try
                {
                    var itemPageAuthed = await BoothHttpClientManager.GetItemJsonAsync(boothId, false, cancellationToken);

                    var itemOrders = RegexStore.OrdersRegex.Matches(itemPageAuthed).Select(m => m.Value);

                    foreach (var order in itemOrders)
                    {
                        if (!string.IsNullOrWhiteSpace(order))
                        {
                            orders.Add(order);
                        }
                    }

                    if (!hasAGiftedItem)
                    {
                        var boothJsonItem = JsonConvert.DeserializeObject<BoothJsonItem>(itemPageAuthed);
                        if (boothJsonItem?.Gift != null)
                        {
                            hasAGiftedItem = true;
                        }
                    }

                    if (!items[boothId].TriedToGetJson)
                    {
                        var itemPage = await BoothHttpClientManager.GetItemJsonAsync(boothId, true, cancellationToken);
                        items[boothId].BoothPageJson = itemPage;
                        AddItemsFromJson(items[boothId].BoothPageJson, boothId, ref items);
                    }
                }
                catch (HttpRequestException)
                {
                    progressBar.WriteLine($"Failed to get page for {boothId}. Invalid Id or Possible deleted.");
                }
                finally
                {
                    items[boothId].TriedToGetJson = true;
                    lock (progressBar)
                    {
                        progressBar.Tick();
                        remainingItems--;
                        progressBar.Message = $"Getting booth jsons ({remainingItems}/{items.Count} Left)";
                    }
                }
            })));
        }

        Console.WriteLine();

        orders = new ConcurrentBag<string>(orders.Distinct());
        var downloads = new ConcurrentBag<(string Id, string Url)>();

        if (!orders.IsEmpty)
        {
            int remainingOrders = orders.Count;
            using (var progressBar = new ProgressBar(remainingOrders, $"Getting orders ({remainingOrders}/{orders.Count} Left)", options))
            {
                await Task.WhenAll(orders.Select(order => Task.Run(async () =>
                {
                    try
                    {
                        var orderPage = await BoothHttpClientManager.HttpClient.GetAsync(order, cancellationToken);
                        var orderContent = await orderPage.Content.ReadAsStringAsync(cancellationToken);

                        HtmlDocument htmlDoc = new();
                        htmlDoc.LoadHtml(orderContent);

                        // Out of all the scraping, this is most likely to break.
                        var downloadNodes = htmlDoc.DocumentNode.Descendants("div")
                            .Where(node => node.GetAttributeValue("class", string.Empty).Equals("sheet sheet--p400 mobile:pt-[13px] mobile:px-16 mobile:pb-8"));

                        if (downloadNodes?.Any() == true)
                        {
                            foreach (var downloadNode in downloadNodes)
                            {
                                var itemMatch = RegexStore.ItemRegex.Match(downloadNode.InnerHtml);

                                if (boothIds.Contains(itemMatch.Groups[1].Value))
                                {
                                    foreach (var downloadUrl in RegexStore.DownloadRegex.Matches(downloadNode.InnerHtml).Select(match => match.Value))
                                    {
                                        downloads.Add((itemMatch.Groups[1].Value, downloadUrl));
                                    }
                                }
                            }
                        }
                    }
                    catch (HttpRequestException)
                    {
                        progressBar.WriteLine($"Failed to get order page for {order}.");
                    }
                    finally
                    {
                        lock (progressBar)
                        {
                            progressBar.Tick();
                            remainingOrders--;
                            progressBar.Message = $"Getting orders ({remainingOrders}/{orders.Count} Left)";
                        }
                    }
                })));
            }

            Console.WriteLine();
        }

        foreach (var (Id, Url) in downloads)
        {
            if (!string.IsNullOrWhiteSpace(Url) && !items[Id].Downloadables.Contains(Url))
            {
                items[Id].Downloadables.Add(Url);
            }
        }

        if (hasAGiftedItem)
        {
            if (gotGiftPage)
            {
                LoggerHelper.GlobalLogger.LogInformation("Gift item detected when grabbing from item page, but already grabbed gifts previously.");
            }
            else
            {
                LoggerHelper.GlobalLogger.LogInformation("Gift item detected when grabbing from item page, going through gifts to get downloadables.");
                var giftItems = await BoothPageParser.GetPageItemsAsync("library/gifts", [], false, cancellationToken: cancellationToken);

                foreach (var giftItem in giftItems)
                {
                    if (boothIds.Contains(giftItem.Key))
                    {
                        foreach (var download in giftItem.Value.Downloadables)
                        {
                            if (!items[giftItem.Key].Downloadables.Contains(download))
                            {
                                items[giftItem.Key].Downloadables.Add(download);
                            }
                        }
                    }
                }
            }
        }

        return items;
    }

    private static BoothJsonItem? AddItemsFromJson(string json, string id, ref Dictionary<string, BoothItemAssets> items)
    {
        var downloadCollection = RegexStore.DownloadRegex.Matches(json)
                                .Select(match => match.Value);

        foreach (var download in downloadCollection)
        {
            if (!string.IsNullOrWhiteSpace(download) && !items[id].Downloadables.Contains(download))
            {
                items[id].Downloadables.Add(download);
            }
        }

        var boothJsonItem = JsonConvert.DeserializeObject<BoothJsonItem>(json);
        if (boothJsonItem?.Images != null)
        {
            foreach (Image image in boothJsonItem.Images)
            {
                if (!string.IsNullOrWhiteSpace(image.Original) && !items[id].Images.Contains(image.Original))
                {
                    items[id].Images.Add(image.Original);
                }
                else if (!string.IsNullOrWhiteSpace(image.Resized) && !items[id].Images.Contains(image.Resized))
                {
                    items[id].Images.Add(image.Resized);
                }
            }
        }

        return boothJsonItem;
    }
}