using System.Text.RegularExpressions;

namespace BoothDownloader.Miscellaneous
{
    public static partial class RegexStore
    {
        public static readonly Regex IdRegex = GetIdRegex();

        public static readonly Regex ItemRegex = GetItemRegex();

        public static readonly Regex DownloadRegex = GetDownloadRegex();


        [GeneratedRegex(@"(\d+)", RegexOptions.Compiled)]
        private static partial Regex GetIdRegex();

        [GeneratedRegex(@"booth\.pm(?:\/\w+)?\/items\/(\d+)", RegexOptions.Compiled)]
        private static partial Regex GetItemRegex();

        [GeneratedRegex(@"https\:\/\/booth\.pm\/downloadables\/[0-9]{0,}", RegexOptions.Compiled)]
        private static partial Regex GetDownloadRegex();
    }
}
