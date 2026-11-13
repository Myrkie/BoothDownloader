using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace BoothDownloader.Configuration;

public class Config<T> where T : new()
{
    public T Instance { get; private set; }

    private readonly string _fileLocation;
    private readonly JsonTypeInfo<T> _jsonTypeInfo;

    public Config(string path, JsonTypeInfo<T> jsonTypeInfo)
    {
        _fileLocation = path;
        _jsonTypeInfo = jsonTypeInfo;
        Instance = new T();
        Load();
    }

    public void Load()
    {
        Instance = new T();

        if (File.Exists(_fileLocation))
        {
            var cfgInstance = JsonSerializer.Deserialize( File.ReadAllText(_fileLocation), _jsonTypeInfo);

            if (cfgInstance != null)
            {
                Instance = cfgInstance;
            }
        }

        Save();
    }

    public void Save()
    {
        File.WriteAllText(_fileLocation, JsonSerializer.Serialize(Instance, _jsonTypeInfo));
    }
}