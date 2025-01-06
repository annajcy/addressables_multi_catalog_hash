using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Editor.Extenstion.Build.MultiCatalogHash.AlternativeIP
{
    [CreateAssetMenu(menuName = "Addressables/Alternative Remote IP", fileName = "AlternativeRemoteIPSetup")]
    public class AlternativeRemoteIP : ScriptableObject
    {
        public string alternativeIpsUrl;
        public List<RemoteIP> remoteIps;
        public Action<List<RemoteIP>> onRemoteIpsLoaded;

        public static List<RemoteIP> LoadRemoteIpsStatic(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                Debug.LogError("alternativeIpsUrl is not set!");
                return null;
            }

            using UnityWebRequest request = UnityWebRequest.Get(url);
            // 发送请求并等待完成
            var operation = request.SendWebRequest();

            while (!operation.isDone) { } // 阻塞等待请求完成

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string json = request.downloadHandler.text;
                    var remoteIps = JsonUtility.FromJson<RemoteIPListWrapper>(json).ips;
                    Debug.Log($"Successfully loaded {remoteIps.Count} remote IPs.");
                    return remoteIps;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Failed to parse remote IPs: {ex.Message}");
                    return null;
                }
            }
            else
            {
                Debug.LogError($"Failed to download remote IPs: {request.error}");
                return null;
            }
        }

        public bool LoadRemoteIps(string url = null)
        {
            url ??= alternativeIpsUrl;

            if (string.IsNullOrEmpty(url))
            {
                Debug.LogError("alternativeIpsUrl is not set!");
                return false;
            }

            using UnityWebRequest request = UnityWebRequest.Get(url);
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
                    onRemoteIpsLoaded?.Invoke(remoteIps); // 触发事件（如果有）
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

        public async Task<bool> LoadRemoteIpsAsync(string url = null)
        {
            url ??= alternativeIpsUrl;
            if (string.IsNullOrEmpty(url))
            {
                Debug.LogError("alternativeIpsUrl is not set!");
                return false;
            }

            using UnityWebRequest request = UnityWebRequest.Get(url);
            var operation = request.SendWebRequest();

            while (!operation.isDone) { await Task.Yield(); }

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string json = request.downloadHandler.text;
                    remoteIps = JsonUtility.FromJson<RemoteIPListWrapper>(json).ips;
                    Debug.Log($"Successfully loaded {remoteIps.Count} remote IPs.");
                    onRemoteIpsLoaded?.Invoke(remoteIps);
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