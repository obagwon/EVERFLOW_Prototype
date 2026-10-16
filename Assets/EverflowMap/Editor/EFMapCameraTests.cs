using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using EFInput = EverflowGraybox.EFInput;

namespace EverflowMap.Editor
{
    public static class EFMapCameraTests
    {
        [MenuItem("Tools/EVERFLOW/Map/Run Third Person Camera Tests")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stop Play mode first.");
            if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            EditorSceneManager.OpenScene(EFMapAudit.ScenePath);
            SessionState.SetBool("EFMapCameraTests",true);EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod]
        static void Register()
        {
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool("EFMapCameraTests",false))return;
                SessionState.SetBool("EFMapCameraTests",false);
                new GameObject("Camera test probe").AddComponent<EFMapCameraProbe>();
            };
        }
    }
    public sealed class EFMapCameraProbe:MonoBehaviour
    {
        EFMapWorld world;
        EFMapPlayer player;
        EFMapCamera follow;
        GameObject fixtures;
        string report="Unity Editor Play Mode: real player/camera Update and LateUpdate, CharacterController, physics sweeps and center-screen ability ray. Deterministic input injection; not a human mouse feel assessment. Collision fixtures are runtime-only and never saved to the map.\n";
        int checks;
        void Check(bool condition,string message)
        {
            report+=(condition?"PASS ":"FAIL ")+message+"\n";Debug.Log("CAMERA TEST "+(condition?"PASS ":"FAIL ")+message);
            if(!condition)throw new Exception(message);checks++;
        }
        IEnumerator Frames(int count,Vector2 move=default,KeyCode key=KeyCode.None,Vector2 look=default,bool click=false)
        {
            EFInput.TestMovement=move;EFInput.TestPress=key;EFMapInput.TestLookDelta=look;EFMapInput.TestClick=click;
            for(int i=0;i<count;i++)
            {yield return null;EFInput.TestPress=KeyCode.None;EFMapInput.TestLookDelta=Vector2.zero;EFMapInput.TestClick=false;}
            EFInput.TestMovement=Vector2.zero;
        }
        GameObject Cube(string name,Vector3 position,Vector3 size)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(fixtures.transform);
            go.transform.position=position;go.transform.localScale=size;return go;
        }
        void Capture(string name)
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)return;
            var camera=player.view;var rt=new RenderTexture(1440,900,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            var texture=new Texture2D(1440,900,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1440,900),0,0);texture.Apply();
            Directory.CreateDirectory(EFMapAbilityTests.Evidence);
            File.WriteAllBytes(EFMapAbilityTests.Evidence+"/camera-"+name+".png",texture.EncodeToPNG());
            camera.targetTexture=null;RenderTexture.active=null;rt.Release();Destroy(rt);Destroy(texture);
        }
        IEnumerator Tests()
        {
            EFInput.TestMode=true;Time.captureDeltaTime=1f/60;yield return Frames(5);
            world=FindFirstObjectByType<EFMapWorld>();player=world.player;follow=player.follow;
            Check(world.CinematicActive&&world.openingCamera.enabled&&!player.view.enabled&&!follow.enabled,"Opening camera owns the existing intro exclusively");
            yield return Frames(190);
            Check(world.GameInputActive&&player.view.enabled&&follow.enabled&&!world.openingCamera.enabled,"Intro returns to player follow camera");
            Check(Mathf.Abs(Mathf.DeltaAngle(follow.yaw,player.transform.eulerAngles.y))<.1f,"Initial view is behind the player's starting heading");
            Check(world.WantsCursorLocked,"Gameplay requests a locked hidden cursor");
            report+="  actual editor cursor state: "+Cursor.lockState+", visible="+Cursor.visible+"\n";
            Capture("start-follow-camera");
            fixtures=new GameObject("Runtime camera test fixtures");
            Cube("Flat movement surface",new Vector3(200,80,200),new Vector3(80,1,80));
            player.Teleport(new Vector3(200,81.6f,200));follow.yaw=0;follow.pitch=18;follow.Snap();
            yield return Frames(100);
            float beforeYaw=follow.yaw,beforePitch=follow.pitch;var actorRotation=player.transform.rotation;
            yield return Frames(1,look:new Vector2(100,20));
            Check(Mathf.Abs(Mathf.DeltaAngle(beforeYaw,follow.yaw)-100*follow.sensitivity)<.01f,"Mouse horizontal delta immediately changes orbit without RMB");
            Check(Mathf.Abs(follow.pitch-(beforePitch-20*follow.sensitivity*follow.verticalSensitivity))<.01f,"Mouse vertical delta immediately changes pitch");
            Check(Quaternion.Angle(actorRotation,player.transform.rotation)<.01f,"Looking around at rest does not rotate the character");
            yield return Frames(1,look:new Vector2(0,10000));Check(follow.pitch==follow.minPitch,"Upward look clamps at minPitch");
            yield return Frames(1,look:new Vector2(0,-10000));Check(follow.pitch==follow.maxPitch,"Downward look clamps at maxPitch without flipping");
            follow.yaw=90;follow.pitch=70;follow.Snap();yield return Frames(3);
            var before=player.transform.position;yield return Frames(20,Vector2.up);
            Vector3 delta=player.transform.position-before;
            Check(delta.x>1.5f&&Mathf.Abs(delta.z)<.05f&&Mathf.Abs(delta.y)<.08f,"W follows camera yaw horizontally even at steep pitch: "+delta);
            Check(Vector3.Angle(player.transform.forward,Vector3.right)<3,"Character smoothly faces actual movement");
            yield return Frames(20,Vector2.left);
            Check(Mathf.Abs(Mathf.DeltaAngle(follow.yaw,90))<.01f,"Strafing/character rotation does not recenter the camera");
            yield return Frames(1,key:KeyCode.C);
            Check(Mathf.Abs(Mathf.DeltaAngle(follow.yaw,player.transform.eulerAngles.y))<.01f,"C explicitly aligns the camera behind the character");
            follow.yaw=0;follow.pitch=18;follow.Snap();yield return Frames(100);
            var focusBefore=follow.FollowPoint;player.transform.position+=Vector3.right;
            yield return Frames(1);
            float focusStep=Vector3.Distance(focusBefore,follow.FollowPoint);
            Check(focusStep>.001f&&focusStep<.9f,"Position follows smoothly rather than jumping to a moved target");
            yield return Frames(100);
            Check(Vector3.Distance(follow.FollowPoint,player.transform.position+Vector3.up*follow.lookHeight)<.01f,"Follow converges without residual drift");
            var stable=follow.transform.position;yield return Frames(30);
            Check(Vector3.Distance(stable,follow.transform.position)<.002f,"Stationary camera settles without measurable jitter");
            float openDistance=follow.CurrentDistance;
            Vector3 anchor=player.transform.position+Vector3.up*follow.lookHeight;
            var orb=Cube("Ignored ability orb fixture",anchor+Vector3.back*3,Vector3.one);
            follow.collisionIgnoreRoots=new[]{player.transform,orb.transform};Physics.SyncTransforms();yield return Frames(3);
            Check(Mathf.Abs(follow.CurrentDistance-openDistance)<.02f,"Player/explicit ability-orb roots do not obstruct camera");
            orb.SetActive(false);
            var wall=Cube("Wall fixture",anchor+Vector3.back*3,new Vector3(15,12,.5f));Physics.SyncTransforms();yield return Frames(3);
            Check(follow.CurrentDistance<3&&follow.CurrentDistance>1,"Wall retracts camera immediately: "+follow.CurrentDistance);
            Check(!wall.GetComponent<Collider>().bounds.Contains(follow.transform.position),"Retracted camera is outside the wall");
            float retracted=follow.CurrentDistance;wall.SetActive(false);Physics.SyncTransforms();yield return Frames(1);
            Check(follow.CurrentDistance>retracted&&follow.CurrentDistance<openDistance-.2f,"Obstacle removal begins a smooth distance return");
            yield return Frames(130);
            Check(Mathf.Abs(follow.CurrentDistance-openDistance)<.03f,"Camera returns to configured unobstructed distance");
            before=player.transform.position;float initialY=follow.FollowPoint.y;
            yield return Frames(12,key:KeyCode.Space);
            Check(player.transform.position.y>before.y+.8f&&follow.FollowPoint.y>initialY+.1f,"Jump is tracked vertically through the real controller");
            Check(follow.FollowPoint.y<player.transform.position.y+follow.lookHeight,"Jump tracking is damped, separate from mouse response");
            yield return Frames(100);
            follow.yaw=0;follow.pitch=0;follow.Snap();yield return Frames(5);
            var targetGo=Cube("Center aim target",player.view.transform.position+player.view.transform.forward*10,Vector3.one);
            var target=targetGo.AddComponent<EFMapNode>();target.world=world;target.stage=0;target.label="Test flower";target.kind=MapKind.Flower;
            Physics.SyncTransforms();yield return Frames(1,click:true);
            Check(player.Hovered==target&&target.Started,"Center-screen selection and left-click ability path hit the intended target");
            target.ResetNode();targetGo.transform.position=player.view.transform.position+player.view.transform.forward*25;Physics.SyncTransforms();
            yield return Frames(1,click:true);
            Check(!target.Started&&world.Message.Contains("Too far"),"Existing 10m ability range still rejects a far target");
            targetGo.transform.position=player.view.transform.position+player.view.transform.forward*10;
            var blocker=Cube("Aim blocker",Vector3.Lerp(player.transform.position+Vector3.up*follow.lookHeight,targetGo.transform.position,.5f),new Vector3(3,3,.3f));
            Physics.SyncTransforms();yield return Frames(1,click:true);
            Check(player.Hovered==null&&!target.Started,"World obstruction prevents center-screen target selection/casting");
            blocker.SetActive(false);targetGo.SetActive(false);
            yield return Frames(1,key:KeyCode.Escape);
            Check(world.Paused&&!world.GameInputActive&&!world.WantsCursorLocked&&Time.timeScale==0,"Esc pauses simulation and releases cursor for menu UI");
            before=player.transform.position;beforeYaw=follow.yaw;
            yield return Frames(10,Vector2.up,look:new Vector2(100,100),click:true);
            Check(Vector3.Distance(before,player.transform.position)<.001f&&follow.yaw==beforeYaw,"Pause blocks movement, mouse look and casts");
            yield return Frames(1,key:KeyCode.Escape);
            Check(!world.Paused&&world.GameInputActive&&world.WantsCursorLocked&&Time.timeScale==1,"Esc resume restores play and cursor capture");
            yield return Frames(2);
            var cut=new GameObject("Temporary cinematic",typeof(Camera)).GetComponent<Camera>();cut.transform.SetParent(fixtures.transform);
            world.BeginCinematic(cut);yield return Frames(3);
            Check(cut.enabled&&!follow.enabled&&!player.view.enabled&&!world.GameInputActive,"Cinematic has one camera owner and blocks gameplay input");
            world.EndCinematic();yield return Frames(3);
            Check(!cut.enabled&&follow.enabled&&player.view.enabled&&world.GameInputActive,"Cinematic end restores follow/input without a long fly-in");
            player.Teleport(player.transform.position+new Vector3(30,5,30));
            Check(Vector3.Distance(follow.FollowPoint,player.transform.position+Vector3.up*follow.lookHeight)<.001f,"Teleport immediately resets follow position and damping velocity");
            Check(Vector3.Distance(follow.transform.position,player.transform.position)<follow.distance+2,"Teleport immediately moves actual camera to the new position");
            player.transform.rotation=Quaternion.Euler(0,180,0);world.Respawn();yield return Frames(5);
            Check(Vector3.Distance(player.transform.position,world.Checkpoint)<.4f&&Vector3.Distance(follow.transform.position,player.transform.position)<follow.distance+2,"Real checkpoint respawn snaps player and camera back to the map");
            Capture("respawn-follow-camera");
        }
        IEnumerator Start()
        {
            Directory.CreateDirectory("docs/camera");var stack=new Stack<IEnumerator>();stack.Push(Tests());bool failed=false;
            while(stack.Count>0)
            {
                object next=null;bool more=false;
                try{more=stack.Peek().MoveNext();if(more)next=stack.Peek().Current;}
                catch(Exception e){report+="ERROR "+e+"\n";Debug.LogException(e);failed=true;break;}
                if(!more){stack.Pop();continue;}if(next is IEnumerator nested){stack.Push(nested);continue;}yield return next;
            }
            if(world!=null&&world.Paused)world.SetPaused(false);
            EFInput.TestMode=false;EFInput.TestMovement=Vector2.zero;EFInput.TestPress=KeyCode.None;
            EFMapInput.TestLookDelta=Vector2.zero;EFMapInput.TestClick=false;Time.captureDeltaTime=0;
            if(fixtures!=null)Destroy(fixtures);
            report+=$"\n{checks} checks passed; overall {(failed?"FAIL":"PASS")}. Human mouse feel, alt-tab and all frame rates remain unverified.\n";
            Directory.CreateDirectory(EFMapAbilityTests.Evidence);
            File.WriteAllText(EFMapAbilityTests.Evidence+"/camera-test-results.txt",report);Debug.Log(report);
            if(Application.isBatchMode)EditorApplication.Exit(failed?1:0);else EditorApplication.ExitPlaymode();
        }
    }
}
