using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace Editor.Extenstion.Build.MultiCatalogHash.Core.CoreEditor
{
    [CustomEditor(typeof(MultiCatalogHashBuild))]
    public class MultiCatalogHashBuildEditor : UnityEditor.Editor
    {
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

            EditorGUILayout.Space();

            if (GUILayout.Button("Build Alternative Remote IP Catalog"))
            {
                string buildResultCacheLoadUrl = multiCatalogHashBuild.buildResultCacheLoadUrl;
                string alternativeIpsUrl = multiCatalogHashBuild.alternativeIpsUrl;
                MultiCatalogHashBuild.BuildAlternativeRemoteIPCatalog(buildResultCacheLoadUrl, alternativeIpsUrl);
            }

            if (GUI.changed)
                EditorUtility.SetDirty(multiCatalogHashBuild);
        }

    }
}