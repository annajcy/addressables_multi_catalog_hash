using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Editor.Extenstion.Build.MultiCatalogHash.Core.Serialized.Interface;
using Editor.Extenstion.Build.MultiCatalogHash.Tools;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine.AddressableAssets.Initialization;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.Serialization;

namespace Editor.Extenstion.Build.MultiCatalogHash.Core.Serialized.BuilderContext
{
    [Serializable]
    public class SerializableAddressableAssetsBuildContext : ISerializable<AddressableAssetsBuildContext>
    {
        public string settingsAssetPath;
        public List<SerializableContentCatalogDataEntry> locations = new List<SerializableContentCatalogDataEntry>();
        public List<Type> providerTypes = new List<Type>();

        public SerializableAddressableAssetsBuildContext() {}

        public SerializableAddressableAssetsBuildContext(AddressableAssetsBuildContext input)
        {
            FromOriginal(input);
        }

        public void FromOriginal(AddressableAssetsBuildContext input)
        {
            settingsAssetPath = (string)ReflectionHelper.GetFieldValue(input, "m_SettingsAssetPath");
            input.locations.ForEach(data =>
            {
                SerializableContentCatalogDataEntry scd = new SerializableContentCatalogDataEntry();
                scd.FromOriginal(data);
                locations.Add(scd);
            });
            foreach (var inputProviderType in input.providerTypes)
                providerTypes.Add(inputProviderType);

        }

        public AddressableAssetsBuildContext ToOriginal()
        {
            var context = new AddressableAssetsBuildContext
            {
                locations = new List<ContentCatalogDataEntry>(),
                providerTypes = new HashSet<Type>(),
                runtimeData = new ResourceManagerRuntimeData()
            };

            providerTypes.ForEach(type => { context.providerTypes.Add(type); });
            locations.ForEach(data => { context.locations.Add(data.ToOriginal()); });
            ReflectionHelper.SetFieldValue(context, "m_SettingsAssetPath", settingsAssetPath);
            return context;
        }
    }
}
