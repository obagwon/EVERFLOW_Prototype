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
    public static class EFV2PuzzlePlayChecks
    {
        internal const string ModeKey="EVERFLOW.V2Puzzle.CheckMode";
        static EFV2PuzzlePlayChecks()
        {
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state==PlayModeStateChange.EnteredEditMode && SessionState.GetString(ModeKey,"").Length>0)
                {
                    string interrupted=SessionState.GetString(ModeKey,"");
                    Finish(interrupted,"INCOMPLETE: Play stopped before runtime checks finished. Not a pass or a playthrough.\n",false);
                    return;
                }
                if(state!=PlayModeStateChange.EnteredPlayMode || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=EFV2PuzzleBuild.Scene)return;
                string mode=SessionState.GetString(ModeKey,"");
                if(mode.Length>0)new GameObject("V2 "+mode+" runtime checks").AddComponent<EFV2PuzzleProbe>().mode=mode;
                else new GameObject("V2 observation-only play recorder").AddComponent<EFV2PlayRecorder>();
            };
            EditorApplication.update+=()=>
            {
                string mode=SessionState.GetString(ModeKey,"");
                if(mode.Length==0 || EditorApplication.timeSinceStartup<=SessionState.GetFloat(ModeKey+".deadline",float.MaxValue))return;
                Finish(mode,"INCOMPLETE: Runtime checks exceeded 300 seconds. This is not a pass or a playthrough.\n",false);
                if(EditorApplication.isPlaying)EditorApplication.ExitPlaymode();
            };
        }
        static void Run(string mode)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stop Play first.");
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            EditorSceneManager.OpenScene(EFV2PuzzleBuild.Scene);EFV2PuzzleBuild.Validate();
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            EditorApplication.isPaused=false;
            SessionState.SetString(ModeKey,mode);SessionState.SetFloat(ModeKey+".deadline",(float)EditorApplication.timeSinceStartup+300);EditorApplication.EnterPlaymode();
        }
        [MenuItem("Tools/EVERFLOW/V2 Puzzles/Run Forced-State Runtime Checks")]
        public static void Forced()=>Run("forced-state");
        [MenuItem("Tools/EVERFLOW/V2 Puzzles/Run Synthetic Input Smoke Checks")]
        public static void InputSmoke()=>Run("synthetic-input-smoke");
        [MenuItem("Tools/EVERFLOW/V2 Puzzles/Run Continuous BLOOM Route (Synthetic Input)")]
        public static void Route()=>Run("route-bloom");
        [MenuItem("Tools/EVERFLOW/V2 Puzzles/Run Continuous Alternate Route (Synthetic Input)")]
        public static void Alternate()=>Run("route-alternate");
        [MenuItem("Tools/EVERFLOW/V2 Puzzles/Run Isolated Exit Physics (Forced Setup)")]
        public static void ExitPhysics()=>Run("exit-isolated");
        [MenuItem("Tools/EVERFLOW/V2 Puzzles/Run Isolated Platforms And Fall (Forced Setup)")]
        public static void Platforms()=>Run("platforms-isolated");
        internal static void Finish(string mode,string result,bool pass)
        {
            SessionState.EraseString(ModeKey);
            Directory.CreateDirectory(EFV2PuzzleBuild.Reports);
            File.WriteAllText(EFV2PuzzleBuild.Reports+"/runtime-"+mode+".txt",DateTime.UtcNow.ToString("O")+"\n"+result);
            Debug.Log(result);
            if(Application.isBatchMode)EditorApplication.Exit(pass?0:1);
        }
    }

    public sealed class EFV2PuzzleProbe : MonoBehaviour
    {
        public string mode;
        public string Progress {get;private set;}="Starting";
        EFV2PuzzleWorld w;
        Mouse mouse;
        Keyboard keyboard;
        readonly StringBuilder report=new StringBuilder();
        int checks,failures;
        readonly System.Collections.Generic.List<string> runtimeErrors=new System.Collections.Generic.List<string>();
        void OnEnable(){Application.logMessageReceived+=OnLog;}
        void OnDisable(){Application.logMessageReceived-=OnLog;}
        void OnLog(string message,string trace,LogType type)
        {
            if((type==LogType.Error||type==LogType.Exception||type==LogType.Assert)&&!message.Contains("Licensing")&&!runtimeErrors.Contains(message)&&runtimeErrors.Count<10)runtimeErrors.Add(message);
        }
        void Check(bool condition,string message)
        {checks++;if(!condition)failures++;report.AppendLine((condition?"PASS ":"FAIL ")+message);}
        IEnumerator Frames(int n) {for(int i=0;i<n;i++)yield return null;}
        IEnumerator Key(Key key)
        {
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));yield return Frames(2);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return Frames(2);
        }
        IEnumerator Click()
        {
            InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left));yield return Frames(2);
            InputSystem.QueueStateEvent(mouse,new MouseState());yield return Frames(2);
        }
        IEnumerator Aim(LabTarget target)
        {
            for(int n=0;n<50;n++)
            {
                if(w.Hovered==target)
                {
                    InputSystem.QueueStateEvent(mouse,new MouseState());yield return Frames(4);
                    if(w.Hovered==target)yield break;
                }
                Vector3 screen=w.player.view.WorldToViewportPoint(target.transform.position);
                Vector3 local=w.player.view.transform.InverseTransformPoint(target.transform.position);
                float yaw=Mathf.Atan2(local.x,local.z)*Mathf.Rad2Deg;
                if(screen.z<0)yaw=Mathf.Sign(yaw)*Mathf.Min(45,Mathf.Abs(yaw));
                float pitch=(screen.y-.5f)*w.player.view.fieldOfView;
                InputSystem.QueueStateEvent(mouse,new MouseState{delta=new Vector2(Mathf.Clamp(yaw,-35,35)/.14f,Mathf.Clamp(pitch,-20,20)/.119f)});
                yield return Frames(3);
            }
        }
        LabTarget Target(string label)=>w.targets.First(t=>t.label==label);
        void Note(string message)
        {
            Progress=message;report.AppendLine(message);
            File.WriteAllText(EFV2PuzzleBuild.Reports+"/route-progress.txt",report.ToString());
        }
        IEnumerator Walk(Vector3 surface,string name)
        {
            Note("WALK "+name+" -> "+surface.ToString("F2"));
            float start=Time.unscaledTime,lastProgress=start,best=float.MaxValue,nextJump=0;
            int jumps=0;
            while(Time.unscaledTime-start<22)
            {
                var position=w.player.transform.position;
                Vector3 delta=Vector3.ProjectOnPlane(surface-position,Vector3.up);
                if(delta.magnitude<.38f)
                {
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return Frames(3);
                    float landingWait=Time.unscaledTime;
                    while(!w.player.GetComponent<CharacterController>().isGrounded && Time.unscaledTime-landingWait<1.5f)yield return null;
                    float error=Mathf.Abs(w.player.transform.position.y-1-surface.y);
                    if(error>.8f)throw new Exception("Wrong landing height at "+name+": player="+w.player.transform.position+" expected surface="+surface);
                    Note("ARRIVED "+name+" feet="+(w.player.transform.position-Vector3.up).ToString("F2"));yield break;
                }
                if(delta.magnitude<best-.04f){best=delta.magnitude;lastProgress=Time.unscaledTime;}
                Progress="WALK "+name+" position="+position.ToString("F2")+" remaining="+delta.magnitude.ToString("F2");
                var forward=Vector3.ProjectOnPlane(w.player.view.transform.forward,Vector3.up).normalized;
                float yaw=Vector3.SignedAngle(forward,delta.normalized,Vector3.up);
                float pitch=w.player.cinemachineRig.orbit.VerticalAxis.Value;
                InputSystem.QueueStateEvent(mouse,new MouseState{delta=new Vector2(Mathf.Clamp(yaw,-12,12)/.14f,Mathf.Clamp((18-pitch)/-.119f,-25,25))});
                var keys=new System.Collections.Generic.List<UnityEngine.InputSystem.Key>();
                if(Mathf.Abs(yaw)<12)keys.Add(UnityEngine.InputSystem.Key.W);
                if(Time.unscaledTime-lastProgress>.9f && Time.unscaledTime>nextJump && jumps<3)
                {keys.Add(UnityEngine.InputSystem.Key.Space);jumps++;nextJump=Time.unscaledTime+1.1f;Note("INPUT Space at obstacle / "+name+" position="+position.ToString("F2"));}
                if(Time.unscaledTime-lastProgress>5)throw new Exception("Physical traversal blocked at "+name+" position="+position+" goal="+surface);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys.ToArray()));yield return null;
            }
            throw new Exception("Walk timed out: "+name+" position="+w.player.transform.position);
        }
        IEnumerator Cast(string label,LabAbility ability,int direction=0)
        {
            var target=Target(label);bool expectedFrozen=!target.Frozen;Note("CAST "+ability+" / "+label);
            yield return Key(ability==LabAbility.Bloom?UnityEngine.InputSystem.Key.Digit1:ability==LabAbility.Freeze?UnityEngine.InputSystem.Key.Digit2:UnityEngine.InputSystem.Key.Digit3);
            bool applied=false;
            for(int attempt=0;attempt<3&&!applied;attempt++)
            {
                yield return Aim(target);
                bool usable=w.CanUse(target,ability,out var reason);
                if(w.Hovered!=target||!usable)throw new Exception("Aim/cast blocked: "+label+" hover="+(w.Hovered!=null?w.Hovered.label:"none")+" reason="+reason+" at player="+w.player.transform.position+" camera="+w.player.view.transform.position);
                if(ability==LabAbility.Flow)
                {
                    if(w.PendingFlow!=target)yield return Click();
                    if(w.PendingFlow!=target){Note("INPUT retry FLOW preview "+label);continue;}
                    int guard=0;while(w.FlowChoice!=direction&&guard++<4)yield return Key(UnityEngine.InputSystem.Key.E);
                    yield return Click();
                }
                else yield return Click();
                yield return new WaitForSeconds(1.6f);
                applied=ability==LabAbility.Bloom?target.Grown:ability==LabAbility.Freeze?target.Frozen==expectedFrozen:target.Direction==direction;
                if(!applied)Note("INPUT retry after missed/damped crosshair click: "+label);
            }
            Check(applied,"Input cast applied "+label);
            if(!applied)throw new Exception("Input cast did not arrive after three attempts: "+label);
        }
        IEnumerator ContinuousRoute()
        {
            Note("CONTINUOUS SYNTHETIC KEYBOARD/MOUSE ROUTE. No Teleport, direct Apply, gate Tick or forced device pose. Camera uses mouse events, movement uses WASD/Space, casts use normal raycasts and orb flight. NOT a human timing test.");
            mouse=InputSystem.AddDevice<Mouse>("V2RouteMouse");keyboard=InputSystem.AddDevice<Keyboard>("V2RouteKeyboard");
            yield return Cast("A ROOT STAIRS",LabAbility.Bloom);
            yield return Walk(new Vector3(-42,.1f,-12.5f),"A shore approach");
            yield return Walk(new Vector3(-40.4f,.1f,-14.8f),"A root foot");
            yield return Walk(new Vector3(-40.4f,6.05f,-27),"A root stair top");
            yield return Walk(new Vector3(-45,6.05f,-28),"A common landing");
            Check(w.CurrentCheckpoint!=null&&w.CurrentCheckpoint.label=="A COMMON MOUND","A actual common landing checkpoint");
            yield return Walk(new Vector3(-40.4f,6.05f,-29),"B bridge threshold");
            yield return Walk(new Vector3(-36,6,-33),"B temporary connector center");
            yield return Walk(new Vector3(-30.2f,6,-37.7f),"B connector far end");
            yield return Walk(new Vector3(-27,6.1f,-40.5f),"B entrance");
            yield return Cast("B ROOT BRIDGE",LabAbility.Bloom);
            yield return Walk(new Vector3(-26.4f,6.08f,-41.5f),"B root bridge near");
            yield return Walk(new Vector3(-26.4f,6.08f,-50.8f),"B root bridge far");
            yield return Walk(new Vector3(-25,6.1f,-51.5f),"B far bank");
            yield return Walk(new Vector3(-25,6.1f,-52),"B ascent foot");
            yield return Walk(new Vector3(-34.5f,15.15f,-58.8f),"B ascent to G");
            yield return Walk(new Vector3(-35,15.16f,-60.5f),"G approach");
            yield return Cast("G RUIN GATE ROOT",LabAbility.Bloom);
            yield return new WaitForSeconds(2);
            Check(w.ruinGate.Latched,"G root opens and latches using input");
            yield return Walk(new Vector3(-29.75f,15.06f,-60.2f),"G doorway front");
            yield return Walk(new Vector3(-30.65f,15.02f,-63.1f),"G doorway pass");
            float[] floors={12,9,6,3,0,-3};
            for(int i=0;i<6;i++)
            {
                float x=i%2==0?-30.65f:-28.85f,z=i%2==0?-68.9f:-63.1f;
                yield return Walk(new Vector3(x,floors[i]+.02f,z),"Tower flight "+(i+1));
                if(i<5)yield return Walk(new Vector3(i%2==0?-28.85f:-30.65f,floors[i]+.02f,z),"Tower switchback "+(i+1));
            }
            yield return Walk(new Vector3(-30.5f,-2.98f,-63.1f),"Tower ground exit lane");
            yield return Walk(new Vector3(-30.5f,-3,-37),"Underground tunnel");
            yield return Walk(new Vector3(-44,-3,-37),"D1 walkway");
            yield return Walk(new Vector3(-46,-3,-38),"D1 balcony threshold");
            yield return Walk(new Vector3(-48,-2.97f,-38.7f),"D1 balcony");
            yield return Cast("D3 VINE ACCESS",LabAbility.Bloom);
            yield return Walk(new Vector3(-40,-3,-37.5f),"D3 vine foot");
            yield return Walk(new Vector3(-39,-7.45f,-45.2f),"D3 vine landing");
            yield return Walk(new Vector3(-40,-7.46f,-57),"D3 workbench");
            Check(w.returnWalk.activeSelf,"Actual workbench arrival enables safe thaw return walkway");
            yield return Walk(new Vector3(-40,-7.45f,-61.7f),"D5 workbench edge");
            yield return Walk(new Vector3(-46,-5.95f,-66),"D5 service access");
            yield return Walk(new Vector3(-46,-5.95f,-64.4f),"D5 channel clearance");
            yield return Walk(new Vector3(-68,-5.95f,-64.5f),"D5 west ledge");
            yield return Walk(new Vector3(-70.1f,-5.95f,-65),"D5 bypass stair foot");
            yield return Walk(new Vector3(-74,-8.9f,-64.8f),"D5 bypass root access");
            yield return Cast("D5 BYPASS ROOT",LabAbility.Bloom);Check(w.Supply,"Input-grown bypass supplies D2");
            yield return Walk(new Vector3(-70.1f,-5.95f,-65),"D5 return ledge");
            yield return Walk(new Vector3(-46,-5.95f,-64.4f),"D5 east return");
            yield return Walk(new Vector3(-46,-5.95f,-66),"D5 stairs return");
            yield return Walk(new Vector3(-40,-7.45f,-62),"D3 upper return");
            yield return Walk(new Vector3(-40,-7.46f,-57),"D3 safe return");
            yield return Walk(new Vector3(-43.5f,-7.45f,-59.2f),"D3 return threshold");
            yield return Walk(new Vector3(-45.15f,-7.45f,-59.2f),"D3 return stairs start");
            yield return Walk(new Vector3(-50,-11.5f,-59.2f),"D3 return stairs end");
            yield return Walk(new Vector3(-56,-11.5f,-59.2f),"D2 return perimeter");
            yield return Walk(new Vector3(-56,-11.1f,-52),"D2 central hub");
            yield return Walk(new Vector3(-56,-11.5f,-55.5f),"D4 dry perimeter entry");
            yield return Walk(new Vector3(-71,-11.5f,-55.5f),"D4 past D1 stair base");
            yield return Walk(new Vector3(-71,-11.5f,-46.5f),"D4 west perimeter");
            yield return Walk(new Vector3(-75.5f,-11.46f,-46.5f),"D4 control landing");
            yield return Cast("D4 EXIT GATE ROOT",LabAbility.Bloom);yield return new WaitForSeconds(1.5f);
            Check(w.exitGate.Latched,"D4 root fully opens and latches using input");
            yield return Walk(new Vector3(-71,-11.5f,-46.5f),"D4 latched return west");
            yield return Walk(new Vector3(-71,-11.5f,-55.5f),"D4 latched return north");
            yield return Walk(new Vector3(-56,-11.5f,-55.5f),"D2 branch return bridge");
            yield return Walk(new Vector3(-56,-11.1f,-52),"D2 main branch aim position");
            yield return Cast("D2 THREE WAY BRANCH",LabAbility.Flow,2);
            float wait=Time.unscaledTime;while(!w.Restored&&Time.unscaledTime-wait<10)yield return null;
            Check(w.Restored,"Continuous actual water supply restores W from input results");
            if(!w.Restored)throw new Exception("Final supply did not restore W");
            ScreenCapture.CaptureScreenshot(EFV2PuzzleBuild.Reports+"/route-bloom-restored.png");
            while(!w.InputActive)yield return null;
            yield return Walk(new Vector3(-56,-11.5f,-55.5f),"Final exit north bridge");
            yield return Walk(new Vector3(-71,-11.5f,-55.5f),"Final exit west bridge");
            yield return Walk(new Vector3(-71,-11.5f,-46.5f),"Final exit control approach");
            yield return Walk(new Vector3(-75.5f,-11.46f,-47.5f),"D4 exit approach");
            yield return Walk(new Vector3(-79.5f,-11.9f,-48.1f),"D4 actual door passage");
            yield return Walk(new Vector3(-82.3f,-11.9f,-47),"Exterior corridor entry");
            yield return Walk(new Vector3(-82.3f,-17.35f,-13.3f),"Safe gorge corridor");
            yield return Walk(new Vector3(-83.4f,-17.35f,-13.3f),"Garden threshold bridge");
            yield return Walk(new Vector3(-83.4f,-17.35f,-2),"Garden east walkway");
            yield return Walk(new Vector3(-84,-17.28f,0),"Restored garden");
            Check(w.GardenReached,"Actual continuous S-A-B-G-D1-D3-D5-D2-D4-W garden arrival");
            Note("AUTOMATED ROUTE elapsed="+w.Elapsed.ToString("F2")+"s. Synthetic route following; not first-time human play time.");
            ScreenCapture.CaptureScreenshot(EFV2PuzzleBuild.Reports+"/route-bloom-garden.png");yield return Frames(4);
            yield return Key(UnityEngine.InputSystem.Key.Enter);yield return Frames(12);
            Check(!w.Restored&&!w.GardenReached&&Vector3.Distance(w.player.transform.position,w.spawnPoint.position)<.5f,"Enter after ending restarts at S through normal keyboard input");
        }
        IEnumerator RideToLanding(string flowLabel,string leafLabel,Vector3 landing)
        {
            yield return Cast(flowLabel,LabAbility.Flow,0);
            var target=Target(leafLabel);var mover=w.movers.First(m=>m.target==target);
            float started=Time.unscaledTime;
            while(Vector3.Distance(mover.motion.position,mover.destinations[0])>.6f && Time.unscaledTime-started<20)
            {Progress="RIDING "+leafLabel+" player="+w.player.transform.position.ToString("F2");yield return null;}
            if(Time.unscaledTime-started>=20)throw new Exception("Leaf did not reach landing: "+leafLabel);
            yield return Cast(leafLabel,LabAbility.Freeze);
            Check(target.Frozen,"Freeze holds ridden platform "+leafLabel);
            yield return Walk(landing,leafLabel+" actual landing");
        }
        IEnumerator AlternateRoute()
        {
            Note("CONTINUOUS ALTERNATE SYNTHETIC INPUT ROUTE: A leaf, C grown leaf, G wheel, D3 ice, D5 alignment/freeze, D4 wheel, ice thaw, garden and restart. No Teleport/Apply/Tick/forced poses.");
            mouse=InputSystem.AddDevice<Mouse>("V2AlternateMouse");keyboard=InputSystem.AddDevice<Keyboard>("V2AlternateKeyboard");
            yield return Walk(new Vector3(-49,.08f,-13),"A leaf shore");
            yield return Walk(new Vector3(-49,.28f,-15.7f),"A board stationary leaf");
            yield return RideToLanding("A POND CURRENT","A FLOATING LEAF",new Vector3(-48.5f,6.05f,-27.5f));
            yield return Walk(new Vector3(-45,6.05f,-28),"A alternate common landing");
            Check(w.CurrentCheckpoint!=null&&w.CurrentCheckpoint.label=="A COMMON MOUND","A leaf reaches same common mound");
            yield return Walk(new Vector3(-50.8f,6.08f,-30.1f),"C connector threshold");
            yield return Walk(new Vector3(-54.5f,6,-34),"C temporary connector");
            yield return Walk(new Vector3(-57.5f,6,-37.5f),"C connector end");
            yield return Walk(new Vector3(-64,6.12f,-40),"C entrance");
            yield return Cast("C GROWABLE LEAF",LabAbility.Bloom);
            yield return Walk(new Vector3(-64,6.25f,-43.8f),"C board grown leaf");
            yield return RideToLanding("C WIND","C GROWABLE LEAF",new Vector3(-62,11.85f,-59));
            Check(Target("A FLOATING LEAF").Frozen&&Target("A POND CURRENT").Direction==0,"Retargeted FREEZE/FLOW preserve earlier A world results");
            yield return Walk(new Vector3(-61,11.9f,-60),"C ascent foot");
            yield return Walk(new Vector3(-57,15.15f,-62),"C ascent top");
            yield return Walk(new Vector3(-53.8f,15.15f,-63),"C arrives G paving");
            yield return Walk(new Vector3(-35,15.16f,-60.5f),"G alternate approach");
            Check(w.Slots[(int)LabAbility.Freeze].target==null&&Target("C GROWABLE LEAF").Frozen,"Distance recall independently returns FREEZE while C remains fixed");
            yield return Cast("G WATER BRANCH",LabAbility.Flow,0);yield return new WaitForSeconds(2);
            Check(w.ruinGate.Latched,"G water wheel alternative opens and latches from input");
            yield return Walk(new Vector3(-29.75f,15.06f,-60.2f),"G alternate doorway front");
            yield return Walk(new Vector3(-30.65f,15.02f,-63.1f),"G alternate doorway pass");
            float[] floors={12,9,6,3,0,-3};
            for(int i=0;i<6;i++)
            {
                float x=i%2==0?-30.65f:-28.85f,z=i%2==0?-68.9f:-63.1f;
                yield return Walk(new Vector3(x,floors[i]+.02f,z),"Alternate tower flight "+(i+1));
                if(i<5)yield return Walk(new Vector3(i%2==0?-28.85f:-30.65f,floors[i]+.02f,z),"Alternate tower turn "+(i+1));
            }
            yield return Walk(new Vector3(-30.5f,-2.98f,-63.1f),"Alternate tower exit lane");
            yield return Walk(new Vector3(-30.5f,-3,-37),"Alternate tunnel");
            yield return Walk(new Vector3(-44,-3,-37),"Alternate D1 walkway");
            yield return Walk(new Vector3(-46,-3,-38),"Alternate D1 threshold");
            yield return Walk(new Vector3(-59.5f,-3,-40),"D1 stairs top");
            yield return Walk(new Vector3(-59.5f,-11.5f,-53),"D1 stairs bottom safe landing");
            yield return Cast("D2 MAIN WATER / ICE BRIDGE",LabAbility.Freeze);
            yield return Key(UnityEngine.InputSystem.Key.R);yield return Frames(12);
            Check(w.mainIce.Frozen&&Target("A FLOATING LEAF").Frozen&&Target("C GROWABLE LEAF").Frozen,"R recovery preserves local ice and completed surface platform states");
            yield return Walk(new Vector3(-49,-11.57f,-52),"D3 ice crossing");
            yield return Walk(new Vector3(-43.8f,-11.4f,-52),"D3 ice to dock");
            yield return Walk(new Vector3(-43.8f,-11.4f,-54),"D3 dock stair foot");
            yield return Walk(new Vector3(-43.8f,-7.5f,-59.9f),"D3 dock stair top");
            yield return Walk(new Vector3(-40,-7.46f,-57),"Alternate D3 workbench");
            yield return Walk(new Vector3(-40,-7.45f,-61),"D5 workbench controls");
            yield return Cast("D5 ALIGNING WIND",LabAbility.Flow,0);
            bool supplied=false;
            for(int attempt=0;attempt<3&&!supplied;attempt++)
            {
                yield return Key(UnityEngine.InputSystem.Key.Digit2);yield return Aim(w.channelStructure);
                float start=Time.unscaledTime;
                while(Vector3.Distance(w.channel.motion.position,w.connectionPoint.position)>.1f && Time.unscaledTime-start<12)
                {Progress="D5 WAIT FOR ALIGNMENT "+w.channel.motion.position.ToString("F3");yield return null;}
                if(w.Hovered!=w.channelStructure)yield return Aim(w.channelStructure);
                yield return Click();yield return new WaitForSeconds(1.1f);supplied=w.Supply;
                if(!supplied&&w.channelStructure.Frozen){Note("D5 retry: release misaligned structure");yield return Click();yield return new WaitForSeconds(.8f);}
            }
            Check(supplied&&w.channelStructure.Frozen,"D5 FLOW alignment and FREEZE structure supplies from real orb flight");
            if(!supplied)throw new Exception("D5 alignment could not be fixed through input");
            yield return Walk(new Vector3(-40,-7.46f,-57),"D3 return after alignment");
            yield return Walk(new Vector3(-43.5f,-7.45f,-59.2f),"Alternate return threshold");
            yield return Walk(new Vector3(-45.15f,-7.45f,-59.2f),"Alternate return stairs start");
            yield return Walk(new Vector3(-50,-11.5f,-59.2f),"Alternate return stairs end");
            yield return Walk(new Vector3(-56,-11.5f,-59.2f),"Alternate return perimeter");
            yield return Walk(new Vector3(-56,-11.1f,-52),"Alternate D2 hub");
            yield return Cast("D2 THREE WAY BRANCH",LabAbility.Flow,1);yield return new WaitForSeconds(2);
            Check(w.exitGate.Latched,"D4 small-wheel alternative fully opens and latches through input");
            yield return Cast("D2 THREE WAY BRANCH",LabAbility.Flow,2);yield return new WaitForSeconds(3.2f);
            Check(!w.Restored&&!w.Arriving&&w.mainIce.Frozen&&w.exitGate.Latched,"Main ice blocks W after D4 remains latched and D2 points to main exit");
            yield return Cast("D2 MAIN WATER / ICE BRIDGE",LabAbility.Freeze);
            Check(!w.mainIce.Frozen&&w.channelStructure.Frozen,"Thaw main water leaves prior D5 structure freeze intact");
            float wait=Time.unscaledTime;while(!w.Restored&&Time.unscaledTime-wait<10)yield return null;
            Check(w.Restored,"Alternate result circuit restores W after thaw");
            if(!w.Restored)throw new Exception("Alternate W restore failed");
            while(!w.InputActive)yield return null;
            yield return Walk(new Vector3(-56,-11.5f,-55.5f),"Alternate exit north");
            yield return Walk(new Vector3(-71,-11.5f,-55.5f),"Alternate exit west bridge");
            yield return Walk(new Vector3(-71,-11.5f,-46.5f),"Alternate exit control approach");
            yield return Walk(new Vector3(-75.5f,-11.46f,-47.5f),"Alternate D4 exit approach");
            yield return Walk(new Vector3(-79.5f,-11.9f,-48.1f),"Alternate actual door passage");
            yield return Walk(new Vector3(-82.3f,-11.9f,-47),"Alternate corridor start");
            yield return Walk(new Vector3(-82.3f,-17.35f,-13.3f),"Alternate safe corridor");
            yield return Walk(new Vector3(-83.4f,-17.35f,-13.3f),"Alternate garden threshold");
            yield return Walk(new Vector3(-83.4f,-17.35f,-2),"Alternate east walkway");
            yield return Walk(new Vector3(-84,-17.28f,0),"Alternate garden");
            Check(w.GardenReached,"Continuous alternate A-leaf/C/G-wheel/D3-ice/D5-align/D4-wheel route reaches garden");
            Note("ALTERNATE AUTOMATED ROUTE elapsed="+w.Elapsed.ToString("F2")+"s; not human first-play time.");
            ScreenCapture.CaptureScreenshot(EFV2PuzzleBuild.Reports+"/route-alternate-garden.png");yield return Frames(4);
            yield return Key(UnityEngine.InputSystem.Key.Enter);yield return Frames(12);
            Check(!w.Restored&&Vector3.Distance(w.player.transform.position,w.spawnPoint.position)<.5f,"Alternate route ending restarts through Enter");
        }
        IEnumerator Smoke()
        {
            report.AppendLine("SYNTHETIC Input System mouse/keyboard events through game Update/LateUpdate, actual raycasts and orb flight. No LabTarget.Apply or forced device state. NOT a human or full traversal playthrough.");
            mouse=InputSystem.AddDevice<Mouse>("V2ProbeMouse");keyboard=InputSystem.AddDevice<Keyboard>("V2ProbeKeyboard");
            var root=w.targets.First(t=>t.label=="A ROOT STAIRS");
            yield return Key(UnityEngine.InputSystem.Key.Digit1);yield return Aim(root);
            Check(w.Hovered==root,"Synthetic mouse aims at the real A root sphere with camera collision enabled");
            if(w.Hovered==root)
            {
                yield return Click();yield return new WaitForSeconds(1.5f);
                Check(root.Grown,"Actual left click, raycast and orb arrival grow A stairs");
                Check(w.Slots[0].target==root&&w.Slots[0].phase==EFV2OrbPhase.Placed,"Only one BLOOM orb occupies A");
            }
            var flow=w.targets.First(t=>t.label=="A POND CURRENT");
            yield return Key(UnityEngine.InputSystem.Key.Digit3);yield return Aim(flow);yield return Click();
            Check(w.PendingFlow==flow,"FLOW click opens valid direction preview");
            if(w.PendingFlow==flow)
            {
                yield return Key(UnityEngine.InputSystem.Key.E);Check(w.FlowChoice==1,"E selects second valid direction");
                yield return Key(UnityEngine.InputSystem.Key.E);yield return Click();yield return new WaitForSeconds(1.5f);
                Check(flow.Direction==0,"Second click confirms chosen direction through real input");
                Check(root.Grown,"FLOW reuse leaves previously grown A stairs complete");
                var mover=w.movers.First(m=>m.flow==flow);var before=mover.motion.position;
                yield return new WaitForSeconds(.8f);Check(Vector3.Distance(before,mover.motion.position)>.5f,"Real imported A leaf and its carrier move");
            }
            var start=w.player.transform.position;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.W));yield return new WaitForSeconds(.22f);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return Frames(3);
            Check(Vector3.Distance(start,w.player.transform.position)>.5f,"W moves actual character with camera-relative locomotion");
            float y=w.player.transform.position.y;yield return Key(UnityEngine.InputSystem.Key.Space);yield return new WaitForSeconds(.15f);
            Check(w.player.transform.position.y>y+.3f,"Space produces physical jump");
            yield return Key(UnityEngine.InputSystem.Key.R);yield return Frames(8);
            Check(root.Grown,"R respawn preserves completed A growth");
            Check(w.orbs.Length==3&&w.orbs.All(t=>t!=null),"Respawn retains exactly three orb visuals");
        }
        IEnumerator IsolatedExit()
        {
            Note("ISOLATED EXIT PHYSICS: forced bypass/branch/gate setup and initial teleport; then actual keyboard/mouse traversal. NOT continuous S-to-ending completion.");
            mouse=InputSystem.AddDevice<Mouse>("V2ExitMouse");keyboard=InputSystem.AddDevice<Keyboard>("V2ExitKeyboard");
            w.bypass.Apply(LabAbility.Bloom);w.exitGate.root.Apply(LabAbility.Bloom);w.exitGate.Tick(10);w.branch.Apply(LabAbility.Flow,2);w.TickCircuit(3);
            while(!w.InputActive)yield return null;
            var checkpoint=w.checkpoints.First(c=>c.label=="D4 EXIT CONTROL");w.player.Teleport(checkpoint.safePosition);w.Reach(checkpoint);
            yield return Walk(new Vector3(-75.5f,-11.46f,-47.5f),"Isolated D4 approach");
            yield return Walk(new Vector3(-79.5f,-11.9f,-48.1f),"Isolated D4 doorway");
            yield return Walk(new Vector3(-82.3f,-11.9f,-47),"Isolated corridor entry");
            yield return Walk(new Vector3(-82.3f,-17.35f,-13.3f),"Isolated corridor");
            yield return Walk(new Vector3(-83.4f,-17.35f,-13.3f),"Isolated garden bridge");
            yield return Walk(new Vector3(-83.4f,-17.35f,-2),"Isolated garden east walkway");
            yield return Walk(new Vector3(-84,-17.28f,0),"Isolated garden");
            Check(w.GardenReached,"Physical final door, corridor and garden passage after forced initial setup");
            ScreenCapture.CaptureScreenshot(EFV2PuzzleBuild.Reports+"/isolated-garden.png");yield return Frames(4);
        }
        IEnumerator IsolatedPlatforms()
        {
            Note("ISOLATED PLATFORM/FALL TESTS: initial player teleports and bypass setup; each ride, cast and fall uses real keyboard/mouse and scene physics. NOT continuous completion.");
            mouse=InputSystem.AddDevice<Mouse>("V2PlatformMouse");keyboard=InputSystem.AddDevice<Keyboard>("V2PlatformKeyboard");
            var b=w.checkpoints.First(c=>c.label=="B ENTRANCE");w.player.Teleport(b.safePosition);w.Reach(b);
            yield return Frames(6);
            yield return Walk(new Vector3(-24,6.08f,-42.2f),"B board moving stone");
            yield return RideToLanding("B WINDMILL","B MOVING STONE",new Vector3(-25,6.1f,-51.5f));
            Check(w.CurrentCheckpoint!=null&&w.CurrentCheckpoint.label=="B FAR BANK","B windmill/stone alternative reaches actual far bank");
            w.player.Teleport(b.safePosition);w.Reach(b);yield return Frames(6);
            yield return Key(UnityEngine.InputSystem.Key.Digit1);yield return Aim(Target("B MOVING STONE"));yield return Click();yield return Frames(6);
            Check(w.Slots[(int)LabAbility.Bloom].target==null&&Target("B MOVING STONE").Frozen,"Invalid BLOOM aim consumes no orb and preserves fixed stone");
            yield return Walk(new Vector3(-28,6.1f,-40),"B-C connector entry");
            yield return Walk(new Vector3(-60,6.1f,-40),"B-C connector span");
            yield return Walk(new Vector3(-64,6.12f,-40),"B-C connector C arrival");
            Check(w.CurrentCheckpoint!=null&&w.CurrentCheckpoint.label=="C ENTRANCE","Physical intermediate connector permits B to C switching");
            yield return Walk(new Vector3(-60,6.1f,-40),"C-B connector return entry");
            yield return Walk(new Vector3(-28,6.1f,-40),"C-B connector return span");
            var hub=w.checkpoints.First(c=>c.label=="D2 CENTRAL HUB");w.player.Teleport(hub.safePosition);w.Reach(hub);w.bypass.Apply(LabAbility.Bloom);
            yield return Frames(8);
            yield return Walk(new Vector3(-52,-11.55f,-51),"D2 board unpowered dock leaf");
            yield return RideToLanding("D2 THREE WAY BRANCH","D2 DOCK LEAF - AFTER SUPPLY",new Vector3(-43.8f,-11.4f,-54));
            yield return Walk(new Vector3(-43.8f,-7.5f,-59.9f),"D3 restored dock stairs");
            yield return Walk(new Vector3(-40,-7.46f,-57),"D3 restored leaf arrival");
            Check(w.returnWalk.activeSelf,"Restored dock leaf and stairs reach workbench through physical traversal");
            Target("A ROOT STAIRS").Apply(LabAbility.Bloom);
            var high=w.checkpoints.First(c=>c.label=="C HIGH LEDGE");w.player.Teleport(high.safePosition);w.Reach(high);yield return Frames(8);
            bool returned=false;Action<string> observe=message=>{if(message.StartsWith("가까운 안전 지점으로 복귀"))returned=true;};w.Recorded+=observe;
            float start=Time.unscaledTime;
            while(!returned&&Time.unscaledTime-start<10)
            {
                var direction=new Vector3(1,0,1).normalized;var forward=Vector3.ProjectOnPlane(w.player.view.transform.forward,Vector3.up).normalized;
                float yaw=Vector3.SignedAngle(forward,direction,Vector3.up);
                InputSystem.QueueStateEvent(mouse,new MouseState{delta=new Vector2(Mathf.Clamp(yaw,-12,12)/.14f,0)});
                InputSystem.QueueStateEvent(keyboard,Mathf.Abs(yaw)<12?new KeyboardState(UnityEngine.InputSystem.Key.W):new KeyboardState());
                Progress="ACTUAL FALL off C ledge position="+w.player.transform.position.ToString("F2");yield return null;
            }
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());w.Recorded-=observe;yield return Frames(8);
            Check(returned&&Vector3.Distance(w.player.transform.position,high.safePosition)<.6f,"Actual fall returns to nearby C safety point");
            Check(Target("A ROOT STAIRS").Grown&&Target("B MOVING STONE").Frozen,"Fall recovery preserves unrelated growth and fixed B platform");
        }
        void Forced()
        {
            report.AppendLine("FORCED device-state runtime checks. Direct LabTarget.Apply, model pose and gate Tick changes. NOT ordinary-input completion or a traversal playthrough.");
            w.enabled=false;w.player.enabled=false;
            foreach(var m in w.movers)m.enabled=false;
            foreach(var g in w.gates)g.enabled=false;
            w.channelPose.enabled=false;
            w.channel.motion.position=w.connectionPoint.position;w.channelPose.model.rotation=w.channelPose.aligned;
            w.channelStructure.Apply(LabAbility.Freeze);w.TickCircuit(0);
            Check(w.Aligned&&w.Supply,"D5 aligned+structure-fixed route supplies actual composed circuit");
            w.upperIce.Apply(LabAbility.Freeze);w.TickCircuit(0);Check(!w.Supply,"Internal water ice blocks channel supply separately from structure freeze");
            w.bypass.Apply(LabAbility.Bloom);w.TickCircuit(0);Check(w.Supply,"BLOOM bypass supplies despite upper-channel ice");
            w.branch.Apply(LabAbility.Flow,0);w.TickCircuit(0);Check(w.dockLeaf.powered&&!w.Arriving,"Dock branch powers leaf, does not feed W");
            w.branch.Apply(LabAbility.Flow,1);w.TickCircuit(0);w.exitGate.Tick(10);
            Check(w.exitGate.Latched,"Small wheel fully opens and latches D4");
            w.branch.Apply(LabAbility.Flow,2);w.mainIce.Apply(LabAbility.Freeze);w.TickCircuit(0);
            Check(!w.Arriving&&w.exitGate.Latched,"Main selection preserves latch, ice blocks W");
            w.mainIce.Undo(LabAbility.Freeze);w.TickCircuit(2.9f);Check(w.Arriving&&!w.Restored,"Thaw restores water, under 3s not complete");
            w.branch.Apply(LabAbility.Flow,0);w.TickCircuit(.2f);Check(w.StableFor==0&&!w.Restored,"Supply interruption resets timer");
            w.branch.Apply(LabAbility.Flow,2);w.TickCircuit(3);Check(w.Restored,"3s continuous actual circuit result restores W");
            w.ruinGate.root.Apply(LabAbility.Bloom);w.ruinGate.Tick(10);Check(w.ruinGate.Latched,"G BLOOM alternative opens and latches imported door");
            foreach(var gate in w.gates)
            {
                Check(gate.door.GetComponentInChildren<MeshRenderer>()!=null && gate.door.GetComponentsInChildren<Collider>().Any(c=>c.enabled&&!c.isTrigger),gate.name+" visible model and active collider share moving carrier");
                Check(gate.door.position==gate.closed+Vector3.up*gate.lift,gate.name+" model/collider carrier reaches fully open position");
            }
            w.Restart();Check(!w.Restored&&!w.GardenReached&&!w.ruinGate.Latched&&!w.exitGate.Latched&&w.targets.All(t=>!t.Grown&&!t.Frozen&&t.Direction<0),"Restart resets all scene-local puzzles and finale");
            foreach(var m in w.movers)m.enabled=true;foreach(var g in w.gates)g.enabled=true;w.channelPose.enabled=true;w.enabled=true;
        }
        IEnumerator Start()
        {
            Application.runInBackground=true;
            Debug.Log("V2 runtime probe started: "+mode);
            yield return Frames(12);
            w=FindFirstObjectByType<EFV2PuzzleWorld>();
            try
            {
                if(w==null||!w.Built)throw new Exception("Puzzle composition did not initialize.");
                w.AutomatedCheck=true;w.SetPaused(false);Application.runInBackground=true;
                Check(FindObjectsByType<EFMapExploration>(FindObjectsSortMode.None).Length==0,"No exploration respawn/input owner");
                Check(FindObjectsByType<PuzzleLab>(FindObjectsSortMode.None).Length==0,"No lab input/recall owner");
                Check(w.player.follow==null&&w.player.world==null&&w.player.cinemachineRig!=null,"Cinemachine and standalone player own camera/locomotion");
            }
            catch(Exception e){report.AppendLine("EXCEPTION "+e);failures++;Finish();yield break;}
            // Exercise the real opening; route input starts only after its normal handoff.
            while(w.OpeningActive)yield return null;
            yield return Frames(3);
            if(mode=="forced-state")
            {
                try{Forced();}catch(Exception e){report.AppendLine("EXCEPTION "+e);failures++;}
            }
            else
            {
                var stack=new System.Collections.Generic.Stack<IEnumerator>();stack.Push(mode=="route-bloom"?ContinuousRoute():mode=="route-alternate"?AlternateRoute():mode=="exit-isolated"?IsolatedExit():mode=="platforms-isolated"?IsolatedPlatforms():Smoke());
                while(stack.Count>0)
                {
                    object current=null;bool more=false;
                    try{more=stack.Peek().MoveNext();if(more)current=stack.Peek().Current;}catch(Exception e){report.AppendLine("EXCEPTION "+e);failures++;break;}
                    if(!more){stack.Pop();continue;}
                    if(current is IEnumerator child){stack.Push(child);continue;}
                    yield return current;
                }
            }
            if(failures>0)
            {
                ScreenCapture.CaptureScreenshot(EFV2PuzzleBuild.Reports+"/runtime-failure.png");yield return Frames(4);
            }
            Finish();
        }
        void Finish()
        {
            if(w!=null)w.AutomatedCheck=false;
            foreach(var error in runtimeErrors)report.AppendLine("UNITY ERROR "+error);
            failures+=runtimeErrors.Count;
            report.AppendLine($"TOTAL {checks} checks, {failures} failures. Full S-to-garden traversal remains a separate manual test.");
            Directory.CreateDirectory(EFV2PuzzleBuild.Reports+"/runs");
            File.WriteAllText(EFV2PuzzleBuild.Reports+"/runs/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ")+"-"+mode+".txt",report.ToString());
            EFV2PuzzlePlayChecks.Finish(mode,report.ToString(),failures==0);
            if(mouse!=null&&mouse.added)InputSystem.RemoveDevice(mouse);
            if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);
            Destroy(gameObject);
        }
    }
}
