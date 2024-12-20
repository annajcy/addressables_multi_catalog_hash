using System;
using System.Collections.Generic;
using Editor.Extenstion.Build.MultiCatalogHash.Core.Serialized.Interface;
using UnityEditor.AddressableAssets.Build;

namespace Editor.Extenstion.Build.MultiCatalogHash.Core.Serialized.BuilderResult
{
    [Serializable]
    public class SerializableBuilderResult : ISerializable<AddressablesPlayerBuildResult>
    {
        public List<SerializableAssetBundleResult> bundleBuildResults = new List<SerializableAssetBundleResult>();
        public SerializableBuilderResult() {}
        public SerializableBuilderResult(AddressablesPlayerBuildResult input) { FromOriginal(input); }

        public void FromOriginal(AddressablesPlayerBuildResult input)
        {
            input.AssetBundleBuildResults.ForEach(results =>
            {
                SerializableAssetBundleResult abr = new SerializableAssetBundleResult();
                abr.FromOriginal(results);
                bundleBuildResults.Add(abr);
            });
        }

        public AddressablesPlayerBuildResult ToOriginal()
        {
            var result = new AddressablesPlayerBuildResult();
            bundleBuildResults.ForEach(data => result.AssetBundleBuildResults.Add(data.ToOriginal()));
            return result;
        }
    }
}