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
        allItems.RemoveAll(s => string.IsNullOrWhiteSpace(s.Id));

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
            content = content.Replace("<", "\r\n<");
            string[] sheet = content.Split("<div class=\"sheet");
            if (!content.Contains("<div class=\"sheet"))
            {
                return (items, true);
            }
            foreach (string itemsheet in sheet)
            {
                BoothItem item = new();
                StringReader reader = new(itemsheet);
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line?.Contains("<a href=\"https://booth.pm/en/items/") == true)
                        item.Id = line.Split('/').Last().Split("\"").First();
                }
                items.Add(item);
            }
            return (items, false);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"exception at method OrdersParse {exception}");
            throw;
        }
    }
}