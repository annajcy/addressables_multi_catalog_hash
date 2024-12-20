using System;
using Editor.Extenstion.Build.MultiCatalogHash.Core.Serialized.Interface;
using UnityEditor.AddressableAssets.Build;

namespace Editor.Extenstion.Build.MultiCatalogHash.Core.Serialized.BuilderResult
{
    [Serializable]
    public class SerializableAssetBundleResult : ISerializable<AddressablesPlayerBuildResult.BundleBuildResult>
    {
        public string filePath;
        public string hash;


        public void FromOriginal(AddressablesPlayerBuildResult.BundleBuildResult input)
        {
            filePath = input.FilePath;
            hash = input.Hash;
        }

        public AddressablesPlayerBuildResult.BundleBuildResult ToOriginal()
        {
            AddressablesPlayerBuildResult.BundleBuildResult result =
                new AddressablesPlayerBuildResult.BundleBuildResult
                {
                    FilePath = filePath,
                    Hash = hash
                };
            return result;
        }
    }
}