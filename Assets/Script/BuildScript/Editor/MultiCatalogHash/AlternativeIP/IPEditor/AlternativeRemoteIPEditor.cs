using UnityEngine;

namespace Script.BuildScript.Editor.MultiCatalogHash.AlternativeIP.IPEditor
{
    [UnityEditor.CustomEditor(typeof(AlternativeRemoteIP))]
    public class AlternativeRemoteIPEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            AlternativeRemoteIP alternativeRemoteIP = (AlternativeRemoteIP)target;
            if (GUILayout.Button("Load Remote IPs Async"))
                LoadRemoteIpsAsync(alternativeRemoteIP);
        }

        private void LoadRemoteIps(AlternativeRemoteIP alternativeRemoteIP)
        {
            bool success = alternativeRemoteIP.LoadRemoteIps();
            if (success) Debug.Log("Remote IPs loaded successfully.");
            else Debug.LogError("Failed to load Remote IPs.");
        }

        private async void LoadRemoteIpsAsync(AlternativeRemoteIP alternativeRemoteIP)
        {
            bool success = await alternativeRemoteIP.LoadRemoteIpsAsync();
            if (success) Debug.Log("Remote IPs loaded successfully.");
            else Debug.LogError("Failed to load Remote IPs.");
        }
    }
}