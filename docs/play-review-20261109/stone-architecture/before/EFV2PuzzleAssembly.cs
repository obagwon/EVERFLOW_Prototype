using System;
using System.Collections.Generic;
using System.Linq;
using EverflowPuzzleLab;
using Unity.Cinemachine;
using UnityEngine;

namespace EverflowMap
{
    // Scene-local composition. No source FBX, prefab, lab or Build Settings writes.
    // All moving model children and their colliders share unit-scale world-space carriers.
    public static class EFV2PuzzleAssembly
    {
        public static Quaternion WheelInitialRotation { get; private set; }
        sealed class Builder
        {
            readonly EFV2PuzzleWorld w;
            readonly Dictionary<string,MeshFilter> meshes;
            readonly Transform root;
            readonly Material stone,wood,leaf,water,ice,gold;
            readonly List<string> bindings=new List<string>();
            public Builder(EFV2PuzzleWorld world)
            {
                w=world;
                meshes=w.mapRoot.GetComponentsInChildren<MeshFilter>(true).ToDictionary(m=>m.name);
                root=Point("V2 Puzzle Runtime - model bindings and support geometry",Vector3.zero,w.transform);
                stone=Mat("V2 stone",new Color(.31f,.37f,.38f));wood=Mat("V2 roots",new Color(.35f,.25f,.12f));
                leaf=Mat("V2 leaves",new Color(.3f,.65f,.23f));water=Mat("V2 flowing water",new Color(.08f,.6f,.85f));
                ice=Mat("V2 ice",new Color(.55f,.88f,1));gold=Mat("V2 aim / landing",new Color(1,.7f,.12f));
            }
            Material Mat(string name,Color color)
            {
                var shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
                return new Material(shader){name=name,color=color};
            }
            Transform Point(string name,Vector3 p,Transform parent=null)
            {
                var t=new GameObject(name).transform;t.position=p;t.SetParent(parent,true);return t;
            }
            GameObject Box(string name,Vector3 p,Vector3 size,Material mat,Transform parent=null,bool solid=true)
            {
                var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.position=p;go.transform.localScale=size;
                go.transform.SetParent(parent??root,true);go.GetComponent<Renderer>().sharedMaterial=mat;
                if(!solid) {go.GetComponent<Collider>().enabled=false;}
                return go;
            }
            MeshFilter Mesh(string name)
            {
                if(!meshes.TryGetValue(name,out var m) || m.sharedMesh==null) throw new InvalidOperationException("V2 required model missing: "+name);
                return m;
            }
            Bounds Bounds(string name)=>Mesh(name).GetComponent<Renderer>().bounds;
            Transform Carrier(string name,bool boxCollider=true,bool pivotAtModel=false)
            {
                var m=Mesh(name);var bounds=m.GetComponent<Renderer>().bounds;
                var carrier=Point("MOVING / "+name,pivotAtModel?m.transform.position:bounds.center,root);
                foreach(var c in m.GetComponents<Collider>()) c.enabled=false;
                m.gameObject.isStatic=false;m.transform.SetParent(carrier,true);
                if(boxCollider)
                {
                    var c=carrier.gameObject.AddComponent<BoxCollider>();c.center=bounds.center-carrier.position;c.size=bounds.size;
                }
                else
                {
                    var c=m.gameObject.AddComponent<MeshCollider>();c.sharedMesh=m.sharedMesh;c.convex=false;
                }
                bindings.Add(name+" pivot="+m.transform.position.ToString("F3")+" bounds="+bounds+" importedScale="+m.transform.lossyScale.ToString("F4")+" carrierScale=1");
                return carrier;
            }
            Transform Rotor(string name)
            {
                var b=Bounds(name);var t=Point("PIVOT / "+name,b.center,root);
                var m=Mesh(name);foreach(var c in m.GetComponents<Collider>()) c.enabled=false;
                m.gameObject.isStatic=false;m.transform.SetParent(t,true);return t;
            }
            TextMesh Label(string text,Vector3 p,float size=.15f,Transform parent=null)
            {
                var t=Point("SIGN / "+text.Split('\n')[0],p,parent??root);
                var label=t.gameObject.AddComponent<TextMesh>();label.text=text;label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                label.GetComponent<Renderer>().sharedMaterial=label.font.material;label.fontSize=52;label.characterSize=size*.22f;
                label.anchor=TextAnchor.LowerCenter;label.alignment=TextAlignment.Center;label.color=Color.white;
                var billboard=t.gameObject.AddComponent<EFV2Billboard>();billboard.view=w.player.view;billboard.world=w;
                return label;
            }
            LabTarget Target(string name,Vector3 p,bool bloom=false,bool freeze=false,bool liquid=false,Transform parent=null)
            {
                var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.name=name;go.transform.position=p;go.transform.localScale=Vector3.one*.65f;
                go.transform.SetParent(parent??root,true);go.GetComponent<Collider>().isTrigger=true;
                var r=go.GetComponent<Renderer>();r.sharedMaterial=bloom?leaf:freeze?ice:water;r.enabled=false;
                // Retain the original aim collider; the smaller display no longer masks the model.
                var t=go.AddComponent<LabTarget>();t.label=name;t.acceptsBloom=bloom;t.acceptsFreeze=freeze;t.water=liquid;t.indicators=Array.Empty<Renderer>();
                w.targets.Add(t);
                var sign=Label(name,p+Vector3.up*.7f,.1f,go.transform);
                sign.GetComponent<EFV2Billboard>().target=t;sign.GetComponent<EFV2Billboard>().world=w;
                return t;
            }
            LabTarget Flow(string name,Vector3 p,string[] choices,Vector3[] directions)
            {
                var t=Target(name,p);t.acceptsFlow=true;t.directions=choices;
                // Direction choices live in the compact cursor UI; remove debug block arrows.
                t.arrows=Array.Empty<Transform>();return t;
            }
            void Growth(LabTarget t,string withered,GameObject[] objects)
            {
                var m=Mesh(withered);t.growth=m.transform;t.repositionGrowth=false;t.smallScale=m.transform.localScale;t.grownScale=t.smallScale*1.8f;
                t.grownObjects=objects;foreach(var go in objects)go.SetActive(false);
                t.indicators=t.indicators.Concat(new[]{m.GetComponent<Renderer>()}).ToArray();
                (m.GetComponent<EFV2TargetProxy>()??m.gameObject.AddComponent<EFV2TargetProxy>()).target=t;
            }
            GameObject Steps(string name,Vector3 from,Vector3 to,float width,Material material,Transform parent=null)
            {
                var t=Point(name,Vector3.zero,parent??root);
                float rise=Mathf.Abs(to.y-from.y),distance=Vector2.Distance(new Vector2(from.x,from.z),new Vector2(to.x,to.z));
                int count=Mathf.Max(2,Mathf.CeilToInt(Mathf.Max(rise/.28f,distance/.65f)));
                Vector3 forward=Vector3.ProjectOnPlane(to-from,Vector3.up).normalized;
                var rotation=Quaternion.LookRotation(forward);
                float run=distance/count;
                for(int i=0;i<count;i++)
                {
                    var p=Vector3.Lerp(from,to,(i+.5f)/count);
                    var step=Box(name+" step "+i,p-Vector3.up*.2f,new Vector3(width,.4f,run+.08f),material,t);
                    step.transform.rotation=rotation;
                }
                // A matching support plane prevents losing ground between short treads at low Editor frame rates.
                // It exists only with this visible stair group, so it cannot bypass an un-grown puzzle.
                var support=Point(name+" continuous tread support",Vector3.zero,t).gameObject;
                Vector3 right=Vector3.Cross(Vector3.up,forward)*(width*.5f-.04f);
                var mesh=new UnityEngine.Mesh{name=name+" walk support"};
                mesh.vertices=new[]{from-right,from+right,to-right,to+right};mesh.triangles=new[]{0,2,1,1,2,3};mesh.RecalculateNormals();mesh.RecalculateBounds();
                support.AddComponent<MeshCollider>().sharedMesh=mesh;
                return t.gameObject;
            }
            GameObject Deck(string name,Vector3 surface,Vector2 size,Material mat,Transform parent=null)
                =>Box(name,surface-Vector3.up*.18f,new Vector3(size.x,.36f,size.y),mat,parent);
            LabMover Mover(string model,string label,Vector3 homeSurface,Vector3 endSurface,LabTarget flow,bool bloom=false,float speed=1.8f)
            {
                var b=Bounds(model);var carrier=Carrier(model);
                carrier.position+=homeSurface-new Vector3(b.center.x,b.max.y,b.center.z);
                var t=Target(label,carrier.position+Vector3.up*(b.size.y*.5f+.65f)+Vector3.right*.8f,bloom,true,false,carrier);
                t.indicators=t.indicators.Concat(new[]{Mesh(model).GetComponent<Renderer>()}).ToArray();
                carrier.gameObject.AddComponent<EFV2TargetProxy>().target=t;
                if(bloom)
                {
                    t.growth=Mesh(model).transform;t.repositionGrowth=false;t.smallScale=t.growth.localScale;t.grownScale=t.smallScale*1.75f;
                    // Broad support grows with the visible leaf; the unit carrier stays unscaled.
                    // A bounding rectangle protruded beyond the leaf and looked like a second platform.
                    // Copy the grown model's exact pose into an unrendered collision shell instead.
                    var plate=Point(label+" grown leaf support",Vector3.zero,carrier).gameObject;
                    plate.transform.localPosition=t.growth.localPosition;plate.transform.localRotation=t.growth.localRotation;plate.transform.localScale=t.grownScale;
                    plate.AddComponent<MeshCollider>().sharedMesh=Mesh(model).sharedMesh;
                    t.grownObjects=new[]{plate};plate.SetActive(false);
                }
                var m=carrier.gameObject.AddComponent<LabMover>();m.target=t;m.flow=flow;m.motion=carrier;
                m.home=carrier.position;m.destinations=new[]{carrier.position+(endSurface-homeSurface),carrier.position};
                m.oscillate=true;m.speed=speed;m.smallReach=.3f;m.smallSpeed=.4f;w.movers.Add(m);
                return m;
            }
            LabGate Gate(string name,string door,string rootModel,string wheelModel,string latchModel,Vector3 rootAim,LabTarget flow,int wheelDirection,float lift)
            {
                var gate=Point(name,Vector3.zero,root).gameObject.AddComponent<LabGate>();
                gate.root=Target(name+" ROOT",rootAim,true);Growth(gate.root,rootModel,Array.Empty<GameObject>());
                gate.flow=flow;gate.wheelDirection=wheelDirection;gate.door=Carrier(door);gate.closed=gate.door.position;
                gate.wheel=Rotor(wheelModel);gate.latch=Rotor(latchModel);gate.lift=lift;gate.speed=3;
                w.gates.Add(gate);return gate;
            }
            EFV2Checkpoint Checkpoint(string label,Vector3 surface,Vector3 size,LabMover mover=null,bool workbench=false,bool garden=false,float yaw=180,bool addDeck=true)
            {
                var t=Point("SAFE / "+label,surface+Vector3.up,root);t.rotation=Quaternion.Euler(0,yaw,0);
                var volume=t.gameObject.AddComponent<BoxCollider>();volume.size=size;volume.isTrigger=true;
                var cp=t.gameObject.AddComponent<EFV2Checkpoint>();cp.world=w;cp.label=label;cp.safePosition=surface+Vector3.up*1.08f;
                cp.localMover=mover;cp.workbench=workbench;cp.garden=garden;w.checkpoints.Add(cp);
                if(addDeck)Deck("Safe landing / "+label,surface,new Vector2(Mathf.Min(3,size.x),Mathf.Min(3,size.z)),stone);
                Label(label,surface+Vector3.up*2.2f,.1f);
                return cp;
            }
            GameObject[] Water(params string[] names)=>names.Select(n=>Mesh(n).gameObject).ToArray();
            void Ground()
            {
                var moving=new HashSet<string>{"PH_A_Leaf01","PH_A_Leaf02","PH_A_Tut_MovingPad","PH_B_MovingStone","PH_C_LeafPlatform","PH_D2_Leaf","PH_G_StoneDoor","PH_D4_Gate","PH_D5_BypassDoor","PH_D5_MovingChannel"};
                foreach(var name in w.solidMeshNames)
                {
                    var m=Mesh(name);
                    if(moving.Contains(name)) {foreach(var c in m.GetComponents<Collider>())c.enabled=false;continue;}
                    if(m.GetComponents<Collider>().Any(c=>c.enabled&&!c.isTrigger))continue;
                    var body=m.gameObject.AddComponent<MeshCollider>();body.sharedMesh=m.sharedMesh;body.convex=false;
                }
                // These tutorial pads are decorative in v2, never a second route across the pond.
                foreach(var n in new[]{"PH_A_Tut_MovingPad","PH_A_Tut_WitheredPad","PH_A_Leaf02"})
                    foreach(var c in Mesh(n).GetComponents<Collider>())c.enabled=false;
                Physics.SyncTransforms();
            }
            void Surface()
            {
                w.spawnPoint.position=new Vector3(-46,1.2f,-6);w.spawnPoint.rotation=Quaternion.Euler(0,225,0);
                Label("S  /  RESTORE THE VALLEY\nLEFT: G HIGH RUIN  |  RIGHT: W WATERWHEEL",new Vector3(-46,2.5f,-9),.18f);
                // The common landing is measured against the team's 6m mound, independent of either solution.
                Deck("A common mound landing",new Vector3(-45,6.05f,-28),new Vector2(12,3.2f),stone);
                Deck("A to C bridge threshold",new Vector3(-50.8f,6.08f,-30.1f),new Vector2(3,2.5f),stone);
                var aRoot=Target("A ROOT STAIRS",new Vector3(-40.4f,1.0f,-15),true);
                // Put the existing non-solid withered root at its actual interaction point.
                // Its old decorative position was hidden beyond the mound; the floating sphere
                // previously substituted for the root. This moves no support/collision geometry.
                var rootMarker=Mesh("PH_A_Root01_Withered");
                rootMarker.transform.position+=new Vector3(-40.4f,.35f,-14.9f)-rootMarker.GetComponent<Renderer>().bounds.center;
                var stairs=Steps("A grown root staircase",new Vector3(-40.4f,.05f,-15),new Vector3(-40.4f,6.05f,-27),3.2f,wood);
                Growth(aRoot,"PH_A_Root01_Withered",new[]{stairs});
                var aFlow=Flow("A POND CURRENT",new Vector3(-49,1.4f,-14.5f),new[]{"둔덕 착지점 / MOUND","시작 물가 / SHORE"},new[]{new Vector3(-1,1,-2),Vector3.forward});
                var a=Mover("PH_A_Leaf01","A FLOATING LEAF",new Vector3(-49,.28f,-16.2f),new Vector3(-50.5f,5.35f,-26.5f),aFlow);
                Checkpoint("S / A SHORE",new Vector3(-46,.15f,-13.3f),new Vector3(6,3,3),a);
                Checkpoint("A COMMON MOUND",new Vector3(-45,6.08f,-28),new Vector3(6,3,4));
                Label("B  ROOT TERRACE  >>>\n<<<  C  WIND GARDEN",new Vector3(-45,8,-30),.14f);

                var bFlow=Flow("B WINDMILL",new Vector3(-28.6f,7.6f,-42),new[]{"돌 발판 건너편 / FAR BANK","입구 / RETURN"},new[]{Vector3.back,Vector3.forward});
                var b=Mover("PH_B_MovingStone","B MOVING STONE",new Vector3(-24,6.08f,-42.2f),new Vector3(-24,6.08f,-49.8f),bFlow,false,1.5f);
                b.rotor=Rotor("PH_B_Windmill_Rotor");
                // Move the existing withered bridge marker from its unused outlying shelf to the playable gap.
                var bRootMesh=Mesh("PH_B_RootBridge_Withered").transform;bRootMesh.position+=new Vector3(-16,0,3);
                var bRoot=Target("B ROOT BRIDGE",new Vector3(-22,7.2f,-41.8f),true);
                var bridge=Deck("B grown root bridge",new Vector3(-26.4f,6.08f,-46),new Vector2(2.4f,9.8f),wood);
                Growth(bRoot,"PH_B_RootBridge_Withered",new[]{bridge});
                Deck("B approach",new Vector3(-25,6.05f,-41),new Vector2(7,3.5f),stone);
                Deck("B far bank",new Vector3(-25,6.05f,-51),new Vector2(7,3.5f),stone);
                Checkpoint("B ENTRANCE",new Vector3(-27,6.1f,-40.5f),new Vector3(4,3,3),b);
                Checkpoint("B FAR BANK",new Vector3(-25,6.1f,-51.5f),new Vector3(5,3,3));
                Steps("B ascent to ruin",new Vector3(-25,6.1f,-52),new Vector3(-34.5f,15.15f,-58.8f),2.8f,stone);
                Label("B: ROOT BRIDGE OR WINDMILL + FROZEN STONE",new Vector3(-24,8.5f,-42),.12f);

                var cFlow=Flow("C WIND",new Vector3(-62,7.4f,-42),new[]{"위쪽 선반 / HIGH LEDGE","입구 / RETURN"},new[]{new Vector3(1,1,-3),Vector3.forward});
                var c=Mover("PH_C_LeafPlatform","C GROWABLE LEAF",new Vector3(-64,6.25f,-44.2f),new Vector3(-62,11.45f,-56.4f),cFlow,true,2);
                Checkpoint("C ENTRANCE",new Vector3(-64,6.12f,-40),new Vector3(5,3,4),c);
                Deck("C visible high landing",new Vector3(-62,11.85f,-59),new Vector2(5.5f,4.5f),stone);
                Checkpoint("C HIGH LEDGE",new Vector3(-62,11.9f,-59),new Vector3(4,3,3));
                Steps("C ascent to ruin",new Vector3(-61,11.9f,-60),new Vector3(-57,15.15f,-62),2.4f,stone);
                Deck("C ruin edge link",new Vector3(-55.5f,15.15f,-62.5f),new Vector2(5,2.5f),stone);
                Label("C: BLOOM LEAF -> FLOW TO HIGH LEDGE\nFREEZE: HOLD POSITION / CLICK AGAIN: RELEASE",new Vector3(-64,9,-43),.12f);
                // Connects the two entrances, never either far bank: it cannot substitute for B/C.
                Deck("B-C intermediate switching walkway",new Vector3(-44,6.1f,-40),new Vector2(33,2),wood);
                Label("B <-> C  /  SWITCH ROUTES",new Vector3(-45,8,-40),.13f);

                Deck("G door approach",new Vector3(-31.5f,15.06f,-60.2f),new Vector2(8,2.4f),stone);
                var gFlow=Flow("G WATER BRANCH",new Vector3(-43.7f,16.25f,-61),new[]{"작은 수차 / OPEN DOOR","유출 / DIVERT"},new[]{Vector3.right,Vector3.forward});
                w.ruinGate=Gate("G RUIN GATE","PH_G_StoneDoor","PH_G_DoorRoot_Withered","PH_G_SmallWheel","PH_G_Latch",new Vector3(-30.8f,16.1f,-60.7f),gFlow,0,8);
                w.ruinWater=Water("W_G_ToWheel");
                Checkpoint("G RUIN",new Vector3(-35,15.16f,-60.5f),new Vector3(4,3,2.2f));
                Label("G: FLOW TO WHEEL OR BLOOM ROOT\nFULLY OPEN -> LATCHED -> WALK INTO THE TOWER",new Vector3(-30,19,-60),.12f);
            }
            void Underground()
            {
                // Existing tower switchback, tunnel and D1 stairs retain their exact mesh colliders.
                float[] heights={15,12,9,6,3,0,-3};
                for(int i=0;i<heights.Length;i++)
                {
                    float z=i%2==0?-63:-69;
                    Checkpoint("TOWER "+i,new Vector3(-29.75f,heights[i]+.02f,z),new Vector3(3,2.2f,.65f));
                }
                Checkpoint("D1 BALCONY",new Vector3(-48,-2.97f,-38.7f),new Vector3(6,3,4));
                Checkpoint("D1 MID STAIRS",new Vector3(-59.5f,-7.4f,-47),new Vector3(3,3,1.5f),addDeck:false);
                Label("D1  /  BROKEN D5 ABOVE - CLOSED D4 ACROSS THE HALL\nLEFT: D3 WORKBENCH / BELOW: D2 WATER BRANCH",new Vector3(-51,-.4f,-40.5f),.13f);
                Checkpoint("D1 STAIR LANDING",new Vector3(-59.5f,-11.5f,-53),new Vector3(3,3,3),null,false,false,90);
                var vine=Target("D3 VINE ACCESS",new Vector3(-40,-1.7f,-38.2f),true);
                var path=Steps("D3 grown vine path",new Vector3(-40,-2.98f,-38),new Vector3(-39,-7.45f,-45),2.4f,wood);
                Growth(vine,"PH_D3_VinePath_Withered",new[]{path});
                w.mainIce=Target("D2 MAIN WATER / ICE BRIDGE",new Vector3(-56,-10.1f,-52),false,true,true);
                var frozen=Box("D2 ICE WALK / blocks final water",new Vector3(-55.7f,-11.72f,-52),new Vector3(18.4f,.3f,11.7f),ice);
                w.mainIce.iceSupport=frozen.GetComponent<Collider>();w.mainIce.iceSupport.enabled=false;
                frozen.AddComponent<EFV2TargetProxy>().target=w.mainIce;
                frozen.AddComponent<EFV2IceVisual>().target=w.mainIce;frozen.GetComponent<EFV2IceVisual>().ice=frozen.GetComponent<Renderer>();
                var hazard=Point("Deep basin - retry locally",new Vector3(-56,-12.15f,-53),root);
                var hc=hazard.gameObject.AddComponent<BoxCollider>();hc.size=new Vector3(18,.65f,12);hc.isTrigger=true;
                hazard.gameObject.AddComponent<EFV2WaterHazard>().world=w;

                w.branch=Flow("D2 THREE WAY BRANCH",new Vector3(-56,-9.55f,-50.8f),new[]{"정비 부두 / DOCK LEAF","작은 수차 / OPEN D4","주 출구 / TO WATERWHEEL"},new[]{Vector3.right,Vector3.left,new Vector3(-1,0,1)});
                w.dockLeaf=Mover("PH_D2_Leaf","D2 DOCK LEAF - AFTER SUPPLY",new Vector3(-52,-11.55f,-51),new Vector3(-45.6f,-11.35f,-52),w.branch,false,1.3f);
                w.dockLeaf.powered=false;
                // Branch has 3 choices; only choice 0 drives the leaf (others are stopped by powered).
                Checkpoint("D2 CENTRAL HUB",new Vector3(-56,-11.1f,-52),new Vector3(3.5f,3,3.5f),null,false,false,90);
                Label("D3: VINE FROM BALCONY OR FREEZE BASIN\nDOCK LEAF IS A SHORTCUT AFTER D5 SUPPLY",new Vector3(-43,-5.8f,-55),.12f);
                Checkpoint("D3 WORKBENCH",new Vector3(-40,-7.46f,-57),new Vector3(4,3,5),null,true,false,180);
                var safety=Point("D3 latched return walking route",Vector3.zero,root);
                Deck("D3 return edge landing",new Vector3(-43.5f,-7.45f,-59.2f),new Vector2(3.2f,2),wood,safety);
                Steps("D3 return stairs",new Vector3(-45.15f,-7.45f,-59.2f),new Vector3(-50,-11.5f,-59.2f),2.4f,wood,safety);
                Deck("D3 return perimeter",new Vector3(-51,-11.5f,-59.6f),new Vector2(12,2),wood,safety);
                Deck("D3 return to hub",new Vector3(-56,-11.5f,-56.5f),new Vector2(2,7.5f),wood,safety);
                w.returnWalk=safety.gameObject;w.returnWalk.SetActive(false);
                Steps("D3 workbench to D5 ledge",new Vector3(-40,-7.45f,-62),new Vector3(-46,-5.95f,-66),2.3f,stone);
                // Keep the service route, with a dry crossing above the fixed channel and its rails.
                Deck("D5 west service walkway",new Vector3(-64.55f,-5.95f,-63.9f),new Vector2(11.9f,2.4f),wood);
                Steps("D5 service bridge west ramp",new Vector3(-58.6f,-5.95f,-63.9f),new Vector3(-57.6f,-5.35f,-63.9f),2.4f,wood);
                Deck("D5 dry channel crossing",new Vector3(-56,-5.35f,-63.9f),new Vector2(3.2f,2.4f),wood);
                Steps("D5 service bridge east ramp",new Vector3(-54.4f,-5.35f,-63.9f),new Vector3(-53.4f,-5.95f,-63.9f),2.4f,wood);
                Deck("D5 east service walkway",new Vector3(-48.45f,-5.95f,-63.9f),new Vector2(9.9f,2.4f),wood);

                var flow=Flow("D5 ALIGNING WIND",new Vector3(-40,-6.05f,-61.4f),new[]{"수로 정렬 / ALIGN CHANNEL","수로 분리 / MISALIGN"},new[]{new Vector3(-1,0,-1),Vector3.right});
                var m=Mesh("PH_D5_MovingChannel");var motion=Carrier(m.name,false,true);
                w.channelStructure=Target("D5 CHANNEL STRUCTURE",motion.position+Vector3.up*2.1f,false,true,false,motion);
                w.channelStructure.indicators=w.channelStructure.indicators.Concat(new[]{m.GetComponent<Renderer>()}).ToArray();
                motion.gameObject.AddComponent<EFV2TargetProxy>().target=w.channelStructure;
                var moving=motion.gameObject.AddComponent<LabMover>();moving.motion=motion;moving.target=w.channelStructure;moving.flow=flow;
                moving.home=motion.position;moving.destinations=new[]{moving.home+new Vector3(-.5f,0,-.5f),moving.home+new Vector3(1.5f,0,0)};
                moving.speed=.23f;moving.oscillate=true;w.movers.Add(moving);w.channel=moving;
                w.channelPose=motion.gameObject.AddComponent<EFV2ChannelPose>();w.channelPose.mover=moving;w.channelPose.model=m.transform;
                w.channelPose.initial=m.transform.rotation;w.channelPose.aligned=Quaternion.identity;
                w.connectionPoint=Point("D5 physical alignment socket / model pivot",moving.destinations[0],root);
                Box("D5 alignment socket marker",new Vector3(-55.6f,-5.6f,-68),new Vector3(.2f,.2f,1.8f),gold,null,false);
                w.upperIce=Target("D5 INTERNAL WATER - NOT STRUCTURE",new Vector3(-55.8f,-4.4f,-66.8f),false,true,true);
                var upperFrozen=Box("D5 internal ice",new Vector3(-56,-5.9f,-66.6f),new Vector3(1.6f,.3f,3.7f),ice);
                w.upperIce.iceSupport=upperFrozen.GetComponent<Collider>();w.upperIce.iceSupport.enabled=false;
                upperFrozen.AddComponent<EFV2IceVisual>().target=w.upperIce;upperFrozen.GetComponent<EFV2IceVisual>().ice=upperFrozen.GetComponent<Renderer>();
                w.bypass=Target("D5 BYPASS ROOT",new Vector3(-74.5f,-7.5f,-64.5f),true);
                Growth(w.bypass,"PH_D5_BypassRoot_Withered",Array.Empty<GameObject>());
                var bypassDoor=Carrier("PH_D5_BypassDoor");var lift=bypassDoor.gameObject.AddComponent<LabRootLift>();
                lift.root=w.bypass;lift.closed=bypassDoor.position;lift.opened=lift.closed+Vector3.up*5;
                Steps("D5 ledge to bypass access",new Vector3(-70.1f,-5.95f,-65),new Vector3(-74,-8.9f,-64.8f),2.3f,stone);
                Checkpoint("D5 SERVICE LEDGE",new Vector3(-64,-5.95f,-65.5f),new Vector3(4,3,2.5f));
                w.supplySign=Label("D5 LEAK - ALIGN OR OPEN BYPASS",new Vector3(-49,-2.8f,-65),.12f);
                w.exitGate=Gate("D4 EXIT GATE","PH_D4_Gate","PH_D4_GateRoot_Withered","PH_D4_SmallWheel","PH_D4_Latch",new Vector3(-76.5f,-10.6f,-47.4f),w.branch,1,6.5f);
                // The old display sphere sat above the landing, but the non-solid root model
                // was buried underneath it. Expose the model without moving the landing/colliders.
                var exitRoot=Mesh("PH_D4_GateRoot_Withered");
                exitRoot.transform.position+=new Vector3(-76.5f,-11.18f,-47.4f)-exitRoot.GetComponent<Renderer>().bounds.center;
                w.exitGate.supplied=false;
                // Pass north of the solid D1 stair base, then west of the turbine, never through them.
                Deck("D2 to D4 dry perimeter",new Vector3(-63.5f,-11.5f,-55.5f),new Vector2(17,2),stone);
                Deck("D2 perimeter connector",new Vector3(-56,-11.5f,-54.7f),new Vector2(2,7),stone);
                Deck("D4 west perimeter connector",new Vector3(-71,-11.5f,-51),new Vector2(2,11),stone);
                Deck("D4 dry control approach",new Vector3(-73,-11.5f,-46.5f),new Vector2(7,2),stone);
                Deck("D4 control landing",new Vector3(-75.5f,-11.5f,-47),new Vector2(4,3),stone);
                Checkpoint("D4 EXIT CONTROL",new Vector3(-75.5f,-11.46f,-46.5f),new Vector3(3,3,2.5f),null,false,false,270);

                w.supplyWater=Water("W_D5_Fixed","W_D5_Fall","W_D5_Fall_Foam","W_D5_ToD2");
                w.bypassWater=Water("W_Bypass_1","W_Bypass_2","W_Bypass_Fall","W_Bypass_Fall_Foam");
                w.dockWater=Water("W_D2_Dock","W_D2_Low");w.turbineWater=Water("W_D2_Wheel");w.mainWater=Water("W_D2_Exit");
                w.exitWater=Water("W_Exit_Tunnel","W_Gorge","W_R_WheelFeed");
                w.leakWater=Water("W_D5_SpillPool","W_D5_SpillFall","W_D5_SpillFall_Foam","W_D5_SpillFloor");
                w.gardenWater=Water("W_R_WheelPool","W_R_IslandFall","W_R_IslandFall_Foam");
                foreach(var go in w.supplyWater.Concat(w.bypassWater).Concat(w.dockWater).Concat(w.turbineWater).Concat(w.mainWater).Concat(w.exitWater).Concat(w.gardenWater))go.SetActive(false);
                // Three visible branch paths and a distinct leak stream share the imported water models.
                w.branchSign=Label("D2: DOCK / SMALL WHEEL / MAIN EXIT",new Vector3(-57,-8.2f,-51),.11f);
                w.gateSign=Label("D4 CLOSED",new Vector3(-75,-7.8f,-48),.11f);
                w.arrivalSign=Label("W NO WATER",new Vector3(-68,-9.2f,-47),.11f);
            }
            void Ending()
            {
                w.wheel=Rotor("PH_W_Waterwheel");WheelInitialRotation=w.wheel.rotation;
                Deck("D4 threshold - follows opened gate passage",new Vector3(-79.5f,-11.9f,-48.1f),new Vector2(4,2.5f),stone);
                Deck("Exit outer landing",new Vector3(-82.1f,-11.9f,-48),new Vector2(3,4),stone);
                Steps("Safe exit gorge corridor",new Vector3(-82.3f,-11.9f,-47),new Vector3(-82.3f,-17.35f,-12),2.7f,stone);
                // Continuous guard rails outside the door opening protect the long descent.
                for(int i=0;i<65;i++)
                {
                    var p=Vector3.Lerp(new Vector3(-82.3f,-11.9f,-47),new Vector3(-82.3f,-17.35f,-12),(i+.5f)/70);
                    foreach(float side in new[]{-1f,1f})Box("Corridor guard",p+new Vector3(side*1.46f,.55f,0),new Vector3(.18f,1.1f,.55f),wood);
                }
                Deck("Garden threshold bridge",new Vector3(-82.85f,-17.35f,-13.3f),new Vector2(3,2.6f),stone);
                Deck("Garden east wheel walkway",new Vector3(-83.4f,-17.35f,-7.4f),new Vector2(1.8f,12.5f),stone);
                Deck("Restored garden viewpoint",new Vector3(-84,-17.32f,0),new Vector2(4,5),stone);
                // The walkway already overlaps this deck with only a 3cm rise; no second stair skin.
                Checkpoint("RESTORED GARDEN",new Vector3(-84,-17.28f,0),new Vector3(3.5f,3,3.5f),null,false,true,180);
                Label("W  /  THE WATERWHEEL\nRESTORE THE FLOW - THEN WALK HERE\nENTER AFTER RESTORATION: RESTART",new Vector3(-84,-14.7f,1),.13f);
                for(int i=0;i<8;i++)
                {
                    var plant=Point("Garden plant "+i,new Vector3(-85.4f+(i%4)*.9f,-17.29f,i<4?1.8f:-1.8f),root);
                    plant.rotation=Quaternion.Euler(0,i*37,0);plant.localScale=new Vector3(.35f,.25f,.35f);
                    var mesh=Mesh("PH_A_Leaf02").sharedMesh;float length=Mathf.Max(mesh.bounds.size.x,mesh.bounds.size.z);
                    var sprout=Point("Centered original leaf mesh",Vector3.zero,plant);var tilt=Quaternion.Euler(-50,0,0);sprout.localRotation=tilt;sprout.localScale=Vector3.one/length;
                    float lower=Mathf.Cos(50*Mathf.Deg2Rad)*mesh.bounds.extents.y+Mathf.Sin(50*Mathf.Deg2Rad)*mesh.bounds.extents.z;
                    sprout.localPosition=-(tilt*mesh.bounds.center)/length+Vector3.up*(lower/length);
                    sprout.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
                    var visual=sprout.gameObject.AddComponent<MeshRenderer>();visual.sharedMaterial=leaf;
                    var stem=GameObject.CreatePrimitive(PrimitiveType.Cylinder);stem.name="Garden sprout stem";stem.transform.SetParent(plant,false);
                    stem.transform.localPosition=Vector3.up*.2f;stem.transform.localScale=new Vector3(.045f,.2f,.045f);
                    stem.GetComponent<Collider>().enabled=false;stem.GetComponent<Renderer>().sharedMaterial=leaf;
                    var state=plant.gameObject.AddComponent<EFV2Plant>();state.visual=visual;state.dryScale=plant.localScale;
                    state.wetScale=new Vector3(.65f,.6f,.65f);w.plants.Add(state);
                }
                var camera=Point("Cinemachine restoration shot",new Vector3(-94,-7,-2),root).gameObject.AddComponent<CinemachineCamera>();
                camera.Priority=-10;camera.Lens.FieldOfView=52;camera.Lens.NearClipPlane=.1f;
                camera.transform.LookAt(w.wheel.position);w.finaleCamera=camera;
            }
            public void Run()
            {
                w.gameObject.AddComponent<EFV2CameraInputGate>().world=w;
                Ground();Surface();Underground();Ending();
                EFV2VisualCleanup.Apply(w);
                for(int i=0;i<3;i++)
                {
                    var orb=new GameObject("Ability transfer timing / "+((LabAbility)i));
                    orb.transform.SetParent(root,true);orb.transform.localScale=Vector3.one*.25f;orb.transform.position=w.spawnPoint.position+Vector3.up;
                    w.orbs[i]=orb.transform;
                }
                Physics.SyncTransforms();
                // Lab's moving stream markers make direction legible in the actual model channels.
                foreach(var item in w.supplyWater.Concat(w.bypassWater).Concat(w.dockWater).Concat(w.turbineWater).Concat(w.mainWater).Concat(w.exitWater))
                {
                    var renderer=item.GetComponent<Renderer>();if(renderer==null)continue;
                    var bounds=renderer.bounds;
                    Vector3 direction=bounds.size.y>Mathf.Max(bounds.size.x,bounds.size.z)?Vector3.down:bounds.size.x>bounds.size.z?Vector3.left:Vector3.forward;
                    if(item.name=="W_D2_Dock" || item.name=="W_D2_Low") direction=Vector3.right;
                    var marker=Box("Moving water glint",bounds.center,new Vector3(.22f,.15f,.22f),gold,item.transform,false);
                    var stream=item.AddComponent<LabStreamMarker>();stream.marker=marker.transform;stream.cyclesPerSecond=.65f;
                    float length=Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z))*.4f;
                    stream.localStart=item.transform.InverseTransformPoint(bounds.center-direction*length+Vector3.up*.13f);
                    stream.localEnd=item.transform.InverseTransformPoint(bounds.center+direction*length+Vector3.up*.13f);
                }
                Debug.Log("[EVERFLOW V2] MODEL BINDINGS\n"+string.Join("\n",bindings));
            }
        }
        public static void Build(EFV2PuzzleWorld world) {new Builder(world).Run();}
    }
}
