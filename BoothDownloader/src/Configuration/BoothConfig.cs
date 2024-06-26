﻿using Newtonsoft.Json;

namespace BoothDownloader.Configuration;

public class BoothConfig
{
    public const string DefaultPath = "./BDConfig.json";
    public const string AnonymousCookie = "ANONYMOUS";

    public static void Setup(string path)
    {
        ConfigInstance = new Config<BoothConfig>(path);
    }

    public static Config<BoothConfig> ConfigInstance { get; private set; } = null!;
    public static BoothConfig Instance => ConfigInstance!.Instance;

    [JsonProperty(nameof(Cookie))]
    public string Cookie { get; set; } = null!;

    [JsonProperty(nameof(AutoZip))]
    public bool AutoZip { get; set; } = true;
}