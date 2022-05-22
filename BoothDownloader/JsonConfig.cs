using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace BoothDownloader
{
    public class JsonConfig
    {
        public static string _configpath = "BDConfig.json";
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
                    if (!File.Exists(_configpath))
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
            private static void loadconf() => _config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(_configpath));

            private static void saveconf() => File.WriteAllText(_configpath, JsonConvert.SerializeObject(_config, Formatting.Indented));
        }
    }
}