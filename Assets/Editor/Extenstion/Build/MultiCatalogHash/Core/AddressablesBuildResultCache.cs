using System;
using System.Collections.Generic;
using Editor.Extenstion.Build.MultiCatalogHash.Core.Serialized.BuilderContext;
using Editor.Extenstion.Build.MultiCatalogHash.Core.Serialized.BuilderInput;
using Editor.Extenstion.Build.MultiCatalogHash.Core.Serialized.BuilderResult;
using Editor.Extenstion.Build.MultiCatalogHash.Core.Serialized.BuildInfo;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEngine;

namespace Editor.Extenstion.Build.MultiCatalogHash.Core
{
    [Serializable, CreateAssetMenu(menuName = "Addressables/Addressables Build Result Cache", fileName = "AddressablesBuildResultCache")]
    public class AddressablesBuildResultCache : ScriptableObject
    {
        public SerializableAddressablesDataBuilderInput builderInput;
        public SerializableAddressableAssetsBuildContext aaContext;
        public SerializableBuilderResult buildResult;
        public SerializableBuildInfos buildInfos;
    }
}