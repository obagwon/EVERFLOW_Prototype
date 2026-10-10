using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EverflowMVP.Editor
{
    public static class EFSceneTools
    {
        const string Root = "Assets/EverflowMVP/";
        [InitializeOnLoadMethod]
        static void AdaptMaterialsOnImport()
        {
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode && Directory.Exists(Root + "Materials") &&
                    AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "Layout.json") != null) PrepareMaterials();
            };
        }
        [MenuItem("Tools/EVERFLOW/Open Included 6-Puzzle Scene")]
        public static void Open()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play mode first."); return; }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(Root + "Scenes/Everflow_6Puzzles.unity");
        }

        [MenuItem("Tools/EVERFLOW/Rebuild 6-Puzzle Scene as New Copy")]
        public static void Rebuild()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play mode first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            TextAsset json = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "Layout.json");
            if (json == null) throw new InvalidOperationException("Missing Assets/EverflowMVP/Layout.json");
            EFLayout layout = JsonUtility.FromJson<EFLayout>(json.text);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var objects = new Dictionary<string, GameObject>();
            var nodes = new Dictionary<string, EFNode>();
            var materials = PrepareMaterials();
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
            objects["World"].AddComponent<EFHud>().world = world;
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
            string path = AssetDatabase.GenerateUniqueAssetPath(Root + "Scenes/Everflow_6Puzzles_Rebuilt.unity");
            if (!EditorSceneManager.SaveScene(scene, path)) throw new IOException("Could not save " + path);
            Selection.activeGameObject = objects["Puzzle_01"];
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
            Debug.Log("Created and saved editable scene: " + path);
        }

        static Dictionary<string, Material> PrepareMaterials()
        {
            var result = new Dictionary<string, Material>();
            Shader shader = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null
                ? Shader.Find("Universal Render Pipeline/Lit") : Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("Use Universal 3D or Built-in 3D.");
            var colors = new Dictionary<string, Color> {
                {"Stone", new Color(.42f,.45f,.49f)}, {"Floor",new Color(.3f,.34f,.38f)},
                {"Bloom",new Color(.22f,.74f,.34f)}, {"Freeze",new Color(.22f,.7f,.95f)},
                {"Pulse",new Color(1,.73f,.16f)}, {"Goal",new Color(.6f,1,.7f)},
                {"Player",Color.white}, {"Wall",new Color(.2f,.25f,.3f)} };
            foreach (var item in colors)
            {
                string path = Root + "Materials/" + item.Key + ".mat";
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
                mat.shader = shader; mat.color = item.Value; EditorUtility.SetDirty(mat); result.Add(item.Key, mat);
            }
            AssetDatabase.SaveAssets();
            return result;
        }

        [MenuItem("Tools/EVERFLOW/Fix Materials for Current Render Pipeline")]
        public static void FixMaterials() { PrepareMaterials(); Debug.Log("EVERFLOW materials updated."); }

        [MenuItem("Tools/EVERFLOW/Validate Open Scene")]
        public static void Validate()
        {
            EFWorld world = UnityEngine.Object.FindFirstObjectByType<EFWorld>();
            if (world == null || world.stages.Length != 6 || world.player == null) throw new InvalidOperationException("Open the EVERFLOW six-puzzle scene first.");
            var registered = new HashSet<EFNode>(world.stages.SelectMany(s => s.nodes));
            if (registered.Contains(null)) throw new InvalidOperationException("A registered node is missing.");
            for (int i = 0; i < 6; i++)
            {
                EFStage stage = world.stages[i];
                if (stage.spawn == null || stage.goalRequirements.Length == 0) throw new InvalidOperationException("Missing spawn or requirements: " + i);
                foreach (EFNode node in stage.nodes)
                {
                    if (node.stage != i || node.outputs.Any(n => n == null || !registered.Contains(n))) throw new InvalidOperationException("Invalid node or output: " + node.name);
                    if (node.kind == NodeKind.Vine && (node.handle == null || node.growthObject == null)) throw new InvalidOperationException("Missing vine references.");
                    if (node.kind == NodeKind.Water && (node.rideStart == null || node.rideEnd == null || node.supply == null)) throw new InvalidOperationException("Missing water references.");
                }
                foreach (EFNode node in stage.goalRequirements) if (!registered.Contains(node)) throw new InvalidOperationException("Unregistered requirement.");
            }
            Debug.Log("PASS: 6 stages; spawns, nodes, power outputs, goals and ride references are present. This does not replace a playthrough.");
        }
    }
}
