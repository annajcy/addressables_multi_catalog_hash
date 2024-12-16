using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Script.BuildScript.Editor.MultiCatalogHash.IPConfiguration
{
    [CreateAssetMenu(menuName = "Addressables/Alternative Remote IP", fileName = "AlternativeRemoteIPSetup")]
    public class AlternativeRemoteIP : ScriptableObject
    {
        public string alternativeIpsUrl;
        public List<RemoteIP> remoteIps;
        public Action<List<RemoteIP>> OnRemoteIpsLoaded;

        public bool LoadRemoteIps()
        {
            if (string.IsNullOrEmpty(alternativeIpsUrl))
            {
                Debug.LogError("alternativeIpsUrl is not set!");
                return false;
            }

            using UnityWebRequest request = UnityWebRequest.Get(alternativeIpsUrl);
            // 发送请求并等待完成
            var operation = request.SendWebRequest();

            while (!operation.isDone) { } // 阻塞等待请求完成

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string json = request.downloadHandler.text;
                    remoteIps = JsonUtility.FromJson<RemoteIPListWrapper>(json).ips;
                    Debug.Log($"Successfully loaded {remoteIps.Count} remote IPs.");
                    OnRemoteIpsLoaded?.Invoke(remoteIps); // 触发事件（如果有）
                    return true;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Failed to parse remote IPs: {ex.Message}");
                    return false;
                }
            }
            else
            {
                Debug.LogError($"Failed to download remote IPs: {request.error}");
                return false;
            }
        }

        public async Task<bool> LoadRemoteIpsAsync()
        {
            if (string.IsNullOrEmpty(alternativeIpsUrl))
            {
                Debug.LogError("alternativeIpsUrl is not set!");
                return false;
            }

            using UnityWebRequest request = UnityWebRequest.Get(alternativeIpsUrl);
            var operation = request.SendWebRequest();

            while (!operation.isDone) { await Task.Yield(); }

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string json = request.downloadHandler.text;
                    remoteIps = JsonUtility.FromJson<RemoteIPListWrapper>(json).ips;
                    Debug.Log($"Successfully loaded {remoteIps.Count} remote IPs.");
                    OnRemoteIpsLoaded?.Invoke(remoteIps);
                    return true;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Failed to parse remote IPs: {ex.Message}");
                    return false;
                }
            }
            else
            {
                Debug.LogError($"Failed to download remote IPs: {request.error}");
                return false;
            }
        }
    }
}