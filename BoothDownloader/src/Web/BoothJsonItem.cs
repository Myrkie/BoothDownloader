using System.Text.Json.Serialization;

namespace BoothDownloader.Web;

public class BoothJsonItem
{
    [JsonPropertyName("images")]
    public List<Image>? Images { get; set; }

    [JsonPropertyName("gift")]
    public object? Gift { get; set; }
}

public class Image
{
    [JsonPropertyName("original")]
    public string? Original { get; set; }

    [JsonPropertyName("resized")]
    public string? Resized { get; set; }
}