using System.Collections.Concurrent;
using System.Reflection;
using SevenZip;

namespace BoothDownloader.misc
{
    public static class Utils
    {
        public static async Task DownloadFileAsync(string url, string destinationPath, IProgress<double> progress, CancellationToken cancellationToken = default)
        {
            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            var contentLength = response.Content.Headers.ContentLength ?? -1;

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var buffer = new byte[81920];
            var totalBytesRead = 0L;
            long bytesRead;
            await using var fileStream = File.Create(destinationPath);
            do
            {
                bytesRead = await stream.ReadAsync(buffer, cancellationToken);
                if (bytesRead <= 0) continue;
                await fileStream.WriteAsync(buffer.AsMemory(0, (int)bytesRead), cancellationToken);
                totalBytesRead += bytesRead;
                progress.Report((double)totalBytesRead / contentLength);
            } while (bytesRead > 0);
        }

        public static string GetUniqueFilename(string binaryDir, string filename, ConcurrentBag<string> files)
        {
            int counter = 0;
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filename);
            string extension = Path.GetExtension(filename);

            string newFilename = $"{fileNameWithoutExtension}{extension}";

            while (File.Exists(Path.Combine(binaryDir, newFilename)) || files.Contains(newFilename))
            {
                counter++;
                newFilename = $"{fileNameWithoutExtension} ({counter}){extension}";
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine($"Duplicate file found, extending name {filename} => {newFilename}");
                Console.ResetColor();
            }

            return newFilename;
        }
    }
}