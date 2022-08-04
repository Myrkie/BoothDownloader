namespace BoothDownloader.Utils;

public class XPath
{
    public static string ClassMatcher(string className)
    {
        return $"[contains(concat(' ', normalize-space(@class), ' '), ' {className} ')]";
    }
}