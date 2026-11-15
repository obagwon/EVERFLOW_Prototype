using System;
using UnityEngine;
namespace EverflowMap
{
    // Small selection-only triangle cache. Original FBX import settings and colliders stay intact.
    public sealed class EFV2TargetMeshCache:ScriptableObject
    {
        [Serializable] public sealed class Entry
        {public Mesh source;public Vector3[] vertices;public int[] triangles;}
        public Entry[] entries=Array.Empty<Entry>();
    }
}
