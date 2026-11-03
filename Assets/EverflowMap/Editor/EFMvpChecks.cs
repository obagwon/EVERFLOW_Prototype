using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Ability=EverflowGraybox.Ability;
using TestInput=EverflowGraybox.EFInput;

namespace EverflowMap.Editor
{
    public static class EFMvpChecks
    {
        public const string OutputKey="EVERFLOW.MVP.Checks.Output";
        const string Pending="EVERFLOW.MVP.Checks.Pending";
        const string BackgroundSaved="EVERFLOW.MVP.Checks.BackgroundSaved";
        const string BackgroundValue="EVERFLOW.MVP.Checks.BackgroundValue";
        internal const string ModeKey="EVERFLOW.MVP.Checks.Mode";
        internal const string SourceKey="EVERFLOW.MVP.Checks.Source";
        internal static void CancelPending() { SessionState.SetBool(Pending,false); }
        [MenuItem("Tools/EVERFLOW/MVP Integration/Run MVP Checks")]
        public static void Run() { Begin(false); }
        [MenuItem("Tools/EVERFLOW/MVP Integration/Check Title Route")]
        public static void Title() { Begin(true); }
        static void Begin(bool title)
        {
            EFMvpSetup.Guard();
            Directory.CreateDirectory(EFMvpSetup.Evidence);
            SessionState.SetString(OutputKey,EFMvpSetup.Evidence+"/mvp-checks-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ")+".txt");
            SessionState.SetString(ModeKey,title?"title":"full");
            SessionState.SetString(SourceKey,EFMvpSetup.RuntimeFingerprint());
            var scene=EditorSceneManager.OpenScene(title?EverflowMVP.Editor.EFReleaseSetup.TitleScene:EFMvpSetup.ScenePath,OpenSceneMode.Single);
            if(!title) EFMvpSetup.Validate(scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<EFMapWorld>(true)).Single());
            SessionState.SetBool(Pending,true);
            SessionState.SetBool(BackgroundValue,PlayerSettings.runInBackground);
            SessionState.SetBool(BackgroundSaved,true);
            Application.runInBackground=true; EditorApplication.isPaused=false;
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod]
        static void Register()
        {
            if(AssetDatabase.IsAssetImportWorkerProcess()) return;
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state==PlayModeStateChange.EnteredEditMode)
                {
                    SessionState.SetBool(Pending,false);
                    if(SessionState.GetBool(BackgroundSaved,false))
                    {
                        Application.runInBackground=SessionState.GetBool(BackgroundValue,false);
                        SessionState.SetBool(BackgroundSaved,false);
                    }
                    return;
                }
                if(state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending,false)) return;
                SessionState.SetBool(Pending,false); EditorApplication.isPaused=false; Application.runInBackground=true;
                EditorApplication.ExecuteMenuItem("Window/General/Game");
                new GameObject("MVP verification probe").AddComponent<EFMvpProbe>();
            };
        }
    }

    public sealed class EFMvpProbe : MonoBehaviour
    {
        EFMapWorld w; EFMapPlayer p; EFMvpFinale f; EFCanalExperiment canal;
        CharacterController cc;
        readonly StringBuilder log=new StringBuilder();
        readonly List<GameObject> fixtures=new List<GameObject>();
        int passed;
        string phase="entry";
        Vector3 Origin=>canal.originalTerrain.position;
        void Check(bool value,string label)
        {
            log.AppendLine((value?"PASS ":"FAIL ")+label);
            Debug.Log("MVP CHECK "+(value?"PASS ":"FAIL ")+label);
            if(!value) throw new InvalidOperationException(label+"; phase="+phase+" player="+(p==null?"none":(p.transform.position-Origin).ToString("F4")));
            passed++;
        }
        void Bind(bool endOpening=true)
        {
            w=FindFirstObjectByType<EFMapWorld>(); p=w.player; f=w.mvpFinale;
            canal=w.GetComponentInChildren<EFCanalExperiment>(); cc=p.GetComponent<CharacterController>();
            w.SetPaused(false); if(endOpening && w.CinematicActive) w.EndCinematic();
        }
        IEnumerator Frames(int count) { for(int i=0;i<count;i++) yield return null; }
        IEnumerator Capture(string label)
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null) yield break;
            string output=SessionState.GetString(EFMvpChecks.OutputKey,EFMvpSetup.Evidence+"/capture.txt");
            string path=Path.ChangeExtension(output,null)+"-"+label+".png";
            ScreenCapture.CaptureScreenshot(path); yield return Frames(3);
            log.AppendLine("CAPTURE "+path+" (Game rendering; visual review is separate from state assertions)");
        }
        IEnumerator Walk(Vector3 local,string label,int cap=700)
        {
            phase=label;
            Vector3 goal=Origin+local; int frame=0;
            for(;frame<cap;frame++)
            {
                Vector3 d=Vector3.ProjectOnPlane(goal-p.transform.position,Vector3.up);
                if(d.magnitude<.22f) break;
                TestInput.TestMovement=new Vector2(Vector3.Dot(d.normalized,p.follow.PlanarRight),Vector3.Dot(d.normalized,p.follow.PlanarForward));
                yield return null;
            }
            TestInput.TestMovement=Vector2.zero;
            yield return Frames(8);
            float remaining=Vector3.ProjectOnPlane(goal-p.transform.position,Vector3.up).magnitude;
            log.AppendLine("MOVE "+label+" frames="+frame+" local="+(p.transform.position-Origin).ToString("F4")+" remaining="+remaining+" grounded="+cc.isGrounded);
            Check(remaining<.3f && cc.isGrounded,"[PHYSICAL] "+label+" reached through actual controller movement");
        }
        IEnumerator Click(EFMapNode node,Vector3 point,Ability ability,bool accepted=true)
        {
            phase="ray "+node.label;
            TestInput.TestMovement=Vector2.zero;
            for(int i=0;i<40;i++)
            {
                if(node.kind==MapKind.Debris) point=node.motion.position+Vector3.up*.2f;
                var angles=Quaternion.LookRotation(point-p.view.transform.position).eulerAngles;
                p.follow.yaw=angles.y; p.follow.pitch=Mathf.Clamp(Mathf.DeltaAngle(0,angles.x),p.follow.minPitch,p.follow.maxPitch);
                yield return null;
            }
            if(ability==Ability.Freeze && !node.Frozen && (node.kind==MapKind.Bridge || node.kind==MapKind.Slate || node.kind==MapKind.Debris))
            {
                // Aim first, then use the middle of the authored generous
                // window. Accepting an outgoing boundary before two input
                // frames was a timing assumption in the probe itself.
                int wait=0;
                while((!node.Aligned || node.kind!=MapKind.Debris &&
                    Mathf.Min(Mathf.Abs(Mathf.DeltaAngle(node.Angle,0)),node.bidirectionalAlignment?Mathf.Abs(Mathf.DeltaAngle(node.Angle,180)):360)>node.tolerance*.35f) && wait++<1200) yield return null;
                Check(node.Aligned,"Broad alignment window available before FREEZE input");
            }
            TestInput.TestPress=ability==Ability.Bloom?KeyCode.Alpha1:ability==Ability.Freeze?KeyCode.Alpha2:KeyCode.Alpha3;
            yield return null; TestInput.TestPress=KeyCode.None; yield return null;
            log.AppendLine("RAY target="+node.name+" hovered="+(p.Hovered==null?"none":p.Hovered.name)+" collider="+(p.AimCollider==null?"none":p.AimCollider.name)+" reason="+p.AimReason);
            Check(p.Hovered==node,"[RAY] Camera center identifies "+node.label);
            int before=p.LastCastFrame; EFMapInput.TestClick=true; yield return null; EFMapInput.TestClick=false; yield return null;
            Check(p.LastCastFrame>before && p.LastCastResult.StartsWith(accepted?"Applied:":"Blocked:"),"[RAY] "+ability+" "+(accepted?"applies":"rejects")+" on "+node.label+": "+p.LastCastResult);
        }
        IEnumerator Ground(Vector3 point)
        {
            p.Teleport(point+Vector3.up*.35f); Physics.SyncTransforms();
            for(int i=0;i<20;i++) { cc.Move(Vector3.down*.1f); if(cc.isGrounded) break; yield return null; }
            Check(cc.isGrounded,"[POSITION FIXTURE] Grounded fixture placement (no route claim)");
        }
        IEnumerator AdvanceToFinal(int stop=5)
        {
            phase="prior stage assistance API fixtures";
            w.enabled=p.enabled=canal.enabled=false;
            for(int stage=w.StageIndex;stage<stop;stage++)
            {
                var at=w.stages[stage].arrival.position;
                if(stage!=3)
                {
                    var support=new GameObject("MVP prior-stage fixture"); fixtures.Add(support);
                    support.transform.position=at-Vector3.up*1.3f;
                    support.AddComponent<BoxCollider>().size=new Vector3(2,.5f,2);
                }
                yield return Ground(at);
                Check(w.TryPassage(stage) && w.Completed[stage] && w.StageIndex==stage+1,"[API FIXTURE] Prior-stage assistance "+stage+" preserves completion and unlocks next stage");
            }
            foreach(var go in fixtures) Destroy(go); fixtures.Clear();
            w.Respawn(); w.enabled=p.enabled=canal.enabled=true;
            yield return Frames(12);
        }
        Vector3 Crystal(EFMapNode node)=>node.indicators.First().bounds.center;
        EFMapNode Named(string name)=>w.stages.SelectMany(s=>s.nodes).Single(n=>n.name==name);
        IEnumerator Press(KeyCode key,int frames=1)
        { TestInput.TestPress=key; yield return null; TestInput.TestPress=KeyCode.None; yield return Frames(frames-1); }
        IEnumerator Jump(Vector3 local,int frames)
        {
            var d=Vector3.ProjectOnPlane(Origin+local-p.transform.position,Vector3.up).normalized;
            TestInput.TestMovement=new Vector2(Vector3.Dot(d,p.follow.PlanarRight),Vector3.Dot(d,p.follow.PlanarForward));
            yield return Press(KeyCode.Space,frames); TestInput.TestMovement=Vector2.zero; yield return Frames(12);
        }
        IEnumerator Freeze(EFMapNode n)
        {
            int wait=0; while(!n.Aligned && wait++<1200) yield return null;
            Check(n.Aligned,"Alignment available for "+n.name);
            yield return Click(n,n.motion.position+Vector3.up*.2f,Ability.Freeze);
            Check(n.Satisfied,"Actual FREEZE stabilizes "+n.name);
        }
        // Adapt the established EFMapPlayTests opening route; keep its actual
        // jump, flower launch, vine ride and three debris traversal, rather than
        // using the grounded assistance fixtures used by skip comparisons.
        IEnumerator Opening()
        {
            var flower=Named("SM_FlowerJumpPad"); var vine=Named("SM_Vine");
            yield return Click(flower,flower.motion.position+Vector3.up*.6f,Ability.Bloom);
            yield return Frames(135);
            var dome=flower.support[0];
            yield return Jump(dome.bounds.center-Origin,18); yield return Frames(30);
            Check((p.transform.position-Origin).y>1.4f,"Ordinary jump reaches bloomed flower");
            yield return Press(KeyCode.Space,95);
            Check(p.FlowerUsed && Vector3.Distance(p.transform.position,flower.launchTarget.position)<1.8f,"Actual flower launch reaches upper terrain");
            yield return Click(vine,vine.handle.position,Ability.Bloom); yield return Frames(110);
            yield return Walk(new Vector3(26,4.1f,23),"Flower ledge to vine handle");
            for(int i=0;i<400 && !p.Riding;i++)
            {
                TestInput.TestPress=Vector3.Distance(p.transform.position,vine.HandlePosition)<3.4f?KeyCode.E:cc.isGrounded?KeyCode.Space:KeyCode.None;
                yield return null; TestInput.TestPress=KeyCode.None;
            }
            Check(p.Riding,"Vine boarded through actual E input");
            int wait=0; while(vine.Angle<18 && wait++<300) yield return null;
            yield return Press(KeyCode.Space,110);
            Check(p.VineUsed && !p.Riding,"Actual vine release completes crossing");
            yield return Walk(new Vector3(44,4.1f,21),"UI P1 far-side arrival");
            Check(w.Completed[0] && w.StageIndex==1,"Physical P1 advances and unlocks FREEZE stage");
            yield return Walk(new Vector3(44,4.1f,16),"UI P2 debris entrance");
            for(int i=0;i<3;i++)
            {
                yield return Freeze(w.debris[i]);
                yield return Jump(new Vector3(44,5.3f+1.2f*i,11-5*i),28);
                Check(p.SteppedDebris[i],"Physical support on frozen debris "+i);
                yield return Walk(new Vector3(44,5.3f+1.2f*i,9.8f-5*i),"Debris takeoff "+i);
            }
            yield return Jump(new Vector3(44,8.6f,-5),33);
            yield return Walk(new Vector3(44,8.6f,-25),"Slate approach");
            yield return Freeze(Named("SM_Slate"));
            yield return Walk(new Vector3(44,8.6f,-27),"Slate entry");
            yield return Walk(new Vector3(44,8.6f,-40),"UI P2 far-side arrival");
            Check(w.Completed[1] && w.StageIndex==2,"Physical P2 advances and unlocks rotary PULSE");
        }
        IEnumerator CanalAndDoor()
        {
            phase="Integrated canal approach and downstream connection";
            w.enabled=p.enabled=canal.enabled=false;
            yield return EFCanalApproachChecks.Run(w,canal,Check,line=>log.AppendLine(line),true);
            w.enabled=p.enabled=canal.enabled=true;
            yield return Capture("canal");
            yield return Press(KeyCode.E);
            Check(p.Riding,"Integrated canal boards leaf with E from walked departure deck");
            int wait=0; while(p.Riding && wait++<700) yield return null;
            Check(p.SlideUsed && !p.Riding,"Integrated leaf capsule traverses to raised exit");
            yield return Walk(new Vector3(-28.5f,27.15f,8),"Step off parked leaf onto far deck");
            yield return Walk(new Vector3(-26,27.15f,11),"Canal arrival line");
            Check(w.Completed[3] && w.StageIndex==4,"Physical canal exit advances UI P4 to UI P5");
            yield return Walk(new Vector3(-27.5f,27.1f,13),"Upper descent entrance");
            yield return Walk(new Vector3(-27.5f,19.1f,25),"Upper descent to turn landing");
            yield return Walk(new Vector3(-22.5f,19.1f,25),"Turn landing to lower descent");
            yield return Walk(new Vector3(-22.5f,11.2f,13),"Lower descent to original plateau");
            yield return Walk(new Vector3(-22,11.1f,8),"UI P5 root and door approach");
            var root=Named("SM_VineRoot_P5"); var door=Named("SM_Door_P5");
            yield return Click(root,root.motion.position+Vector3.up*.2f,Ability.Pulse,false);
            yield return Click(root,root.motion.position+Vector3.up*.2f,Ability.Bloom);
            yield return Frames(135);
            yield return Click(root,root.motion.position+Vector3.up*.2f,Ability.Pulse); yield return Frames(90);
            Check(root.Done && root.SourceOn && door.Done,"Existing root BLOOM then PULSE opens UI P5 door");
            yield return Walk(new Vector3(-20.5f,11,5),"Door entrance");
            yield return Walk(new Vector3(-14,13.6f,5),"UI P5 door crossing");
            yield return Walk(new Vector3(-6,16.2f,5),"Existing stairs to final plateau");
            Check(w.Completed.Take(5).All(v=>v) && w.StageIndex==5,"Physical opening, rotary, canal and door connect to final stage");
        }
        IEnumerator Rotary()
        {
            phase="rotary A and B";
            var bridge=w.stages[2].nodes.Single(n=>n.kind==MapKind.Bridge);
            var source=w.stages[2].nodes.Single(n=>n.kind==MapKind.Source);
            var marker=FindObjectsByType<EFMapAbilityTarget>(FindObjectsSortMode.None).Single(t=>t.name=="Rotary B FREEZE marker");
            Check(!bridge.CanApply(Ability.Freeze,out _),"Unpowered B rejects FREEZE");
            yield return Click(source,Crystal(source),Ability.Bloom,false);
            yield return Click(source,Crystal(source),Ability.Pulse);
            yield return Walk(new Vector3(42,8.77f,-42),"Fixed A entrance");
            yield return Walk(new Vector3(29.8f,8.77f,-42),"Fixed A to middle judgement deck");
            yield return Frames(12);
            var a=GameObject.Find("P3 UI - fixed connection A").GetComponent<BoxCollider>();
            yield return Capture("rotary");
            yield return Click(bridge,marker.GetComponent<BoxCollider>().bounds.center,Ability.Freeze);
            Check(bridge.Satisfied && a.enabled,"B freezes with broad alignment and A remains available");
            yield return Click(bridge,marker.GetComponent<BoxCollider>().bounds.center,Ability.Freeze);
            Check(!bridge.Frozen && a.enabled,"Releasing B does not remove A");
            yield return Click(bridge,marker.GetComponent<BoxCollider>().bounds.center,Ability.Freeze);
            Check(bridge.Satisfied && a.enabled,"B reconnects without losing A");
            w.Respawn(); yield return Frames(10);
            Check(Vector3.Distance(w.Checkpoint,Origin+new Vector3(29.8f,8.77f,-42))<.01f && bridge.Satisfied,"Rotary fall uses safe middle checkpoint and retains connection");
            yield return Walk(new Vector3(18,8.7f,-42),"B crossing to western ridge");
            Check(w.Completed[2] && w.StageIndex==3 && bridge.Frozen,"Actual B crossing advances and stabilizes completion");
            bridge.Apply(Ability.Freeze); w.Respawn();
            Check(w.Completed[2] && bridge.Frozen && a.enabled,"Rotary reentry/repeated input/fall cannot regress completed A or B");
        }
        IEnumerator PrepareAndCross(bool rayInputs)
        {
            Check(!w.WheelRunning && !w.Ended && !w.Completed[5],"P6 entry does not start ending");
            Check(!f.finalControl.CanApply(Ability.Pulse,out _) && !f.machine.CanApply(Ability.Pulse,out _),"Final PULSE and maintenance PULSE reject missing preparation");
            if(rayInputs)
            {
                var root=FindObjectsByType<EFMapAbilityTarget>(FindObjectsSortMode.None).Single(t=>t.name=="BLOOM approach roots - reused model");
                yield return Click(f.growth,root.GetComponent<BoxCollider>().bounds.center,Ability.Bloom);
                yield return Frames(130);
                Check(f.growth.Done && f.bloomRoute.enabled,"BLOOM animation enables the approach route");
                yield return Walk(new Vector3(-6,16.2f,2.4f),"P6 staircase entrance");
                yield return Walk(new Vector3(-6,20.35f,-5.1f),"P6 approach staircase");
                yield return Walk(new Vector3(-6,20.35f,-11.8f),"BLOOM route to maintenance");
                yield return Walk(new Vector3(-3,20.35f,-13.5f),"Maintenance safe deck");
                yield return Click(f.machine,Crystal(f.machine),Ability.Pulse);
                Check(!f.gate.Started && !w.WheelRunning,"Maintenance power alone keeps final sluice and ending idle");
                int wait=0; while(!f.connection.Aligned && wait++<180) yield return null;
                yield return Click(f.connection,f.connection.motion.GetComponent<Renderer>().bounds.center,Ability.Freeze);
                if(!f.connection.Satisfied)
                {
                    yield return Click(f.connection,f.connection.motion.GetComponent<Renderer>().bounds.center,Ability.Freeze);
                    while(!f.connection.Aligned) yield return null;
                    yield return Click(f.connection,f.connection.motion.GetComponent<Renderer>().bounds.center,Ability.Freeze);
                }
            }
            else
            {
                log.AppendLine("SETUP [API/POSITION FIXTURE] Skip/restart case preparation and bridge placement; normal case used real approach and ray input.");
                f.growth.Apply(Ability.Bloom); foreach(var a in f.growth.actions) a.Tick(a.Duration); f.growth.Tick(0);
                f.machine.Apply(Ability.Pulse);
                while(!f.connection.Aligned) yield return null;
                f.connection.Apply(Ability.Freeze);
                yield return Frames(2);
                yield return Ground(f.nearCheckpoint.position);
            }
            yield return Frames(12);
            Check(f.Prepared && f.frozenRoute.enabled && !f.gate.Started && !w.WheelRunning,"Prepared route is safe but never auto-starts final restoration");
            if(!rayInputs)
            {
                yield return Ground(f.farCheckpoint.position);
                Check(!f.Crossed && !f.finalControl.CanApply(Ability.Pulse,out _) && !w.Ended,"[BYPASS FIXTURE] Prepared final-deck arrival alone cannot authorize final PULSE");
                yield return Ground(f.frozenRoute.bounds.center+Vector3.forward*3+Vector3.up*1.3f);
                yield return Ground(f.farCheckpoint.position);
                Check(!f.BridgeVisited && !f.Crossed && !f.finalControl.CanApply(Ability.Pulse,out _),"[BYPASS FIXTURE] Touching only the far edge and returning to final deck cannot count as full crossing");
                yield return Ground(f.frozenRoute.bounds.center+Vector3.back*3+Vector3.up*1.3f);
            }
            else yield return Walk(new Vector3(0,20.35f,-12),"Frozen connection entry");
            yield return Frames(8);
            yield return Walk(new Vector3(0,20.35f,-7),"Frozen channel physical crossing");
            yield return Walk(new Vector3(0,20.35f,-2.5f),"Final far deck physical arrival");
            yield return Frames(12);
            Check(f.Crossed && f.BridgeVisited && !w.WheelRunning && !w.Ended,"Direct grounded crossing records evidence; mere final arrival still does not start ending");
            Check(!w.TryPassage(5) && !w.TryArrive(5),"Final traversal cannot be completed by passage assistance");
            Check(!f.finalControl.CanApply(Ability.Bloom,out _) && !f.finalControl.CanApply(Ability.Freeze,out _),"Final control rejects both wrong abilities");
            w.Respawn(); yield return Frames(8);
            Check(f.Crossed && f.Prepared && w.Completed.Take(5).All(v=>v),"Fall recovery preserves final preparation, crossing and earlier completion");
        }
        string Signature()
        {
            return string.Join("|",w.Completed.Select(v=>v.ToString()))+"|"+w.Ended+"|"+w.WheelRunning+"|"+f.State+"|"+f.gate.Done+
                "|"+string.Join(",",w.finalFlow.Select(g=>g.activeSelf))+"|"+f.restoredLight.intensity+
                "|"+w.endingCamera.enabled+"|"+p.enabled+"|"+p.follow.enabled+"|"+p.view.enabled+
                "|"+string.Join(",",w.stages[5].restoration.Select(a=>a.Complete));
        }
        IEnumerator Fresh()
        {
            w.RestartWorld(); yield return Frames(12); Bind();
            Check(w.gameObject.scene.path==EFMvpSetup.ScenePath && w.StageIndex==0 && w.Completed.All(v=>!v) && !w.WheelRunning && !w.Ended &&
                !f.Crossed && !f.Prepared && f.State==EFMvpFinale.RestorationState.Preparing && !f.finalControl.SourceOn && !f.gate.Started &&
                !canal.water.Frozen && !canal.DepartureSaved && !f.bloomRoute.enabled && !f.frozenRoute.enabled && !f.reactionCamera.enabled,
                "[RELOAD] New game resets progression, devices, water/ice, checkpoint flags and cinematic state");
            Check(FindObjectsByType<EFMapPlayer>(FindObjectsSortMode.None).Length==1 && Time.timeScale==1 && p.view.enabled && p.follow.enabled,"[RELOAD] One player, gameplay camera and normal time scale");
        }
        IEnumerator TitleRoute()
        {
            phase="actual title button route";
            Time.captureDeltaTime=1f/60;
            yield return Frames(8);
            var title=FindFirstObjectByType<EverflowMVP.EFTitleScreen>();
            Check(title!=null && title.startButton!=null && title.startButton.interactable,"Actual existing title and start button present");
            yield return Capture("title");
            title.startButton.onClick.Invoke();
            int wait=0; while(FindFirstObjectByType<EFMapWorld>()==null && wait++<600) yield return null;
            yield return Frames(2); Bind(false);
            Check(w.gameObject.scene.path==EFMvpSetup.ScenePath && w.StageIndex==0 && w.Completed.All(v=>!v),"Actual title button event loads fresh integrated MVP");
            Check(w.CinematicActive && !p.enabled && w.openingCamera.enabled,"Preserved opening overview locks movement during introduction");
            yield return Frames(185);
            Check(w.GameInputActive && p.enabled && p.view.enabled && p.follow.enabled,"Opening overview restores movement and follow camera automatically");
            yield return Capture("start");
        }
        IEnumerator Tests()
        {
            if(SessionState.GetString(EFMvpChecks.ModeKey,"")=="title") { yield return TitleRoute(); yield break; }
            yield return Frames(3); Bind(); TestInput.TestMode=true; Time.captureDeltaTime=1f/60;
            w.ReturnToTitle(); yield return Frames(8); yield return TitleRoute();
            Check(w.gameObject.scene.path==EFMvpSetup.ScenePath && f!=null,"Saved integrated scene has opt-in finale");
            yield return Opening();
            yield return Rotary();
            yield return CanalAndDoor();
            yield return PrepareAndCross(true);
            yield return Click(f.finalControl,Crystal(f.finalControl),Ability.Bloom,false);
            yield return Click(f.finalControl,Crystal(f.finalControl),Ability.Freeze,false);
            yield return Capture("final-control");
            yield return Click(f.finalControl,Crystal(f.finalControl),Ability.Pulse);
            Check(f.State==EFMvpFinale.RestorationState.Restoring && !p.enabled,"Last actual PULSE starts restoration and controls are locked");
            Check(!f.finalControl.CanApply(Ability.Pulse,out _),"Repeated final PULSE cannot restart the sequence");
            p.Teleport(f.farCheckpoint.position+Vector3.down*10); yield return Frames(2);
            Check(Vector3.Distance(p.transform.position,f.farCheckpoint.position)<.1f && f.State==EFMvpFinale.RestorationState.Restoring,"Exceptional fall during cinematic recovers to safe final deck without restarting sequence");
            w.Retry(); Check(f.State==EFMvpFinale.RestorationState.Restoring,"Retry cannot corrupt an accepted restoration");
            int frames=0; while(!w.Ended && frames++<900) yield return null;
            Check(w.Ended && w.WheelRunning && w.Completed[5] && f.gate.Done,"Normal restoration opens sluice, connects water, spins wheel and reaches ending");
            string final=Signature(); log.AppendLine("NORMAL FINAL STATE "+final);
            yield return Capture("ending");
            for(int skip=0;skip<2;skip++)
            {
                yield return Fresh(); yield return AdvanceToFinal(); yield return PrepareAndCross(false);
                p.selected=Ability.Pulse;
                Check(p.CastAt(f.finalControl,Crystal(f.finalControl)),"[API INPUT] Last PULSE accepted for skip comparison: "+p.LastCastResult);
                if(skip==1) { int wait=0; while(!w.WheelRunning && wait++<600) yield return null; }
                else yield return Frames(2);
                if(skip==0) yield return Press(KeyCode.Space,2);
                else f.CompleteRestoration(); // Same method as the on-screen Skip button.
                Check(Signature()==final,(skip==0?"Early":"Late")+" skip converges on normal final state (continuous wheel angle and elapsed playtime excluded)");
                if(skip==1)
                {
                    w.ResumeExploration(); yield return Frames(60);
                    Check(w.GameInputActive && p.enabled && p.view.enabled && p.follow.enabled && !w.endingCamera.enabled && w.Completed[5] && w.WheelRunning,
                        "Continue exploring restores controls/camera, retains completion and does not replay ending");
                }
            }
            yield return Fresh(); yield return AdvanceToFinal(); yield return PrepareAndCross(false);
            p.selected=Ability.Pulse;
            Check(p.CastAt(f.finalControl,Crystal(f.finalControl)),"[API INPUT] Start restoration before restart-interruption case: "+p.LastCastResult);
            yield return Frames(12); yield return Fresh(); yield return Fresh();
        }
        IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            log.AppendLine("MVP checks UTC "+DateTime.UtcNow.ToString("O"));
            log.AppendLine("Normal route: actual title button, all six UI stages through CharacterController movement, ray/key/click abilities, vine/leaf rides. Includes deliberate retry/fall recovery checks. Skip/restart comparison cases use explicit grounded API/position fixtures. Programmatic camera/input, no human playthrough or duration claim.");
            var stack=new Stack<IEnumerator>(); stack.Push(Tests()); bool failed=false;
            while(stack.Count>0)
            {
                bool more=false; object next=null;
                try { more=stack.Peek().MoveNext(); if(more) next=stack.Peek().Current; }
                catch(Exception error) { failed=true; log.AppendLine("FAIL "+error); Debug.LogException(error); break; }
                if(!more) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                if(next is IEnumerator nested) stack.Push(nested); else yield return next;
            }
            while(stack.Count>0) (stack.Pop() as IDisposable)?.Dispose();
            foreach(var go in fixtures) if(go!=null) Destroy(go);
            TestInput.TestMode=false; TestInput.TestMovement=Vector2.zero; TestInput.TestPress=KeyCode.None; EFMapInput.TestClick=false; Time.captureDeltaTime=0;
            if(SessionState.GetString(EFMvpChecks.SourceKey,"")!=EFMvpSetup.RuntimeFingerprint())
            { failed=true; log.AppendLine("FAIL Runtime sources changed during Play; rerun after import/compilation."); }
            log.AppendLine("Last phase: "+phase);
            log.AppendLine("Runtime checks passed: "+passed+"; overall "+(failed?"FAIL":"PASS")+".");
            log.AppendLine("NOT VERIFIED BY THIS EDITOR REPORT: human understanding/fun/duration; standalone build execution. See separate player smoke evidence when available.");
            string output=SessionState.GetString(EFMvpChecks.OutputKey,EFMvpSetup.Evidence+"/fallback.txt");
            File.WriteAllText(output,log.ToString());
            if(SessionState.GetString(EFMvpChecks.ModeKey,"")=="full")
                File.WriteAllText(EFMvpSetup.Evidence+"/verified.json",JsonUtility.ToJson(new EFMvpSetup.MvpVerificationStamp {pass=!failed,sceneHash=EFMvpSetup.Hash(EFMvpSetup.ScenePath),sourceHash=EFMvpSetup.RuntimeFingerprint()},true));
            Debug.Log("MVP CHECKS "+(failed?"FAIL":"PASS")+" "+output);
            EditorApplication.ExitPlaymode();
        }
    }
}
