using Newtonsoft.Json;

namespace BoothDownloader.Configuration;

public class JsonConfig
{
    private string Path { get; }
    public Config Config = new();

    public JsonConfig(string path)
    {
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
            Console.WriteLine(e);
            throw;
        }
    }

    private void Load()
    {
        try
        {
            Config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(Path)) ?? throw new InvalidOperationException();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}