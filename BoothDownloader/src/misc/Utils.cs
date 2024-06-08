using System.Reflection;
using SevenZip;

namespace BoothDownloader.misc
{
    public static class Utils
    {
        public static void Init7Z()
        {
            var resourceName = "BoothDownloader.resources.7z.dll";
            var tempDirectory = Path.Combine(Path.GetTempPath(), "BoothDownloader");
            var tempDllPath = Path.Combine(tempDirectory, "7z.dll");

            if (File.Exists(tempDllPath))
            {
                SevenZipBase.SetLibraryPath(tempDllPath);
                return;
            }

            Directory.CreateDirectory(tempDirectory);

            var assembly = Assembly.GetExecutingAssembly();
            using (var resourceStream = assembly.GetManifestResourceStream(resourceName))
            {
                if (resourceStream == null)
                {
                    Console.WriteLine("Error: 7z.dll resource not found.");
                    return;
                }

                using var fileStream = File.Create(tempDllPath);
                resourceStream.CopyTo(fileStream);
            }
            SevenZipBase.SetLibraryPath(tempDllPath);
        }

        public static void CompressDirectory(string sourceDirectory, string destinationArchive, IProgress<double> progress)
        {
            var compressor = new SevenZipCompressor
            {
                CompressionLevel = CompressionLevel.Ultra,
                CompressionMethod = CompressionMethod.Lzma2,
                DirectoryStructure = true,
                PreserveDirectoryRoot = true,
                ArchiveFormat = OutArchiveFormat.SevenZip
            };
            compressor.Compressing += (_, e) =>
            {
                progress.Report(e.PercentDone / 100.0);
            };

            try
            {
                compressor.CompressDirectory(sourceDirectory, destinationArchive);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception caught in CompressDirectory: {0}", ex);
            }
        }
        public static async Task DownloadFileAsync(string url, string destinationPath, IProgress<double> progress, CancellationToken cancellationToken)
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

        public static string GetUniqueFilename(string binaryDir, string filename)
        {
            int counter = 0;
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filename);
            string extension = Path.GetExtension(filename);

            string newFilename = $"{fileNameWithoutExtension}{extension}";

            while (File.Exists(Path.Combine(binaryDir, newFilename)))
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