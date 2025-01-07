using System;
using System.Collections.Generic;
using System.IO;
using Editor.Extenstion.Build.MultiCatalogHash.Core.Serialized.BuilderContext;
using Editor.Extenstion.Build.MultiCatalogHash.Core.Serialized.BuilderInput;
using Editor.Extenstion.Build.MultiCatalogHash.Core.Serialized.BuilderResult;
using Editor.Extenstion.Build.MultiCatalogHash.Core.Serialized.BuildInfo;
using Editor.Extenstion.Build.MultiCatalogHash.Core.Serialized.ProviderData;
using Unity.Plastic.Newtonsoft.Json;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEngine;
using UnityEngine.ResourceManagement.Util;

namespace Editor.Extenstion.Build.MultiCatalogHash.Core
{
    [Serializable]
    public class AddressablesBuildResultCache
    {
        public SerializableAddressablesDataBuilderInput builderInput;
        public SerializableAddressableAssetsBuildContext aaContext;
        public SerializableBuilderResult buildResult;
        public SerializableBuildInfos buildInfos;
        public SerializableResourceProviderDataList resourceProviderDataList;
        public string sceneProvider;
        public string instanceProvider;

        public static JsonSerializerSettings settings = new JsonSerializerSettings()
        {
            Formatting = Formatting.Indented,
            Converters = new List<JsonConverter>()
            { new ObjectInitializationDataConverter(), }
        };

        public AddressablesBuildResultCache() {}

        public AddressablesBuildResultCache(
            AddressablesDataBuilderInput builderInput,
            AddressableAssetsBuildContext aaContext,
            AddressablesPlayerBuildResult buildResult,
            List<CatalogBuildInfo> buildInfos,
            List<ObjectInitializationData> resourceProviderDataList,
            Type sceneProvider,
            Type instanceProvider)
        {
            this.builderInput = new SerializableAddressablesDataBuilderInput(builderInput);
            this.aaContext = new SerializableAddressableAssetsBuildContext(aaContext);
            this.buildResult = new SerializableBuilderResult(buildResult);
            this.buildInfos = new SerializableBuildInfos(buildInfos);
            this.resourceProviderDataList = new SerializableResourceProviderDataList(resourceProviderDataList);
            this.sceneProvider = sceneProvider.AssemblyQualifiedName;
            this.instanceProvider = instanceProvider.AssemblyQualifiedName;
        }

        /// <summary>
        /// Save the AddressablesBuildResultCache object to a JSON file.
        /// </summary>
        /// <param name="filePath">The path to the JSON file where the object will be saved.</param>
        public void SaveToJson(string filePath)
        {
            try
            {
                // Serialize the AddressablesBuildResultCache object to JSON string
                string json = JsonConvert.SerializeObject(this, Formatting.Indented, settings);

                // Write the JSON string to the file
                File.WriteAllText(filePath, json);
                Debug.Log($"Data successfully saved to {filePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error saving to JSON: {e.Message}");
            }
        }

        /// <summary>
        /// Load an AddressablesBuildResultCache object from a JSON file.
        /// </summary>
        /// <param name="json">The JSON file to load the object from.</param>
        /// <returns>AddressablesBuildResultCache object loaded from the file.</returns>
        public static AddressablesBuildResultCache LoadFromJson(string json)
        {
            try
            {
                // Deserialize the JSON string to AddressablesBuildResultCache object
                AddressablesBuildResultCache result = JsonConvert.DeserializeObject<AddressablesBuildResultCache>(json, settings);
                Debug.Log($"Data successfully loaded from {json}");
                return result;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error loading from JSON: {e.Message}");
                return null;
            }
        }

    }
}
