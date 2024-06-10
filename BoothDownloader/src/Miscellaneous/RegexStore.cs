using System.Text.RegularExpressions;

namespace BoothDownloader.Miscellaneous
{
    public static partial class RegexStore
    {
        public static readonly Regex IdRegex = GetIdRegex();

        public static readonly Regex ItemRegex = GetItemRegex();

        public static readonly Regex DownloadRegex = GetDownloadRegex();

        public static readonly Regex OrdersRegex = GetOrdersRegex();


        [GeneratedRegex(@"(\d+)", RegexOptions.Compiled)]
        private static partial Regex GetIdRegex();

        [GeneratedRegex(@"booth\.pm(?:\/\w+)?\/items\/(\d+)", RegexOptions.Compiled)]
        private static partial Regex GetItemRegex();

        [GeneratedRegex(@"https\:\/\/booth\.pm\/downloadables\/(\d+)", RegexOptions.Compiled)]
        private static partial Regex GetDownloadRegex();

        [GeneratedRegex(@"https\:\/\/accounts\.booth\.pm\/orders\/(\d+)", RegexOptions.Compiled)]
        private static partial Regex GetOrdersRegex();
    }
}
