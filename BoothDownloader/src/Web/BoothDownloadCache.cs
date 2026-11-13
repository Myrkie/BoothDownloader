using System.Text.Json;

namespace BoothDownloader.Web;

internal sealed class BoothDownloadCache
{
    private const string FileName = "_BoothDownloadCache.json";

    public int Version { get; set; } = 1;

    public Dictionary<string, BoothDownloadCacheItem> Items { get; set; } = [];

    internal static BoothDownloadCache Load(string outputDirectory)
    {
        var path = Path.Combine(outputDirectory, FileName);

        if (File.Exists(path))
        {
            return JsonSerializer.Deserialize( File.ReadAllText(path), BoothJsonContext.Default.BoothDownloadCache)
                   ?? new BoothDownloadCache();
        }

        var legacyDebugPath = Path.Combine(outputDirectory, "_Debug_BatchDownloadDebug.json");

        if (!File.Exists(legacyDebugPath))
        {
            return new BoothDownloadCache();
        }

        var legacyItems = JsonSerializer.Deserialize(File.ReadAllText(legacyDebugPath), BoothJsonContext.Default.DictionaryStringBoothItemAssets);

        return new BoothDownloadCache
        {
            Items = legacyItems?.ToDictionary(
                item => item.Key,
                item => BoothDownloadCacheItem.From(item.Value)) ?? []
        };
    }

    internal void Save(string outputDirectory)
    {
        var path = Path.Combine(outputDirectory, FileName);
        var temporaryPath = path + ".tmp";

        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(this, BoothJsonContext.Default.BoothDownloadCache));

        File.Move(temporaryPath, path, true);
    }
}

internal sealed class BoothDownloadCacheItem
{
    public string BoothPageJson { get; set; } = string.Empty;

    public List<string> InnerHtml { get; set; } = [];

    public List<string> Images { get; set; } = [];

    public List<string> Downloadables { get; set; } = [];

    public Dictionary<string, string> ImageFiles { get; set; } = [];

    public Dictionary<string, string> DownloadableFiles { get; set; } = [];

    public bool ItemPageUnavailable { get; set; }

    internal static BoothDownloadCacheItem From(BoothItemAssets item)
    {
        return new BoothDownloadCacheItem
        {
            BoothPageJson = item.BoothPageJson,
            InnerHtml = item.InnerHtml.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList(),
            Images = item.Images.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList(),
            Downloadables = item.Downloadables.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList(),
            ItemPageUnavailable = string.IsNullOrWhiteSpace(item.BoothPageJson)
        };
    }
}