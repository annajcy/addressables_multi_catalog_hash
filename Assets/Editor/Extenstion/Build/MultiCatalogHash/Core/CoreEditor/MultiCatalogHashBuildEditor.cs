using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace Editor.Extenstion.Build.MultiCatalogHash.Core.CoreEditor
{
    [CustomEditor(typeof(MultiCatalogHashBuild))]
    public class MultiCatalogHashBuildEditor : UnityEditor.Editor
    {
        public AddressableAssetSettings addressableAssetSettings;
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

            if (GUILayout.Button("Build Alternative Remote IP Catalog"))
            {
                if (multiCatalogHashBuild.buildResultCache == null)
                    Debug.Log("Build Result Cache is Null");
                else
                {
                    multiCatalogHashBuild.BuildAlternativeRemoteIPCatalog(
                        multiCatalogHashBuild.buildResultCache.builderInput.ToOriginal(addressableAssetSettings),
                        multiCatalogHashBuild.buildResultCache.aaContext.ToOriginal(),
                        multiCatalogHashBuild.buildResultCache.buildResult.ToOriginal(),
                        multiCatalogHashBuild.buildResultCache.catalogs);
                    Debug.Log("Build Alternative Remote IP Catalog Performed for MultiCatalogHashBuild.");
                }
            }

            if (GUI.changed)
                EditorUtility.SetDirty(multiCatalogHashBuild);
        }

    }
}