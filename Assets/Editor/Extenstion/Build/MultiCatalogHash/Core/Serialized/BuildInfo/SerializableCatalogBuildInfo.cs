using System;
using System.Collections.Generic;
using Editor.Extenstion.Build.MultiCatalogHash.Core.Serialized.BuilderContext;
using Editor.Extenstion.Build.MultiCatalogHash.Core.Serialized.Interface;

namespace Editor.Extenstion.Build.MultiCatalogHash.Core.Serialized.BuildInfo
{
    [Serializable]
    public class SerializableCatalogBuildInfo : ISerializable<CatalogBuildInfo>
    {
        public string identifier;
        public string fileName;
        public string buildPath;
        public string loadPath;
        public string rootBuildPath;
        public bool registerToSettings;
        public List<SerializableContentCatalogDataEntry> locations = new List<SerializableContentCatalogDataEntry>();
        public List<string> includedBundles = new List<string>();


        public void FromOriginal(CatalogBuildInfo input)
        {
            identifier = input.identifier;
            fileName = input.fileName;
            buildPath = input.buildPath;
            loadPath = input.loadPath;
            rootBuildPath = input.rootBuildPath;
            registerToSettings = input.registerToSettings;
            includedBundles = input.includedBundles;
            input.locations.ForEach(loc =>
            {
                SerializableContentCatalogDataEntry scd = new SerializableContentCatalogDataEntry();
                scd.FromOriginal(loc);
                locations.Add(scd);
            });
        }

        public CatalogBuildInfo ToOriginal()
        {
            var catalogBuildInfo = new CatalogBuildInfo(identifier, fileName)
            {
                buildPath = buildPath,
                loadPath = loadPath,
                rootBuildPath = rootBuildPath,
                registerToSettings = registerToSettings,
                includedBundles = new List<string>(includedBundles)
            };

            // Convert locations back to original ContentCatalogDataEntry objects
            catalogBuildInfo.locations.Clear();  // Ensure the list is empty before adding new data
            locations.ForEach(scd =>
            {
                var originalLocation = scd.ToOriginal();  // Convert back to ContentCatalogDataEntry
                catalogBuildInfo.locations.Add(originalLocation);
            });

            return catalogBuildInfo;
        }
    }
}