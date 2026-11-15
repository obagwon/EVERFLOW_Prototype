using System.Collections;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace EverflowMap.Editor
{
    // Integration checks use the real keyboard/mouse path; animation is never driven by the probe.
    [InitializeOnLoad]
    public static class EFV2CharacterPlayChecks
    {
        const string Key="EVERFLOW.Character.Check";
        static EFV2CharacterPlayChecks()
        {
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false))
                {SessionState.SetBool(Key,false);new GameObject("Character input integration checks").AddComponent<EFV2CharacterProbe>();}
                if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetString(EFV2PuzzlePlayChecks.ModeKey,"").StartsWith("route-"))
                    new GameObject("Character continuous route audit").AddComponent<EFV2CharacterRouteAudit>();
            };
        }
        [MenuItem("Tools/EVERFLOW/V2 Puzzles/Check Character 1 Input And Animation")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.Exception("Stop Play first.");
            SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
    }

    public sealed class EFV2CharacterRouteAudit:MonoBehaviour
    {
        EFV2PuzzleWorld world;EFV2CharacterAnimator bridge;Vector3 previous;float idleFor;
        int transportedIdle,transportFailures,pausedFrames,pauseFailures;float previousClock;bool wasPaused;string mode;
        readonly System.Collections.Generic.HashSet<string> states=new System.Collections.Generic.HashSet<string>();
        IEnumerator Start()
        {yield return null;mode=SessionState.GetString(EFV2PuzzlePlayChecks.ModeKey,"");world=FindFirstObjectByType<EFV2PuzzleWorld>();bridge=world.player.GetComponent<EFV2CharacterAnimator>();previous=world.player.transform.position;}
        void LateUpdate()
        {
            if(world==null||bridge==null)return;
            var p=world.player;float clock=bridge.animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            if(world.SimulationPaused)
            {if(wasPaused){pausedFrames++;if(clock!=previousClock)pauseFailures++;}}
            else
            {
                states.Add(bridge.CurrentMotion);
                idleFor=p.Grounded&&p.LocomotionSpeed<.05f?idleFor+Time.deltaTime:0;
                float carried=Vector3.Distance(previous,p.transform.position);
                if(idleFor>.5f&&carried>.002f&&carried<.5f)
                {transportedIdle++;if(bridge.MotionSpeed>.2f)transportFailures++;}
            }
            previous=p.transform.position;previousClock=clock;wasPaused=world.SimulationPaused;
            if(SessionState.GetString(EFV2PuzzlePlayChecks.ModeKey,"").Length>0)return;
            string result=System.DateTime.UtcNow.ToString("O")+"\nObserved states: "+string.Join(", ",states)+
                "\nIdle transport samples="+transportedIdle+", erroneous running="+transportFailures+"\nPaused animation frames="+pausedFrames+", clock advances="+pauseFailures+"\nRoute puzzle result is in docs/v2-puzzle-validation/runtime-"+mode+".txt.\n";
            File.WriteAllText("docs/character-player/route-animation-audit-"+mode+".txt",result);
            File.WriteAllText("docs/character-player/route-animation-audit.txt",result);
            Destroy(gameObject);
        }
    }

    public sealed class EFV2CharacterProbe:MonoBehaviour
    {
        EFV2PuzzleWorld world;EFMapPlayer player;EFV2CharacterAnimator bridge;
        Keyboard keyboard;Mouse mouse;readonly StringBuilder report=new StringBuilder();int checks,failures;
        const string Folder="docs/character-player";
        void Check(bool pass,string text){checks++;if(!pass)failures++;report.AppendLine((pass?"PASS ":"FAIL ")+text);}
        IEnumerator Frames(int count){for(int i=0;i<count;i++)yield return null;}
        void Keys(params UnityEngine.InputSystem.Key[] keys)=>InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
        IEnumerator Tap(UnityEngine.InputSystem.Key key){Keys(key);yield return Frames(2);Keys();yield return Frames(2);}
        void Shot(string name)=>ScreenCapture.CaptureScreenshot(Folder+"/"+name+".png");
        IEnumerator Start()
        {
            Directory.CreateDirectory(Folder);yield return Frames(12);
            world=FindFirstObjectByType<EFV2PuzzleWorld>();player=world.player;bridge=player.GetComponent<EFV2CharacterAnimator>();
            world.AutomatedCheck=true;world.SetPaused(false);
            while(world.OpeningActive)yield return null;
            keyboard=InputSystem.AddDevice<Keyboard>("CharacterIntegrationKeyboard");mouse=InputSystem.AddDevice<Mouse>("CharacterIntegrationMouse");
            SessionState.SetString(EFV2PuzzlePlayChecks.ModeKey,"character-integration");
            SessionState.SetFloat(EFV2PuzzlePlayChecks.ModeKey+".deadline",(float)EditorApplication.timeSinceStartup+120);
            EFDemoReview.Start("../character-player/character-integration");
            yield return new WaitForSeconds(.3f);
            var controller=player.GetComponent<CharacterController>();
            Check(!player.GetComponent<MeshRenderer>().enabled,"Original capsule renderer hidden");
            Check(controller.height==2&&controller.radius==.4f&&controller.center==Vector3.zero,"Original collision dimensions retained");
            Check(bridge.animator.avatar.isHuman&&bridge.animator.avatar.isValid&&!bridge.animator.applyRootMotion,"Valid Humanoid, root motion disabled");
            Check(bridge.CurrentMotion=="Locomotion"&&bridge.MotionSpeed<.1f&&player.Grounded,"Idle after normal opening handoff");
            Shot("01-idle-back");yield return new WaitForSeconds(.7f);
            // Orbit through the existing Cinemachine input, preserving the same player root.
            for(int i=0;i<32;i++){InputSystem.QueueStateEvent(mouse,new MouseState{delta=new Vector2(40,0)});yield return null;}
            InputSystem.QueueStateEvent(mouse,new MouseState());yield return new WaitForSeconds(.5f);
            Shot("02-idle-front");yield return new WaitForSeconds(.8f);
            var start=player.transform.position;Keys(UnityEngine.InputSystem.Key.W);yield return new WaitForSeconds(.4f);
            Check(player.LocomotionSpeed>5&&bridge.MotionSpeed>4.5f&&bridge.CurrentMotion=="Locomotion","Real movement selects run without root-motion displacement");
            Shot("03-run");Keys();yield return new WaitForSeconds(.3f);
            Check(player.LocomotionSpeed<.1f&&bridge.MotionSpeed<.2f,"Release input blends back to idle (self="+player.LocomotionSpeed.ToString("F3")+", blend="+bridge.MotionSpeed.ToString("F3")+")");
            // A slow self-motion signal is valid for analogue/blocked movement; don't change the motor speed for a test.
            yield return Tap(UnityEngine.InputSystem.Key.Space);yield return new WaitForSeconds(.12f);
            Check(bridge.CurrentMotion=="Jump"&&!player.Grounded,"Space selects jump while physically airborne");
            Shot("04-jump");
            float deadline=Time.unscaledTime+2;bool fall=false,land=false;
            while(Time.unscaledTime<deadline)
            {
                if(bridge.CurrentMotion=="Fall"&&!fall){fall=true;Shot("05-fall");}
                if(bridge.CurrentMotion=="Land"&&!land){land=true;Shot("06-land");}
                yield return null;
            }
            Check(fall&&land&&bridge.CurrentMotion=="Locomotion","Fall, ground contact, landing and idle complete");
            Keys(UnityEngine.InputSystem.Key.W);yield return new WaitForSeconds(.25f);
            yield return Tap(UnityEngine.InputSystem.Key.Digit1);
            var bone=bridge.animator.GetBoneTransform(HumanBodyBones.LeftFoot);var pose=bone.localPosition;
            var position=player.transform.position;float clock=bridge.animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            Shot("07-ability-frozen");yield return new WaitForSecondsRealtime(.7f);
            Check(world.AbilityMode&&Time.timeScale==0&&position==player.transform.position&&pose==bone.localPosition&&clock==bridge.animator.GetCurrentAnimatorStateInfo(0).normalizedTime,"Ability mode freezes animation pose and physical movement");
            yield return Tap(UnityEngine.InputSystem.Key.Escape);yield return new WaitForSeconds(.3f);
            Check(!world.AbilityMode&&!world.Paused&&Time.timeScale==1&&bridge.MotionSpeed<.2f,"Cancel resumes animation without stale running input");
            yield return Tap(UnityEngine.InputSystem.Key.Escape);clock=bridge.animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            yield return new WaitForSecondsRealtime(.3f);
            Check(world.Paused&&clock==bridge.animator.GetCurrentAnimatorStateInfo(0).normalizedTime,"Normal pause also freezes character animation");
            yield return Tap(UnityEngine.InputSystem.Key.Escape);
            yield return Tap(UnityEngine.InputSystem.Key.R);yield return new WaitForSeconds(.35f);
            Check(player.Grounded&&bridge.CurrentMotion=="Locomotion"&&bridge.MotionSpeed<.1f,"Real R respawn resets airborne/landing animation");
            world.Restart();yield return new WaitForSeconds(.3f);
            Check(player.transform.Find("Character Visual")!=null&&player.GetComponentsInChildren<EFV2CharacterAnimator>().Length==1,"Restart retains one character animation owner");
            while(world.OpeningActive)yield return null;
            Shot("08-restored-idle");yield return new WaitForSeconds(.7f);
            report.AppendLine("TOTAL "+checks+" checks, "+failures+" failures. Input-driven character integration; full route checked separately.");
            File.WriteAllText(Folder+"/character-checks.txt",System.DateTime.UtcNow.ToString("O")+"\n"+report);
            Debug.Log(report.ToString());EFDemoReview.Stop();SessionState.EraseString(EFV2PuzzlePlayChecks.ModeKey);
            InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(mouse);world.AutomatedCheck=false;Destroy(gameObject);
        }
    }
}
