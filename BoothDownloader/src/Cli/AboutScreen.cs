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
            .. AboutLines.Select(line =>
                Enumerable.Range(0, line.Length).Select(_ => Random.Next(60, 101)).ToArray())
        ];
        int startTop = Math.Max(0, (Console.WindowHeight - AboutLines.Length) / 2);
        int[] startLeft = [.. AboutLines.Select(line => Math.Max(0, (Console.WindowWidth - line.Length) / 2))];
        for (int i = 0; i < AboutLines.Length; i++)
        {
            Console.SetCursorPosition(startLeft[i], startTop + i);
            Console.Write(new string(' ', Math.Min(AboutLines[i].Length, Console.WindowWidth - startLeft[i])));
        }

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

                Console.SetCursorPosition(startLeft[i], startTop + i);
                Console.Write(buffer);
            }

            if (animationComplete) break;
            await Task.Delay(16);
        }

        Console.SetCursorPosition(0, Console.WindowHeight - 1);
    }
}