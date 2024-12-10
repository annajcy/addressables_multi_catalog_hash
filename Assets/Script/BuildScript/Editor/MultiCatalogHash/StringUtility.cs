using UnityEditor;

namespace Script.BuildScript.Editor.MultiCatalogHash
{
    /// <summary>
    /// The BuildTask used to create location lists for Addressable assets.
    /// </summary>
    public static class StringUtility
    {
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
