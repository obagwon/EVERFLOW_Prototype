using System;
using UnityEngine;
namespace EverflowMap.Editor
{
    public sealed class EFNaturalRiverBackup:ScriptableObject
    {
        [Serializable]public sealed class Ground
        {public string name;public Mesh mesh;public Material[] materials;}
        public Ground[] ground=Array.Empty<Ground>();
        public Mesh waterBefore;
        public Vector3[] impactsBefore;
    }
}
