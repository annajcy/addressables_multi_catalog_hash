using System.Globalization;
using System.Linq;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace Script.BuildScript.Editor.MultiCatalogHash.Core.CoreEditor
{
    [CustomEditor(typeof(AddressablesBuildResultCache))]
    public class AddressablesBuildResultCacheEditor : UnityEditor.Editor
    {
        // 用于引用目标 ScriptableObject
        private AddressablesBuildResultCache cache;

        // 标题样式
        private GUIStyle headerStyle;

        private void OnEnable()
        {
            // 初始化目标对象
            cache = (AddressablesBuildResultCache)target;

            // 初始化标题样式
            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                normal = { textColor = Color.white },
                alignment = TextAnchor.MiddleLeft
            };
        }

        /// <summary>
        /// 绘制 catalogs 列表的信息
        /// </summary>
        private void DrawCatalogs()
        {
            EditorGUILayout.LabelField("Catalogs", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            if (cache.catalogs != null && cache.catalogs.Count > 0)
            {
                for (int i = 0; i < cache.catalogs.Count; i++)
                {
                    var catalog = cache.catalogs[i];
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField($"Catalog {i + 1}", EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;

                    EditorGUILayout.LabelField("Identifier", catalog.identifier);
                    EditorGUILayout.LabelField("File Name", catalog.fileName);
                    EditorGUILayout.LabelField("Build Path", catalog.buildPath ?? "N/A");
                    EditorGUILayout.LabelField("Load Path", catalog.loadPath ?? "N/A");
                    EditorGUILayout.LabelField("Root Build Path", catalog.rootBuildPath ?? "N/A");
                    EditorGUILayout.LabelField("Registered to Settings", catalog.registerToSettings ? "Yes" : "No");

                    // 显示 Locations 的数量
                    EditorGUILayout.LabelField("Locations Count", catalog.locations?.Count.ToString() ?? "0");

                    // 显示 Included Bundles 数量
                    EditorGUILayout.LabelField("Included Bundles Count", catalog.includedBundles?.Count.ToString() ?? "0");

                    EditorGUI.indentLevel--;
                    EditorGUILayout.Space();
                }
            }
            else EditorGUILayout.HelpBox("No Catalogs Available.", MessageType.Info);

            EditorGUI.indentLevel--;
            EditorGUILayout.Space();
        }

        private void DrawBuilderInput()
        {
            // 显示 builderInput
            EditorGUILayout.LabelField("Builder Input", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            if (cache.builderInput != null)
            {
                EditorGUILayout.LabelField("Build Target", cache.builderInput.Target.ToString());
                EditorGUILayout.LabelField("Runtime Catalog Filename", cache.builderInput.RuntimeCatalogFilename);
                EditorGUILayout.LabelField("Player Version", cache.builderInput.PlayerVersion);
                EditorGUILayout.LabelField("Previous Content State", cache.builderInput.PreviousContentState != null ? "Available" : "Null");
            }
            else EditorGUILayout.HelpBox("Builder Input is null.", MessageType.Warning);

            EditorGUI.indentLevel--;

            EditorGUILayout.Space();
        }

        private void DrawAddressableAssetsBuildContext()
        {
            // 显示 AddressableAssetsBuildContext
            EditorGUILayout.LabelField("Addressables Build Context", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            if (cache.aaContext != null)
            {
                bool isSettingsAvailable = cache.aaContext.Settings != null;
                EditorGUILayout.LabelField("Settings", isSettingsAvailable ? "Available" : "Null");
                EditorGUI.indentLevel++;
                if (isSettingsAvailable)
                {
                    int groupIndex = 0;
                    foreach (var groups in cache.aaContext.Settings.groups)
                    {
                        EditorGUILayout.LabelField($"Group {groupIndex ++} : {groups.Name}");
                        int entryIndex = 0;
                        foreach (var addressableAssetEntry in groups.entries)
                            EditorGUILayout.LabelField($"Entry {entryIndex ++} : {addressableAssetEntry.address}");
                    }
                }
                EditorGUI.indentLevel--;

                bool isLocationsAvailable = cache.aaContext.locations != null;
                EditorGUILayout.LabelField("Locations", isLocationsAvailable ? "Available" : "Null");
                EditorGUI.indentLevel++;
                if (isLocationsAvailable)
                {
                    int entryIndex = 0;
                    foreach (var catalogDataEntry in cache.aaContext.locations)
                        EditorGUILayout.LabelField($"Entry {entryIndex ++} : {catalogDataEntry.Keys.First()}");
                }
                EditorGUI.indentLevel--;
            }
            else EditorGUILayout.HelpBox("Addressables Build Context is null.", MessageType.Warning);

            EditorGUI.indentLevel--;

            EditorGUILayout.Space();
        }

        private void DrawBuilderResult()
        {
            // 显示 buildResult
            EditorGUILayout.LabelField("Build Result", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            if (cache.buildResult != null)
            {
                EditorGUILayout.LabelField("Duration", cache.buildResult.Duration.ToString(CultureInfo.InvariantCulture));
                EditorGUILayout.LabelField("Output Path", cache.buildResult.OutputPath);
                EditorGUILayout.LabelField("Succeeded", cache.buildResult.Error == null ? "True" : "False");
                if (cache.buildResult.Error == null)
                {
                    EditorGUILayout.LabelField("Build Result");
                    EditorGUI.indentLevel++;
                    foreach (var result in cache.buildResult.AssetBundleBuildResults)
                    {
                        EditorGUILayout.LabelField(result.FilePath);
                        EditorGUI.indentLevel++;
                        EditorGUILayout.LabelField("Hash", result.Hash);
                        EditorGUILayout.LabelField("Internal Bundle Name", result.InternalBundleName);
                        EditorGUILayout.LabelField("Source Asset Group", result.SourceAssetGroup.Name);
                        EditorGUI.indentLevel--;
                        EditorGUILayout.Space();
                    }
                    EditorGUI.indentLevel--;
                }
            }
            else EditorGUILayout.HelpBox("Build Result is null.", MessageType.Warning);

            EditorGUI.indentLevel--;
            EditorGUILayout.Space();
        }

        public override void OnInspectorGUI()
        {
            // 绘制自定义 Inspector 界面
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Addressables Build Result Cache", headerStyle);
            EditorGUILayout.Space();

            DrawBuilderInput();
            DrawAddressableAssetsBuildContext();
            DrawBuilderResult();
            DrawCatalogs();
        }
    }
}