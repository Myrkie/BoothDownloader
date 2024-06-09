using Newtonsoft.Json;

namespace BoothDownloader.Configuration;

public class Config<T>
    where T : new()
{
    public T Instance { get; private set; }
    private readonly string FileLocation;
    private readonly JsonSerializerSettings JsonSerializerSettings = new();

    public Config(string path)
    {
        FileLocation = path;
        Instance = new();
        Load();
    }

    public void Load()
    {
        Instance = new();
        if (File.Exists(FileLocation))
        {
            var cfgInstance = JsonConvert.DeserializeObject<T>(File.ReadAllText(FileLocation), JsonSerializerSettings);

            if (cfgInstance != null)
            {
                Instance = cfgInstance;
            }
        }
        Save();
    }

    public void Save()
    {
        File.WriteAllText(FileLocation, JsonConvert.SerializeObject(Instance, Formatting.Indented));
    }
}