using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace Editor.Extenstion.Build.MultiCatalogHash.Core.CoreEditor
{
    [CustomEditor(typeof(MultiCatalogHashBuild))]
    public class MultiCatalogHashBuildEditor : UnityEditor.Editor
    {
        public string buildResultCacheLoadPath = "Assets/AddressableAssetsData/BuildResult/build_cache.json";
        public string addressablesSettingPath = "Assets/AddressableAssetsData/AddressableAssetSettings.asset";

        private AddressableAssetSettings addressableAssetSettings;
        private AddressableAssetSettings AddressableAssetSettings
        {
            get
            {
                if (addressableAssetSettings == null && !string.IsNullOrEmpty(addressablesSettingPath))
                    addressableAssetSettings = AssetDatabase.LoadAssetAtPath<AddressableAssetSettings>(addressablesSettingPath);
                return addressableAssetSettings;
            }
        }

        // 用于引用目标 ScriptableObject
        private MultiCatalogHashBuild multiCatalogHashBuild;

        private void OnEnable()
        {
            multiCatalogHashBuild = (MultiCatalogHashBuild)target;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();

            // 绘制 addressablesSettingPath 字段
            EditorGUILayout.LabelField("Addressables Setting Path", EditorStyles.boldLabel);
            addressablesSettingPath = EditorGUILayout.TextField(
                "Setting Path",
                addressablesSettingPath);

            // 绘制 buildResultCacheLoadPath 字段
            EditorGUILayout.LabelField("Build Result Cache Load Path", EditorStyles.boldLabel);
            buildResultCacheLoadPath = EditorGUILayout.TextField(
                "Load Path",
                buildResultCacheLoadPath);

            EditorGUILayout.Space();

            if (GUILayout.Button("Restore Build Cache"))
            {
                multiCatalogHashBuild.buildResultCache ??= AddressablesBuildResultCache.LoadFromJson(buildResultCacheLoadPath);
            }

            if (GUILayout.Button("Build Alternative Remote IP Catalog"))
            {
                multiCatalogHashBuild.buildResultCache ??= AddressablesBuildResultCache.LoadFromJson(buildResultCacheLoadPath);

                if (multiCatalogHashBuild.buildResultCache != null)
                {
                    multiCatalogHashBuild.BuildAlternativeRemoteIPCatalog(
                        multiCatalogHashBuild.buildResultCache.builderInput.ToOriginal(AddressableAssetSettings),
                        multiCatalogHashBuild.buildResultCache.aaContext.ToOriginal(),
                        multiCatalogHashBuild.buildResultCache.buildResult.ToOriginal(),
                        multiCatalogHashBuild.buildResultCache.buildInfos.ToOriginal(),
                        multiCatalogHashBuild.buildResultCache.resourceProviderDataList.ToOriginal());
                    Debug.Log("Build Alternative Remote IP Catalog Performed for MultiCatalogHashBuild.");
                }
                else Debug.LogError("Failed To Build Alternative Remote IP Catalog Performed for MultiCatalogHashBuild.");
            }

            if (GUI.changed)
                EditorUtility.SetDirty(multiCatalogHashBuild);
        }

    }
}