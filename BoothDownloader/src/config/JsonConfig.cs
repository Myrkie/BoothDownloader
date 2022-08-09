using BoothDownloader.log;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace BoothDownloader.config;

public class JsonConfig
{
    private ILogger Logger;
    private string Path { get; }
    public Config Config = new();

    public JsonConfig(string path)
    {
        Logger = Log.Factory.CreateLogger("JsonConfig");
        Path = path;

        if (!File.Exists(Path))
        {
            Save();
        }
        else
        {
            Load();
        }
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(Path, JsonConvert.SerializeObject(Config, Formatting.Indented));
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Config failed to save");
            throw;
        }
    }

    public void Load()
    {
        try
        {
            Config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(Path)) ?? throw new InvalidOperationException();
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Config failed to load");
            throw;
        }
    }
}