using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;

namespace Editor.Extenstion.Build.MultiCatalogHash.Tools
{
    public static class Utility
    {
        /// <summary>
        /// Moves a file to the specified folder, creating the target folder if it doesn't exist.
        /// If a file with the same name exists in the target folder, it will be overwritten.
        /// </summary>
        /// <param name="filePath">The full path of the source file</param>
        /// <param name="targetFolderPath">The path of the target folder</param>
        public static void MoveFile(string filePath, string targetFolderPath)
        {
            try
            {
                // Check if the source file exists
                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"Source file does not exist: {filePath}");
                    return;
                }

                // Ensure the target folder exists
                if (!Directory.Exists(targetFolderPath))
                {
                    Directory.CreateDirectory(targetFolderPath);
                    Console.WriteLine($"Target folder created: {targetFolderPath}");
                }

                // Get the file name and the full path of the target file
                string fileName = Path.GetFileName(filePath);
                string targetFilePath = Path.Combine(targetFolderPath, fileName);

                // If the target file exists, delete it to allow overwriting
                if (File.Exists(targetFilePath))
                {
                    File.Delete(targetFilePath);
                    Console.WriteLine($"Existing file deleted: {targetFilePath}");
                }

                // Move the file to the target folder
                File.Move(filePath, targetFilePath);
                Console.WriteLine($"File moved to: {targetFilePath}");
            }
            catch (Exception ex)
            {
                // Handle exceptions and display an error message
                Console.WriteLine($"Failed to move file: {ex.Message}");
            }
        }

        public static string AppendFileNameExtension(string fileName)
        {
            return fileName +
#if ENABLE_JSON_CATALOG
                    ".json";
#else
                   ".bin";
#endif
        }

        public static string GetFileName(string path, BuildTarget target)
        {
            char directorySeparatorChar = PathSeparatorForPlatform(target);

            if (path != null)
            {
                int length = path.Length;
                for (int i = length; --i >= 0;)
                {
                    char ch = path[i];
                    // Catch name when path[i] meet directorySeparatorChar Or just meet '/', to avoid get wrong name when group loadPath is remote
                    if (ch == directorySeparatorChar || ch == '/')
                        return path.Substring(i + 1, length - i - 1);
                }
            }

            return path;
        }

        public static string ReplaceIPAddress(string url, string ipAddress)
        {
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(ipAddress))
            {
                throw new ArgumentException("URL or IP address cannot be null or empty.");
            }

            // 匹配 IP 地址和端口号的正则表达式
            string pattern = @"(?<=://)(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}(:\d+)?|[a-zA-Z0-9\.\-]+(:\d+)?)(?=/|$)";

            // 使用正则表达式替换 URL 中的 IP 地址和端口号
            string replacedUrl = Regex.Replace(url, pattern, ipAddress);

            return replacedUrl;
        }

        private static char PathSeparatorForPlatform(BuildTarget target)
        {
            switch (target)
            {
                case BuildTarget.StandaloneWindows64:
                case BuildTarget.StandaloneWindows:
                case BuildTarget.XboxOne:
                    return '\\';
                case BuildTarget.GameCoreXboxOne:
                    return '\\';
                case BuildTarget.Android:
                    return '/';
                default:
                    return '/';
            }
        }
    }
}
