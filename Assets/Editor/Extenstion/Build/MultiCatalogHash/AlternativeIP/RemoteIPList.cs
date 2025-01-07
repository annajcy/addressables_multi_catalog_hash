using System;
using System.Collections.Generic;
using UnityEngine;

namespace Editor.Extenstion.Build.MultiCatalogHash.AlternativeIP
{
    [CreateAssetMenu(menuName = "Addressables/Remote IP List", fileName = "RemoteIPList")]
    public class RemoteIPList : ScriptableObject
    {
        public List<RemoteIP> ips = new List<RemoteIP>();
        public int Count => ips.Count;

        public static List<RemoteIP> LoadFromJson(string json)
        {
            try
            {
                var remoteIps = JsonUtility.FromJson<RemoteIPListWrapper>(json).ips;
                Debug.Log($"Successfully loaded {remoteIps.Count} remote IPs.");
                return remoteIps;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return null;
            }
        }
    }
}