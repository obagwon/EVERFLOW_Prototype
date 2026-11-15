using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace EverflowMap.Editor
{
    [InitializeOnLoad]
    public static class EFV2PresentationChecks
    {
        const string Key="EVERFLOW.PresentationChecks";
        static EFV2PresentationChecks()
        {
            EditorApplication.playModeStateChanged+=s=>
            {
                if(!SessionState.GetBool(Key,false))return;
                if(s==PlayModeStateChange.EnteredPlayMode)new GameObject("Opening and ability image actual-input checks").AddComponent<EFV2PresentationProbe>();
                if(s==PlayModeStateChange.EnteredEditMode)SessionState.EraseBool(Key);
            };
            EditorApplication.update+=()=>
            {
                if(!SessionState.GetBool(Key,false))return;
                Application.runInBackground=true;EditorApplication.QueuePlayerLoopUpdate();
                if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+".deadline",float.MaxValue))Finish("FAIL presentation checks timed out.\n");
            };
        }
        [MenuItem("Tools/EVERFLOW/V2 Puzzles/Check Ability Images And Opening (Play)")]
        public static void Run()
        {
            Directory.CreateDirectory(EFV2PresentationTools.Reports);EditorSceneManager.OpenScene(EFV2PuzzleBuild.Scene);
            SessionState.SetBool(Key,true);SessionState.SetFloat(Key+".deadline",(float)EditorApplication.timeSinceStartup+120);
            EditorApplication.ExecuteMenuItem("Window/General/Game");EditorApplication.isPaused=false;EditorApplication.EnterPlaymode();
        }
        internal static void Finish(string text)
        {File.WriteAllText(EFV2PresentationTools.Reports+"/runtime-presentation.txt",DateTime.UtcNow.ToString("O")+"\n"+text);SessionState.EraseBool(Key);EditorApplication.ExitPlaymode();}
    }
    public sealed class EFV2PresentationProbe:MonoBehaviour
    {
        EFV2PuzzleWorld w;EFV2ScenePresentation p;EFV2Guidance ui;Keyboard keyboard;Mouse mouse;
        readonly StringBuilder report=new StringBuilder();
        readonly System.Collections.Generic.List<string> errors=new System.Collections.Generic.List<string>();
        int passes,failures;
        void OnEnable(){Application.logMessageReceived+=Log;}
        void OnDisable(){Application.logMessageReceived-=Log;if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);if(mouse!=null&&mouse.added)InputSystem.RemoveDevice(mouse);}
        void Log(string m,string s,LogType t){if((t==LogType.Error||t==LogType.Exception||t==LogType.Assert)&&!errors.Contains(m))errors.Add(m);}
        void Check(bool good,string text){if(good)passes++;else failures++;report.AppendLine((good?"PASS ":"FAIL ")+text);}
        IEnumerator Frames(int n){for(int i=0;i<n;i++)yield return null;}
        IEnumerator Key(Key key){InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));yield return Frames(2);InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return Frames(2);}
        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();var t=ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(EFV2PresentationTools.Reports+"/"+name+".png",t.EncodeToPNG());Destroy(t);
            report.AppendLine("GAME VIEW "+name+": "+Screen.width+"x"+Screen.height+", camera="+w.player.view.transform.position.ToString("F3")+", introElapsed="+p.IntroElapsed.ToString("F3"));
        }
        IEnumerator Start()
        {
            // Catch coroutine failures and always produce a truthful report/exit Play.
            var stack=new System.Collections.Generic.Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0)
            {
                bool more=false;object next=null;
                try{more=stack.Peek().MoveNext();if(more)next=stack.Peek().Current;}
                catch(Exception e){report.AppendLine("EXCEPTION "+e);failures++;break;}
                if(!more){stack.Pop();continue;}
                if(next is IEnumerator child){stack.Push(child);continue;}yield return next;
            }
            foreach(var e in errors)report.AppendLine("UNITY ERROR "+e);failures+=errors.Count;
            report.AppendLine($"{passes} passes, {failures} failures.");EFV2PresentationChecks.Finish(report.ToString());
        }
        IEnumerator Run()
        {
            yield return null;
            w=FindFirstObjectByType<EFV2PuzzleWorld>();w.AutomatedCheck=true;w.SetPaused(false);
            p=w.presentation;ui=w.GetComponent<EFV2Guidance>();
            keyboard=InputSystem.AddDevice<Keyboard>("PresentationKeyboard");mouse=InputSystem.AddDevice<Mouse>("PresentationMouse");
            report.AppendLine("REAL PLAY, existing MainCamera/Cinemachine and synthetic keyboard/mouse. No camera pose, target Apply, teleport or circuit writes. Not human testing or a full route.");
            Check(p!=null&&w.OpeningActive&&!w.InputActive&&!w.player.enabled,"Opening locks movement and ability input");
            Check(!w.player.cinemachineRig.input.enabled,"Opening disables gameplay look input");
            yield return Frames(3);
            Check(w.player.cinemachineRig.brain.ActiveVirtualCamera==p.OpeningCamera,"Cinemachine alone owns the overview");
            Check(Vector3.Distance(w.player.view.transform.position,p.overviewPosition)<.1f,"Actual rendering camera starts at far overview");
            yield return Capture("01-opening-overview");
            Vector3 start=w.player.transform.position;
            float overviewYaw=w.player.cinemachineRig.orbit.HorizontalAxis.Value,overviewPitch=w.player.cinemachineRig.orbit.VerticalAxis.Value;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.W,UnityEngine.InputSystem.Key.Digit3));
            InputSystem.QueueStateEvent(mouse,new MouseState{delta=new Vector2(150,60)}.WithButton(MouseButton.Left));yield return Frames(5);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.QueueStateEvent(mouse,new MouseState());yield return Frames(3);
            Check(Vector3.Distance(start,w.player.transform.position)<.01f&&w.Selected==EverflowPuzzleLab.LabAbility.Bloom&&w.Slots.All(s=>s.phase==EFV2OrbPhase.Ready),"W, mouse look, 3 and cast cannot move/cast during overview");
            Check(Mathf.Abs(Mathf.DeltaAngle(overviewYaw,w.player.cinemachineRig.orbit.HorizontalAxis.Value))<.01f&&Mathf.Abs(overviewPitch-w.player.cinemachineRig.orbit.VerticalAxis.Value)<.01f,"Mouse input cannot rotate standby gameplay orbit during opening");
            yield return Key(UnityEngine.InputSystem.Key.Escape);float pausedAt=p.IntroElapsed;Vector3 pausedCamera=w.player.view.transform.position;
            yield return new WaitForSecondsRealtime(.35f);
            Check(w.Paused&&!w.InputActive&&Mathf.Abs(p.IntroElapsed-pausedAt)<.001f,"Esc pause stops the opening clock");
            Check(Vector3.Distance(pausedCamera,w.player.view.transform.position)<.02f,"Opening camera remains still while paused");
            yield return Capture("02-opening-paused");yield return Key(UnityEngine.InputSystem.Key.Escape);
            Check(!w.Paused&&w.OpeningActive&&!w.player.enabled,"Resume continues opening without releasing input early");
            while(p.IntroElapsed<p.overviewSeconds+.55f)yield return null;
            yield return Capture("03-opening-approach");
            Check(Vector3.Distance(w.player.view.transform.position,p.overviewPosition)>3,"Camera approaches the character after overview hold");
            while(p.IntroElapsed<p.overviewSeconds+p.approachSeconds-.06f&&w.OpeningActive)yield return null;
            Vector3 before=w.player.view.transform.position;
            while(w.OpeningActive)yield return null;
            yield return Frames(3);
            Check(w.InputActive&&w.player.enabled&&w.player.cinemachineRig.input.enabled,"Opening returns movement, cast and look controls");
            Check(w.player.cinemachineRig.brain.ActiveVirtualCamera==w.player.cinemachineRig.virtualCamera,"Gameplay Cinemachine camera regains priority");
            Check(Mathf.Abs(Mathf.DeltaAngle(overviewYaw,w.player.cinemachineRig.orbit.HorizontalAxis.Value))<.01f&&Mathf.Abs(overviewPitch-w.player.cinemachineRig.orbit.VerticalAxis.Value)<.01f,"No deferred mouse input rotates gameplay orbit on handoff");
            Check(Vector3.Distance(before,w.player.view.transform.position)<.5f,"Near-camera handoff has no large position jump");
            Check(Cursor.lockState==CursorLockMode.Locked&&!Cursor.visible,"Cursor is captured for gameplay");
            Check(FindObjectsByType<EFMapCamera>(FindObjectsSortMode.None).Length==0&&FindObjectsByType<EFMapAbilityHUD>(FindObjectsSortMode.None).Length==0,"No old transform-camera or old-world HUD/input owner copied into puzzle");
            Check(p.abilityIcons.Length==3&&p.abilityIcons.All(s=>s!=null)&&p.iconUV.All(r=>r.width>0&&r.height>0),"Three real reference sprites and visible crops bound");
            Check(ui.GoalTitle.Contains("둔덕")&&ui.GoalDetail.Contains("BLOOM")&&ui.GoalDetail.Contains("FLOW"),"Original puzzle objective and both A hints preserved");
            var keys=new[]{UnityEngine.InputSystem.Key.Digit1,UnityEngine.InputSystem.Key.Digit2,UnityEngine.InputSystem.Key.Digit3};
            string[] words={"식물","고정","방향"};
            for(int i=0;i<3;i++)
            {
                yield return Key(keys[i]);
                Check((int)w.Selected==i&&ui.AbilityDescription.Contains(words[i]),"Actual key "+(i+1)+" selects matching ability and description");
                yield return Capture("04-ability-"+(i+1));
            }
            yield return Key(UnityEngine.InputSystem.Key.F1);Check(ui.HelpOpen&&!w.Paused,"Existing F1 help remains usable");yield return Capture("05-help");yield return Key(UnityEngine.InputSystem.Key.F1);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.W));yield return new WaitForSeconds(.3f);InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return Frames(3);
            Check(Vector3.ProjectOnPlane(w.player.transform.position-start,Vector3.up).magnitude>.3f,"Actual W movement works after camera handoff");
            float yaw=w.player.cinemachineRig.orbit.HorizontalAxis.Value;
            InputSystem.QueueStateEvent(mouse,new MouseState{delta=new Vector2(80,0)});yield return Frames(3);InputSystem.QueueStateEvent(mouse,new MouseState());yield return Frames(3);
            Check(Mathf.Abs(Mathf.DeltaAngle(yaw,w.player.cinemachineRig.orbit.HorizontalAxis.Value))>1,"Actual mouse look works after handoff");
            yield return Key(UnityEngine.InputSystem.Key.R);yield return Frames(4);
            Check(w.InputActive&&Vector3.Distance(w.player.transform.position,w.spawnPoint.position)<.5f,"Existing R recovery remains immediate");
            yield return Key(UnityEngine.InputSystem.Key.Escape);Check(w.Paused&&!w.player.enabled,"Gameplay pause still works");yield return Key(UnityEngine.InputSystem.Key.Escape);
            Check(w.InputActive,"Gameplay resume still works");
            Check(FindFirstObjectByType<EFV2WaterIntegration>()!=null&&w.targets.Count>15,"Existing water presentation and puzzle composition preserved");
        }
    }
}
