using System;
using System.Collections.Generic;
using UnityEngine;

namespace Script.BuildScript.Editor.MultiCatalogHash.IPConfiguration
{
    [Serializable]
    public class RemoteIPListWrapper
    {
        public List<RemoteIP> ips;
    }

    [Serializable]
    public class RemoteIP
    {
        public string identifier;
        public string ip;
        public string port;

        public string Address => ip + ":" + port;

        public RemoteIP(string identifier, string ip, string port)
        {
            this.identifier = identifier;
            this.ip = ip;
            this.port = port;
        }
    }
}