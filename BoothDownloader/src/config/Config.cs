using Newtonsoft.Json;

namespace BoothDownloader.config;

public class Config
{
    [JsonProperty("Cookie")] public string Cookie { get; set; } = "";

    [JsonProperty("FirstBoot")] public bool FirstBoot { get; set; } = true;

    [JsonProperty("Auto_Zip")] public bool AutoZip { get; set; } = true;

    [JsonProperty("OutputDirectory")] public string OutputDirectory { get; set; } = "./BoothDownloaderOut";
}