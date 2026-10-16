using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EverflowGraybox.Editor
{
    public static partial class EFGrayboxBuilder
    {
        static void Configure(Dictionary<string, GameObject> objects, EFWorld world, Dictionary<string, Material> mats)
        {
            Transform Group(string name) => new GameObject(name).transform;
            var puzzles = Group("Puzzles");
            var terrain = Group("Terrain");
            var connections = Group("Connections");
            var landmark = Group("Landmark");
            var restoration = Group("Restoration");
            var cameras = Group("Cameras");
            var checkpoints = Group("Checkpoints");
            objects["World"].name = "Systems";
            objects["Player"].transform.SetParent(null, true);
            objects["Main Camera"].transform.SetParent(cameras, true);
            GameObject Box(string name, Vector3 position, Vector3 size, Transform parent, string material = "Stone", bool solid = true)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = name; go.transform.SetParent(parent, false); go.transform.position = position; go.transform.localScale = size;
                go.GetComponent<Renderer>().sharedMaterial = mats[material];
                if (!solid) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
                return go;
            }
            void Path(string name, Vector3 a, Vector3 b, Transform parent, float width = 6, bool solid = true, string mat = "Floor")
            {
                // Endpoints denote walkable top centres. Slab thickness does not alter the slope.
                var d = b - a;
                var go = Box(name, (a + b) * .5f - Vector3.up * .35f, new Vector3(width, .7f, d.magnitude + .15f), parent, mat, solid);
                go.transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
            }
            var origins = new[] { new Vector3(-50,0,-55), new Vector3(-50,3,-8), new Vector3(-40,5,40),
                new Vector3(8,-1,40), new Vector3(55,1,22), new Vector3(40,1,-24) };
            float[] yaw = {0,0,90,90,180,270};
            for (int i = 0; i < 6; i++)
            {
                Transform root = objects[$"Puzzle_{i+1:00}"].transform;
                root.SetParent(puzzles, false); root.position = origins[i]; root.rotation = Quaternion.Euler(0,yaw[i],0);
                // Keep original scale and all local puzzle geometry; remove the lab's continuous tall walls.
                foreach (var child in root.GetComponentsInChildren<Transform>().Where(t => t.name.Contains("Boundary")).ToArray())
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                var goal = objects[$"P{i+1}_Goal"];
                goal.transform.localScale = new Vector3(12,3,3);
                // Low parapets only on safe landings, not across motion envelopes.
                foreach (var child in root.GetComponentsInChildren<Transform>().Where(t => t.name.EndsWith("Landing") || t.name.EndsWith("Start")).ToArray())
                {
                    if (child.GetComponent<Renderer>() == null) continue;
                    Vector3 size = child.localScale;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        var rail = Box(child.name + "_Edge", Vector3.zero, new Vector3(.3f,.5f,size.z), root, "Wall");
                        rail.transform.localPosition = child.localPosition + new Vector3(side * (size.x * .5f - .15f), size.y * .5f + .25f,0);
                        rail.transform.localRotation = Quaternion.identity;
                    }
                }
                // Entry detector is at the approach edge, before any interactable.
                var entry = Box($"P{i+1:00}_Entry", root.TransformPoint(new Vector3(0, i == 3 ? 7.5f : 1.5f, -2)), new Vector3(12,3,2), checkpoints, "Stone");
                entry.transform.rotation = root.rotation;
                entry.GetComponent<Renderer>().enabled = false;
                entry.GetComponent<Collider>().isTrigger = true; entry.layer = 2;
                var trigger = entry.AddComponent<EFGrayboxEntry>(); trigger.world = world; trigger.stage = i;
            }
            // P04: preserve the original launch lip/gap, add landing tolerance and downstream braking room.
            objects["P4_Landing"].transform.localPosition = new Vector3(0,1.5f,42);
            objects["P4_Landing"].transform.localScale = new Vector3(14,1,20);
            // Remove obsolete narrow landing rails after widening.
            foreach (var go in puzzles.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("P4_Landing_Edge")).ToArray()) UnityEngine.Object.DestroyImmediate(go.gameObject);
            // P05 door cannot be skirted using the landing's edge.
            Transform p5 = objects["Puzzle_05"].transform;
            for (int side = -1; side <= 1; side += 2)
            {
                var cheek = Box("DoorRuins", Vector3.zero, new Vector3(.6f,6,8), p5, "Wall");
                cheek.transform.localPosition = new Vector3(side * 6,3,29); cheek.transform.localRotation = Quaternion.identity;
            }
            Box("StartTerrace", new Vector3(0,-1,-55), new Vector3(24,2,20), terrain, "Floor");
            // Reach P01 via the front of its landing, avoiding a side entrance through its parapet.
            Path("StartWest", new Vector3(-10,0,-60), new Vector3(-50,0,-60), connections);
            var initial = Group("S_InitialSpawn"); initial.SetParent(checkpoints); initial.position = new Vector3(0,1.15f,-58); world.initialSpawn = initial;
            var env = world.gameObject.AddComponent<EFGrayboxEnvironment>(); world.environment = env; env.world = world;
            env.restoration = new GameObject[6]; env.exitPaths = new GameObject[6];
            Vector3[][] routes = {
                new[] {new Vector3(-50,3,-17),new Vector3(-50,3,-12)},
                new[] {new Vector3(-50,5,29),new Vector3(-50,5,40),new Vector3(-43,5,40)},
                new[] {new Vector3(-2,5,40),new Vector3(3,5,40)},
                new[] {new Vector3(60,1,40),new Vector3(66,1,40),new Vector3(66,1,27),new Vector3(55,1,27)},
                new[] {new Vector3(55,1,-14),new Vector3(55,1,-24),new Vector3(43,1,-24)},
                new[] {new Vector3(-8,1,-24),new Vector3(-12,1,-24),new Vector3(-12,3,-10),new Vector3(-12,3,-6),new Vector3(0,3,-6)}
            };
            for (int i = 0; i < 6; i++)
            {
                var pathRoot = Group($"P{i+1:00}_RestoredExitPath"); pathRoot.SetParent(connections); env.exitPaths[i] = pathRoot.gameObject;
                for (int j = 1; j < routes[i].Length; j++) Path("SafeConnection", routes[i][j-1],routes[i][j],pathRoot);
                // Corner pads avoid missing collision at 90-degree joints.
                foreach (var point in routes[i])
                    if (!(i == 5 && point == new Vector3(-12,3,-10)))
                        Box("Corner", point - Vector3.up * .35f, new Vector3(6,.7f,6),pathRoot,"Floor");
                var trace = Group($"P{i+1:00}_Restoration"); trace.SetParent(restoration); env.restoration[i] = trace.gameObject;
                Transform goal = objects[$"P{i+1}_Goal"].transform;
                for (int j = 0; j < 5; j++)
                {
                    Vector3 pos = goal.position + goal.parent.right * (j-2) * 2 + Vector3.down * 1.4f;
                    Box("RestoredGlyph",pos,new Vector3(.7f,.15f,.7f),trace,i == 3 ? "Freeze" : "Goal",false);
                    Box("NewGrowth",pos + goal.parent.forward * 2 + Vector3.up * .7f,new Vector3(.7f,1.5f,.7f),trace,"Bloom",false);
                }
                pathRoot.gameObject.SetActive(false); trace.gameObject.SetActive(false);
            }
            var plaza = Box("WaterwheelPlaza",new Vector3(0,2,3),new Vector3(28,2,14),terrain,"Floor");
            env.plaza = Group("W_PlazaArrival"); env.plaza.SetParent(checkpoints); env.plaza.position = new Vector3(0,4.15f,0);
            for (int side = -1; side <= 1; side += 2) Box("FixedPillar", new Vector3(side*14,10,5),new Vector3(2,14,3),landmark,"Wall");
            Box("FixedAxle",new Vector3(0,15,3),new Vector3(30,1,1),landmark,"Wall",false);
            var wheel = Group("WheelVisual"); wheel.SetParent(landmark); wheel.position = new Vector3(0,15,3); env.wheel = wheel;
            for (int i = 0; i < 32; i++)
            {
                float angle = i * Mathf.PI * 2 / 32;
                var rim = Box("Rim", wheel.position + new Vector3(Mathf.Sin(angle)*12,Mathf.Cos(angle)*12,0),new Vector3(2.5f,.8f,3),wheel,"Stone",false);
                rim.transform.rotation = Quaternion.Euler(0,0,-angle*Mathf.Rad2Deg);
            }
            for (int i = 0; i < 8; i++)
            {
                float angle = i*45;
                var spoke = Box("Spoke",wheel.position,new Vector3(.6f,24,1),wheel,"Wall",false); spoke.transform.rotation = Quaternion.Euler(0,0,angle);
            }
            // A non-walkable elevated aqueduct supplies the wheel above its crown.
            var flow = Group("RestoredWaterFlow"); flow.SetParent(landmark); env.waterFlow = flow.gameObject;
            Path("AqueductBed",new Vector3(0,29,34),new Vector3(0,28,4),landmark,2,false,"Wall");
            Path("Inlet",new Vector3(0,29.4f,34),new Vector3(0,28.4f,4),flow,1.4f,false,"Freeze");
            Path("Waterfall",new Vector3(0,28,3),new Vector3(0,3,3),flow,.8f,false,"Freeze");
            Path("Outlet",new Vector3(0,2,-5),new Vector3(0,-2,-43),flow,2,false,"Freeze");
            for (int z = 10; z < 35; z += 10)
                for (int side = -1; side <= 1; side += 2) Box("AqueductPier",new Vector3(side*3,16,z),new Vector3(.9f,25,.9f),landmark,"Wall",false);
            flow.gameObject.SetActive(false);
            var camGO = new GameObject("OverviewCamera"); camGO.transform.SetParent(cameras);
            var cam = camGO.AddComponent<Camera>(); cam.transform.position = new Vector3(0,115,-105); cam.transform.LookAt(new Vector3(0,4,-4));
            cam.orthographic = true; cam.orthographicSize = 80; cam.farClipPlane = 400;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.09f,.13f,.18f); cam.enabled = false; env.overview = cam;
            world.debugStageSelection = false;
            // Complete P02 only after using the falling stone as well as the slate.
            world.stages[1].goalRequirements = world.stages[1].nodes;
            world.player.transform.position = initial.position;
            Polish(world);
        }

        [MenuItem("Tools/EVERFLOW/Graybox/Validate World")]
        public static void Validate()
        {
            var world = UnityEngine.Object.FindFirstObjectByType<EFWorld>();
            if (world == null || world.stages.Length != 6 || world.environment == null) throw new Exception("Missing graybox world");
            if (UnityEngine.Object.FindObjectsByType<EFPlayer>(FindObjectsSortMode.None).Length != 1) throw new Exception("Player count");
            var nodes = new HashSet<EFNode>(world.stages.SelectMany(s => s.nodes));
            if (nodes.Count != 14 || nodes.Contains(null)) throw new Exception("Expected 14 original puzzle nodes");
            for (int i = 0; i < 6; i++)
            {
                var stage = world.stages[i];
                if (stage.spawn == null || stage.goalRequirements.Length == 0) throw new Exception("Stage references " + i);
                foreach (var n in stage.nodes)
                {
                    if (n.stage != i || n.outputs.Any(o => !nodes.Contains(o))) throw new Exception("Power reference " + n.name);
                    if (n.kind == NodeKind.Vine && (n.handle == null || n.growthObject == null)) throw new Exception("Vine references");
                    if (n.kind == NodeKind.Water && (n.supply == null || n.rideStart == null || n.rideEnd == null)) throw new Exception("Ride references");
                }
                if (stage.goalRequirements.Any(n => !nodes.Contains(n))) throw new Exception("Goal references");
            }
            if (world.environment.exitPaths.Length != 6 || world.environment.restoration.Length != 6) throw new Exception("Environment references");
            foreach (var root in world.gameObject.scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0) throw new Exception("Missing script: " + t.name);
            Debug.Log("GRAYBOX VALIDATION PASS: six stages, 14 nodes, one player, local power/ride/goal links, six exit paths and restoration groups.");
        }
        public static void OpenAndValidate()
        {
            EditorSceneManager.OpenScene(ScenePath);
            Validate();
        }
    }
}
