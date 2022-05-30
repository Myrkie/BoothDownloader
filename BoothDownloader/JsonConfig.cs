using Newtonsoft.Json;
// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming

namespace BoothDownloader
{
    public static class JsonConfig
    {
        private const string Configpath = "BDConfig.json";
        public static Config _config = new Config();

        public class Config
        {
            [JsonProperty("Cookie")]
            public string _Cookie { get; set; } = "";
            [JsonProperty("FirstBoot")]

            public bool _firstboot { get; set; } = true;
            [JsonProperty("Auto_Zip")]
            public bool _autozip { get; set; } = true;
        }

        public static class Configure
        {
            public static void load()
            {
                try
                {
                    if (!File.Exists(Configpath))
                    {
                        saveconf();
                    }
                    else
                    {
                        loadconf();
                    }
                }
                catch(Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }

            public static void forcesave() => saveconf();
            private static void loadconf() => _config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(Configpath))!;

            private static void saveconf() => File.WriteAllText(Configpath, JsonConvert.SerializeObject(_config, Formatting.Indented));
        }
    }
}