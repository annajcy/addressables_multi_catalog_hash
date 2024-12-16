using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Script.BuildScript.Editor.MultiCatalogHash.IPConfiguration.IPEditor
{
    [UnityEditor.CustomEditor(typeof(RemoteIPList))]
    public class RemoteIPListEditor : UnityEditor.Editor
    {
        private string outputFileName = "remote_ips.json";
        private string outputFolderPath = "ServerData";
        private string jsonFilePath = "ServerData/remote_ips.json";

        public override void OnInspectorGUI()
        {
            RemoteIPList remoteIPList = (RemoteIPList)target;

            EditorGUILayout.LabelField("Remote IP List Editor", EditorStyles.boldLabel);

            // 显示并编辑远程 IP 列表
            remoteIPList.ips ??= new List<RemoteIP>();

            EditorGUILayout.Space();
            for (int i = 0; i < remoteIPList.ips.Count; i++)
            {
                EditorGUILayout.BeginVertical();

                // 输入 IP 和端口
                remoteIPList.ips[i].identifier = EditorGUILayout.TextField("Identifier", remoteIPList.ips[i].identifier);
                remoteIPList.ips[i].ip = EditorGUILayout.TextField("IP", remoteIPList.ips[i].ip);
                remoteIPList.ips[i].port = EditorGUILayout.TextField("Port", remoteIPList.ips[i].port);

                // 删除按钮
                if (GUILayout.Button("Remove", GUILayout.Width(60)))
                {
                    remoteIPList.ips.RemoveAt(i);
                    EditorUtility.SetDirty(remoteIPList); // 标记为已修改
                    break;
                }

                EditorGUILayout.EndVertical();
            }

            // 添加新 IP 按钮
            if (GUILayout.Button("Add New Remote IP"))
            {
                remoteIPList.ips.Add(new RemoteIP($"ip_{remoteIPList.Count}", "127.0.0.1", "8080"));
                EditorUtility.SetDirty(remoteIPList);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Export Settings", EditorStyles.boldLabel);

            // 导出设置
            outputFileName = EditorGUILayout.TextField("Output File Name", outputFileName);
            outputFolderPath = EditorGUILayout.TextField("Output Folder Path", outputFolderPath);

            if (GUILayout.Button("Export to JSON"))
                ExportToJson(remoteIPList);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Import Settings", EditorStyles.boldLabel);

            // JSON 文件路径输入框和按钮
            jsonFilePath = EditorGUILayout.TextField("JSON File Path", jsonFilePath);

            if (GUILayout.Button("Load from JSON"))
            {
                LoadFromJson(remoteIPList);
            }

            // 保存更改
            if (GUI.changed)
            {
                EditorUtility.SetDirty(remoteIPList);
            }
        }

        private void ExportToJson(RemoteIPList remoteIPList)
        {
            if (string.IsNullOrEmpty(outputFileName))
            {
                Debug.LogError("Output file name cannot be empty.");
                return;
            }

            string fullPath = Path.Combine(outputFolderPath, outputFileName);

            try
            {
                string json = JsonUtility.ToJson(new RemoteIPListWrapper { ips = remoteIPList.ips }, true);
                File.WriteAllText(fullPath, json);
                Debug.Log($"Remote IP list exported successfully to {fullPath}");
                AssetDatabase.Refresh(); // 刷新 Asset 数据库
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error exporting Remote IP list: {ex.Message}");
            }
        }

        private void LoadFromJson(RemoteIPList remoteIPList)
        {
            if (string.IsNullOrEmpty(jsonFilePath))
            {
                Debug.LogError("Please specify a valid JSON file path.");
                return;
            }

            if (!File.Exists(jsonFilePath))
            {
                Debug.LogError($"JSON file not found: {jsonFilePath}");
                return;
            }

            try
            {
                string json = File.ReadAllText(jsonFilePath);
                RemoteIPListWrapper wrapper = JsonUtility.FromJson<RemoteIPListWrapper>(json);

                if (wrapper != null && wrapper.ips != null)
                {
                    remoteIPList.ips = wrapper.ips;
                    EditorUtility.SetDirty(remoteIPList); // 标记为已修改
                    Debug.Log($"Successfully loaded {wrapper.ips.Count} Remote IPs from JSON file.");
                }
                else
                {
                    Debug.LogError("Invalid JSON file format.");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error loading JSON file: {ex.Message}");
            }
        }
    }
}