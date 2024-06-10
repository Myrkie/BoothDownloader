using System.Collections.Specialized;
using System.Web;
using BoothDownloader.Configuration;
using Discord.Common.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace BoothDownloader.Miscellaneous;

public static class BoothDownloaderProtocol
{
    private const string ProtocolName = "BoothDownloader";
    private const string ProtocolDescription = $"URL:{ProtocolName} Protocol";
    private const string RegistryPath = $@"Software\Classes\{ProtocolName}";
    private const string ShellOpenCommandRegistryPath = $@"Software\Classes\{ProtocolName}\shell\open\command";
#pragma warning disable CA1416
    public static void RegisterContext()
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath, true) ??
                                      Registry.CurrentUser.CreateSubKey(RegistryPath))
            {
                if (key != null)
                {
                    key.SetValue("", ProtocolDescription);
                    key.SetValue("URL Protocol", "");
                }
            }

            using (RegistryKey commandKey = Registry.CurrentUser.CreateSubKey(ShellOpenCommandRegistryPath))
            {
                if (commandKey != null)
                {
                    var commandValue = $"\"{Environment.GetCommandLineArgs()[0]}\" \"%1\"";
                    string? existingCommandValue = commandKey?.GetValue("") as string;
                    if (commandKey != null && existingCommandValue != commandValue)
                    {
                        commandKey.SetValue("", commandValue);
                    }
                }
            }

            LoggerHelper.GlobalLogger.LogInformation("Registered URL Handler");
            Exit();
        }
        catch (Exception e)
        {
            LoggerHelper.GlobalLogger.LogError(e, "Error when Registering URL Handler");
            LoggerHelper.GlobalLogger.LogInformation("Exiting...");
            Thread.Sleep(5000);
            throw;
        }
    }

    public static void UnregisterContext()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(RegistryPath);
            LoggerHelper.GlobalLogger.LogInformation("Unregistered URL Handler");
            Exit();
        }
        catch (Exception e)
        {
            LoggerHelper.GlobalLogger.LogError(e, "Error when Unregistering URL Handler");
            LoggerHelper.GlobalLogger.LogInformation("Exiting...");
            Thread.Sleep(5000);
            throw;
        }
    }
#pragma warning restore CA1416

    private const string BoothProtocolPrefix = "boothdownloader://open/";
    private const string TokenArguement = "token";
    private const string IdArguement = "id";

    public static string[] HandleProtocol(string[] args)
    {
        if (args.Length <= 0 || args[0].StartsWith(BoothProtocolPrefix, StringComparison.OrdinalIgnoreCase) == false)
        {
            return args;
        }

        var result = new List<string>();

        var call = new Uri(args[0]);
        var action = call.Authority;

        switch (action)
        {
            case "open":
                NameValueCollection query = HttpUtility.ParseQueryString(call.Query);

                string? token = query[TokenArguement];
                string? id = query[IdArguement];
                if (token != null)
                {
                    BoothConfig.Setup(BoothConfig.DefaultPath);
                    BoothConfig.Instance.Cookie = token;
                    BoothConfig.ConfigInstance.Save();
                    LoggerHelper.GlobalLogger.LogInformation("Cookie set from Protocol");

                    if (id == null)
                    {
                        Exit();
                    }
                }

                if (id != null)
                {
                    result.Add("--booth");
                    result.Add(id);
                }
                break;
        }

        return [.. result];
    }

    private static void Exit()
    {
        LoggerHelper.GlobalLogger.LogInformation("Exiting...");
        Thread.Sleep(5000);
        Environment.Exit(0);
    }
}
