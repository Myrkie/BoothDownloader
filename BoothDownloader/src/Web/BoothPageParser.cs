using BoothDownloader.Miscellaneous;
using BoothDownloader.src.Web;
using HtmlAgilityPack;
using Newtonsoft.Json;
using ShellProgressBar;

namespace BoothDownloader.Web;

public class BoothPageParser
{
    public static async Task<Dictionary<string, BoothItemAssets>> GetPageItemsAsync(string path, Dictionary<string, BoothItemAssets>? incomingItems = null, CancellationToken cancellationToken = default)
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

        var itemsToGetJsonsOf = items.Where(x => !x.Value.TriedToGetJson);
        int remainingItems = itemsToGetJsonsOf.Count();
        using (var jsonProgressBar = new ProgressBar(remainingItems, $"Getting booth jsons ({remainingItems}/{itemsToGetJsonsOf.Count()} Left)", options))
        {
            jsonProgressBar.WriteLine("Getting booth jsons");
            if(itemsToGetJsonsOf.Count() == 0)
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

                        var downloadCollection = RegexStore.DownloadRegex.Matches(itemPage)
                            .Select(match => match.Value);

                        foreach (var download in downloadCollection)
                        {
                            if (!string.IsNullOrWhiteSpace(download) && !items[item.Key].Downloadables.Contains(download))
                            {
                                items[item.Key].Downloadables.Add(download);
                            }
                        }

                        var boothJsonItem = JsonConvert.DeserializeObject<BoothJsonItem>(itemPage);
                        if (boothJsonItem?.Images != null)
                        {
                            foreach (Image image in boothJsonItem.Images)
                            {
                                if (!string.IsNullOrWhiteSpace(image.Original) && !items[item.Key].Images.Contains(image.Original))
                                {
                                    items[item.Key].Images.Add(image.Original);
                                }
                                else if (!string.IsNullOrWhiteSpace(image.Resized) && !items[item.Key].Images.Contains(image.Resized))
                                {
                                    items[item.Key].Images.Add(image.Resized);
                                }
                            }
                        }
                    }
                    catch (HttpRequestException)
                    {
                        jsonProgressBar.WriteLine($"Failed to get page for {item.Key}. Possibly deleted. Grabbing image from preview.");

                        var htmlDoc = new HtmlDocument();

                        foreach (var innerHtml in item.Value.InnerHtml)
                        {
                            htmlDoc.LoadHtml(innerHtml);

                            var imageNodes = htmlDoc.DocumentNode.SelectNodes("//img[contains(@class, 'l-library-item-thumbnail')]");

                            if (imageNodes?.Any() == true)
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
        return items;
    }
}