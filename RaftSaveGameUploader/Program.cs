using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;
using System.Threading;

namespace RaftSaveGameUploader
{
    internal class Program
    {
        private const string BotToken = "ODM5MTA4MDYzMTA5NDQ3NzIw.GD3UEm.H4UTgHF4nhgrbTzF1XuVnH_uhYeHnqYoOD9UzM";
        private const ulong ChannelId = 1111714694080765953;

        private const string PathJsonFileName = "path.json";

        private static DiscordSocketClient _client;

        static void Main()
        {
            _client = new DiscordSocketClient();
            _client.Log += Log;

            _client.LoginAsync(TokenType.Bot, BotToken).GetAwaiter().GetResult();
            _client.StartAsync().GetAwaiter().GetResult();

            Ready();

            Thread.Sleep(Timeout.Infinite);
        }

        private static Task Log(LogMessage arg)
        {
            Console.WriteLine(arg);
            return Task.CompletedTask;
        }

        private static void Ready()
        {
            string zipFilePath = "C:\\Users\\yagiz\\Desktop\\elesko.zip";
            Console.WriteLine("Bot is connected and ready.");

            // Read or prompt for the directory path
            string directoryPath = GetDirectoryPath();

            // Call the function to create a zip file
            SafelyCreateZipFromDirectory(directoryPath, zipFilePath);

            // Call the function to upload the zip file to Discord
            UploadToDiscord(zipFilePath);

            // Optionally, you may want to delete the zip file after uploading
            // File.Delete(zipFilePath);
        }

        static string GetDirectoryPath()
        {
            string directoryPath;

            if (File.Exists(PathJsonFileName))
            {
                string json = File.ReadAllText(PathJsonFileName);
                var pathObject = JsonConvert.DeserializeObject<PathObject>(json);

                directoryPath = pathObject?.Path;

                if (string.IsNullOrWhiteSpace(directoryPath))
                {
                    Console.WriteLine("Invalid path stored in path.json. Please enter a new path:");
                    directoryPath = PromptUserForPath();
                }
            }
            else
            {
                Console.WriteLine("path.json file not found. Please enter the path to the directory:");
                directoryPath = PromptUserForPath();
            }

            SavePathToJson(directoryPath);

            return directoryPath;
        }

        static string PromptUserForPath()
        {
            Console.Write("Enter the path to the directory: ");
            return Console.ReadLine();
        }

        static void SavePathToJson(string path)
        {
            var pathObject = new PathObject { Path = path };
            string json = JsonConvert.SerializeObject(pathObject, Formatting.Indented);
            File.WriteAllText(PathJsonFileName, json);
            Console.WriteLine($"Path saved to {PathJsonFileName}");
        }
        static string CreateZipFile(string directoryPath)
        {
            string zipFileName = $"{Path.GetFileName(directoryPath)}_{DateTime.Now:yyyyMMddHHmmss}.zip";
            string zipFilePath = Path.Combine(directoryPath, zipFileName);

            const int maxAttempts = 3;
            const int retryDelayMilliseconds = 500;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    ZipFile.CreateFromDirectory(directoryPath, zipFilePath);
                    Console.WriteLine($"Zip file created successfully: {zipFilePath}");
                    return zipFilePath;
                }
                catch (IOException ex) when (attempt < maxAttempts)
                {
                    // If an IOException occurs (file in use), retry after a delay
                    Console.WriteLine($"Error creating zip file (Attempt {attempt}/{maxAttempts}): {ex.Message}");
                    Thread.Sleep(retryDelayMilliseconds);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error creating zip file: {ex.Message}");
                    return null;
                }
            }

            Console.WriteLine($"Error: Failed to create zip file after {maxAttempts} attempts.");
            return null;
        }

        static void SafelyCreateZipFromDirectory(string sourceDirectoryName, string zipFilePath)
        {
            using (FileStream zipToOpen = new FileStream(zipFilePath, FileMode.Create))
            using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Create))
            {
                foreach (var file in Directory.GetFiles(sourceDirectoryName))
                {
                    var entryName = Path.GetFileName(file);
                    var entry = archive.CreateEntry(entryName);
                    entry.LastWriteTime = File.GetLastWriteTime(file);
                    using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var stream = entry.Open())
                    {
                        fs.CopyTo(stream);
                    }
                }
            }
        }



        static void UploadToDiscord(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    Console.WriteLine("Error: Invalid file path for upload.");
                    return;
                }

                SocketTextChannel channel = _client.GetChannel(ChannelId) as SocketTextChannel;

                if (channel != null)
                {
                    channel.SendFileAsync(filePath, "Here is the zip file.").GetAwaiter().GetResult();
                    Console.WriteLine("File uploaded to Discord successfully.");
                }
                else
                {
                    Console.WriteLine("Invalid channel ID or channel is not a text channel.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error uploading file to Discord: {ex.Message}");
            }
        }

        static bool IsFileInUse(string filePath)
        {
            try
            {
                using (FileStream fs = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    // If the file can be opened, it is not in use
                    return false;
                }
            }
            catch (IOException)
            {
                // If an IOException occurs, the file is still in use
                return true;
            }
        }

        class PathObject
        {
            public string Path { get; set; }
        }
    }
}