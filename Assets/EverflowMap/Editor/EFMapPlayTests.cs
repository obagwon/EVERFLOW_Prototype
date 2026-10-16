using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using EFInput = EverflowGraybox.EFInput;
using Ability = EverflowGraybox.Ability;

namespace EverflowMap.Editor
{
    public static class EFMapPlayTests
    {
        [MenuItem("Tools/EVERFLOW/Map/Run Physical and State Tests")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stop Play mode first.");
            if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            EditorSceneManager.OpenScene(EFMapAudit.ScenePath);EFMapSetup.Validate();
            SessionState.SetBool("EFMapTests",true);EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod]
        static void Register()
        {
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool("EFMapTests",false))return;
                SessionState.SetBool("EFMapTests",false);
                new GameObject("Map physical test probe").AddComponent<EFMapPlayProbe>();
            };
        }
    }
    public sealed class EFMapPlayProbe:MonoBehaviour
    {
        EFMapWorld world;
        EFMapPlayer player;
        Vector3 offset;
        string report="Unity Editor Play Mode. Abilities use injected selection/click through EFMapPlayer.Update and the real center camera ray, unless explicitly marked API condition check. Traversal uses CharacterController. Camera is programmatically aimed; no human/OS mouse playthrough or duration measurement.\n";
        int checks;
        void Check(bool condition,string text)
        {
            report+=(condition?"PASS ":"FAIL ")+text+"\n";Debug.Log("MAP TEST "+(condition?"PASS ":"FAIL ")+text);
            if(!condition)throw new Exception(text);checks++;
        }
        EFMapNode Node(string name)=>FindObjectsByType<EFMapNode>(FindObjectsSortMode.None).Single(n=>n.gameObject.name==name);
        Vector3 Local=>player.transform.position-offset;
        IEnumerator Frames(int count,Vector2 move=default,KeyCode press=KeyCode.None)
        {
            EFInput.TestMovement=move;EFInput.TestPress=press;
            for(int i=0;i<count;i++){yield return null;EFInput.TestPress=KeyCode.None;}
            EFInput.TestMovement=Vector2.zero;
        }
        IEnumerator WalkTo(Vector3 modelPoint,int max=900)
        {
            Vector3 target=offset+modelPoint;float d=1000;
            for(int i=0;i<max;i++)
            {
                Vector3 delta=Vector3.ProjectOnPlane(target-player.transform.position,Vector3.up);d=delta.magnitude;
                if(d<.25f)break;
                Vector3 forward=Vector3.ProjectOnPlane(player.view.transform.forward,Vector3.up).normalized;
                Vector3 right=Vector3.Cross(Vector3.up,forward);
                EFInput.TestMovement=new Vector2(Vector3.Dot(delta.normalized,right),Vector3.Dot(delta.normalized,forward));yield return null;
            }
            EFInput.TestMovement=Vector2.zero;
            Check(d<.3f,"Walk to "+modelPoint+" actual "+Local);
        }
        IEnumerator JumpToward(Vector3 modelPoint,int frames=45)
        {
            Vector3 delta=Vector3.ProjectOnPlane(offset+modelPoint-player.transform.position,Vector3.up).normalized;
            Vector3 f=Vector3.ProjectOnPlane(player.view.transform.forward,Vector3.up).normalized;
            var input=new Vector2(Vector3.Dot(delta,Vector3.Cross(Vector3.up,f)),Vector3.Dot(delta,f));
            yield return Frames(frames,input,KeyCode.Space);yield return Frames(12);
            report+="  jump landed "+Local+"\n";
        }
        IEnumerator FreezeNear(EFMapNode node)
        {
            yield return AimAt(node);
            for(int i=0;i<1200&&!node.Aligned;i++)yield return null;
            Check(node.Aligned,"Timing window exists for "+node.name);
            yield return ClickAt(node,Ability.Freeze);
            Check(node.Frozen&&node.Aligned,"Correct freeze "+node.name);
        }
        Vector3 AimPoint(EFMapNode node)
        {
            if(node.kind==MapKind.Vine) return node.handle.position;
            if(node.kind==MapKind.Water) return node.GetComponentsInChildren<BoxCollider>().Single(c=>c.name=="Ability target - slide surface").bounds.center;
            if(node.kind==MapKind.Source) return node.indicators[0].bounds.center;
            if(node.kind==MapKind.Bridge)
            {
                var mesh=node.motion.GetComponent<MeshFilter>();var b=mesh.sharedMesh.bounds;
                var local=node.motion.InverseTransformPoint(player.transform.position);
                local.x=Mathf.Clamp(local.x,b.min.x+.7f,b.max.x-.7f);
                local.z=Mathf.Clamp(local.z,b.min.z+.5f,b.max.z-.5f);
                local.y=b.max.y-.05f;return node.motion.TransformPoint(local);
            }
            if(node.kind==MapKind.Repair) return offset+new Vector3(0,16.1f,-19);
            if(node.kind==MapKind.Flower) return node.motion.position+Vector3.up*.6f;
            return node.motion.position+Vector3.up*.2f;
        }
        IEnumerator AimAt(EFMapNode node)
        {
            // Solve shoulder-camera parallax, then let the real camera LateUpdate position itself.
            for(int i=0;i<16;i++)
            {
                Vector3 delta=AimPoint(node)-player.view.transform.position;
                Vector3 angles=Quaternion.LookRotation(delta).eulerAngles;
                player.follow.yaw=angles.y;
                player.follow.pitch=Mathf.Clamp(Mathf.DeltaAngle(0,angles.x),player.follow.minPitch,player.follow.maxPitch);
                yield return Frames(1);
            }
        }
        IEnumerator ClickAt(EFMapNode node,Ability ability)
        {
            yield return AimAt(node);
            yield return Frames(1,Vector2.zero,ability==Ability.Bloom?KeyCode.Alpha1:ability==Ability.Freeze?KeyCode.Alpha2:KeyCode.Alpha3);
            EFMapInput.TestClick=true;yield return Frames(1);EFMapInput.TestClick=false;
            Check(player.Hovered==node,"Center ray detects "+node.name+" at player "+Local+" point="+AimPoint(node)+" message="+world.Message);
            Check(player.LastCastResult != null && player.LastCastResult.StartsWith("Applied:"),"Click applies "+ability+" to "+node.name+": "+player.LastCastResult);
            yield return Frames(1);
        }
        IEnumerator BlockedClickAt(EFMapNode node,Ability ability,string expected)
        {
            yield return AimAt(node);
            yield return Frames(1,Vector2.zero,ability==Ability.Bloom?KeyCode.Alpha1:ability==Ability.Freeze?KeyCode.Alpha2:KeyCode.Alpha3);
            EFMapInput.TestClick=true;yield return Frames(1);EFMapInput.TestClick=false;
            Check(player.Hovered==node&&player.LastCastResult.StartsWith("Blocked:")&&player.LastCastResult.Contains(expected),"Target detected; expected input denial "+node.name+": "+player.LastCastResult);
        }
        void Capture(string name,Camera camera)
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)return;
            var rt=new RenderTexture(1440,900,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            var texture=new Texture2D(1440,900,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1440,900),0,0);texture.Apply();
            Directory.CreateDirectory(EFMapAbilityTests.Evidence);
            File.WriteAllBytes(EFMapAbilityTests.Evidence+"/"+name+".png",texture.EncodeToPNG());
            camera.targetTexture=null;RenderTexture.active=null;rt.Release();Destroy(rt);Destroy(texture);
        }
        IEnumerator Tests()
        {
            yield return null;yield return null;
            world=FindFirstObjectByType<EFMapWorld>();player=world.player;offset=GameObject.Find("EF_Terrain").transform.position;
            EFInput.TestMode=true;Time.captureDeltaTime=1f/60;
            world.ReturnToTitle();yield return Frames(30);
            var initialTitle=FindFirstObjectByType<EverflowMVP.EFTitleScreen>();
            Check(initialTitle!=null,"Actual title scene loads before traversal");
            initialTitle.startButton.onClick.Invoke();yield return Frames(5);
            world=FindFirstObjectByType<EFMapWorld>();player=world.player;offset=GameObject.Find("EF_Terrain").transform.position;
            yield return Frames(10);if(world.openingCamera!=null)Capture("opening-goal-camera",world.openingCamera);
            yield return Frames(185);Capture("start-camera",player.view);
            Check(world.StageIndex==0&&world.Completed.All(v=>!v),"Initial stage and six incomplete areas");
            Check(FindObjectsByType<EFMapAnimation>(FindObjectsSortMode.None).All(a=>!a.Started&&!a.Complete&&a.Elapsed==0),"No animation autoplay");
            Check(!world.TryArrive(0),"Unsolved arrival cannot complete P1");
            yield return EFMapAbilityRegression.Run(world,player,Check);
            yield return Frames(1,Vector2.zero,KeyCode.Alpha2);Check(player.selected==Ability.Freeze,"Ability 2 selects FREEZE through player input");
            yield return Frames(1,Vector2.zero,KeyCode.Alpha3);Check(player.selected==Ability.Pulse,"Ability 3 selects PULSE through player input");
            yield return Frames(1,Vector2.zero,KeyCode.Alpha1);Check(player.selected==Ability.Bloom,"Ability 1 selects BLOOM through player input");
            var flower=Node("SM_FlowerJumpPad");var vine=Node("SM_Vine");
            Check(vine.GetComponentsInChildren<BoxCollider>().Any(c=>c.bounds.Contains(vine.HandlePosition)),"REGRESSION: dormant visible vine tip is inside its ability collider");
            Check(flower.support.All(c=>!c.enabled),"Flower support disabled before bloom completion");
            player.selected=Ability.Bloom;
            yield return ClickAt(flower,Ability.Bloom);
            yield return Frames(135);
            Check(flower.Done&&flower.support.All(c=>c.enabled),"Flower bloom completes; support enables only afterward");
            Check(flower.actions[0].Complete&&Mathf.Abs(flower.actions[0].Duration-49f/24)<.0001f,"Flower frame range uses imported 24 FPS");
            var body=flower.support[0];
            report+="  bloomed dome bounds "+body.bounds+"; player starts "+Local+"\n";
            yield return JumpToward(body.bounds.center-offset,18);yield return Frames(30);
            Check(Vector3.Distance(Vector3.ProjectOnPlane(player.transform.position-body.bounds.center,Vector3.up),Vector3.zero)<.8f&&Local.y>1.4f,"Ordinary jump from initial ground reaches flower dome: "+Local);
            yield return Frames(95,Vector2.zero,KeyCode.Space);
            Check(player.FlowerUsed&&Vector3.Distance(player.transform.position,flower.launchTarget.position)<1.8f,"P1 real flower launch lands on upper terrain: "+Local);
            Capture("p1-flower-landing",player.view);
            yield return ClickAt(vine,Ability.Bloom);yield return Frames(110);
            Check(vine.Done&&vine.handle.lossyScale.sqrMagnitude>.1f,"Vine growth exposes actual FBX handle");
            Capture("p1-vine-grown",player.view);
            yield return WalkTo(new Vector3(26,4.1f,23));
            bool boarded=false;
            for(int i=0;i<400&&!boarded;i++)
            {
                if(Vector3.Distance(player.transform.position,vine.HandlePosition)<3.4f)EFInput.TestPress=KeyCode.E;
                else if(player.GetComponent<CharacterController>().isGrounded)EFInput.TestPress=KeyCode.Space;
                yield return null;EFInput.TestPress=KeyCode.None;boarded=player.Riding;
            }
            Check(boarded,"P1 handle reachable from flower ledge");
            for(int i=0;i<300&&vine.Angle<18;i++)yield return null;
            var vineLandingInView=player.view.WorldToViewportPoint(vine.launchTarget.position);
            Check(vineLandingInView.z>0&&vineLandingInView.x>0&&vineLandingInView.x<1&&vineLandingInView.y>0&&vineLandingInView.y<1,"Vine camera frames the release landing: "+vineLandingInView);
            Capture("camera-vine-ride",player.view);
            yield return Frames(110,Vector2.zero,KeyCode.Space);
            Check(player.VineUsed&&!player.Riding&&Local.x>40&&Local.y>3,"P1 vine release lands east: "+Local);
            yield return WalkTo(new Vector3(44,4.1f,21));yield return Frames(5);
            Check(world.Completed[0]&&world.StageIndex==1,"P1 physical arrival advances only after flower and vine use");
            yield return Frames(135);Check(world.stages[0].restoration[0].Complete,"P1 restoration runs once and holds");

            var firstRestore=world.stages[0].restoration[0];float heldTime=firstRestore.Elapsed;firstRestore.Begin();yield return Frames(4);
            Check(firstRestore.Elapsed==heldTime&&firstRestore.Complete,"Repeated restoration Begin does not replay or erase its result");
            world.Retry();yield return Frames(2);
            world.debris[0].Apply(Ability.Freeze); // API-only wrong-pose condition fixture.
            Check(world.debris[0].Frozen&&!world.debris[0].Aligned&&!world.ConditionsMet(1),"Incorrect debris height remains unsolved");
            player.Teleport(world.Checkpoint+Vector3.down*20);yield return Frames(8);
            Check(Vector3.Distance(player.transform.position,world.Checkpoint)<.4f&&world.debris[0].Frozen,"Automatic fall returns to checkpoint and preserves puzzle state");
            world.Retry();yield return Frames(2);
            Check(world.debris.All(n=>!n.Frozen)&&world.Completed[0]&&firstRestore.Complete,"Debris retry resets current puzzle without resetting P1 restoration");

            yield return WalkTo(new Vector3(44,4.1f,16));
            yield return ClickAt(world.debris[0],Ability.Freeze);
            Check(world.debris[0].Frozen,"FREEZE input holds a debris target");
            yield return ClickAt(world.debris[0],Ability.Freeze);
            Check(!world.debris[0].Frozen,"Second FREEZE input releases target for a retry");
            yield return FreezeNear(world.debris[0]);
            yield return JumpToward(new Vector3(44,5.3f,11),28);
            Check(player.SteppedDebris[0],"P2 first frozen debris physically supports player: "+Local);
            yield return WalkTo(new Vector3(44,5.3f,9.8f));
            yield return FreezeNear(world.debris[1]);
            yield return JumpToward(new Vector3(44,6.5f,6),28);
            Check(player.SteppedDebris[1],"P2 second debris reached: "+Local);
            yield return WalkTo(new Vector3(44,6.5f,4.8f));
            yield return FreezeNear(world.debris[2]);
            Check(world.debris.All(n=>n.Frozen),"Three FREEZE states coexist after sequential approach; no replacement limit");
            yield return JumpToward(new Vector3(44,7.7f,1),28);
            Check(player.SteppedDebris[2],"P2 third debris reached: "+Local);
            yield return WalkTo(new Vector3(44,7.7f,-.2f));
            yield return JumpToward(new Vector3(44,8.6f,-5),33);
            Check(Local.y>8,"P2 debris exit reaches 7.5m terrain: "+Local);
            yield return WalkTo(new Vector3(44,8.6f,-25));
            var slate=Node("SM_Slate");yield return FreezeNear(slate);
            yield return WalkTo(new Vector3(44,8.6f,-27));
            yield return WalkTo(new Vector3(44,8.6f,-40));yield return Frames(5);
            Check(world.Completed[1]&&world.StageIndex==2,"P2 slate crossed and area advanced");

            var bridge=Node("SM_RotatingBridge");
            bridge.Apply(Ability.Freeze);Check(!bridge.Frozen,"P3 cannot freeze unpowered bridge (API condition check)");
            yield return ClickAt(Node("SM_PowerSource_P3"),Ability.Pulse);
            yield return WalkTo(new Vector3(42,8.6f,-42));yield return FreezeNear(bridge);
            yield return WalkTo(new Vector3(42,8.6f,-42));yield return WalkTo(new Vector3(33,8.6f,-42));
            var relative=bridge.motion.InverseTransformPoint(player.transform.position);bridge.Apply(Ability.Freeze);yield return Frames(10);
            Check(Vector3.Distance(relative,bridge.motion.InverseTransformPoint(player.transform.position))<.18f,"Standing controller follows the rotating FBX bridge collider");
            bridge.Apply(Ability.Freeze);yield return Frames(3);
            yield return WalkTo(new Vector3(18,8.6f,-42));yield return Frames(5);
            Check(world.Completed[2]&&world.StageIndex==3,"P3 bridge crossed to ridge");
            yield return WalkTo(new Vector3(10,9,-44));yield return WalkTo(new Vector3(0,14,-46));
            yield return WalkTo(new Vector3(-10,22,-48));yield return WalkTo(new Vector3(-19.4f,25,-47));
            Check(Local.y>24,"P3 to P4 ridge ascent with original move speed/jump");

            var water=Node("SM_IceSlide");water.Apply(Ability.Freeze);Check(!water.Frozen,"P4 cannot freeze dry slide (API condition check)");
            yield return ClickAt(Node("SM_PowerSource_P4"),Ability.Pulse);yield return Frames(90);
            Check(world.gate4.Done&&water.Filled&&world.sideWater.enabled,"P4 open gate supplies both waterways");
            var sideOriginal=world.sideWater.sharedMaterial;
            yield return JumpToward(new Vector3(-22,26,-46),28);
            Check(Local.y>25.8f,"P4 reservoir rim reachable by an ordinary jump: "+Local);
            yield return AimAt(water);Capture("p4-surface-aim",player.view);
            yield return ClickAt(water,Ability.Freeze);yield return Frames(2);
            Check(water.Frozen&&world.sideWater.sharedMaterial==sideOriginal&&world.sideWater.sharedMaterial!=water.iceMaterial,"P4 only slide surface freezes; side channel stays water");
            yield return WalkTo(new Vector3(-19.4f,25,-47));
            yield return Frames(1,Vector2.zero,KeyCode.E);
            Check(player.Riding,"P4 E input boards leaf at dock after ridge ascent");
            yield return Frames(100);
            var leafLandingInView=player.view.WorldToViewportPoint(water.ridePath[water.ridePath.Length-1].position);
            Check(leafLandingInView.z>0&&leafLandingInView.x>0&&leafLandingInView.x<1&&leafLandingInView.y>0&&leafLandingInView.y<1,"Leaf camera frames the downhill landing: "+leafLandingInView);
            Capture("camera-leaf-ride",player.view);
            yield return Frames(400);Capture("p4-landing",player.view);
            Check(player.SlideUsed&&!player.Riding&&Local.y>9&&Local.x< -20,"P4 collision-swept leaf reaches P5 landing and restores controller: "+Local);
            Check(world.Completed[3]&&world.StageIndex==4,"P4 actual landing triggers restoration/valley water");

            var root=Node("SM_VineRoot_P5");var door=Node("SM_Door_P5");
            yield return WalkTo(new Vector3(-22,11.1f,8));
            yield return BlockedClickAt(root,Ability.Pulse,"BLOOM");Check(!root.SourceOn,"P5 power cannot bypass ungrown wire");
            yield return ClickAt(root,Ability.Bloom);yield return BlockedClickAt(root,Ability.Pulse,"BLOOM");Check(!root.SourceOn,"P5 input waits for animation completion");
            yield return Frames(135);yield return ClickAt(root,Ability.Pulse);yield return Frames(90);
            Check(root.Done&&root.SourceOn&&door.Done,"P5 grown wire relays root PULSE to animated door");
            yield return WalkTo(new Vector3(-20.5f,11,5));yield return WalkTo(new Vector3(-14,13.6f,5));yield return Frames(5);
            Check(world.Completed[4]&&world.StageIndex==5,"P5 door crossing advances to final plateau");
            yield return WalkTo(new Vector3(-6,16.2f,5));Check(Local.y>15.5f,"P5 to P6 stairs climbed with 2m controller");
            var restore1=world.stages[0].restoration[0];world.Retry();yield return Frames(5);
            Check(world.Completed.Take(5).All(v=>v)&&restore1.Complete,"Current-stage retry preserves all earlier restorations");

            var swing=Node("SM_Channel_Swing_P6");var repair=Node("P6_ChannelRepair");
            yield return WalkTo(new Vector3(-5,16.1f,-19));yield return WalkTo(new Vector3(5,16.1f,-19));
            var source6=Node("SM_PowerSource_P6");player.selected=Ability.Pulse;
            yield return ClickAt(source6,Ability.Pulse);
            yield return ClickAt(repair,Ability.Bloom);
            yield return Frames(135);
            Check(!world.WheelRunning&&!world.Completed[5],"P6 incomplete final alignment prevents waterwheel");
            yield return WalkTo(new Vector3(5,16.1f,-8));
            yield return FreezeNear(swing);yield return Frames(700);
            Check(world.WheelRunning&&world.finalFlow.All(g=>g.activeSelf)&&world.Completed[5],"P6 water propagates through repaired/aligned channel before wheel turns");
            var wheelRotation=world.wheel.localRotation;
            var wheelBody=world.wheel.parent.Find("SM_Waterwheel_Body");var bodyRotation=wheelBody.localRotation;
            yield return Frames(30);
            Check(Quaternion.Angle(world.wheel.localRotation,wheelRotation)>5&&Quaternion.Angle(wheelBody.localRotation,bodyRotation)<.001f,"Only SM_Waterwheel_Wheel rotates; fixed body remains still");
            Check(world.Ended&&world.stages[5].restoration[0].Complete,"P6 restoration completes and ending camera activates");
            Check(world.stages.SelectMany(s=>s.restoration).All(a=>a.Started&&a.Complete),"All supplied restoration clips and valley water finish and retain their results");
            Capture("ending-camera",world.endingCamera);
            Check(world.CinematicActive&&!world.WantsCursorLocked&&!player.view.enabled,"Ending camera releases cursor for ending UI");
            world.ResumeExploration();yield return Frames(10);
            Check(world.GameInputActive&&player.follow.enabled&&player.view.enabled&&!world.endingCamera.enabled,"Continue exploring restores gameplay camera/input after ending");
            yield return Frames(60);Check(!world.Ended,"Ending camera does not reactivate after returning to play");
            world.RestartWorld();yield return Frames(205);
            world=FindFirstObjectByType<EFMapWorld>();player=world.player;
            Check(world.StageIndex==0&&world.Completed.All(v=>!v)&&!world.WheelRunning&&!world.Ended,"Whole-world restart reloads all six initial states");
            Check(FindObjectsByType<EFMapAnimation>(FindObjectsSortMode.None).All(a=>!a.Started&&!a.Complete),"Whole restart resets animated poses and one-shot flags");
            world.ReturnToTitle();yield return Frames(30);
            var title=FindFirstObjectByType<EverflowMVP.EFTitleScreen>();
            Check(title!=null&&title.startButton!=null,"Return to original title UI");
            title.startButton.onClick.Invoke();yield return Frames(205);
            world=FindFirstObjectByType<EFMapWorld>();player=world==null?null:world.player;
            Check(world!=null&&SceneManager.GetActiveScene().path==EFMapAudit.ScenePath,"Actual title button event loads Everflow_Map");
            Check(FindObjectsByType<EFMapPlayer>(FindObjectsSortMode.None).Length==1&&world.StageIndex==0,"Title entry creates one player at P1");
        }
        IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);Directory.CreateDirectory("docs/map-integration");
            var stack=new Stack<IEnumerator>();stack.Push(Tests());bool failed=false;
            while(stack.Count>0)
            {
                object next=null;bool more=false;
                try{more=stack.Peek().MoveNext();if(more)next=stack.Peek().Current;}
                catch(Exception e){report+="ERROR "+e+"\n";Debug.LogException(e);failed=true;break;}
                if(!more){stack.Pop();continue;}
                if(next is IEnumerator nested){stack.Push(nested);continue;}
                yield return next;
            }
            EFInput.TestMode=false;EFInput.TestMovement=Vector2.zero;EFInput.TestPress=KeyCode.None;Time.captureDeltaTime=0;
            report+=$"\n{checks} checks passed; overall {(failed?"FAIL":"PASS")}. No human completion time measured.\n";
            Directory.CreateDirectory(EFMapAbilityTests.Evidence);
            File.WriteAllText(EFMapAbilityTests.Evidence+"/input-traversal-results.txt",report);
            Debug.Log("MAP PHYSICAL TESTS "+(failed?"FAILED":"PASSED"));
            if(Application.isBatchMode)EditorApplication.Exit(failed?1:0);else EditorApplication.ExitPlaymode();
        }
    }
}
