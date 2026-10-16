using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EverflowMap.Editor
{
    public static class EFMapAbilityTests
    {
        public const string Evidence = "docs/ability-validation";
        public static void VerifyAndRunCamera()
        {
            EFMapAbilitySetup.Apply();
            EFMapVerification.Run();
            Audit();
            EFMapCameraTests.Run();
        }
        [MenuItem("Tools/EVERFLOW/Map/Audit Ability Colliders")]
        public static void Audit()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play mode first.");
            EditorSceneManager.OpenScene(EFMapAudit.ScenePath);
            Directory.CreateDirectory(Evidence);
            var world = UnityEngine.Object.FindFirstObjectByType<EFMapWorld>();
            var report = new System.Text.StringBuilder("Saved scene collider audit, before runtime initialization. Not a playthrough.\n");
            foreach (var node in world.stages.SelectMany(s => s.nodes))
            {
                report.AppendLine($"P{node.stage+1} {node.name} {node.kind} interactive={node.interactive} motion={node.motion.name}");
                foreach (var body in node.GetComponentsInChildren<Collider>(true))
                    report.AppendLine($"  {body.name}: enabled={body.enabled}, active={body.gameObject.activeInHierarchy}, trigger={body.isTrigger}, layer={body.gameObject.layer}, center={body.bounds.center}, size={body.bounds.size}, receiver={EFMapAbilityTarget.Resolve(body)?.name ?? "none"}");
            }
            File.WriteAllText(Evidence+"/saved-collider-audit.txt",report.ToString());
            Debug.Log(report.ToString());
        }
        public static void VerifySaved()
        {
            var before = File.ReadAllBytes(EFMapAudit.ScenePath);
            const string prefab = "Assets/EverflowMap/Prefabs/EFMapPlayer.prefab";
            var prefabBefore = File.ReadAllBytes(prefab);
            EFMapAbilitySetup.Apply(); EFMapAbilitySetup.Apply();
            if (!before.SequenceEqual(File.ReadAllBytes(EFMapAudit.ScenePath)) || !prefabBefore.SequenceEqual(File.ReadAllBytes(prefab)))
                throw new Exception("Ability repair is not idempotent: scene/prefab bytes changed.");
            var world = UnityEngine.Object.FindFirstObjectByType<EFMapWorld>();
            foreach (var node in world.stages.SelectMany(s => s.nodes))
                foreach (var collider in node.GetComponentsInChildren<Collider>(true))
                    if (collider.GetComponents<EFMapAbilityTarget>().Length != 1) throw new Exception("Missing/duplicate receiver link: " + collider.name);
            if (world.player.view != world.player.follow.GetComponent<Camera>() || world.player.world != world)
                throw new Exception("Gameplay camera/player/world references mismatch.");
            var water = world.stages.SelectMany(s => s.nodes).Single(n => n.kind == MapKind.Water);
            if (water.GetComponentsInChildren<BoxCollider>().Count(c => c.name == "Ability target - slide surface" && c.isTrigger) != 1)
                throw new Exception("Slide ability and support must be separate colliders.");
            Directory.CreateDirectory(Evidence);
            File.WriteAllText(Evidence+"/saved-reference-results.txt", "PASS Ability repair twice preserves scene and prefab bytes.\nPASS Exactly one explicit receiver link per node collider.\nPASS Gameplay camera/player/world instance references agree.\nPASS One slide query trigger separate from physical surface support.\n");
        }
    }
}
