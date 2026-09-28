using Newtonsoft.Json;

namespace BoothDownloader.Web;

internal sealed class BoothDownloadCache
{
    internal const string FileName = "_BoothDownloadCache.json";

    [JsonProperty(nameof(Version))]
    public int Version { get; set; } = 1;

    [JsonProperty(nameof(Items))]
    public Dictionary<string, BoothDownloadCacheItem> Items { get; set; } = [];

    internal static BoothDownloadCache Load(string outputDirectory)
    {
        var path = Path.Combine(outputDirectory, FileName);
        if (File.Exists(path))
        {
            return JsonConvert.DeserializeObject<BoothDownloadCache>(File.ReadAllText(path))
                   ?? new BoothDownloadCache();
        }

        var legacyDebugPath = Path.Combine(outputDirectory, "_Debug_BatchDownloadDebug.json");
        if (!File.Exists(legacyDebugPath))
        {
            return new BoothDownloadCache();
        }

        var legacyItems = JsonConvert.DeserializeObject<Dictionary<string, BoothItemAssets>>(
            File.ReadAllText(legacyDebugPath));

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
        File.WriteAllText(temporaryPath, JsonConvert.SerializeObject(this, Formatting.Indented));
        File.Move(temporaryPath, path, true);
    }
}

internal sealed class BoothDownloadCacheItem
{
    [JsonProperty(nameof(BoothPageJson))]
    public string BoothPageJson { get; set; } = string.Empty;

    [JsonProperty(nameof(InnerHtml))]
    public List<string> InnerHtml { get; set; } = [];

    [JsonProperty(nameof(Images))]
    public List<string> Images { get; set; } = [];

    [JsonProperty(nameof(Downloadables))]
    public List<string> Downloadables { get; set; } = [];

    [JsonProperty(nameof(ImageFiles))]
    public Dictionary<string, string> ImageFiles { get; set; } = [];

    [JsonProperty(nameof(DownloadableFiles))]
    public Dictionary<string, string> DownloadableFiles { get; set; } = [];

    [JsonProperty(nameof(ItemPageUnavailable))]
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
