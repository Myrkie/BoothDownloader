using BoothDownloader.config;
using BoothDownloader.log;
using Microsoft.Extensions.Logging;

namespace BoothDownloader.processes;

public static class InitializationProcess
{
    public static void Run(JsonConfig config)
    {
        var logger = Log.Factory.CreateLogger("InitializationProcess");
        
        #region First Boot

        if (config.Config.FirstBoot)
        {
            Console.WriteLine("Please paste in your cookie from browser.");
            var cookie = Console.ReadLine();
            config.Config.Cookie = cookie!;
            config.Config.FirstBoot = false;
            config.Save();
            logger.Log(LogLevel.Information, "Cookie Set");
        }

        #endregion
    }
}