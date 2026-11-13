
namespace BoothDownloader.Configuration;

public class BoothConfig
{
    public const string DefaultPath = "./BDConfig.json";
    public const string AnonymousCookie = "ANONYMOUS";

    public static void Setup(string path)
    {
        ConfigInstance = new Config<BoothConfig>(path, Web.BoothJsonContext.Default.BoothConfig);
    }

    public static Config<BoothConfig> ConfigInstance { get; private set; } = null!;
    public static BoothConfig Instance => ConfigInstance!.Instance;

    public string Cookie { get; set; } = null!;

    public bool AutoZip { get; set; } = true;
}