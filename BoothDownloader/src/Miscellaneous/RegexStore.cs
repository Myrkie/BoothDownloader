using System.Text.RegularExpressions;

namespace BoothDownloader.Miscellaneous
{
    public static partial class RegexStore
    {

        public static readonly Regex ImageRegex = GetImageRegex();

        public static readonly Regex ImageGifRegex = GetImageGifRegex();

        public static readonly Regex IdRegex = GetIdRegex();

        public static readonly Regex GuidRegex = GetGuidRegex();

        public static readonly Regex ItemRegex = GetItemRegex();

        public static readonly Regex DownloadRegex = GetDownloadRegex();

        public static readonly Regex DownloadNameRegex = GetDownloadNameRegex();

        public static readonly Regex OrdersRegex = GetOrdersRegex();


        [GeneratedRegex(@"https\:\/\/booth\.pximg\.net\/[a-f0-9-]{0,}\/i\/[0-9]{0,}\/[a-zA-Z0-9\-_]{0,}\.(jpg|png)", RegexOptions.Compiled)]
        private static partial Regex GetImageRegex();

        [GeneratedRegex(@"https\:\/\/booth\.pximg\.net\/[a-f0-9-]{0,}\/i\/[0-9]{0,}\/[a-zA-Z0-9\-_]{0,}\.(gif)", RegexOptions.Compiled)]
        private static partial Regex GetImageGifRegex();

        [GeneratedRegex(@"[^/]+(?=/$|$)", RegexOptions.Compiled)]
        private static partial Regex GetIdRegex();

        [GeneratedRegex(@"[a-f0-9-]{0,}\/i\/[0-9]{0,}\/[a-zA-Z0-9\-_]{0,}\.(png|jpg|gif)", RegexOptions.Compiled)]
        private static partial Regex GetGuidRegex();

        [GeneratedRegex(@"booth\.pm(?:\/\w+)?\/items\/(\d+)", RegexOptions.Compiled)]
        private static partial Regex GetItemRegex();

        [GeneratedRegex(@"https\:\/\/booth\.pm\/downloadables\/[0-9]{0,}", RegexOptions.Compiled)]
        private static partial Regex GetDownloadRegex();

        [GeneratedRegex(@".*\/(.*)\?", RegexOptions.Compiled)]
        private static partial Regex GetDownloadNameRegex();

        [GeneratedRegex(@"https\:\/\/accounts\.booth\.pm\/orders\/[0-9]{0,}", RegexOptions.Compiled)]
        private static partial Regex GetOrdersRegex();
    }
}
