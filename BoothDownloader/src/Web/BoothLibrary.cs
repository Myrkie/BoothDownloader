using BoothDownloader.Miscellaneous;

namespace BoothDownloader.Web;

public class BoothLibrary
{
    public static async Task<List<BoothItem>> LibraryLoopAsync(bool isGift, CancellationToken cancellationToken = default)
    {
        List<BoothItem> allItems = [];

        int pageNumber = 1;
        bool isDone = false;
        while (!isDone)
        {
            var (Items, IsDone) = await LibraryParseAsync(isGift, pageNumber, cancellationToken);
            allItems.AddRange(Items);
            isDone = IsDone;
            pageNumber++;
        }

        return allItems;
    }

    private static async Task<(List<BoothItem> Items, bool IsDone)> LibraryParseAsync(bool isGift, int pageNumber, CancellationToken cancellationToken = default)
    {
        List<BoothItem> items = [];
        var boothclient = new BoothClient(BoothDownloader.Configextern.Config);
        var client = boothclient.MakeHttpClient();
        string url = $"https://accounts.booth.pm/library{(isGift ? "/gifts" : string.Empty)}?page={pageNumber}";
        HttpResponseMessage? response = await client.GetAsync(url, cancellationToken);
        try
        {
            string content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!content.Contains("<div class=\"mb-16 "))
            {
                return (items, true);
            }
            // Class separates the next item in the order list
            var splitByItem = content.Split("<div class=\"mb-16 ");

            foreach (var itemHtml in splitByItem)
            {
                var itemMatch = RegexStore.ItemRegex.Match(itemHtml);
                if (itemMatch.Success)
                    items.Add(new BoothItem { Id = itemMatch.Groups[1].Value });
            }

            return (items, false);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"exception at method LibraryParseAsync {exception}");
            throw;
        }
    }
}