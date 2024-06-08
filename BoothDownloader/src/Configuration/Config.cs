using Newtonsoft.Json;

namespace BoothDownloader.Configuration;

public class Config
{
    [JsonProperty(nameof(Cookie))]
    public string Cookie { get; set; } = "";

    [JsonProperty(nameof(FirstBoot))]
    public bool FirstBoot { get; set; } = true;

    [JsonProperty(nameof(AutoZip))]
    public bool AutoZip { get; set; } = true;
}