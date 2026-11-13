using BoothDownloader.Miscellaneous;

namespace BoothDownloader.Cli;

internal static class AboutScreen
{
    private static readonly string[] AboutLines =
    [
        "Booth Downloader - Downloads images and files from Booth.pm",
        $"Version {Constants.FormattedVersion}",
        "",
        "original by Myrkur, DubyaDude",
        "distributed under the Apache License v2",
        "",
        Constants.GithubUrl
    ];

    private static readonly Random Random = new();

    public static async Task ShowAsync()
    {
        Console.Clear();

        int[][] countdowns =
        [
            .. AboutLines.Select(line => Enumerable.Range(0, line.Length).Select(_ => Random.Next(30, 40)).ToArray())
        ];

        while (true)
        {
            bool animationComplete = true;

            for (int i = 0; i < AboutLines.Length; i++)
            {
                string line = AboutLines[i];
                char[] buffer = new char[line.Length];

                for (int j = 0; j < line.Length; j++)
                {
                    switch (countdowns[i][j])
                    {
                        case > 60:
                            buffer[j] = ' ';
                            countdowns[i][j]--;
                            animationComplete = false;
                            break;
                        case > 0:
                            buffer[j] = (char)Random.Next(65, 119);
                            countdowns[i][j]--;
                            animationComplete = false;
                            break;
                        default:
                            buffer[j] = line[j];
                            break;
                    }
                }

                string output = new string(buffer);

                int left = Math.Max(0, (Console.WindowWidth - output.Length) / 2);
                int top = Math.Max(0, (Console.WindowHeight - AboutLines.Length) / 2 + i);

                Console.SetCursorPosition(left, top);
                Console.Write(output);
            }

            if (animationComplete)
                break;

            await Task.Delay(16);
        }

        Console.SetCursorPosition(0, Console.WindowHeight - 1);
    }
}