using System;
using System.Collections.Generic;
using UnityEngine;

namespace EverflowMap
{
    // Scene-local host: reuses locomotion without instantiating the stage/puzzle world.
    [DisallowMultipleComponent, DefaultExecutionOrder(-250)]
    public sealed class EFMapExploration : MonoBehaviour
    {
        public EFMapPlayer player;
        public Transform spawnPoint;
        public Transform mapRoot;
        [Tooltip("Exact imported MeshFilter object names. Water, FX and small decoration are excluded.")]
        public string[] solidMeshNames = Array.Empty<string>();
        [Tooltip("Below all exported terrain (-36.60m), including the underground (-12m).")]
        public float fallHeight = -45;
        public bool Paused { get; private set; }

        void Awake()
        {
            ValidateConfiguration();
            EnsureTerrainColliders();
        }
        void Start() { Respawn(); SetPaused(false); }
        void Update()
        {
            if (EFMapInput.KeyDown(KeyCode.Escape)) SetPaused(!Paused);
            if (EverflowGraybox.EFInput.KeyDown(KeyCode.R) || player.transform.position.y < fallHeight)
                Respawn();
        }
        public void ValidateConfiguration()
        {
            if (player == null || spawnPoint == null || mapRoot == null)
                throw new InvalidOperationException("Exploration needs Player, Spawn Point and Map Root scene references.");
            bool cameraConnected = player.cinemachineRig != null
                ? player.follow == null && player.cinemachineRig.player == player &&
                  player.cinemachineRig.brain != null && player.view == player.cinemachineRig.brain.GetComponent<Camera>()
                : player.follow != null && player.follow.player == player && player.view == player.follow.GetComponent<Camera>();
            if (!player.puzzleLabLocomotion || player.world != null || !cameraConnected)
                throw new InvalidOperationException("Exploration requires standalone locomotion and reciprocal player/camera references.");
            if (solidMeshNames == null || solidMeshNames.Length == 0 || fallHeight >= spawnPoint.position.y - 3)
                throw new InvalidOperationException("Exploration needs explicit solid meshes and a fall height below normal terrain.");
        }
        // Runtime by default; the optional Editor menu persists the same colliders as overrides.
        // Two passes validate every entry before any mutation. Existing solid colliders are reused.
        public int EnsureTerrainColliders(Func<GameObject, MeshCollider> add = null)
        {
            var filters = mapRoot.GetComponentsInChildren<MeshFilter>(true);
            var byName = new Dictionary<string, MeshFilter>();
            var requested = new HashSet<string>(solidMeshNames);
            if (requested.Count != solidMeshNames.Length)
                throw new InvalidOperationException("Duplicate exploration collider name.");
            foreach (var filter in filters)
            {
                if (!requested.Contains(filter.name)) continue;
                if (filter.sharedMesh == null || byName.ContainsKey(filter.name))
                    throw new InvalidOperationException("Missing or ambiguous exploration mesh: " + filter.name);
                if (filter.GetComponentInParent<Rigidbody>() != null || filter.gameObject.layer == 2)
                    throw new InvalidOperationException("Exploration terrain must be static and visible to the camera collision mask: " + filter.name);
                byName.Add(filter.name, filter);
            }
            foreach (var name in solidMeshNames)
                if (!byName.ContainsKey(name)) throw new InvalidOperationException("Exploration FBX object not found: " + name);
            int created = 0;
            foreach (var name in solidMeshNames)
            {
                var filter = byName[name];
                bool solid = false;
                foreach (var body in filter.GetComponents<Collider>())
                    if (body.enabled && !body.isTrigger && (!(body is MeshCollider mesh) || mesh.sharedMesh != null)) solid = true;
                if (solid) continue;
                var collider = add != null ? add(filter.gameObject) : filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                collider.convex = false;
                created++;
            }
            Physics.SyncTransforms();
            return created;
        }
        public void Respawn()
        {
            player.transform.rotation = spawnPoint.rotation;
            player.Teleport(spawnPoint.position); // clears ride, velocity, momentum and ground support; snaps camera
        }
        void SetPaused(bool value)
        {
            Paused = value;
            player.enabled = !value;
            if (player.cinemachineRig != null) player.cinemachineRig.SetInputActive(!value);
            else
            {
                player.follow.enabled = !value;
                player.follow.DiscardLookDelta();
            }
            Cursor.lockState = value ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = value;
        }
        void OnApplicationFocus(bool focused) { if (!focused && Application.isPlaying && player != null) SetPaused(true); }
        void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        void OnDrawGizmosSelected()
        {
            if (spawnPoint == null) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(spawnPoint.position, .4f);
            Gizmos.DrawRay(spawnPoint.position, spawnPoint.forward * 3);
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(new Vector3(spawnPoint.position.x, fallHeight, spawnPoint.position.z), new Vector3(120, .05f, 120));
        }
    }
}
