using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EverflowMap.Editor
{
    public static class EFMapExplorationTools
    {
        static EFMapExploration Host()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/Everflow_Map_v2_test.unity")
                throw new InvalidOperationException("Open Everflow_Map_v2_test first.");
            var hosts = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<EFMapExploration>(true)).ToArray();
            if (hosts.Length != 1) throw new InvalidOperationException("Expected exactly one exploration host.");
            hosts[0].ValidateConfiguration();
            return hosts[0];
        }
        [MenuItem("Tools/EVERFLOW/V2 Exploration/Bake Terrain Colliders In Open Scene")]
        public static void Bake()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first.");
            var host = Host();
            int count = host.EnsureTerrainColliders(go => Undo.AddComponent<MeshCollider>(go));
            if (count > 0) EditorSceneManager.MarkSceneDirty(host.gameObject.scene);
            Debug.Log($"V2 exploration: added {count} mesh colliders. Save the scene to persist overrides; original FBX unchanged.");
        }
        [MenuItem("Tools/EVERFLOW/V2 Exploration/Validate Open Scene")]
        public static void Validate()
        {
            var host = Host();
            var objects = host.gameObject.scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
            Require(objects.Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)) == 0, "Missing Script");
            Require(objects.Count(t => t.GetComponent<EFMapPlayer>() != null) == 1, "Exactly one player");
            Require(objects.Count(t => t.GetComponent<Camera>() != null && t.GetComponent<Camera>().enabled && t.gameObject.activeInHierarchy) == 1, "Exactly one active camera");
            Require(objects.Count(t => t.GetComponent<AudioListener>() != null && t.GetComponent<AudioListener>().enabled && t.gameObject.activeInHierarchy) == 1, "Exactly one AudioListener");
            Require(objects.All(t => t.GetComponent<EFMapWorld>() == null), "No legacy puzzle World");
            Require(!Physics.GetIgnoreLayerCollision(0, host.player.gameObject.layer), "Terrain/player layer collision");
            foreach (var filter in host.mapRoot.GetComponentsInChildren<MeshFilter>(true))
                if (host.solidMeshNames.Contains(filter.name))
                    Require(filter.GetComponents<Collider>().Any(c => c.enabled && !c.isTrigger),
                        "Terrain collider missing on " + filter.name + ". Enter Play or use Bake Terrain Colliders first.");
            var cc = host.player.GetComponent<CharacterController>();
            var feet = host.spawnPoint.position + cc.center - Vector3.up * cc.height * .5f;
            Require(Physics.Raycast(feet + Vector3.up * .1f, Vector3.down, out var hit, 1.5f, ~(1 << 2), QueryTriggerInteraction.Ignore), "Spawn floor within 1.5m of feet");
            Require(hit.normal.y > .7f && feet.y >= hit.point.y - .05f, "Spawn on safe floor");
            Require(!Physics.CheckCapsule(host.spawnPoint.position + cc.center + Vector3.up * (cc.height * .5f - cc.radius),
                host.spawnPoint.position + cc.center - Vector3.up * (cc.height * .5f - cc.radius), cc.radius - cc.skinWidth,
                ~(1 << 2), QueryTriggerInteraction.Ignore), "Spawn capsule clear");
            Debug.Log("V2 exploration references/physics configuration PASS. This does not verify traversal or human controls.");
        }
        static void Require(bool valid, string message)
        {
            if (!valid) throw new InvalidOperationException("V2 exploration validation: " + message);
        }
    }
}
