using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using EverflowMap;

namespace EverflowPuzzleLab.Editor
{
    public static class PuzzleLabBuilder
    {
        public const string ScenePath = "Assets/Scenes/Everflow_V02_PuzzleLab.unity";
        public const string T01Path = "Assets/Scenes/Everflow_V02_PuzzleLab_T01_Check.unity";
        static Material stone, rootMat, water, metal, leaf, yellow, ice;
        static PuzzleLab lab;
        static Transform parent;
        static Vector3 origin;
        static readonly List<LabArea> areas = new List<LabArea>();
        [MenuItem("Tools/EVERFLOW/v0.2 Puzzle Lab/Create or Open Lab")]
        public static void CreateOrOpen()
        {
            if (!Ready()) return;
            if (File.Exists(ScenePath)) { EditorSceneManager.OpenScene(ScenePath); return; }
            Build(ScenePath, false);
        }
        [MenuItem("Tools/EVERFLOW/v0.2 Puzzle Lab/Generate Fresh Copy (preserve existing)")]
        public static void FreshCopy()
        {
            if (Ready()) Build(AssetDatabase.GenerateUniqueAssetPath(ScenePath), false);
        }
        [MenuItem("Tools/EVERFLOW/v0.2 Puzzle Lab/Create or Open T01 Check")]
        public static void CreateT01()
        {
            if(!Ready()) return;
            if(File.Exists(T01Path)) { EditorSceneManager.OpenScene(T01Path); return; }
            Build(T01Path,true);
        }
        static bool Ready()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first.");
            for (int i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save current scene edits first. No scene was replaced.");
            return true;
        }
        static Material Mat(string name, Color color)
        {
            string path = "Assets/EverflowPuzzleLab/Materials/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, color = color };
            AssetDatabase.CreateAsset(m, path); return m;
        }
        public static void Build(string path, bool onlyT01)
        {
            Ready();
            if (File.Exists(path)) throw new InvalidOperationException("Refusing to overwrite existing scene: " + path);
            Directory.CreateDirectory("Assets/EverflowPuzzleLab/Materials"); AssetDatabase.Refresh();
            stone = Mat("Lab stone", new Color(.36f,.4f,.43f)); rootMat = Mat("Lab root", new Color(.45f,.27f,.12f));
            water = Mat("Lab water", new Color(.05f,.45f,.65f)); metal = Mat("Lab machinery", new Color(.48f,.52f,.6f));
            leaf = Mat("Lab leaf", new Color(.25f,.6f,.22f)); yellow = Mat("Lab target", new Color(.95f,.65f,.15f));
            ice = Mat("Lab ice", new Color(.55f,.85f,1));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            lab = new GameObject("EVERFLOW v0.2 PuzzleLab").AddComponent<PuzzleLab>();
            areas.Clear(); parent = lab.transform; origin = Vector3.zero;
            var sun = new GameObject("Lab directional light").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.4f;
            sun.transform.rotation = Quaternion.Euler(45,-25,0);
            RenderSettings.ambientLight = new Color(.55f,.6f,.65f); RenderSettings.fog = false;
            Player(); BuildT01();
            if (!onlyT01) BuildOtherAreas();
            lab.areas = areas.ToArray();
            parent = lab.transform; origin = Vector3.zero;
            if (!onlyT01) Box("Surface station walkway", new Vector3(36,-.3f,-6), new Vector3(88,.6f,4), stone);
            for (int i = 0; i < 3; i++)
            {
                var orb = Primitive("ONE " + ((LabAbility)i) + " ORB", PrimitiveType.Sphere, new Vector3(i,.7f,-4), Vector3.one*.22f, i==0?leaf:i==1?ice:water);
                UnityEngine.Object.DestroyImmediate(orb.GetComponent<Collider>()); lab.orbs[i] = orb.transform;
            }
            lab.player.Teleport(areas[0].spawn.position);
            LabSignageSetup.Apply(lab);
            PuzzleLabValidation.Validate(lab,onlyT01);
            if(!EditorSceneManager.SaveScene(scene,path)) throw new IOException("Could not save generated scene: "+path);
            AssetDatabase.SaveAssets();
            Debug.Log("PUZZLELAB GENERATED " + path);
        }
        static void Player()
        {
            var go = new GameObject("Player - reused EFMap locomotion");
            var cc = go.AddComponent<CharacterController>(); cc.height = 1.8f; cc.radius = .3f; cc.center = Vector3.zero; cc.stepOffset = .3f;
            lab.player = go.AddComponent<EFMapPlayer>(); lab.player.puzzleLabLocomotion = true;
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule); body.name = "Primitive player"; body.transform.SetParent(go.transform,false);
            body.transform.localScale = new Vector3(.55f,.85f,.55f); body.GetComponent<Renderer>().sharedMaterial = yellow;
            UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
            var camera = new GameObject("Third person - reused EFMapCamera").AddComponent<Camera>(); camera.tag = "MainCamera"; camera.nearClipPlane = .08f; camera.farClipPlane = 250;
            camera.backgroundColor = new Color(.12f,.17f,.2f); camera.clearFlags = CameraClearFlags.Skybox; camera.fieldOfView = 65;
            camera.gameObject.AddComponent<AudioListener>();
            var follow = camera.gameObject.AddComponent<EFMapCamera>(); follow.player = lab.player; follow.yaw = 0; follow.pitch = 18; follow.distance = 6; follow.shoulderOffset = .5f;
            lab.player.view = camera; lab.player.follow = follow;
        }
        static GameObject Primitive(string name, PrimitiveType type, Vector3 p, Vector3 size, Material m)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name;
            go.transform.position = origin+p; go.transform.localScale = size; go.transform.SetParent(parent,true);
            go.GetComponent<Renderer>().sharedMaterial = m; return go;
        }
        static GameObject Box(string name, Vector3 p, Vector3 size, Material m) => Primitive(name,PrimitiveType.Cube,p,size,m);
        static Transform Point(string name, Vector3 p)
        {
            var go = new GameObject(name); go.transform.position = origin+p; go.transform.SetParent(parent,true); return go.transform;
        }
        static TextMesh Label(string text, Vector3 p, float size = .18f)
        {
            var mesh = Point("Sign - " + text.Split('\n')[0],p).gameObject.AddComponent<TextMesh>();
            mesh.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); mesh.GetComponent<Renderer>().sharedMaterial=mesh.font.material;
            mesh.text = text; mesh.fontSize = 48; mesh.characterSize = size; mesh.anchor = TextAnchor.UpperCenter; mesh.alignment = TextAlignment.Center; mesh.color = Color.white;
            return mesh;
        }
        static LabArea Area(string title, string objective, Vector3 position, Vector3 size)
        {
            parent = lab.transform; origin = position;
            var go = new GameObject(title); go.transform.SetParent(parent,false); go.transform.position = position;
            var area = go.AddComponent<LabArea>(); area.lab = lab; area.title = title; area.objective = objective;
            area.bounds = new Bounds(position + new Vector3(4,3,3), size); area.recoveryHeight = position.y-2.5f;
            parent = go.transform; area.spawn = Point("SAFE START",new Vector3(0,1,-3));
            area.sign = Label(title+"\n"+objective,new Vector3(3,4,-2),.15f);
            areas.Add(area); return area;
        }
        static LabTarget Target(LabArea area,string name,Vector3 p, bool bloom=false,bool freeze=false,bool isWater=false)
        {
            var go = Box(name,p,new Vector3(.65f,.65f,.65f),bloom?rootMat:freeze?ice:yellow);
            var t = go.AddComponent<LabTarget>(); t.area = area; t.owner=lab; t.label = name; t.acceptsBloom=bloom; t.acceptsFreeze=freeze; t.water=isWater;
            t.indicators = new[]{go.GetComponent<Renderer>()};
            Label(name+"\n"+(bloom?"BLOOM ":"")+(freeze?(isWater?"FREEZE WATER / X THAW":"FREEZE STRUCTURE / X RELEASE"):""),p+Vector3.up*1.1f,.1f);
            return t;
        }
        static LabTarget Flow(LabArea area,string name,Vector3 p,string[] choices,Vector3[] directions)
        {
            var t = Target(area,name,p); t.acceptsFlow=true; t.directions=choices; t.arrows=new Transform[choices.Length];
            Label("FLOW: click, E preview, click confirm",p+Vector3.up*1.55f,.09f);
            for(int i=0;i<choices.Length;i++)
            {
                var arrowRoot = Point("FLOW " + choices[i],p+new Vector3((i-(choices.Length-1)*.5f)*1.2f,0,1));
                var marker = arrowRoot.gameObject.AddComponent<LabDirection>(); marker.target=t; marker.index=i;
                var old = parent; parent=arrowRoot;
                var shaft = Box("Arrow shaft",p+new Vector3((i-(choices.Length-1)*.5f)*1.2f,0,1),new Vector3(.12f,.12f,.75f),yellow);
                var head=Box("Arrow head",shaft.transform.position-origin+Vector3.forward*.4f,new Vector3(.35f,.12f,.35f),yellow); head.transform.localRotation=Quaternion.Euler(0,45,0);
                parent=old; arrowRoot.rotation=Quaternion.LookRotation(directions[i]); t.arrows[i]=arrowRoot;
                Label((i+1)+": "+choices[i],p+new Vector3((i-(choices.Length-1)*.5f)*1.5f,.7f,1),.08f);
            }
            return t;
        }
        static LabTarget GrowStep(LabArea area,string name,Vector3 center,Vector3 size)
        {
            var t=Target(area,name,center-new Vector3(0,center.y-.3f,0),true);
            var step=Box(name+" grown support",center,size,rootMat); t.grownObjects=new[]{step}; step.SetActive(false); return t;
        }
        static LabMover MovingLeaf(LabArea area,string name,Vector3 p,LabTarget flow,Vector3[] endpoints,bool bloom=false)
        {
            var go=Box(name,p,new Vector3(2.6f,.3f,2.6f),leaf);
            var t=go.AddComponent<LabTarget>(); t.label=name; t.area=area; t.owner=lab; t.acceptsFreeze=true; t.acceptsBloom=bloom;
            t.indicators=new[]{go.GetComponent<Renderer>()};
            if(bloom) { t.growth=go.transform; t.repositionGrowth=false; t.smallScale=new Vector3(1.3f,.3f,1.3f); t.grownScale=new Vector3(3,.3f,3); t.smallPosition=t.grownPosition=go.transform.localPosition; }
            var mover=go.AddComponent<LabMover>(); mover.target=t; mover.motion=go.transform; mover.flow=flow; mover.home=origin+p;
            mover.destinations=Array.ConvertAll(endpoints,x=>origin+x); mover.smallReach=bloom?.3f:1; mover.smallSpeed=.3f;
            for(int i=0;i<endpoints.Length;i++)
            {
                var delta=endpoints[i]-p;
                if(delta.sqrMagnitude<.01f) continue;
                var rail=Box(name+" visual rail "+i,(p+endpoints[i])*.5f-Vector3.up*.3f,new Vector3(.06f,.06f,delta.magnitude),metal);
                rail.transform.rotation=Quaternion.LookRotation(delta); UnityEngine.Object.DestroyImmediate(rail.GetComponent<Collider>());
            }
            // Moving labels inherit motion, separate from the physical surface.
            var old=parent; parent=go.transform;
            Label(name+"\n"+(bloom?"BLOOM unfold | ":"")+"FREEZE / X release",p+Vector3.up*.8f,.1f); parent=old;
            t.Refresh(); return mover;
        }
        static void Goal(LabArea a,Vector3 p)
        {
            a.goal=Point("NORMAL PLAY arrival",p+Vector3.up*1.1f); a.goalSize=new Vector3(3,2.5f,3);
            Box("Goal marking",p+Vector3.up*.03f,new Vector3(2.5f,.06f,2.5f),yellow);
            Label("REACH THIS LEDGE",p+Vector3.up*2,.12f);
        }
        static void BuildT01()
        {
            var a=Area("T01 - A / SAME MOUND","Reach mound: root OR flow leaf + freeze",Vector3.zero,new Vector3(20,16,20));
            Box("T01 departure",new Vector3(0,-.3f,0),new Vector3(5,.6f,10),stone);
            Box("T01 shallow basin floor",new Vector3(6,-.8f,0),new Vector3(8,.5f,10),stone);
            var surface=Box("Shallow water (non-solid)",new Vector3(6,-.35f,0),new Vector3(8,.08f,10),water); UnityEngine.Object.DestroyImmediate(surface.GetComponent<Collider>());
            Box("Mound: top 2.4m > jump 1.35m",new Vector3(6,1.2f,4),new Vector3(5,2.4f,3),stone); Goal(a,new Vector3(6,2.4f,4));
            GrowStep(a,"A ROOT STEP",new Vector3(3, .6f, 2.5f),new Vector3(2,1.2f,2.5f));
            var flow=Flow(a,"A WATER CURRENT",new Vector3(1,.5f,-1),new[]{"MOUND","DEPARTURE"},new[]{Vector3.forward,Vector3.back});
            MovingLeaf(a,"A LEAF",new Vector3(6,1.05f,-3.5f),flow,new[]{new Vector3(6,1.05f,1.5f),new Vector3(3,1.05f,-2.5f)});
            Box("Recovery shelf: below goal, no stair to mound",new Vector3(9,-.2f,-2),new Vector3(2,.4f,3),stone);
            Label("SAFETY ONLY\nBackspace returns to start",new Vector3(9,1.6f,-2),.1f);
        }
        static void BuildOtherAreas() { BuildT02(); BuildT03(); BuildT04(); BuildUnderground(); }
        static LabTarget RootRoute(LabArea area,string name,Vector3 targetPosition,Vector3[] centers,Vector3[] sizes)
        {
            var t=Target(area,name,targetPosition,true); var objects=new List<GameObject>();
            for(int i=0;i<centers.Length;i++) objects.Add(Box(name+" grown segment "+(i+1),centers[i],sizes[i],rootMat));
            t.grownObjects=objects.ToArray(); foreach(var go in t.grownObjects) go.SetActive(false); return t;
        }
        static void BuildT02()
        {
            var a=Area("T02 - B / ROOT TERRACES","Long root route OR wind-driven short platform + freeze",new Vector3(22,0,0),new Vector3(22,16,22));
            Box("B departure",new Vector3(0,-.3f,0),new Vector3(6,.6f,10),stone);
            Box("B landing terrace",new Vector3(11.5f,1.2f,2),new Vector3(4,2.4f,7),stone); Goal(a,new Vector3(11.5f,2.4f,1));
            RootRoute(a,"B LONG ROOT ROUTE",new Vector3(1,.4f,3),
                new[]{new Vector3(3,.6f,5),new Vector3(5.5f,1.2f,5),new Vector3(8,1.2f,5)},
                new[]{new Vector3(2,1.2f,3),new Vector3(3,2.4f,3),new Vector3(3,2.4f,3)});
            var wind=Flow(a,"B WIND ROTOR",new Vector3(0,.6f,-1),new[]{"FORWARD","REVERSE"},new[]{Vector3.right,Vector3.left});
            var mover=MovingLeaf(a,"B ROTARY PLATFORM",new Vector3(4,1.05f,0),wind,new[]{new Vector3(8,1.05f,0),new Vector3(4,1.05f,0)});
            mover.oscillate=true; mover.speed=1; mover.smallSpeed=1;
            var old=parent; parent=mover.motion;
            mover.rotor=Point("B rotor visual",new Vector3(4,2.2f,0)); parent=mover.rotor;
            Box("Rotor horizontal vane",new Vector3(4,2.2f,0),new Vector3(1.8f,.12f,.12f),metal);
            Box("Rotor vertical vane",new Vector3(4,2.2f,0),new Vector3(.12f,1.8f,.12f),metal); parent=old;
            Label("Short route: broad 2.6m deck\nFreeze near terrace; X resumes here",new Vector3(6,4,0),.12f);
        }
        static void BuildT03()
        {
            var a=Area("T03 - C / WIND AND WIDE LEAF","Unfold leaf; choose LOW / HIGH wind; fix, land, release",new Vector3(44,0,0),new Vector3(22,18,24));
            Box("C departure",new Vector3(0,-.3f,0),new Vector3(5,.6f,8),stone);
            Box("C low landing",new Vector3(9,1.2f,0),new Vector3(4,2.4f,4),stone);
            Box("C high landing",new Vector3(9,1.8f,5),new Vector3(4,3.6f,4),stone);
            Goal(a,new Vector3(9,2.4f,0)); a.secondaryGoal=Point("HIGH normal arrival",new Vector3(9,4.7f,5));
            Label("HIGH LANDING / also accepted",new Vector3(9,5.4f,5),.12f);
            var wind=Flow(a,"C WIND ZONE",new Vector3(0,.5f,-1),new[]{"LOW SHELF","HIGH SHELF","RETURN"},new[]{Vector3.right,new Vector3(1,.3f,1),Vector3.left});
            var mover=MovingLeaf(a,"C UNFOLDING LEAF",new Vector3(3.5f,.6f,0),wind,
                new[]{new Vector3(7,1.05f,0),new Vector3(7,2.25f,5),new Vector3(3.5f,.6f,0)},true);
            mover.speed=1.2f; mover.smallReach=.25f;
            Box("C fall recovery floor",new Vector3(5,-2.8f,3),new Vector3(18,.5f,16),stone); a.recoveryHeight=-1.4f;
            Label("SMALL = 25% reach / 30% speed\nBLOOM = full rail reach / broad support",new Vector3(4,4,-1),.11f);
        }
        static Transform Wheel(string name,Vector3 p,float radius)
        {
            var pivot=Point(name,p); var old=parent; parent=pivot;
            var axle=Primitive("Axle",PrimitiveType.Cylinder,p,new Vector3(.2f,.5f,.2f),metal); axle.transform.rotation=Quaternion.Euler(90,0,0);
            for(int i=0;i<8;i++)
            {
                float angle=i*Mathf.PI/4; var paddle=Box("Wheel paddle "+i,p+new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius,0),new Vector3(.45f,.55f,.55f),rootMat);
                paddle.transform.rotation=Quaternion.Euler(0,0,i*45);
                var spoke=Box("Wheel spoke "+i,p+new Vector3(Mathf.Cos(angle)*radius*.5f,Mathf.Sin(angle)*radius*.5f,0),new Vector3(radius,.1f,.12f),metal);
                spoke.transform.rotation=Quaternion.Euler(0,0,i*45);
            }
            parent=old; return pivot;
        }
        static LabGate Gate(LabArea a,string name,Vector3 p,LabTarget flow,int direction,LabTarget root)
        {
            var gate=Point(name,p).gameObject.AddComponent<LabGate>(); gate.flow=flow; gate.wheelDirection=direction; gate.root=root;
            gate.door=Box(name+" door panel",p+Vector3.up*1.4f,new Vector3(3,2.8f,.45f),metal).transform; gate.closed=gate.door.position;
            Box(name+" frame left",p+new Vector3(-1.8f,2,0),new Vector3(.4f,4,.8f),stone);
            Box(name+" frame right",p+new Vector3(1.8f,2,0),new Vector3(.4f,4,.8f),stone);
            Box(name+" frame lintel",p+new Vector3(0,4.2f,0),new Vector3(4,.4f,.8f),stone);
            gate.latch=Box(name+" visible latch",p+new Vector3(1.4f,3.7f,-.6f),new Vector3(.2f,.9f,.2f),yellow).transform;
            gate.wheel=Wheel(name+" small waterwheel",p+new Vector3(3,1.2f,0),.8f);
            Label(name+"\nFULL OPEN = LATCHED",p+new Vector3(0,6,0),.13f); return gate;
        }
        static GameObject Channel(string name,Vector3 a,Vector3 b,float width=1)
        {
            Vector3 delta=b-a; var old=parent; var pivot=Point(name+" trough",(a+b)*.5f); parent=pivot;
            var floor=Box(name+" bed",(a+b)*.5f-Vector3.up*.15f,new Vector3(width,.2f,delta.magnitude),stone);
            floor.transform.rotation=Quaternion.LookRotation(delta);
            var surface=Box(name+" visible flowing surface",(a+b)*.5f,new Vector3(width*.8f,.08f,delta.magnitude),water);
            surface.transform.rotation=Quaternion.LookRotation(delta); UnityEngine.Object.DestroyImmediate(surface.GetComponent<Collider>());
            for(int side=-1;side<=1;side+=2)
            {
                var wall=Box(name+" bank",(a+b)*.5f+Vector3.up*.1f,new Vector3(.12f,.35f,delta.magnitude),stone);
                wall.transform.rotation=Quaternion.LookRotation(delta); wall.transform.position+=wall.transform.right*width*.5f*side;
            }
            var mark=Box(name+" flow direction",(a+b)*.5f+Vector3.up*.08f,new Vector3(.18f,.08f,.6f),yellow); mark.transform.rotation=Quaternion.LookRotation(delta);
            UnityEngine.Object.DestroyImmediate(mark.GetComponent<Collider>());
            // Surface and arrow visibility use exactly the hydraulic result.
            mark.transform.SetParent(surface.transform,true);
            var stream=surface.AddComponent<LabStreamMarker>(); stream.marker=mark.transform;
            stream.localStart=surface.transform.InverseTransformPoint(origin+a+Vector3.up*.08f);
            stream.localEnd=surface.transform.InverseTransformPoint(origin+b+Vector3.up*.08f);
            parent=old; return surface;
        }
        static void BuildT04()
        {
            var a=Area("T04 - G / WATERWHEEL GATE","Waterwheel OR growing root; fully open gate must latch",new Vector3(66,0,0),new Vector3(24,18,24));
            Box("G floor",new Vector3(3,-.3f,2),new Vector3(15,.6f,14),stone);
            // Side walls prevent walking around the gate into its goal.
            Box("G left passage wall",new Vector3(-1.5f,1.8f,5),new Vector3(6,3.6f,.6f),stone);
            Box("G right passage wall",new Vector3(7.5f,1.8f,5),new Vector3(6,3.6f,.6f),stone);
            Box("G left enclosure",new Vector3(-4.5f,1.8f,6),new Vector3(.5f,3.6f,6),stone);
            Box("G right enclosure",new Vector3(10.5f,1.8f,6),new Vector3(.5f,3.6f,6),stone);
            var flow=Flow(a,"G WATER BRANCH",new Vector3(0,.6f,0),new[]{"SMALL WHEEL","SPILL"},new[]{Vector3.right,Vector3.forward});
            var root=GrowStep(a,"G GATE LIFT ROOT",new Vector3(3,1.8f,4.5f),new Vector3(1.1f,3.6f,.8f));
            a.goalGate=Gate(a,"G STONE GATE",new Vector3(3,0,5),flow,0,root);
            Goal(a,new Vector3(3,0,7.5f));
            var visual=Point("G local water visualization",Vector3.zero).gameObject.AddComponent<LabGateFlow>(); visual.flow=flow;
            visual.wheelWater=new[]{Channel("G source to wheel",new Vector3(0,1.7f,2),new Vector3(6,.7f,4))};
            visual.spillWater=new[]{Channel("G source to spill",new Vector3(0,1.7f,2),new Vector3(-2,.5f,6))};
            Box("G existing source",new Vector3(0,1.6f,2),new Vector3(2,.3f,2),water);
        }
        static void BuildUnderground()
        {
            var a=Area("D1-D5 + W / INTEGRATED CAVERN","Connect OR bypass -> open gate -> MAIN -> thaw -> W 3s",new Vector3(22,-10,30),new Vector3(43,30,41));
            a.bounds=new Bounds(origin+new Vector3(0,3,0),new Vector3(42,22,40)); a.recoveryHeight=origin.y-4;
            Box("One open cavern floor",new Vector3(0,-.5f,0),new Vector3(38,1,34),stone);
            Box("Cavern back wall",new Vector3(0,4,16.8f),new Vector3(38,9,.5f),stone);
            Box("Cavern west wall",new Vector3(-19,4,0),new Vector3(.5f,9,34),stone);
            Box("Cavern east wall",new Vector3(19,4,0),new Vector3(.5f,9,34),stone);
            Box("D1 entry balcony",new Vector3(0,4,-13),new Vector3(14,.5f,4),stone);
            a.spawn.position=origin+new Vector3(0,5.2f,-13); a.spawn.rotation=Quaternion.identity;
            a.sign.transform.position=origin+new Vector3(0,8,-11);
            Label("D1 OVERLOOK\nD5 north / D3 west / D2 center / D4 east / W below",new Vector3(0,6.5f,-10),.16f);
            // Two broad flights down, plus a perimeter path back to the workbench.
            for(int i=0;i<8;i++) Box("D1 safe stair "+i,new Vector3(-9,3.6f-i*.48f,-12+i*1.25f),new Vector3(4,.5f,1.4f),stone);
            Box("D3 workbench",new Vector3(-12,1.2f,4),new Vector3(5,2.4f,5),stone);
            for(int i=0;i<10;i++) Box("D3 permanent maintenance stair "+i,new Vector3(-16,.12f+i*.24f,1+i*.6f),new Vector3(3,.24f,.8f),stone);
            Box("D3 maintenance stair landing",new Vector3(-14,2.25f,6.4f),new Vector3(4,.3f,2),stone);
            Label("D3 WEST WORKBENCH\nRoot / dock leaf / ice\nPermanent west stair for recovery",new Vector3(-12,5,4),.12f);
            RootRoute(a,"D3 GROWN VINE PATH",new Vector3(-5,.4f,3),new[]{new Vector3(-7,.6f,4),new Vector3(-9,1.2f,4)},new[]{new Vector3(2.5f,1.2f,2),new Vector3(2.5f,2.4f,2)});
            var branch=Flow(a,"D2 CENTRAL THREE-WAY",new Vector3(0,.6f,0),new[]{"MAINTENANCE DOCK","SMALL WHEEL","MAIN EXIT"},new[]{Vector3.left,Vector3.right,Vector3.back});
            var dockLeaf=MovingLeaf(a,"D3 DOCK LEAF",new Vector3(-4,1.05f,1),branch,new[]{new Vector3(-9,1.05f,2),new Vector3(-4,1.05f,1),new Vector3(-4,1.05f,1)});
            dockLeaf.smallSpeed=1;
            WaterIce(a,"D3 ICE CROSSING",new Vector3(-8,1.075f,0),new Vector3(6,.25f,2));
            // D5 source is above the central splitter; its moving aqueduct ends over the receiver.
            Box("D5 elevated source reservoir",new Vector3(0,4.6f,13),new Vector3(3,.5f,3),water);
            var wind=Flow(a,"D5 ALIGNMENT WIND",new Vector3(-11,3.1f,5),new[]{"CONNECT","LEAK"},new[]{Vector3.right,Vector3.left});
            var aqueduct=MovingLeaf(a,"D5 AQUEDUCT STRUCTURE",new Vector3(-3,3.8f,9),wind,new[]{new Vector3(0,3.8f,9),new Vector3(-3,3.8f,9)});
            aqueduct.motion.localScale=new Vector3(2,.35f,6); aqueduct.speed=.8f; aqueduct.smallSpeed=1;
            aqueduct.motion.GetComponent<Renderer>().sharedMaterial=metal;
            var previous=parent; parent=aqueduct.motion;
            var movingWater=Channel("D5 moving aqueduct water",new Vector3(-3,4.05f,12),new Vector3(-3,3.85f,6),1.5f);
            parent=previous;
            var upperIce=WaterIce(a,"D5 WATER INSIDE (not structure)",new Vector3(-3,4.15f,9),new Vector3(1.5f,.2f,3));
            upperIce.transform.SetParent(aqueduct.motion,true);
            var bypass=GrowStep(a,"D5 BYPASS ROOT GATE",new Vector3(6.2f,2.425f,8),new Vector3(.8f,4.85f,.8f));
            var bypassDoor=Box("D5 bypass sluice panel",new Vector3(5,3.5f,8),new Vector3(2,1.3f,.3f),metal).AddComponent<LabRootLift>();
            bypassDoor.root=bypass; bypassDoor.closed=bypassDoor.transform.position; bypassDoor.opened=bypassDoor.closed+Vector3.up*2;
            Box("D5 bypass gate frame",new Vector3(6.7f,4.1f,8),new Vector3(.3f,3,.5f),stone);
            var mainIce=WaterIce(a,"MAIN ICE WALKWAY / SUPPLY BLOCK",new Vector3(0,.8f,-5),new Vector3(4,.25f,2));
            var liftRoot=GrowStep(a,"D4 GATE LIFT ROOT",new Vector3(8.2f,1.8f,-6.55f),new Vector3(.8f,3.6f,.8f));
            var gate=Gate(a,"D4 EXIT SLUICE",new Vector3(7,0,-7),branch,1,liftRoot); gate.supplied=false;
            var canal=Point("Hydraulic circuit - single reset unit",Vector3.zero).gameObject.AddComponent<LabCanal>();
            a.canal=canal; canal.aqueduct=aqueduct; canal.dockLeaf=dockLeaf; canal.bypassRoot=bypass; canal.upperIce=upperIce; canal.mainIce=mainIce; canal.branch=branch; canal.gate=gate;
            canal.movingWater=movingWater;
            canal.leakWater=new[]{Channel("D5 visible leak",new Vector3(0,4.5f,12),new Vector3(1,.2f,11))};
            Channel("D5 source outlet",new Vector3(0,4.6f,13),new Vector3(0,4.05f,12));
            canal.connectionPoint=origin+new Vector3(0,3.8f,9); canal.connectionTolerance=.5f; canal.successSeconds=3;
            canal.connectedWater=new[]{Channel("D5 receiver to D2",new Vector3(0,3.8f,6),new Vector3(0,1.8f,1))};
            canal.bypassWater=new[]{Channel("D5 bypass first leg",new Vector3(1,4.6f,13),new Vector3(5,3.8f,10)),Channel("D5 bypass root gate leg",new Vector3(5,3.8f,10),new Vector3(5,2.5f,5)),Channel("D5 bypass return",new Vector3(5,2.5f,5),new Vector3(0,1.8f,1))};
            canal.dockWater=new[]{Channel("D2 dock branch",new Vector3(0,1.8f,1),new Vector3(-11,.8f,1))};
            canal.turbineWater=new[]{Channel("D2 small wheel branch",new Vector3(0,1.8f,1),new Vector3(10,.7f,-7))};
            canal.mainWater=new[]{Channel("D2 main to ice barrier",new Vector3(0,1.8f,1),new Vector3(0,.8f,-5))};
            canal.gateApproachWater=new[]{Channel("Main to D4 gate",new Vector3(0,.8f,-5),new Vector3(7,.45f,-7))};
            canal.exitWater=new[]{Channel("D4 open gate to W",new Vector3(7,.45f,-7),new Vector3(7,.2f,-13))};
            canal.wheel=Wheel("W FINAL WATERWHEEL",new Vector3(7,1.7f,-13),1.6f);
            Label("W FINAL RESTORATION\nRequires arriving water for 3 seconds",new Vector3(7,5.3f,-13),.14f);
            canal.readout=Label("LIVE CIRCUIT",new Vector3(4,5,-3),.13f);
            canal.vegetation=new Transform[6]; for(int i=0;i<6;i++) canal.vegetation[i]=Primitive("W restoration plant "+i,PrimitiveType.Cube,new Vector3(4+i*.9f,.4f,-15),Vector3.one*.5f,leaf).transform;
            Label("D5 STRUCTURE FREEZE != WATER FREEZE\nAlign within 0.5m, or grow bypass root\nOrder is free; connection is measured",new Vector3(0,7,9),.13f);
            Label("FAILURE RECOVERY\nDock selected? choose MAIN\nGate shut? SMALL WHEEL or root\nLeak? CONNECT wind / realign\nICE? 2 + aim water + X thaw",new Vector3(13,5,2),.12f);
            canal.ResetCanal();
            // Short surface-to-cavern walking route, 0.5m stairs along an outer service stair.
            for(int i=0;i<10;i++) Box("Surface descent service stair "+i,new Vector3(-12,9.5f-i*.5f,-23+i),new Vector3(4,.5f,1.3f),stone);
            Box("Service stair to balcony",new Vector3(-6,4,-13),new Vector3(12,.5f,3),stone);
            Box("Surface link to stairs",new Vector3(-12,9.7f,-29),new Vector3(4,.6f,12),stone);
        }
        static LabTarget WaterIce(LabArea area,string name,Vector3 p,Vector3 size)
        {
            var go=Box(name,p,size,water); var target=go.AddComponent<LabTarget>(); target.label=name; target.area=area; target.owner=lab; target.acceptsFreeze=true; target.water=true;
            var surface=go.GetComponent<BoxCollider>(); surface.isTrigger=true;
            var support=go.AddComponent<BoxCollider>(); support.enabled=false; target.iceSupport=support; target.indicators=new[]{go.GetComponent<Renderer>()};
            Label(name+"\n2 FREEZE WATER / X THAW",p+Vector3.up*.8f,.1f); return target;
        }
    }
}
