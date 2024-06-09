using BoothDownloader.Miscellaneous;

namespace BoothDownloader.Web;

public class BoothPageParser
{
    private const string _seperator = "<div class=\"mb-16 ";

    public static async Task<List<BoothItem>> ParserLoopAsync(string path, CancellationToken cancellationToken = default)
    {
        List<BoothItem> allItems = [];

        int pageNumber = 1;
        bool isDone = false;
        while (!isDone)
        {
            var (Items, IsDone) = await LibraryParseAsync(path, pageNumber, cancellationToken);
            allItems.AddRange(Items);
            isDone = IsDone;
            pageNumber++;
        }

        return allItems;
    }

    private static async Task<(List<BoothItem> Items, bool IsDone)> LibraryParseAsync(string path, int pageNumber, CancellationToken cancellationToken = default)
    {
        List<BoothItem> items = [];
        var boothclient = new BoothClient();
        var client = boothclient.MakeHttpClient();
        string url = $"https://accounts.booth.pm/{path}?page={pageNumber}";
        HttpResponseMessage? response = await client.GetAsync(url, cancellationToken);
        try
        {
            string content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!content.Contains(_seperator))
            {
                return (items, true);
            }
            // Class separates the next item in the order list
            var splitByItem = content.Split(_seperator);

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