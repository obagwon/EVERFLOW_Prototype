using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EverflowMap.Editor
{
    public static class EFMapSetup
    {
        const string Root = "Assets/EverflowMap/";
        static Transform systems;
        static Vector3 offset;
        static Dictionary<string, GameObject> models;
        static EFMapWorld world;
        [MenuItem("Tools/EVERFLOW/Map/Apply Integration")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play mode first.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(EFMapAudit.ScenePath);
            world = UnityEngine.Object.FindFirstObjectByType<EFMapWorld>();
            if (world != null)
            {
                bool upgraded=UpdateOpening(world);
                upgraded|=UpdateStairCollision(world);
                upgraded|=EFMapTerrainRepair.Apply(world);
                upgraded|=EFMapAbilitySetup.Upgrade(world);
                upgraded|=EFPresentationSetup.UpgradeMap(world);
                if (upgraded) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
                Validate(); Debug.Log("MAP APPLY: existing integration preserved; no duplicates created."); return;
            }
            models = scene.GetRootGameObjects().Where(g => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(g).Replace('\\', '/').StartsWith("Assets/map_ver1/fbx/"))
                .ToDictionary(g => g.name);
            if (models.Count != 37) throw new Exception($"Expected 37 authored individual model instances, found {models.Count}. No changes made.");
            if (scene.GetRootGameObjects().Any(g => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(g).EndsWith("/map_ver1.fbx")))
                throw new Exception("Reference-only map_ver1.fbx is present. Remove the duplicate intentionally before applying.");
            Directory.CreateDirectory("docs/map-integration");
            if (!File.Exists("docs/map-integration/Everflow_Map.before-integration.unity.txt"))
                File.Copy(EFMapAudit.ScenePath, "docs/map-integration/Everflow_Map.before-integration.unity.txt");
            offset = models["EF_Terrain"].transform.position;
            systems = new GameObject("EF_MapSystems").transform;
            world = systems.gameObject.AddComponent<EFMapWorld>();
            var authored = models.Values.Select(g => new { go = g, p = g.transform.position, q = g.transform.rotation, s = g.transform.lossyScale }).ToArray();
            var modelRoot = new GameObject("EF_World").transform;
            foreach (var model in models.Values) model.transform.SetParent(modelRoot, true);
            var terrainImporter = (ModelImporter)AssetImporter.GetAtPath("Assets/map_ver1/fbx/EF_Terrain.fbx");
            if (!terrainImporter.addCollider) { terrainImporter.addCollider = true; terrainImporter.SaveAndReimport(); }
            foreach (var pair in models)
            {
                if (pair.Key.StartsWith("Restore") || pair.Key == "SM_ValleyWater" || pair.Key == "EF_Terrain" || pair.Key == "SM_Vine" || pair.Key == "SM_FlowerJumpPad" || pair.Key == "SM_VineWire_P5" || pair.Key == "P6_ChannelRepair") continue;
                AddMeshes(pair.Value);
            }
            var flower = Node("SM_FlowerJumpPad", MapKind.Flower, 0, "Flower — BLOOM, then Space on its open dome");
            flower.actions = new[] { Animation("SM_FlowerJumpPad", 1, 50) };
            flower.motion = Part("SM_FlowerJumpPad", "SM_FlowerJumpPad");
            var dome = Part("SM_FlowerJumpPad", "FlowerJumpPad_Dome");
            var domeBody = dome.gameObject.AddComponent<MeshCollider>(); domeBody.sharedMesh = dome.GetComponent<MeshFilter>().sharedMesh;
            flower.support = new Collider[] { domeBody };
            flower.launchTarget = Marker("Flower landing / checkpoint", new Vector3(23, Ground(23, 24) + 1.1f, 24));
            AimProxy(flower, flower.motion.position + Vector3.up * .7f, new Vector3(2.5f, 2.5f, 2.5f));

            var vine = Node("SM_Vine", MapKind.Vine, 0, "Vine — BLOOM, E at handle, Space to release");
            vine.actions = new[] { Animation("SM_Vine", 1, 40) };
            vine.motion = Part("SM_Vine", "SM_Vine"); vine.handle = Part("SM_Vine", "Vine_Handle");
            vine.axis = Vector3.forward; vine.amplitude = 42;
            vine.launchTarget = Marker("Vine landing", new Vector3(44, Ground(44, 22) + 1.1f, 22));
            AimProxy(vine, vine.motion.position + Vector3.down * 4.5f, new Vector3(2, 5, 2));

            world.debris = Enumerable.Range(1, 3).Select(i =>
            {
                string id = "SM_FallingDebris_0" + i;
                var node = Node(id, MapKind.Debris, 1, "Debris " + i + " — FREEZE at blue height mark");
                node.motion = Part(id, id); node.support = node.motion.GetComponents<Collider>();
                AimProxy(node, node.motion.position, new Vector3(4, 1, 4), node.motion);
                return node;
            }).ToArray();
            var slate = Node("SM_Slate", MapKind.Slate, 1, "Slate — FREEZE level");
            slate.motion = Part("SM_Slate", "SM_Slate"); slate.axis = Vector3.right; slate.amplitude = 22;
            var source3 = Source("SM_PowerSource_P3", 2);
            var bridge = Node("SM_RotatingBridge", MapKind.Bridge, 2, "Bridge — PULSE source, FREEZE along the route");
            bridge.motion = Part("SM_RotatingBridge", "SM_RotatingBridge"); bridge.axis = Vector3.up;
            bridge.supply = source3; bridge.initialAngle = 55; bridge.speed = 27;
            var source4 = Source("SM_PowerSource_P4", 3);
            var gate4 = Node("SM_WaterGate_P4", MapKind.Gate, 3, "Watergate P4"); gate4.interactive = false;
            gate4.supply = source4; gate4.actions = new[] { Animation("SM_WaterGate_P4", 1, 30) }; world.gate4 = gate4;
            var water = Node("SM_IceSlide", MapKind.Water, 3, "Slide surface — FREEZE after water arrives");
            water.motion = Part("SM_IceSlide", "SM_IceSlide");
            water.supply = gate4; water.waterSurface = Part("SM_IceSlide", "IceSlide_Surface").GetComponent<Renderer>();
            water.waterMaterial = water.waterSurface.sharedMaterial;
            water.iceMaterial = Material("Ice", new Color(.43f, .84f, 1));
            var iceBody = water.waterSurface.gameObject.AddComponent<MeshCollider>();
            iceBody.sharedMesh = water.waterSurface.GetComponent<MeshFilter>().sharedMesh;
            water.support = new Collider[] { iceBody };
            water.rideVisual = Part("SM_RideLeaf", "SM_RideLeaf");
            water.ridePath = new[]
            {
                Marker("Leaf dock", water.rideVisual.position - offset + Vector3.up * 1.1f),
                Marker("Slide entry", new Vector3(-24, 25.1f, -42)),
                Marker("Slide lip", new Vector3(-24, 12.85f, -6.2f)),
                Marker("Leaf flight", new Vector3(-24, 14.1f, -.5f)),
                Marker("Leaf landing", new Vector3(-26, Ground(-26, 8) + 1.12f, 8))
            };
            world.sideWater = Part("SM_SideChannel", "SideChannel_Water").GetComponent<Renderer>();
            var root5 = Node("SM_VineRoot_P5", MapKind.Root, 4, "Root terminal — BLOOM, then PULSE here");
            root5.motion = Part("SM_VineRoot_P5", "SM_VineRoot_P5"); root5.pulseAtRoot = true;
            root5.actions = new[] { Animation("SM_VineWire_P5", 1, 50) };
            AimProxy(root5, root5.motion.position + Vector3.up * .3f, new Vector3(1.7f, 1.5f, 1.7f));
            // The remote source is deliberately not an ability target. The root is the relay input.
            root5.indicators = new[] { Part("SM_PowerSource_P5", "SM_PowerSource_P5_Crystal").GetComponent<Renderer>() };
            var door = Node("SM_Door_P5", MapKind.Door, 4, "Powered door"); door.interactive = false;
            door.supply = root5; door.actions = new[] { Animation("SM_Door_P5", 1, 30) };
            var source6 = Source("SM_PowerSource_P6", 5);
            var gate6 = Node("SM_WaterGate_P6", MapKind.Gate, 5, "Final watergate"); gate6.interactive = false;
            gate6.supply = source6; gate6.actions = new[] { Animation("SM_WaterGate_P6", 1, 30) };
            var repair = Node("P6_ChannelRepair", MapKind.Repair, 5, "Broken channel roots — BLOOM");
            repair.actions = new[] { Animation("P6_ChannelRepair", 1, 42) };
            AimProxy(repair, offset + new Vector3(0, 16.1f, -19), new Vector3(3, 3, 9));
            var swing = Node("SM_Channel_Swing_P6", MapKind.Slate, 5, "Last channel — FREEZE aligned");
            swing.motion = Part("SM_Channel_Swing_P6", "SM_Channel_Swing_P6");
            // Authored local X follows the channel (its parent/model yaw is 270 degrees).
            swing.axis = Vector3.up; swing.amplitude = 26; swing.tolerance = 7;
            var requirements = new[]
            {
                new[] {flower, vine}, world.debris.Concat(new[] {slate}).ToArray(), new[] {source3, bridge},
                new[] {source4, gate4, water}, new[] {root5, door}, new[] {source6, gate6, repair, swing}
            };
            string[] hints = {
                "P1: BLOOM flower; stand on the open dome and Space. BLOOM vine; E at handle, then Space to cross east.",
                "P2: FREEZE all three debris at blue marks, jump across them, then FREEZE the slate level. No freeze count limit.",
                "P3: PULSE the crystal. FREEZE the bridge when it connects east and west; cross west to the ridge.",
                "P4: Follow the ridge up. PULSE crystal, FREEZE slide surface, E near the leaf. Land toward the door.",
                "P5: BLOOM the root beside the door. After the wire finishes growing, PULSE that same root; go through the door.",
                "P6: PULSE source, BLOOM broken channel roots, FREEZE last channel aligned. Watch water reach the wheel."
            };
            Vector3[] starts = { new Vector3(18, Ground(18,36)+1.12f,36), new Vector3(44,Ground(44,21)+1.12f,21),
                new Vector3(44,Ground(44,-40)+1.12f,-40), new Vector3(17,Ground(17,-42)+1.12f,-42),
                new Vector3(-26,Ground(-26,8)+1.12f,8), new Vector3(-6,Ground(-6,5)+1.12f,5) };
            Vector3[] goals = { new Vector3(44,Ground(44,21)+1,21), new Vector3(44,Ground(44,-40)+1,-40),
                new Vector3(18,Ground(18,-42)+1,-42), new Vector3(-26,Ground(-26,8)+1,8),
                new Vector3(-14.5f,Ground(-14.5f,5)+1,5), new Vector3(0,16,-8) };
            world.stages = Enumerable.Range(0,6).Select(i => new EFMapStage
            {
                hint = hints[i], spawn = Marker("P"+(i+1)+" spawn", starts[i]), arrival = Marker("P"+(i+1)+" arrival",goals[i]),
                arrivalSize = i == 4 ? new Vector3(2,5,5) : new Vector3(6,5,7), requirements = requirements[i],
                nodes = UnityEngine.Object.FindObjectsByType<EFMapNode>(FindObjectsSortMode.None).Where(n => n.stage == i).ToArray()
            }).ToArray();
            world.stages[0].restoration = new[] { Animation("Restore_P1", 1, 50) };
            world.stages[1].restoration = new[] { Animation("Restore_P2", 1, 55) };
            world.stages[3].restoration = new[] { Animation("Restore_P4", 1, 110), Animation("SM_ValleyWater",20,120) };
            world.stages[4].restoration = new[] { Animation("Restore_P5", 1, 75) };
            world.stages[5].restoration = new[] { Animation("Restore_P6", 1, 80) };
            Checkpoint(0,flower.launchTarget, new[] {flower}, new Vector3(7,4,6));
            Checkpoint(1,Marker("P2 intermediate checkpoint",new Vector3(44,Ground(44,-15)+1.12f,-15)),world.debris,new Vector3(7,4,6));
            world.wheel = Part("SM_Waterwheel", "SM_Waterwheel_Wheel");
            // The wheel mesh is in its local XY plane; local Z is its axle.
            world.wheelAxis = Vector3.forward;
            SetupPlayer();
            var camera = GameObject.Find("Main Camera").GetComponent<Camera>();
            var follow = camera.gameObject.AddComponent<EFMapCamera>(); follow.player = world.player;
            camera.farClipPlane = 500; camera.fieldOfView = 60;
            world.player.view = camera; world.player.follow = follow;
            Vector3 goalDelta = world.wheel.position - world.stages[0].spawn.position;
            follow.yaw = Mathf.Atan2(goalDelta.x, goalDelta.z)*Mathf.Rad2Deg; follow.pitch = 8;
            world.player.transform.position = world.stages[0].spawn.position;
            follow.Snap();
            var ending = new GameObject("Final waterway camera",typeof(Camera)).GetComponent<Camera>();
            ending.transform.SetParent(systems,true); ending.transform.position = offset + new Vector3(18,27,12);
            ending.transform.LookAt(offset+new Vector3(0,18,-9)); ending.fieldOfView=58; ending.farClipPlane=500;
            ending.enabled=false; world.endingCamera=ending;
            var flowMaterial = Material("Flow",new Color(.18f,.65f,.83f));
            Vector3[] flowPoints = {new Vector3(0,18.93f,-24.9f),new Vector3(0,18.93f,-23.1f),new Vector3(0,18.87f,-20.6f),
                new Vector3(0,18.81f,-18.3f),new Vector3(0,18.75f,-16),new Vector3(0,18.7f,-14.1f),
                new Vector3(0,18.7f,-11.3f),new Vector3(0,18.7f,-8.3f),new Vector3(0,18.6f,-4.7f)};
            world.finalFlow = Enumerable.Range(1,flowPoints.Length-1).Select(i => FlowSegment(flowPoints[i-1],flowPoints[i],flowMaterial,i)).ToArray();
            foreach(var go in world.finalFlow) go.SetActive(false);
            UpdateOpening(world);
            UpdateStairCollision(world);
            EFMapTerrainRepair.Apply(world);
            world.sideWater.enabled=false; water.waterSurface.enabled=false;
            foreach(var collider in flower.support.Concat(world.debris.SelectMany(n=>n.support))) collider.enabled=false;
            foreach (var before in authored)
                if (Vector3.Distance(before.go.transform.position,before.p)>.0001f || Quaternion.Angle(before.go.transform.rotation,before.q)>.001f || Vector3.Distance(before.go.transform.lossyScale,before.s)>.0001f)
                    throw new Exception("Authored model root transform changed: " + before.go.name);
            ConfigureScenes();
            EFMapAbilitySetup.Upgrade(world);
            EFPresentationSetup.UpgradeMap(world);
            EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene)) throw new Exception("Map save failed.");
            AssetDatabase.SaveAssets(); Validate();
            Debug.Log("MAP INTEGRATION SAVED: 37 original models, 6 stages, player prefab, title route.");
        }
        static Transform Marker(string name,Vector3 p)
        {var t=new GameObject(name).transform;t.SetParent(systems,true);t.position=offset+p;return t;}
        static bool UpdateOpening(EFMapWorld w)
        {
            if(w.openingCamera!=null)return false;
            var terrain=w.gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>()).Single(t=>t.name=="EF_Terrain");
            var origin=terrain.position;
            // Move only the generated spawn away from the north cliff.
            w.stages[0].spawn.position-=Vector3.forward*2;
            w.player.transform.position=w.stages[0].spawn.position;
            w.player.follow.yaw=180;w.player.follow.pitch=18;w.player.follow.distance=7;w.player.follow.Snap();
            var camera=new GameObject("Opening waterwheel view",typeof(Camera)).GetComponent<Camera>();
            camera.transform.SetParent(w.transform,true);camera.transform.position=origin+new Vector3(22,31,37);
            camera.transform.LookAt(w.wheel.position+Vector3.up);camera.fieldOfView=52;camera.farClipPlane=500;camera.enabled=false;
            w.openingCamera=camera;return true;
        }
        static bool UpdateStairCollision(EFMapWorld w)
        {
            const string path=Root+"StairWalkSurface.asset";
            var stairs=w.gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>()).Single(t=>t.name=="T_Stairs");
            var collider=stairs.GetComponent<MeshCollider>();
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh!=null&&collider.sharedMesh==mesh)return false;
            // Diagnose the actual failed controller pose against the open door.
            var door=w.stages[4].nodes.Single(n=>n.kind==MapKind.Door).actions[0];
            door.clip.SampleAnimation(door.gameObject,(door.lastFrame-door.takeFirstFrame)/door.clip.frameRate);
            Physics.SyncTransforms();
            var terrain=stairs.parent.parent;
            Vector3 failedPose=terrain.position+new Vector3(-18.44f,10.73f,5.04f);
            foreach(var hit in Physics.CapsuleCastAll(failedPose-Vector3.up*.6f,failedPose+Vector3.up*.6f,.4f,Vector3.right,.8f))
                Debug.Log("MAP STAIR DIAG: "+hit.collider.name+" distance="+hit.distance+" normal="+hit.normal);
            door.clip.SampleAnimation(door.gameObject,0);
            if(mesh==null)
            {
                // Existing imported collider gets a scene-only sharedMesh override.
                // No second terrain collider; source FBX and its GUID stay unchanged.
                var origin=terrain.position;
                Vector3[] points={new Vector3(-19.6f,9.88f,3.05f),new Vector3(-19.6f,9.88f,6.95f),
                    new Vector3(-8.45f,15.08f,3.05f),new Vector3(-8.45f,15.08f,6.95f)};
                mesh=new Mesh {name="P5 stair walk surface"};
                mesh.vertices=points.Select(p=>stairs.InverseTransformPoint(origin+p)).ToArray();
                mesh.triangles=new[]{0,1,2,2,1,3};mesh.RecalculateNormals();mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh,path);
            }
            collider.sharedMesh=mesh;return true;
        }
        static float Ground(float x,float z)
        {
            Physics.SyncTransforms();
            var terrain=models["EF_Terrain"].transform;
            var hits=Physics.RaycastAll(offset+new Vector3(x,65,z),Vector3.down,100).Where(h=>h.collider.transform.IsChildOf(terrain)&&h.normal.y>.5f).OrderByDescending(h=>h.point.y).ToArray();
            if(hits.Length==0) throw new Exception($"No walkable terrain at model ({x},{z})");
            return hits[0].point.y-offset.y;
        }
        static Transform Part(string model,string name)
        {
            var parts=models[model].GetComponentsInChildren<Transform>(true).Where(t=>t.name==name&&t!=models[model].transform).ToArray();
            if(parts.Length!=1)throw new Exception($"Expected unique {model}/{name}; found {parts.Length}");
            return parts[0];
        }
        static EFMapNode Node(string model,MapKind kind,int stage,string label)
        {
            var n=models[model].AddComponent<EFMapNode>();n.world=world;n.kind=kind;n.stage=stage;n.label=label;n.motion=models[model].transform;return n;
        }
        static EFMapNode Source(string model,int stage)
        {
            var node=Node(model,MapKind.Source,stage,"Power crystal — PULSE");node.motion=Part(model,model);
            node.indicators=new[]{Part(model,model+"_Crystal").GetComponent<Renderer>()};return node;
        }
        static EFMapAnimation Animation(string model,int first,int last)
        {
            var go=models[model];var path=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
            var clip=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().SingleOrDefault(c=>!c.name.StartsWith("__preview"));
            if(clip==null||clip.frameRate<=0||AnimationUtility.GetCurveBindings(clip).Length==0)throw new Exception("Missing usable animation: "+model);
            var importer=(ModelImporter)AssetImporter.GetAtPath(path);
            var takes=importer.defaultClipAnimations;
            if(takes.Length!=1)throw new Exception("Ambiguous animation take: "+model);
            var action=go.AddComponent<EFMapAnimation>();action.clip=clip;action.firstFrame=first;action.lastFrame=last;
            // Imported clip time zero represents the take's first exported frame.
            action.takeFirstFrame=Mathf.RoundToInt(takes[0].firstFrame);
            if(action.takeFirstFrame!=1)throw new Exception($"Unexpected take origin {model}: {takes[0].firstFrame}; inspect before applying frame ranges.");
            if(clip.isLooping)throw new Exception("Looping clip: "+model);
            return action;
        }
        static void AddMeshes(GameObject root)
        {
            foreach(var mesh in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if(mesh.name=="IceSlide_Surface"||mesh.name=="SideChannel_Water")continue;
                if(mesh.sharedMesh==null||mesh.GetComponent<Collider>()!=null)continue;
                mesh.gameObject.AddComponent<MeshCollider>().sharedMesh=mesh.sharedMesh;
            }
        }
        static void AimProxy(EFMapNode node,Vector3 position,Vector3 size,Transform parent=null)
        {
            var go=new GameObject("Ability target");go.transform.SetParent(parent==null?node.transform:parent,true);go.transform.position=position;
            var box=go.AddComponent<BoxCollider>();box.size=size;box.isTrigger=true;
        }
        static Material Material(string name,Color color)
        {
            Directory.CreateDirectory(Root+"Materials");string path=Root+"Materials/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",.8f);AssetDatabase.CreateAsset(material,path);}return material;
        }
        static void SetupPlayer()
        {
            Directory.CreateDirectory(Root+"Prefabs");const string path=Root+"Prefabs/EFMapPlayer.prefab";
            if(AssetDatabase.LoadAssetAtPath<GameObject>(path)==null)
            {
                var go=GameObject.CreatePrimitive(PrimitiveType.Capsule);go.name="EFMapPlayer";
                UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());go.layer=2;
                var cc=go.AddComponent<CharacterController>();cc.height=2;cc.radius=.4f;cc.center=Vector3.zero;cc.skinWidth=.05f;cc.stepOffset=.55f;
                go.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/EverflowMVP/Materials/Player.mat");
                go.AddComponent<EFMapPlayer>();PrefabUtility.SaveAsPrefabAsset(go,path);UnityEngine.Object.DestroyImmediate(go);
            }
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            instance.transform.SetParent(systems,true);world.player=instance.GetComponent<EFMapPlayer>();world.player.world=world;
        }
        static void Checkpoint(int stage,Transform point,EFMapNode[] required,Vector3 size)
        {
            var go=new GameObject("P"+(stage+1)+" checkpoint",typeof(BoxCollider),typeof(EFMapCheckpoint));
            go.transform.SetParent(systems,true);go.transform.position=point.position;go.layer=2;
            var c=go.GetComponent<BoxCollider>();c.isTrigger=true;c.size=size;
            var cp=go.GetComponent<EFMapCheckpoint>();cp.world=world;cp.stage=stage;cp.destination=point;cp.requirements=required;
        }
        static GameObject FlowSegment(Vector3 a,Vector3 b,Material material,int index)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="Temporary channel water "+index;
            go.transform.SetParent(systems,true);go.transform.position=offset+(a+b)*.5f;go.transform.rotation=Quaternion.FromToRotation(Vector3.forward,b-a);
            go.transform.localScale=new Vector3(.8f,.045f,Vector3.Distance(a,b));UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial=material;return go;
        }
        static void ConfigureScenes()
        {
            string title=EverflowMVP.Editor.EFReleaseSetup.TitleScene;
            var scenes=new[]{new EditorBuildSettingsScene(title,true),new EditorBuildSettingsScene(EFMapAudit.ScenePath,true)}
                .Concat(EditorBuildSettings.globalScenes.Where(s=>s.path!=title&&s.path!=EFMapAudit.ScenePath)).ToArray();
            EditorBuildSettings.globalScenes=scenes;
            var profile=BuildProfile.GetActiveBuildProfile();if(profile!=null&&profile.overrideGlobalScenes){profile.scenes=scenes;EditorUtility.SetDirty(profile);}
        }
        [MenuItem("Tools/EVERFLOW/Map/Validate Saved Integration")]
        public static void Validate()
        {
            var w=UnityEngine.Object.FindFirstObjectByType<EFMapWorld>();
            if(w==null||w.stages.Length!=6||w.player==null||w.wheel==null||w.endingCamera==null||w.debris.Length!=3)throw new Exception("Map manager references incomplete.");
            var scene=w.gameObject.scene;
            var components=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
            if(components.Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject))!=0)throw new Exception("Missing scripts in map.");
            if(components.Select(t=>t.GetComponent<EFMapPlayer>()).Count(p=>p!=null)!=1)throw new Exception("Duplicate player.");
            foreach(var s in w.stages)
            {
                if(s.spawn==null||s.arrival==null||s.nodes.Length==0||s.requirements.Any(n=>n==null))throw new Exception("Incomplete stage references.");
                foreach(var n in s.nodes)if(n.world!=w||n.motion==null||n.actions.Any(a=>a==null||a.clip==null))throw new Exception("Incomplete node: "+n.name);
            }
            foreach(var r in components.Select(t=>t.GetComponent<Renderer>()).Where(r=>r!=null))
                if(r.sharedMaterials.Any(m=>m==null||m.shader==null))throw new Exception("Missing material: "+r.name);
            int modelCount=components.Count(t=>PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)&&PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject).Replace('\\','/').StartsWith("Assets/map_ver1/fbx/"));
            if(modelCount!=37)throw new Exception("Model instance count "+modelCount);
            Debug.Log("MAP VALIDATE PASS: 37 FBX instances, 6 stages, no missing scripts/materials; authored model root transforms preserved by Apply.");
        }
    }
}
