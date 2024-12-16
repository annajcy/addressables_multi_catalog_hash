using System.Collections.Generic;
using UnityEngine;

namespace Script.BuildScript.Editor.MultiCatalogHash.IPConfiguration
{
    [CreateAssetMenu(menuName = "Addressables/Remote IP List", fileName = "RemoteIPList")]
    public class RemoteIPList : ScriptableObject
    {
        public List<RemoteIP> ips = new List<RemoteIP>();
        public int Count => ips.Count;
    }
}