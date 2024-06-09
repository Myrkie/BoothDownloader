using ShellProgressBar;

namespace BoothDownloader.Miscellaneous;

public static class BoothProgressBarOptions
{
    public static ProgressBarOptions Layer1 => new()
    {
        BackgroundColor = ConsoleColor.White,
        ForegroundColor = ConsoleColor.Yellow,
        ProgressCharacter = '─'
    };

    public static ProgressBarOptions Layer2 => new()
    {
        BackgroundColor = ConsoleColor.White,
        ForegroundColor = ConsoleColor.Cyan,
        ProgressCharacter = '─'
    };

    public static ProgressBarOptions Layer3 => new()
    {
        BackgroundColor = ConsoleColor.White,
        ForegroundColor = ConsoleColor.Green,
        ProgressCharacter = '─'
    };
}
