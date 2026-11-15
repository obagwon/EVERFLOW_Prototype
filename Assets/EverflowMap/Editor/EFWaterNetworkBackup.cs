using System;
using UnityEngine;

namespace EverflowMap.Editor
{
    public sealed class EFWaterNetworkBackup : ScriptableObject
    {
        [Serializable] public sealed class Entry
        {
            public string label;
            public Mesh mesh;
            public Material material;
            public bool suppressed,network,worldUV;
            public Vector3[] impacts;
            public string[] sources;
        }
        public Entry[] entries;
        public bool prototypeSprayActive;
    }
}
