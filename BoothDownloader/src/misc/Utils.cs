namespace BoothDownloader.misc
{
    public class Utils
    {
        public static async Task DownloadFileAsync(string url, string destinationPath, IProgress<double> progress)
        {
            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            var contentLength = response.Content.Headers.ContentLength ?? -1;

            using var stream = await response.Content.ReadAsStreamAsync();
            var buffer = new byte[81920];
            var totalBytesRead = 0L;
            var bytesRead = 0L;
            using var fileStream = File.Create(destinationPath);
            do
            {
                bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, (int)bytesRead); 
                    totalBytesRead += bytesRead;
                    progress.Report((double)totalBytesRead / contentLength);
                }
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