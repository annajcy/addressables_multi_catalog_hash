using System;
using System.IO;
using UnityEditor;

namespace Script.BuildScript.Editor.MultiCatalogHash
{
    /// <summary>
    /// The BuildTask used to create location lists for Addressable assets.
    /// </summary>
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

        public static char PathSeparatorForPlatform(BuildTarget target)
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
