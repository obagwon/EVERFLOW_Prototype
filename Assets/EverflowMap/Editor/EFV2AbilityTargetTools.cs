using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace EverflowMap.Editor
{
    public static class EFV2AbilityTargetTools
    {
        [MenuItem("Tools/EVERFLOW/V2 Puzzles/Rebuild Ability Surface Picking Data")]
        public static void Rebuild()
        {
            var w=UnityEngine.Object.FindFirstObjectByType<EFV2PuzzleWorld>();
            if(Application.isPlaying||w==null||w.gameObject.scene.path!=EFV2PuzzleBuild.Scene)throw new InvalidOperationException("Open the puzzle scene outside Play first.");
            var meshes=w.mapRoot.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name.StartsWith("PH_")&&f.sharedMesh!=null&&!f.sharedMesh.isReadable).Select(f=>f.sharedMesh).Distinct();
            var entries=new List<EFV2TargetMeshCache.Entry>();
            foreach(var mesh in meshes)
            {
                using(var data=MeshUtility.AcquireReadOnlyMeshData(mesh))
                {
                    var d=data[0];var vertices=new NativeArray<Vector3>(d.vertexCount,Allocator.Temp);
                    try
                    {
                        d.GetVertices(vertices);var triangles=new List<int>();
                        for(int sub=0;sub<d.subMeshCount;sub++)
                        {
                            var sm=d.GetSubMesh(sub);if(sm.topology!=MeshTopology.Triangles)continue;
                            if(d.indexFormat==IndexFormat.UInt16)
                            {var indices=d.GetIndexData<ushort>();for(int i=0;i<sm.indexCount;i++)triangles.Add(indices[sm.indexStart+i]+sm.baseVertex);}
                            else
                            {var indices=d.GetIndexData<uint>();for(int i=0;i<sm.indexCount;i++)triangles.Add((int)indices[sm.indexStart+i]+sm.baseVertex);}
                        }
                        entries.Add(new EFV2TargetMeshCache.Entry{source=mesh,vertices=vertices.ToArray(),triangles=triangles.ToArray()});
                    }
                    finally{vertices.Dispose();}
                }
            }
            const string path="Assets/EverflowMap/Materials/AbilityPickingData.asset";
            var cache=AssetDatabase.LoadAssetAtPath<EFV2TargetMeshCache>(path);
            if(cache==null){cache=ScriptableObject.CreateInstance<EFV2TargetMeshCache>();AssetDatabase.CreateAsset(cache,path);}
            cache.entries=entries.ToArray();w.presentation.targetPickingData=cache;
            EditorUtility.SetDirty(cache);EditorUtility.SetDirty(w.presentation);AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(w.gameObject.scene);EditorSceneManager.SaveScene(w.gameObject.scene);
            Debug.Log("Ability surface cache rebuilt without changing FBX read/write settings: "+entries.Count+" meshes");
        }
    }
}
