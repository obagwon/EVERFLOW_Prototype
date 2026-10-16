using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EverflowMap.Editor
{
    public static class EFMapAbilitySetup
    {
        [MenuItem("Tools/EVERFLOW/Map/Repair Ability Targets")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play mode first.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(EFMapAudit.ScenePath);
            var world = UnityEngine.Object.FindFirstObjectByType<EFMapWorld>();
            if (world == null) throw new Exception("Apply map integration first.");
            if (Upgrade(world)) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
            const string prefabPath = "Assets/EverflowMap/Prefabs/EFMapPlayer.prefab";
            var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                // Save new targeting options into the existing player prefab.
                PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            AssetDatabase.SaveAssets();
            EFMapSetup.Validate();
            Debug.Log("ABILITY REPAIR SAVED: explicit collider receivers, dormant vine tip, separate slide aim/support. Authored models preserved.");
        }

        internal static bool Upgrade(EFMapWorld world)
        {
            bool changed = false;
            var nodes = world.stages.SelectMany(s => s.nodes).ToArray();
            string[] previousHints = {
                "P1: BLOOM flower; stand on the open dome and Space. BLOOM vine; E at handle, then Space to cross east.",
                "P2: FREEZE all three debris at blue marks, jump across them, then FREEZE the slate level. No freeze count limit.",
                "P3: PULSE the crystal. FREEZE the bridge when it connects east and west; cross west to the ridge.",
                "P4: Follow the ridge up. PULSE crystal, FREEZE slide surface, E near the leaf. Land toward the door."
            };
            string[] hints = {
                "P1: BLOOM flower; jump onto its dome, Space to launch. BLOOM the vine bud; approach the handle (jump + E if below it), Space to cross east.",
                "P2: Approach and FREEZE each falling debris at its blue height mark; jump onto it before aiming at the next. FREEZE the slate level. FREEZE again releases; no count limit.",
                "P3: PULSE the crystal. Approach the bridge entrance and aim at the NEAR deck; FREEZE along east-west, then cross west to the ridge.",
                "P4: Climb the ridge, PULSE crystal. Jump onto the reservoir rim to aim at the slide entry; FREEZE the surface. Return to the leaf and E to ride."
            };
            for (int i = 0; i < previousHints.Length; i++)
                if (world.stages[i].hint == previousHints[i]) { world.stages[i].hint = hints[i]; changed = true; }
            var vine = nodes.Single(n => n.kind == MapKind.Vine);
            // The dormant FBX handle is at pivot - .14 m; the old box ended
            // 2 m below it. Cover the visible bud AND the eventual grown handle.
            changed |= Proxy(vine, "Ability target", vine.motion, new Vector3(0, -3.5f, 0), new Vector3(2, 8, 2));
            var water = nodes.Single(n => n.kind == MapKind.Water);
            var terrain = world.gameObject.scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Single(t => t.name == "EF_Terrain");
            Vector3 entry = terrain.position + new Vector3(-24, 24.15f, -43.5f);
            // Query-only entry surface remains aimable when dry/unfrozen while
            // its separate MeshCollider enables physical support only when icy.
            changed |= Proxy(water, "Ability target - slide surface", water.motion,
                water.motion.InverseTransformPoint(entry), new Vector3(4.6f, .7f, 3));
            foreach (var node in nodes)
            foreach (var body in node.GetComponentsInChildren<Collider>(true))
            {
                EFMapNode receiver = node.interactive ? node : null;
                if (node.kind == MapKind.Water && body.gameObject != water.waterSurface.gameObject && body.name != "Ability target - slide surface") receiver = null;
                var link = body.GetComponent<EFMapAbilityTarget>();
                if (link == null) { link = body.gameObject.AddComponent<EFMapAbilityTarget>(); changed = true; }
                if (link.receiver != receiver) { link.receiver = receiver; changed = true; }
            }
            return changed;
        }
        static bool Proxy(EFMapNode node, string name, Transform parent, Vector3 localPosition, Vector3 size)
        {
            bool changed = false;
            var existing = node.GetComponentsInChildren<Transform>(true).Where(t => t.name == name).ToArray();
            if (existing.Length > 1) throw new Exception("Duplicate ability proxy: " + node.name);
            Transform proxy;
            if (existing.Length == 0) { proxy = new GameObject(name).transform; changed = true; }
            else proxy = existing[0];
            if (proxy.parent != parent) { proxy.SetParent(parent, true); changed = true; }
            if (Vector3.Distance(proxy.localPosition, localPosition) > .0001f) { proxy.localPosition = localPosition; changed = true; }
            if (Quaternion.Angle(proxy.localRotation, Quaternion.identity) > .001f) { proxy.localRotation = Quaternion.identity; changed = true; }
            if (proxy.localScale != Vector3.one) { proxy.localScale = Vector3.one; changed = true; }
            var box = proxy.GetComponent<BoxCollider>();
            if (box == null) { box = proxy.gameObject.AddComponent<BoxCollider>(); changed = true; }
            if (box.size != size) { box.size = size; changed = true; }
            if (!box.isTrigger) { box.isTrigger = true; changed = true; }
            return changed;
        }
    }
}
