using System.Collections.Generic;
using UnityEngine;

namespace Editor.Extenstion.Build.MultiCatalogHash.AlternativeIP
{
    [CreateAssetMenu(menuName = "Addressables/Remote IP List", fileName = "RemoteIPList")]
    public class RemoteIPList : ScriptableObject
    {
        public List<RemoteIP> ips = new List<RemoteIP>();
        public int Count => ips.Count;
    }
}