using UnityEngine;

namespace EverflowMap.Editor
{
    // Stable asset references let the local prototype be reverted after an Editor restart.
    public sealed class EFLeakPrototypeBackup : ScriptableObject
    {
        public string[] labels;
        public Mesh[] meshes;
        public Material[] overrides;
        public bool[] suppressed;
    }
}
