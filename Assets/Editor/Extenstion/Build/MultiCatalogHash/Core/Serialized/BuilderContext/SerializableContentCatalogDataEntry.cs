using System;
using System.Collections.Generic;
using Editor.Extenstion.Build.MultiCatalogHash.Core.Serialized.Interface;
using UnityEngine.AddressableAssets.ResourceLocators;

namespace Editor.Extenstion.Build.MultiCatalogHash.Core.Serialized.BuilderContext
{
    [Serializable]
    public class SerializableContentCatalogDataEntry : ISerializable<ContentCatalogDataEntry>
    {
        public string internalId;
        public string provider;
        public List<string> keys;
        public List<string> dependencies;
        public string resourceType;

        public void FromOriginal(ContentCatalogDataEntry input)
        {
            internalId = input.InternalId;
            provider = input.Provider;
            keys = input.Keys.ConvertAll(k => k.ToString());
            dependencies = input.Dependencies.ConvertAll(k => k.ToString());
            resourceType = input.ResourceType.AssemblyQualifiedName;
        }

        public ContentCatalogDataEntry ToOriginal()
        {
            var type = Type.GetType(resourceType);  // 使用反射恢复类型
            var entry = new ContentCatalogDataEntry(type, internalId, provider, keys.ConvertAll(k => (object)k), dependencies.ConvertAll(k => (object)k));
            return entry;
        }
    }
}