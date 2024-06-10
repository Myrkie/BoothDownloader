using Newtonsoft.Json;

namespace BoothDownloader.Web;

public class BoothJsonItem
{
    [JsonProperty("images")]
    public List<Image>? Images { get; set; }

    [JsonProperty("gift")]
    public object? Gift { get; set; }
}

public class Image
{
    [JsonProperty("original")]
    public string? Original { get; set; }

    [JsonProperty("resized")]
    public string? Resized { get; set; }
}
