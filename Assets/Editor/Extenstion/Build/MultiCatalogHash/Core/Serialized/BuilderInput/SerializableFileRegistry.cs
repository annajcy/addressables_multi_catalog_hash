using System;
using System.Collections.Generic;
using System.Linq;
using Editor.Extenstion.Build.MultiCatalogHash.Core.Serialized.Interface;
using UnityEditor.AddressableAssets.Build;

namespace Editor.Extenstion.Build.MultiCatalogHash.Core.Serialized.BuilderInput
{
    [Serializable]
    public class SerializableFileRegistry : ISerializable<FileRegistry>
    {
        // 用于存储文件路径的列表
        public List<string> filePaths = new List<string>();

        /// <summary>
        /// 从原始 FileRegistry 对象提取数据。
        /// </summary>
        /// <param name="registry">原始 FileRegistry 对象。</param>
        public void FromOriginal(FileRegistry registry)
        {
            filePaths = registry.GetFilePaths().ToList();
        }

        /// <summary>
        /// 将数据反序列化回 FileRegistry 对象。
        /// </summary>
        /// <returns>返回还原的 FileRegistry 对象。</returns>
        public FileRegistry ToOriginal()
        {
            var registry = new FileRegistry();
            foreach (var filePath in filePaths)
                registry.AddFile(filePath);

            return registry;
        }
    }
}