namespace BoothDownloader.misc
{
    public class Utils
    {
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