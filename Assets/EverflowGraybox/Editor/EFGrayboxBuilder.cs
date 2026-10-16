using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace EverflowGraybox.Editor
{
    public static partial class EFGrayboxBuilder
    {
        const string Root = "Assets/EverflowMVP/";
        public const string ScenePath = "Assets/Scenes/Everflow_World_Graybox.unity";
        [MenuItem("Tools/EVERFLOW/Graybox/Create New World Copy")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            if (File.Exists(ScenePath)) throw new IOException("World already exists; open it directly. Builder never overwrites authored work.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            TextAsset json = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "Layout.json");
            if (json == null) throw new InvalidOperationException("Missing Assets/EverflowMVP/Layout.json");
            EFLayout layout = JsonUtility.FromJson<EFLayout>(json.text);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var objects = new Dictionary<string, GameObject>();
            var nodes = new Dictionary<string, EFNode>();
            var materials = new Dictionary<string, Material>();
            foreach (string name in new[] { "Stone", "Floor", "Bloom", "Freeze", "Pulse", "Goal", "Player", "Wall" })
                materials[name] = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/" + name + ".mat");
            foreach (EFObjectSpec spec in layout.objects)
            {
                GameObject go;
                if (string.IsNullOrEmpty(spec.shape)) go = new GameObject(spec.id);
                else go = GameObject.CreatePrimitive((PrimitiveType)Enum.Parse(typeof(PrimitiveType), spec.shape));
                go.name = spec.id;
                if (!string.IsNullOrEmpty(spec.parent)) go.transform.SetParent(objects[spec.parent].transform, false);
                go.transform.localPosition = spec.position;
                go.transform.localRotation = Quaternion.Euler(spec.rotation);
                go.transform.localScale = spec.scale;
                Collider body = go.GetComponent<Collider>();
                if (body != null && !spec.collider) UnityEngine.Object.DestroyImmediate(body);
                else if (body != null) body.isTrigger = spec.trigger;
                if (spec.kinematic)
                {
                    Rigidbody rb = go.AddComponent<Rigidbody>();
                    rb.useGravity = false; rb.isKinematic = true;
                }
                Renderer renderer = go.GetComponent<Renderer>();
                if (renderer != null) { renderer.sharedMaterial = materials[spec.material ?? "Stone"]; renderer.enabled = !spec.hidden; }
                objects.Add(spec.id, go);
                if (spec.role == "node") nodes.Add(spec.id, go.AddComponent<EFNode>());
            }
            EFWorld world = objects["World"].AddComponent<EFWorld>();
            EFPlayer player = objects["Player"].AddComponent<EFPlayer>();
            CharacterController cc = player.GetComponent<CharacterController>();
            cc.height = 2; cc.radius = .4f; cc.center = Vector3.zero; cc.stepOffset = .3f; cc.skinWidth = .05f;
            player.gameObject.layer = 2;
            Camera camera = objects["Main Camera"].AddComponent<Camera>();
            camera.tag = "MainCamera"; camera.fieldOfView = 58; camera.farClipPlane = 600;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.11f, .15f, .21f);
            camera.gameObject.AddComponent<AudioListener>();
            EFCamera follow = camera.gameObject.AddComponent<EFCamera>(); follow.player = player;
            Light sun = objects["Directional Light"].AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.2f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.65f, .65f, .7f);
            world.player = player; player.world = world; player.view = camera; player.follow = follow;
            
            Transform T(string id) => !string.IsNullOrEmpty(id) ? objects[id].transform : null;
            EFNode N(string id) => !string.IsNullOrEmpty(id) ? nodes[id] : null;
            foreach (EFObjectSpec spec in layout.objects)
            {
                if (spec.role == "node")
                {
                    EFNode node = nodes[spec.id];
                    node.label = spec.label; node.stage = spec.stage; node.kind = (NodeKind)spec.kind;
                    node.acceptsPulse = spec.acceptsPulse; node.requiresPower = spec.requiresPower;
                    node.alignmentConducts = spec.alignmentConducts; node.supportOnlyWhenFrozen = spec.supportOnlyWhenFrozen;
                    node.supply = N(spec.supply); node.outputs = (spec.outputs ?? new string[0]).Select(N).ToArray();
                    node.growthObject = !string.IsNullOrEmpty(spec.growthObject) ? objects[spec.growthObject] : null;
                    node.handle = T(spec.handle); node.rideStart = T(spec.rideStart); node.rideEnd = T(spec.rideEnd); node.rideVisual = T(spec.rideVisual);
                    node.axis = spec.axis; node.speed = spec.speed; node.amplitude = spec.amplitude; node.targetAngle = spec.targetAngle;
                    node.tolerance = spec.tolerance; node.initialAngle = spec.initialAngle; node.ropeLength = spec.ropeLength; node.launchVelocity = spec.launchVelocity;
                }
                if (spec.role == "zone")
                {
                    objects[spec.id].layer = 2; // Trigger volumes must not intercept ability rays.
                    EFZone zone = objects[spec.id].AddComponent<EFZone>();
                    zone.stage = spec.stage; zone.checkpoint = spec.checkpoint; zone.destination = T(spec.destination);
                }
            }
            world.stages = layout.stages.Select(s => new EFStage { title = s.title, hint = s.hint, spawn = T(s.spawn),
                nodes = s.nodes.Select(N).ToArray(), goalRequirements = s.requirements.Select(N).ToArray() }).ToArray();

            Configure(objects, world, materials);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Save failed");
            Validate();
            Debug.Log("GRAYBOX BUILD PASS: " + ScenePath);
        }
    }
}
