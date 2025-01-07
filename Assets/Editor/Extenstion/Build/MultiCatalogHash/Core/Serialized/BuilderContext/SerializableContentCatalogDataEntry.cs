using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Editor.Extenstion.Build.MultiCatalogHash.Core.Serialized.Interface;
using Editor.Extenstion.Build.MultiCatalogHash.Tools;
using UnityEngine;
using UnityEngine.AddressableAssets.ResourceLocators;

namespace Editor.Extenstion.Build.MultiCatalogHash.Core.Serialized.BuilderContext
{
    [Serializable]
    public class SerializableContentCatalogDataEntry : ISerializable<ContentCatalogDataEntry>
    {
        public string internalId;
        public string provider;
        public string resourceType;
        public List<string> keys = new List<string>();
        public List<string> dependencies = new List<string>();
        public string data;

        public void FromOriginal(ContentCatalogDataEntry input)
        {
            internalId = input.InternalId;
            provider = input.Provider;
            keys = input.Keys.ConvertAll(k => k.ToString());
            dependencies = input.Dependencies.ConvertAll(k => k.ToString());
            resourceType = input.ResourceType.AssemblyQualifiedName;
            data = BinaryObjectPersistence.SaveObjectToString(input.Data);
        }

        public ContentCatalogDataEntry ToOriginal()
        {
            var type = Type.GetType(resourceType);  // 使用反射恢复类型
            var entry = new ContentCatalogDataEntry(
                type,
                internalId,
                provider,
                keys.ConvertAll(k => (object)k),
                dependencies.ConvertAll(k => (object)k),
                BinaryObjectPersistence.LoadObjectFromString(data));
            return entry;
        }
    }
}