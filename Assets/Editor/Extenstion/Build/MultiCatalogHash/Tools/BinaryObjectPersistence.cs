using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

namespace Editor.Extenstion.Build.MultiCatalogHash.Tools
{
    public static class BinaryObjectPersistence
    {
        // 保存 object 到字符串
        public static string SaveObjectToString(object obj)
        {
            if (obj == null) return string.Empty;
            MemoryStream memoryStream = new MemoryStream();
            BinaryFormatter formatter = new BinaryFormatter();
            formatter.Serialize(memoryStream, obj);
            byte[] bytes = memoryStream.ToArray();
            string base64String = Convert.ToBase64String(bytes); // 转换为 Base64 字符串
            Debug.Log($"Object serialized to string: {base64String}.");
            return base64String;
        }

        // 从字符串加载 object
        public static object LoadObjectFromString(string base64String)
        {
            if (string.IsNullOrEmpty(base64String))
            {
                Debug.LogWarning("Input string is null or empty.");
                return null;
            }

            try
            {
                byte[] bytes = Convert.FromBase64String(base64String);
                using (MemoryStream memoryStream = new MemoryStream(bytes))
                {
                    BinaryFormatter formatter = new BinaryFormatter();
                    object obj = formatter.Deserialize(memoryStream);
                    Debug.Log("Object deserialized from string.");
                    return obj;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to deserialize object: {ex.Message}");
                return null;
            }
        }
    }
}