using BoothDownloader.Miscellaneous;

namespace BoothDownloader.Web;

public class BoothOrders
{
    public static async Task<List<BoothItem>> OrdersLoopAsync(CancellationToken cancellationToken = default)
    {
        List<BoothItem> allItems = [];

        int pageNumber = 1;
        bool isDone = false;
        while (!isDone)
        {
            var (Items, IsDone) = await OrdersParseAsync(pageNumber, cancellationToken);
            allItems.AddRange(Items);
            isDone = IsDone;
            pageNumber++;
        }

        return allItems;
    }

    private static async Task<(List<BoothItem> Items, bool IsDone)> OrdersParseAsync(int pageNumber, CancellationToken cancellationToken = default)
    {
        List<BoothItem> items = [];
        var boothclient = new BoothClient(BoothDownloader.Configextern.Config);
        var client = boothclient.MakeHttpClient();
        string url = $"https://accounts.booth.pm/orders?page={pageNumber}";
        HttpResponseMessage? response = await client.GetAsync(url, cancellationToken);
        try
        {
            string content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!content.Contains("<div class=\"sheet"))
            {
                return (items, true);
            }

            string[] splitByItem = content.Split("<div class=\"sheet");

            foreach (string itemHtml in splitByItem)
            {
                var itemMatch = RegexStore.ItemRegex.Match(itemHtml);
                if (itemMatch.Success)
                    items.Add(new BoothItem { Id = itemMatch.Groups[1].Value });
            }
            return (items, false);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"exception at method OrdersParseAsync {exception}");
            throw;
        }
    }
}