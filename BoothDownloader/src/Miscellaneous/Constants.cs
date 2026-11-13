using System.Reflection;

namespace BoothDownloader.Miscellaneous;

public static class Constants
{
    internal static readonly string FormattedVersion = 
        $"{Assembly.GetExecutingAssembly().GetName().Version!.Major}." +
        $"{Assembly.GetExecutingAssembly().GetName().Version!.Minor}." +
        $"{Assembly.GetExecutingAssembly().GetName().Version!.Build}";
    
    internal static readonly string UserAgent = $"BoothDownloader/{FormattedVersion} (+{GithubUrl})";
    
    internal const string GithubUrl = "https://github.com/Myrkie/BoothDownloader";
    internal static readonly string Version = $"Booth Downloader - V{FormattedVersion}";

}