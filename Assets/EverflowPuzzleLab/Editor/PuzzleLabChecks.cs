using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using InputProbe = EverflowGraybox.EFInput;

namespace EverflowPuzzleLab.Editor
{
    public static class PuzzleLabChecks
    {
        const string Pending = "EVERFLOW.V02.LabChecks";
        const string Running = "EVERFLOW.V02.LabChecksRunning";
        const string Deadline = "EVERFLOW.V02.LabChecksDeadline";
        [MenuItem("Tools/EVERFLOW/v0.2 Puzzle Lab/Run T01 Checks")]
        public static void RunT01() { Run(PuzzleLabBuilder.T01Path); }
        [MenuItem("Tools/EVERFLOW/v0.2 Puzzle Lab/Run Saved Lab Checks")]
        public static void RunAll() { Run(PuzzleLabBuilder.ScenePath); }
        [MenuItem("Tools/EVERFLOW/v0.2 Puzzle Lab/Run Open Lab Checks")]
        public static void RunOpen()
        {
            var lab=UnityEngine.Object.FindFirstObjectByType<PuzzleLab>();
            if(lab==null) throw new InvalidOperationException("Open a generated lab scene first.");
            Run(lab.gameObject.scene.path);
        }
        static void Run(string path)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first.");
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save scene edits first.");
            EditorSceneManager.OpenScene(path);
            var lab=UnityEngine.Object.FindFirstObjectByType<PuzzleLab>();
            if(lab==null) throw new InvalidOperationException("The selected scene has no PuzzleLab.");
            PuzzleLabValidation.Validate(lab,lab.areas.Length==1);
            SessionState.SetFloat(Deadline,(float)EditorApplication.timeSinceStartup+150);
            SessionState.SetBool(Running,true);
            SessionState.SetBool(Pending,true); EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod]
        static void Register()
        {
            EditorApplication.update += () =>
            {
                if(!SessionState.GetBool(Running,false)) return;
                if(EditorApplication.timeSinceStartup<=SessionState.GetFloat(Deadline,0)) return;
                SessionState.SetBool(Running,false); SessionState.SetBool(Pending,false);
                System.IO.Directory.CreateDirectory("docs/puzzle-lab");
                System.IO.File.WriteAllText("docs/puzzle-lab/play-check-timeout.txt","INCOMPLETE: Play checks exceeded 150 seconds. No passing result is implied.\n");
                if(Application.isBatchMode) EditorApplication.Exit(1); else EditorApplication.ExitPlaymode();
            };
            EditorApplication.playModeStateChanged += state =>
            {
                if(state==PlayModeStateChange.EnteredEditMode)
                {
                    InputProbe.TestMode=false; InputProbe.TestMovement=Vector2.zero; InputProbe.TestPress=KeyCode.None;
                    if(SessionState.GetBool(Running,false))
                    {
                        SessionState.SetBool(Running,false); SessionState.SetBool(Pending,false);
                        Directory.CreateDirectory("docs/puzzle-lab");
                        File.WriteAllText("docs/puzzle-lab/play-check-interrupted.txt","INCOMPLETE: Play stopped before checks finished.\n");
                    }
                    return;
                }
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending,false)) return;
                SessionState.SetBool(Pending,false); new GameObject("V02 test probe").AddComponent<PuzzleLabProbe>();
            };
        }
        internal static void Finished() { SessionState.SetBool(Running,false); }
    }
    public sealed class PuzzleLabProbe : MonoBehaviour
    {
        PuzzleLab lab;
        string report = "Unity Editor Play checks. API state checks and injected physical/center-camera input checks are labeled separately. Teleports are fixture setup, never traversal proof. No human playthrough.\n";
        int failures, checks;
        void Check(bool ok,string message) { checks++; if(!ok) failures++; report+=(ok?"PASS ":"FAIL ")+message+"\n"; Debug.Log("LABCHECK "+(ok?"PASS ":"FAIL ")+message); }
        LabTarget T(string label) => lab.GetComponentsInChildren<LabTarget>(true).Single(t=>t.label==label);
        IEnumerator Start()
        {
            Application.runInBackground=true;
            lab=FindFirstObjectByType<PuzzleLab>(); report+="Scene: "+lab.gameObject.scene.path+"\n"; InputProbe.TestMode=true;
            var stack=new Stack<IEnumerator>(); stack.Push(Exercise());
            while(stack.Count>0)
            {
                object next=null; bool more=false;
                try { more=stack.Peek().MoveNext(); if(more) next=stack.Peek().Current; }
                catch(Exception error) { Check(false,"EXCEPTION "+error); break; }
                if(!more) { stack.Pop(); continue; }
                if(next is IEnumerator iterator) stack.Push(iterator); else yield return next;
            }
            InputProbe.TestMode=false; InputProbe.TestMovement=Vector2.zero; InputProbe.TestPress=KeyCode.None;
            PuzzleLabChecks.Finished();
            report+=$"TOTAL {checks} checks; {failures} failures\n";
            Directory.CreateDirectory("docs/puzzle-lab");
            File.WriteAllText(lab.areas.Length==1?"docs/puzzle-lab/t01-play-checks.txt":"docs/puzzle-lab/full-play-checks.txt",report);
            Debug.Log(report);
            if(Application.isBatchMode) EditorApplication.Exit(failures==0?0:1); else EditorApplication.ExitPlaymode();
        }
        IEnumerator Wait(float seconds) { float end=Time.time+seconds; while(Time.time<end) yield return null; }
        IEnumerator Walk(Vector3 target,float timeout=6)
        {
            float until=Time.time+timeout;
            while(Time.time<until)
            {
                var delta=Vector3.ProjectOnPlane(target-lab.player.transform.position,Vector3.up); if(delta.magnitude<.18f) break;
                var f=lab.player.follow.PlanarForward; var r=lab.player.follow.PlanarRight;
                InputProbe.TestMovement=new Vector2(Vector3.Dot(delta.normalized,r),Vector3.Dot(delta.normalized,f)); yield return null;
            }
            InputProbe.TestMovement=Vector2.zero;
        }
        IEnumerator Jump(Vector3 target)
        {
            InputProbe.TestPress=KeyCode.Space; yield return null; InputProbe.TestPress=KeyCode.None;
            yield return Walk(target,1.1f); yield return Wait(.3f);
        }
        IEnumerator AimClick(LabTarget t,LabAbility ability)
        {
            lab.selected=ability;
            for(int i=0;i<24;i++)
            {
                var delta=t.transform.position-lab.player.view.transform.position;
                lab.player.follow.yaw=Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg;
                lab.player.follow.pitch=-Mathf.Atan2(delta.y,new Vector2(delta.x,delta.z).magnitude)*Mathf.Rad2Deg;
                yield return null;
            }
            Check(lab.Hovered==t,"INPUT center camera ray finds "+t.label+" ("+lab.AimMessage+")");
            lab.testClick=true; yield return null; yield return null;
        }
        IEnumerator Exercise()
        {
            yield return Wait(.3f);
            Check(lab.player.puzzleLabLocomotion && lab.player.follow!=null && lab.player.world==null,"REFERENCES reused standalone locomotion and camera; no legacy stage world");
            Check(FindObjectsByType<EverflowMap.EFMapPlayer>(FindObjectsSortMode.None).Length==1,"REFERENCES single player");
            Check(lab.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject))==0,"REFERENCES no missing scripts");
            var root=T("A ROOT STEP"); var leaf=T("A LEAF"); var current=T("A WATER CURRENT"); var mover=leaf.GetComponent<LabMover>();
            yield return AimClick(root,LabAbility.Bloom);
            Check(root.Grown,"INPUT click grows A root"); lab.Recall(LabAbility.Bloom);
            Check(root.Grown && lab.slots[0]==null,"API recall retains growth");
            yield return Walk(new Vector3(1.8f,1,2.5f)); yield return Jump(new Vector3(3,2,2.5f));
            Check(lab.player.transform.position.y>1.9f,"PHYSICAL jump onto root support");
            yield return Jump(new Vector3(4.8f,3.4f,3.5f)); yield return Wait(.3f);
            Check(lab.areas[0].Complete,"PHYSICAL T01 root route reaches same grounded goal");
            lab.areas[0].ResetArea();
            Check(!root.Grown && !lab.areas[0].Complete && lab.slots.All(t=>t==null),"API area reset clears local state and slots");
            Check(lab.TryApply(current,LabAbility.Flow,1),"API choose departure current"); lab.Recall(LabAbility.Flow);
            Check(current.Direction==1,"API current retained after recall");
            yield return Wait(3.2f);
            yield return Walk(new Vector3(1.4f,1,-2)); yield return Jump(new Vector3(3,2,-2));
            Check(lab.player.transform.position.y>1.9f,"PHYSICAL jump onto bounded leaf");
            var riderStart=lab.player.transform.position;
            lab.TryApply(current,LabAbility.Flow,0); lab.Recall(LabAbility.Flow);
            yield return Wait(2);
            Check(lab.player.transform.position.z>riderStart.z+1,"PHYSICAL moving support carries standing CharacterController");
            lab.TryApply(leaf,LabAbility.Freeze); lab.Recall(LabAbility.Freeze);
            Vector3 fixedPose=leaf.transform.position; yield return Wait(.4f);
            Check(leaf.Frozen && Vector3.Distance(fixedPose,leaf.transform.position)<.001f,"API freeze + recall preserves pose");
            leaf.Undo(LabAbility.Freeze); yield return Wait(.4f);
            Check(!leaf.Frozen && Vector3.Distance(fixedPose,leaf.transform.position)>.1f,"API release resumes from current pose");
            yield return Wait(2); lab.TryApply(leaf,LabAbility.Freeze); lab.Recall(LabAbility.Freeze);
            yield return Jump(new Vector3(6,3.4f,3.4f)); yield return Wait(.3f);
            Check(lab.areas[0].Complete,"PHYSICAL T01 leaf route reaches same goal");
            // Extra targets are API fixtures, not a claim of traversal.
            var fixture=new GameObject("API target lifecycle fixture").AddComponent<LabTarget>(); fixture.acceptsBloom=fixture.acceptsFreeze=fixture.acceptsFlow=true; fixture.area=lab.areas[0]; fixture.directions=new[]{"A","B"};
            lab.TryApply(root,LabAbility.Bloom);
            Check(!lab.TryApply(fixture,LabAbility.Bloom),"API second target rejected until recall");
            lab.Recall(LabAbility.Bloom); lab.TryApply(fixture,LabAbility.Bloom);
            Check(root.Grown && fixture.Grown,"API sequential growth retained together");
            fixture.gameObject.SetActive(false); Check(lab.slots[0]==null,"API disabled target releases placed orb"); Destroy(fixture.gameObject);
            if(lab.areas.Length>1) yield return FullChecks();
            lab.ResetAll();
        }
        IEnumerator FullChecks()
        {
            lab.ResetAll(); lab.Navigate(1);
            Check(!lab.areas[1].Complete,"API debug navigation does not complete T02");
            var bRoot=T("B LONG ROOT ROUTE"); var bFlow=T("B WIND ROTOR"); var bPlatform=T("B ROTARY PLATFORM"); var bMover=bPlatform.GetComponent<LabMover>();
            lab.TryApply(bRoot,LabAbility.Bloom); lab.Recall(LabAbility.Bloom);
            lab.TryApply(bFlow,LabAbility.Flow,0); lab.Recall(LabAbility.Flow); bMover.Tick(1);
            Vector3 pose=bMover.motion.position; lab.TryApply(bPlatform,LabAbility.Freeze); lab.Recall(LabAbility.Freeze); bMover.Tick(1);
            Check(Vector3.Distance(pose,bMover.motion.position)<.001f,"API T02 freeze persists independently of FLOW");
            bPlatform.Undo(LabAbility.Freeze); bMover.Tick(.1f);
            Check(Vector3.Distance(pose,bMover.motion.position)>.01f&&Vector3.Distance(pose,bMover.motion.position)<.2f,"API T02 release continues current phase without teleport");
            var cLeaf=T("C UNFOLDING LEAF"); var cWind=T("C WIND ZONE"); var cMover=cLeaf.GetComponent<LabMover>();
            lab.TryApply(cWind,LabAbility.Flow,1); lab.Recall(LabAbility.Flow); cMover.Tick(1);
            float smallDistance=Vector3.Distance(cMover.home,cMover.motion.position); pose=cMover.motion.position;
            lab.TryApply(cLeaf,LabAbility.Bloom); lab.Recall(LabAbility.Bloom);
            Check(Vector3.Distance(pose,cMover.motion.position)<.001f,"API unfolding moving leaf preserves position"); cMover.Tick(1);
            Check(Vector3.Distance(pose,cMover.motion.position)>smallDistance*2,"API expanded leaf responds faster to same wind");
            lab.TryApply(cLeaf,LabAbility.Freeze); lab.Recall(LabAbility.Freeze); pose=cMover.motion.position; cMover.Tick(2);
            Check(cLeaf.Grown&&cLeaf.Frozen&&cWind.Direction==1&&Vector3.Distance(pose,cMover.motion.position)<.001f,"API T03 unfolding, wind and freeze retained together after all recalls");
            cLeaf.Undo(LabAbility.Freeze); cMover.Tick(.1f);
            Check(Vector3.Distance(pose,cMover.motion.position)>.01f,"API T03 release reacts to retained current wind");
            lab.areas[2].ResetArea(); Check(bRoot.Grown&&bFlow.Direction==0,"API T03 reset preserves T02 state");
            var gFlow=T("G WATER BRANCH"); var gRoot=T("G GATE LIFT ROOT"); var g=lab.areas[3].goalGate;
            lab.TryApply(gFlow,LabAbility.Flow,0); lab.Recall(LabAbility.Flow); g.Tick(3);
            Check(g.Latched,"API T04 wheel opens and latches gate"); lab.TryApply(gFlow,LabAbility.Flow,1); lab.Recall(LabAbility.Flow); g.Tick(10);
            Check(g.IsOpen,"API T04 changed water direction retains fully open gate"); lab.areas[3].ResetArea();
            lab.TryApply(gRoot,LabAbility.Bloom); lab.Recall(LabAbility.Bloom); g.Tick(3);
            Check(g.Latched&&gRoot.Grown,"API T04 root alternative retains open gate after recall");
            var u=lab.areas[4]; var circuit=u.canal; var gate=circuit.gate;
            foreach(var m in u.GetComponentsInChildren<LabMover>()) m.enabled=false;
            gate.enabled=false; circuit.enabled=false;
            u.ResetArea();
            lab.TryApply(circuit.branch,LabAbility.Flow,0); lab.Recall(LabAbility.Flow); circuit.Tick(0);
            Check(!circuit.Supply&&!circuit.Arriving,"API misaligned upper canal prevents supply");
            lab.TryApply(circuit.bypassRoot,LabAbility.Bloom); lab.Recall(LabAbility.Bloom); circuit.Tick(0);
            Check(circuit.Supply&&!circuit.Arriving&&circuit.dockWater.All(x=>x.activeSelf)&&circuit.mainWater.All(x=>!x.activeSelf),"API dock branch carries water and cannot feed final wheel");
            lab.TryApply(circuit.branch,LabAbility.Flow,2); lab.Recall(LabAbility.Flow); circuit.Tick(0);
            Check(!circuit.Arriving&&!gate.IsOpen&&circuit.exitWater.All(x=>!x.activeSelf),"API closed gate blocks W and its downstream visuals");
            lab.TryApply(gate.root,LabAbility.Bloom); lab.Recall(LabAbility.Bloom); gate.Tick(3); circuit.Tick(0);
            Check(circuit.Arriving&&!circuit.Success,"API root-open gate + main + bypass starts arrival, no instant win");
            lab.TryApply(circuit.mainIce,LabAbility.Freeze); lab.Recall(LabAbility.Freeze); circuit.Tick(4);
            Check(!circuit.Arriving&&!circuit.Success&&circuit.gateApproachWater.All(x=>!x.activeSelf),"API main ice blocks supply after recall, downstream stays dry");
            circuit.mainIce.Undo(LabAbility.Freeze); circuit.Tick(2.9f); Check(!circuit.Success,"API W requires full 3 seconds"); circuit.Tick(.2f);
            Check(circuit.Success&&circuit.Arriving&&circuit.exitWater.All(x=>x.activeSelf),"API thaw recovers circuit and final restoration");
            u.ResetArea();
            lab.TryApply(circuit.aqueduct.target,LabAbility.Freeze); lab.Recall(LabAbility.Freeze);
            lab.TryApply(circuit.aqueduct.flow,LabAbility.Flow,0); lab.Recall(LabAbility.Flow); circuit.aqueduct.Tick(20); circuit.Tick(0);
            Check(!circuit.Connected,"API freezing misaligned structure cannot fake a connection");
            circuit.aqueduct.target.Undo(LabAbility.Freeze); circuit.aqueduct.Tick(20); circuit.Tick(0);
            Check(circuit.Connected&&circuit.Supply,"API release and realignment recover upper supply");
            lab.TryApply(circuit.aqueduct.target,LabAbility.Freeze); lab.Recall(LabAbility.Freeze); circuit.Tick(0);
            Check(circuit.Supply,"API structure freeze alone does not freeze contained water");
            lab.TryApply(circuit.upperIce,LabAbility.Freeze); lab.Recall(LabAbility.Freeze); circuit.Tick(0);
            Check(!circuit.Supply,"API separate inner-water target blocks upper supply");
            circuit.upperIce.Undo(LabAbility.Freeze);
            lab.TryApply(circuit.branch,LabAbility.Flow,1); lab.Recall(LabAbility.Flow); circuit.Tick(0); gate.Tick(3);
            Check(gate.Latched,"API D4 small-wheel branch fully latches exit sluice");
            lab.TryApply(circuit.branch,LabAbility.Flow,2); lab.Recall(LabAbility.Flow); circuit.Tick(3);
            Check(circuit.Success&&gate.IsOpen,"API alignment alternative and wheel-open gate feed W, regardless of prior input order");
            lab.TryApply(circuit.branch,LabAbility.Flow,0); lab.Recall(LabAbility.Flow); circuit.Tick(0);
            Check(!circuit.Arriving&&circuit.Success&&gate.Latched,"API completed restoration and gate persist; live water reflects changed branch");
            u.ResetArea(); Check(!circuit.Success&&!gate.IsOpen&&bRoot.Grown,"API integrated reset clears its circuit and preserves surface changes");
            foreach(var m in u.GetComponentsInChildren<LabMover>()) m.enabled=true;
            gate.enabled=true; circuit.enabled=true;
            yield return null;
        }
    }
}
