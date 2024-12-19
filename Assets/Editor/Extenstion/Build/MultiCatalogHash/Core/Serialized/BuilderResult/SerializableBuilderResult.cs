using System;
using System.Collections.Generic;
using Editor.Extenstion.Build.MultiCatalogHash.Core.Serialized.Interface;
using UnityEditor.AddressableAssets.Build;

namespace Editor.Extenstion.Build.MultiCatalogHash.Core.Serialized.BuilderResult
{
    [Serializable]
    public class SerializableBuilderResult : ISerializable<AddressablesPlayerBuildResult>
    {
        public List<AddressablesPlayerBuildResult.BundleBuildResult> bundleBuildResults;

        public void FromOriginal(AddressablesPlayerBuildResult input)
        {
            bundleBuildResults = input.AssetBundleBuildResults;
        }

        public AddressablesPlayerBuildResult ToOriginal()
        {
            var result = new AddressablesPlayerBuildResult();
            bundleBuildResults.ForEach(data => result.AssetBundleBuildResults.Add(data));
            return result;
        }
    }
}