using System.Collections.Generic;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEngine;

namespace Editor.Extenstion.Build.MultiCatalogHash.Core
{
    [CreateAssetMenu(menuName = "Addressables/Addressables Build Result Cache", fileName = "AddressablesBuildResultCache")]

    public class AddressablesBuildResultCache : ScriptableObject
    {
        public AddressablesDataBuilderInput builderInput;
        public AddressableAssetsBuildContext aaContext;
        public AddressablesPlayerBuildResult buildResult;
        public List<CatalogBuildInfo> catalogs;
    }
}