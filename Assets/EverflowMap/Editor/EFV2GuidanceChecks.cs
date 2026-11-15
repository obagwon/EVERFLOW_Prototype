using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using EverflowPuzzleLab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace EverflowMap.Editor
{
    [InitializeOnLoad]
    public static class EFV2GuidanceChecks
    {
        const string Key="EVERFLOW.GuidanceChecks";
        public const string Reports="docs/guidance-validation";
        static EFV2GuidanceChecks()
        {
            EditorApplication.playModeStateChanged+=s=>
            {
                if(!SessionState.GetBool(Key,false))return;
                if(s==PlayModeStateChange.EnteredPlayMode)new GameObject("Guidance UI checks - mixed input and explicit forced states").AddComponent<EFV2GuidanceProbe>();
                if(s==PlayModeStateChange.EnteredEditMode)SessionState.EraseBool(Key);
            };
            EditorApplication.update+=()=>
            {
                if(!SessionState.GetBool(Key,false))return;
                Application.runInBackground=true;EditorApplication.QueuePlayerLoopUpdate();
                if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+".deadline",float.MaxValue))Finish("FAIL Guidance checks timed out.\n");
            };
        }
        public static void Run()
        {
            Directory.CreateDirectory(Reports);EditorSceneManager.OpenScene(EFV2PuzzleBuild.Scene);
            SessionState.SetBool(Key,true);SessionState.SetFloat(Key+".deadline",(float)EditorApplication.timeSinceStartup+120);
            EditorApplication.ExecuteMenuItem("Window/General/Game");EditorApplication.isPaused=false;EditorApplication.EnterPlaymode();
        }
        public static void Finish(string report)
        {File.WriteAllText(Reports+"/runtime-guidance.txt",DateTime.UtcNow.ToString("O")+"\n"+report);SessionState.EraseBool(Key);EditorApplication.ExitPlaymode();}
    }
    public sealed class EFV2GuidanceProbe:MonoBehaviour
    {
        EFV2PuzzleWorld w;EFV2Guidance ui;Camera view;Keyboard keyboard;Mouse mouse;
        readonly StringBuilder report=new StringBuilder();
        readonly System.Collections.Generic.List<string> errors=new System.Collections.Generic.List<string>();
        int passes,failures;
        void OnEnable(){Application.logMessageReceived+=Log;}
        void OnDisable(){Application.logMessageReceived-=Log;if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);if(mouse!=null&&mouse.added)InputSystem.RemoveDevice(mouse);}
        void Log(string m,string stack,LogType t){if((t==LogType.Error||t==LogType.Exception||t==LogType.Assert)&&errors.Count<10&&!errors.Contains(m))errors.Add(m);}
        void Check(bool valid,string text)
        {if(valid)passes++;else failures++;report.AppendLine((valid?"PASS ":"FAIL ")+text);File.WriteAllText(EFV2GuidanceChecks.Reports+"/progress.txt",report.ToString());}
        IEnumerator Frames(int count){for(int i=0;i<count;i++)yield return null;}
        IEnumerator Key(Key key)
        {InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));yield return Frames(2);InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return Frames(2);}
        IEnumerator Click()
        {InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left));yield return Frames(2);InputSystem.QueueStateEvent(mouse,new MouseState());yield return Frames(2);}
        IEnumerator Capture(string name)
        {yield return new WaitForEndOfFrame();var t=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(EFV2GuidanceChecks.Reports+"/"+name+".png",t.EncodeToPNG());Destroy(t);yield return null;}
        void Aim(LabTarget t)
        {view.transform.position=t.transform.position+new Vector3(0,1.5f,5);view.transform.LookAt(t.transform.position);}
        void At(string checkpoint)
        {var cp=w.checkpoints.Single(c=>c.label==checkpoint);w.player.Teleport(cp.safePosition);w.Reach(cp);w.player.enabled=false;}
        IEnumerator Start()
        {
            w=FindFirstObjectByType<EFV2PuzzleWorld>();w.AutomatedCheck=true;w.SetPaused(false);yield return Frames(3);
            while(w.OpeningActive)yield return null;
            yield return Frames(3);
            ui=w.GetComponent<EFV2Guidance>();keyboard=InputSystem.AddDevice<Keyboard>("GuidanceKeyboard");mouse=InputSystem.AddDevice<Mouse>("GuidanceMouse");
            report.AppendLine("ACTUAL PLAY GUI: F1, ability keys, FLOW selection use synthetic Input System events. Later circuit cases explicitly force device states and pose; not traversal.");
            Check(ui!=null&&ui.GoalTitle.Contains("둔덕"),"Start goal describes the first mound");
            Check(ui.GoalDetail.Contains("BLOOM")&&ui.GoalDetail.Contains("FLOW"),"Start presents both A solutions, no staged ability lock");
            Check(w.Slots.All(s=>s.phase==EFV2OrbPhase.Ready),"All orb slots ready at startup");yield return Capture("start");
            yield return Key(UnityEngine.InputSystem.Key.F1);Check(ui.HelpOpen&&!w.Paused,"F1 opens non-pausing help through actual input");yield return Capture("help");
            yield return Key(UnityEngine.InputSystem.Key.F1);Check(!ui.HelpOpen,"F1 closes help");
            foreach(var c in FindObjectsByType<Camera>(FindObjectsSortMode.None))c.enabled=false;
            view=new GameObject("Guidance verification camera - forced pose").AddComponent<Camera>();view.nearClipPlane=.1f;view.farClipPlane=300;w.player.view=view;
            w.player.cinemachineRig.SetInputActive(false);
            var root=w.targets.Single(t=>t.label=="A ROOT STAIRS");Aim(root);yield return Key(UnityEngine.InputSystem.Key.Digit1);yield return Frames(4);
            Check(w.Hovered==root&&ui.ContextTitle=="뿌리 계단","Actual raycast resolves Korean target name");
            Check(ui.ContextValid&&ui.ContextAction.Contains("BLOOM"),"Correct ability has explicit use prompt");yield return Capture("bloom-target");
            yield return Key(UnityEngine.InputSystem.Key.Digit2);yield return Frames(3);
            Check(!ui.ContextValid&&ui.ContextAction.Contains("1 BLOOM"),"Wrong ability displays the available ability key");yield return Capture("wrong-ability");
            var flow=w.targets.Single(t=>t.label=="A POND CURRENT");Aim(flow);yield return Key(UnityEngine.InputSystem.Key.Digit3);yield return Frames(4);yield return Click();
            Check(w.PendingFlow==flow&&ui.ContextAction.Contains("확정"),"First click opens actual FLOW choice");yield return Capture("flow-choice");
            int choice=w.FlowChoice;yield return Key(UnityEngine.InputSystem.Key.E);Check(w.FlowChoice!=choice,"E changes actual selected FLOW direction");yield return Click();yield return new WaitForSeconds(.7f);
            Check(w.PendingFlow==null&&flow.Direction==1,"Second click confirms actual FLOW direction");
            report.AppendLine("FORCED STATE PRESENTATION CASES begin here.");
            At("D3 WORKBENCH");view.transform.position=new Vector3(-48,-2,-57);view.transform.LookAt(new Vector3(-55,-7,-65));yield return Frames(4);
            Check(ui.GoalTitle.Contains("D5")&&ui.CircuitLines[0].Contains("공급 없음"),"Workbench points to D5 when supply missing");yield return Capture("d5-leak");
            w.channelStructure.Apply(LabAbility.Freeze);yield return Frames(3);Check(ui.GoalTitle.Contains("고정을 푸"),"Misaligned structure freeze explains release before alignment");
            w.upperIce.Apply(LabAbility.Freeze);yield return Frames(3);Check(ui.GoalTitle.Contains("얼음")&&ui.GoalDetail.Contains("우회"),"Internal ice explains thaw and valid bypass alternative");yield return Capture("d5-ice-block");
            w.bypass.Apply(LabAbility.Bloom);yield return Frames(4);Check(w.Supply&&ui.CircuitLines[0].Contains("연결됨")&&ui.GoalTitle.Contains("D4"),"Actual bypass supply overrides inactive normal-channel ice warning");
            w.branch.Apply(LabAbility.Flow,0);yield return Frames(3);Check(ui.CircuitLines[1].Contains("정비 부두"),"Dock branch readout");
            w.branch.Apply(LabAbility.Flow,1);yield return new WaitForSeconds(2.5f);Check(w.exitGate.IsOpen,"Existing wheel rule opens D4");yield return Frames(3);
            Check(ui.GoalTitle.Contains("주 출구")&&ui.CircuitLines[2].Contains("열림"),"Open gate shifts objective to main exit");yield return Capture("d2-main-needed");
            w.mainIce.Apply(LabAbility.Freeze);w.branch.Apply(LabAbility.Flow,2);yield return Frames(4);
            Check(ui.GoalTitle.Contains("얼음")&&ui.ContextTitle!="D2 MAIN WATER / ICE BRIDGE","Main outlet ice gets a distinct thaw objective");yield return Capture("main-ice-block");
            w.mainIce.Undo(LabAbility.Freeze);yield return new WaitForSeconds(.7f);
            Check(w.Arriving&&ui.GoalTitle.Contains("물레방아"),"Unblocked actual arrival displays supply progress");yield return Capture("arrival");
            yield return new WaitForSeconds(3.1f);Check(w.Restored,"Actual 3 second rule still restores W");yield return Capture("cinematic");
            yield return new WaitForSeconds(4.2f);Check(w.InputActive&&ui.GoalTitle.Contains("정원"),"Cinematic returns control and tells player to walk to garden");
            At("RESTORED GARDEN");yield return Frames(4);Check(w.GardenReached&&ui.GoalTitle.Contains("되살아"),"Garden completion has explore/restart copy");yield return Capture("ending");
            yield return Key(UnityEngine.InputSystem.Key.Enter);yield return Frames(6);Check(!w.Restored&&!ui.HelpOpen&&ui.GoalTitle.Contains("둔덕"),"Enter restart resets presentation along with existing world");
            foreach(var e in errors)report.AppendLine("UNITY ERROR "+e);failures+=errors.Count;
            report.AppendLine($"{passes} passes, {failures} failures.");EFV2GuidanceChecks.Finish(report.ToString());
        }
    }
}
