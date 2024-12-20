using System;
using System.Collections.Generic;
using Editor.Extenstion.Build.MultiCatalogHash.Core.Serialized.BuilderContext;
using Editor.Extenstion.Build.MultiCatalogHash.Core.Serialized.Interface;

namespace Editor.Extenstion.Build.MultiCatalogHash.Core.Serialized.BuildInfo
{
    [Serializable]
    public class SerializableBuildInfos : ISerializable<List<CatalogBuildInfo>>
    {
        public List<SerializableCatalogBuildInfo> catalogs = new List<SerializableCatalogBuildInfo>();

        public SerializableBuildInfos() {}

        public SerializableBuildInfos(List<CatalogBuildInfo> input)
        {
            FromOriginal(input);
        }

        public void FromOriginal(List<CatalogBuildInfo> input)
        {
            input.ForEach(info =>
            {
                SerializableCatalogBuildInfo scb = new SerializableCatalogBuildInfo();
                scb.FromOriginal(info);
                catalogs.Add(scb);
            });
        }

        public List<CatalogBuildInfo> ToOriginal()
        {
            List<CatalogBuildInfo> infos = new List<CatalogBuildInfo>();
            catalogs.ForEach(info => infos.Add(info.ToOriginal()));
            return infos;
        }
    }
}