using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EverflowMap.Editor
{
    public static class EFV2PuzzleBuild
    {
        public const string Source = "Assets/Scenes/Everflow_Map_v2_test.unity";
        public const string Scene = "Assets/Scenes/Everflow_Map_v2_test_with_puzzle.unity";
        public const string Reports = "docs/v2-puzzle-validation";
        [MenuItem("Tools/EVERFLOW/V2 Puzzles/Open Puzzle Scene")]
        public static void Open()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first.");
            if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(Scene);
        }
        [MenuItem("Tools/EVERFLOW/V2 Puzzles/Validate Saved Scene References")]
        public static void Validate()
        {
            ValidateScene(SceneManager.GetActiveScene());
            Directory.CreateDirectory(Reports);
            File.WriteAllText(Reports+"/unity-scene-reference-check.txt",DateTime.UtcNow.ToString("O")+"\nPASS imported scene references, unique asset GUID, missing scripts and camera/input ownership. NOT traversal or Play.\n");
            Debug.Log("V2 puzzle imported scene references PASS. Enter Play to compose model bindings and support geometry.");
        }
        internal static void ValidateScene(Scene scene)
        {
            if(scene.path!=Scene)throw new InvalidOperationException("Open the independent puzzle scene first.");
            var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
            var worlds=all.Select(t=>t.GetComponent<EFV2PuzzleWorld>()).Where(w=>w!=null).ToArray();
            if(worlds.Length!=1)throw new InvalidOperationException("V2 scene requires one puzzle world.");
            var world=worlds[0];
            if(world.player==null || world.mapRoot==null || world.spawnPoint==null || world.player.view==null)
                throw new InvalidOperationException("V2 world references missing.");
            if(all.Any(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)!=0))
                throw new InvalidOperationException("Missing script in the V2 scene.");
            if(all.Count(t=>t.GetComponent<EFMapPlayer>()!=null)!=1 || all.Any(t=>t.GetComponent<EFMapExploration>()!=null || t.GetComponent<EverflowPuzzleLab.PuzzleLab>()!=null || t.GetComponent<EFMapWorld>()!=null || t.GetComponent<EFMapCamera>()!=null))
                throw new InvalidOperationException("Duplicate legacy owner in the V2 scene.");
            if(world.player.follow!=null || world.player.cinemachineRig==null || world.player.world!=null || !world.player.puzzleLabLocomotion)
                throw new InvalidOperationException("Invalid V2 player/camera ownership.");
            if(AssetDatabase.AssetPathToGUID(Source)==AssetDatabase.AssetPathToGUID(Scene))
                throw new InvalidOperationException("V2 scene GUID collision.");
        }
        public static void Audit()
        {
            Directory.CreateDirectory(Reports);
            EditorSceneManager.OpenScene(Source);
            var host = UnityEngine.Object.FindFirstObjectByType<EFMapExploration>();
            host.ValidateConfiguration(); host.EnsureTerrainColliders();
            var s = new StringBuilder("MAP AUDIT " + DateTime.UtcNow.ToString("O") + "\n");
            s.AppendLine("spawn " + host.spawnPoint.position.ToString("F3"));
            foreach (var r in host.mapRoot.GetComponentsInChildren<MeshRenderer>(true))
                s.AppendLine(r.name + " center=" + r.bounds.center.ToString("F3") + " size=" + r.bounds.size.ToString("F3") + " scale=" + r.transform.lossyScale.ToString("F3") + " pivot=" + r.transform.position.ToString("F3"));
            s.AppendLine("GROUND GRID x,z: heights/name (topmost y<22)");
            for (int z=-45; z<=50; z+=2)
            for (int x=-50; x<=55; x+=2)
            {
                var hits=Physics.RaycastAll(new Vector3(x,22,z),Vector3.down,70,~(1<<2),QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance).ToArray();
                if(hits.Length>0) s.AppendLine($"{x},{z}: {hits[0].point.y:F2} {hits[0].collider.name}");
            }
            File.WriteAllText(Reports+"/map-audit.txt",s.ToString());
            Debug.Log("V2 AUDIT DONE");
        }
    }
}
