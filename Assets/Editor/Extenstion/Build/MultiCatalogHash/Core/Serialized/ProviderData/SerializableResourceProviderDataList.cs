using System;
using System.Collections.Generic;
using Editor.Extenstion.Build.MultiCatalogHash.Core.Serialized.Interface;
using UnityEngine.ResourceManagement.Util;

namespace Editor.Extenstion.Build.MultiCatalogHash.Core.Serialized.ProviderData
{
    [Serializable]
    public class SerializableResourceProviderDataList : ISerializable<List<ObjectInitializationData>>
    {
        public List<ObjectInitializationData> resourceProviderData = new List<ObjectInitializationData>();
        public SerializableResourceProviderDataList() {}

        public SerializableResourceProviderDataList(List<ObjectInitializationData> resourceProviderData)
        {
            FromOriginal(resourceProviderData);
        }

        public void FromOriginal(List<ObjectInitializationData> input)
        {
            foreach (var data in input)
                resourceProviderData.Add(data);
        }

        public List<ObjectInitializationData> ToOriginal()
        {
            List<ObjectInitializationData> result = new List<ObjectInitializationData>();
            foreach (var data in resourceProviderData)
                result.Add(data);
            return result;
        }
    }
}