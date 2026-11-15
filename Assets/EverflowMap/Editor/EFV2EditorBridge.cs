using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EverflowMap.Editor
{
    // Fixed project-local Unity operations only. No shell, arbitrary code, license or ACL changes.
    // The user's requested test session is granted by an expiring file in this project's Temp.
    [InitializeOnLoad]
    public static class EFV2EditorBridge
    {
        const string Folder="Temp/v2-puzzle-bridge", StateKey="EVERFLOW.V2Bridge.State", StampKey="EVERFLOW.V2Bridge.SourceStamp";
        static Status state;
        static double nextPoll,nextPublish;
        static bool busy;
        [Serializable] sealed class Grant {public string expiresUtc;}
        [Serializable] sealed class Request {public string session, id, command, issuedUtc;}
        [Serializable] sealed class Status
        {
            public string session="", id="", command="", outcome="idle", message="", updatedUtc="", activeScene="";
            public bool enabled, playing, compiling, updating, compilationFailed;
            public int editorPid;
            public int frame, probes;
            public bool editorPaused;
            public string progress="";
            public string report="";
            public long startedTicks;
        }
        static EFV2EditorBridge()
        {
            if(AssetDatabase.IsAssetImportWorkerProcess())return;
            try {state=JsonUtility.FromJson<Status>(SessionState.GetString(StateKey,""));}catch{}
            if(state==null)state=new Status{session=Guid.NewGuid().ToString("N")};
            if(string.IsNullOrEmpty(state.session))state.session=Guid.NewGuid().ToString("N");
            EditorApplication.update+=Tick;
        }
        static bool Granted()
        {
            try
            {
                var grant=JsonUtility.FromJson<Grant>(File.ReadAllText(Folder+"/grant.json"));
                return DateTime.TryParse(grant.expiresUtc,null,System.Globalization.DateTimeStyles.RoundtripKind,out var expiry)&&expiry.ToUniversalTime()>DateTime.UtcNow;
            }
            catch{return false;}
        }
        [MenuItem("Tools/EVERFLOW/V2 Puzzles/Disable Test Bridge")]
        public static void Disable()
        {
            if(File.Exists(Folder+"/grant.json"))File.Delete(Folder+"/grant.json");
            state.message="V2 bridge disabled. No new commands will execute.";Publish();
        }
        static void Publish()
        {
            state.enabled=Granted();state.playing=EditorApplication.isPlaying;state.compiling=EditorApplication.isCompiling;
            state.updating=EditorApplication.isUpdating;state.compilationFailed=EditorUtility.scriptCompilationFailed;
            state.activeScene=SceneManager.GetActiveScene().path;state.updatedUtc=DateTime.UtcNow.ToString("O");state.editorPid=Process.GetCurrentProcess().Id;
            state.frame=Time.frameCount;state.editorPaused=EditorApplication.isPaused;
            state.probes=UnityEngine.Object.FindObjectsByType<EFV2PuzzleProbe>(FindObjectsSortMode.None).Length;
            var probe=UnityEngine.Object.FindFirstObjectByType<EFV2PuzzleProbe>();state.progress=probe!=null?probe.Progress:"";
            SessionState.SetString(StateKey,JsonUtility.ToJson(state));
            Directory.CreateDirectory(Folder);File.WriteAllText(Folder+"/status.json",JsonUtility.ToJson(state,true));
        }
        static void Guard(bool allowPlay=false)
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorUtility.scriptCompilationFailed)throw new Exception("Wait for successful Unity import/compilation.");
            if(EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if(!allowPlay || SceneManager.GetActiveScene().path!=EFV2PuzzleBuild.Scene)throw new Exception("Another Play run is active; preserved.");
            }
            else for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new Exception("Unsaved scene edits preserved. Save them in Unity first.");
        }
        static void Open()
        {
            Guard();EditorSceneManager.OpenScene(EFV2PuzzleBuild.Scene);EFV2PuzzleBuild.Validate();
        }
        static void Tick()
        {
            if(busy||EditorApplication.timeSinceStartup<nextPoll)return;
            nextPoll=EditorApplication.timeSinceStartup+.25;
            if(!Granted())return;
            try
            {
                if(EditorApplication.isPlaying&&state.command=="play")
                {Application.runInBackground=true;EditorApplication.QueuePlayerLoopUpdate();}
                if(state.outcome=="running" && (state.command=="forced"||state.command=="input"||state.command=="route"||state.command=="alternate"||state.command=="exitcheck"||state.command=="platforms"))
                {
                    Application.runInBackground=true;
                    EditorApplication.QueuePlayerLoopUpdate();
                    string report=EFV2PuzzleBuild.Reports+"/runtime-"+(state.command=="forced"?"forced-state":state.command=="route"?"route-bloom":state.command=="alternate"?"route-alternate":state.command=="exitcheck"?"exit-isolated":state.command=="platforms"?"platforms-isolated":"synthetic-input-smoke")+".txt";
                    if(File.Exists(report)&&File.GetLastWriteTimeUtc(report).Ticks>=state.startedTicks)
                    {
                        string text=File.ReadAllText(report);state.report=report;state.outcome=text.Contains(", 0 failures.")?"passed":"failed";
                        state.message="Runtime report saved; this is not a full human playthrough.";Publish();
                        if(EditorApplication.isPlaying)EditorApplication.ExitPlaymode();
                    }
                }
                if(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating)
                {
                    string stamp=string.Join("|",Directory.GetFiles("Assets/EverflowMap","EFV2*.cs",SearchOption.AllDirectories).OrderBy(p=>p).Select(p=>p+":"+File.GetLastWriteTimeUtc(p).Ticks));
                    string previous=SessionState.GetString(StampKey,"");SessionState.SetString(StampKey,stamp);
                    if(previous.Length>0&&previous!=stamp) {AssetDatabase.Refresh();return;}
                }
                string path=Folder+"/request.json";
                if(File.Exists(path)&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating)
                {
                    var request=JsonUtility.FromJson<Request>(File.ReadAllText(path));
                    File.Move(path,Folder+"/consumed-"+Guid.NewGuid().ToString("N")+".json");
                    if(request.session!=state.session)throw new Exception("Stale bridge session.");
                    if(!DateTime.TryParse(request.issuedUtc,null,System.Globalization.DateTimeStyles.RoundtripKind,out var issued)||Math.Abs((DateTime.UtcNow-issued.ToUniversalTime()).TotalSeconds)>120)throw new Exception("Expired bridge request.");
                    if(state.outcome=="running"&&request.command!="stop")
                    {state.message="Runtime test already running; competing request ignored.";Publish();return;}
                    state.id=request.id;state.command=request.command;state.report="";state.startedTicks=DateTime.UtcNow.Ticks;state.outcome="accepted";Publish();
                    busy=true;
                    switch(request.command)
                    {
                        case "presentation-setup":Guard();EFV2PresentationTools.Configure();break;
                        case "presentation-check":Guard();EFV2PresentationChecks.Run();break;
                        case "guidance-check":Guard();EFV2GuidanceChecks.Run();break;
                        case "visual-before":Guard();EFV2VisualChecks.Run(false);break;
                        case "visual-after":Guard();EFV2VisualChecks.Run(true);break;
                        case "water-audit":Guard();EFV2WaterTools.Audit();break;
                        case "water-build":Guard();EFV2WaterTools.BuildTest();break;
                        case "water-integrate":Guard();EFV2WaterTools.Integrate();break;
                        case "water-test":Guard();EFV2WaterTools.Test(false);break;
                        case "water-check":Guard();EFV2WaterTools.Test(true);break;
                        case "water-verify":Guard();EFV2WaterTools.Verify();break;
                        case "open":Open();break;
                        case "forced":Guard();state.outcome="running";Publish();EFV2PuzzlePlayChecks.Forced();break;
                        case "input":Guard();state.outcome="running";Publish();EFV2PuzzlePlayChecks.InputSmoke();break;
                        case "route":Guard();state.outcome="running";Publish();EFV2PuzzlePlayChecks.Route();break;
                        case "alternate":Guard();state.outcome="running";Publish();EFV2PuzzlePlayChecks.Alternate();break;
                        case "exitcheck":Guard();state.outcome="running";Publish();EFV2PuzzlePlayChecks.ExitPhysics();break;
                        case "platforms":Guard();state.outcome="running";Publish();EFV2PuzzlePlayChecks.Platforms();break;
                        case "play":Open();EditorApplication.ExecuteMenuItem("Window/General/Game");EditorApplication.EnterPlaymode();break;
                        case "stop":Guard(true);EditorApplication.ExitPlaymode();break;
                        case "refresh":Guard();AssetDatabase.Refresh();break;
                        case "inspect":Guard(true);Inspect();break;
                        case "screenshot":
                            Guard(true);if(!EditorApplication.isPlaying)throw new Exception("Enter puzzle Play first.");
                            state.report=EFV2PuzzleBuild.Reports+"/live-game.png";ScreenCapture.CaptureScreenshot(state.report);break;
                        default:throw new Exception("Command is not in the V2 project test allowlist.");
                    }
                    if(state.outcome=="accepted")state.outcome="completed";
                    state.message="Fixed Unity command dispatched: "+request.command;Publish();
                    busy=false;
                }
                if(EditorApplication.timeSinceStartup>=nextPublish){nextPublish=EditorApplication.timeSinceStartup+1;Publish();}
            }
            catch(Exception e){busy=false;state.outcome="failed";state.message=e.ToString();Publish();UnityEngine.Debug.LogException(e);}
        }
        [Serializable] sealed class Inspection
        {
            public bool built,paused,grounded,supply,arriving,restored;
            public Vector3 player,camera;
            public string checkpoint;
            public TargetInfo[] targets;
            public MovingInfo[] movers;
        }
        [Serializable] sealed class TargetInfo {public string name,state;public Vector3 position;}
        [Serializable] sealed class MovingInfo {public string name;public Vector3 home,position;public bool frozen;}
        static void Inspect()
        {
            var w=UnityEngine.Object.FindFirstObjectByType<EFV2PuzzleWorld>();
            if(w==null)throw new Exception("Puzzle scene is not open.");
            var data=new Inspection{built=w.Built,paused=w.Paused,player=w.player.transform.position,camera=w.player.view.transform.position,
                grounded=w.player.GetComponent<CharacterController>().isGrounded,supply=w.Supply,arriving=w.Arriving,restored=w.Restored,
                checkpoint=w.CurrentCheckpoint!=null?w.CurrentCheckpoint.label:"S",targets=w.targets.Select(t=>new TargetInfo{name=t.label,state=t.State,position=t.transform.position}).ToArray(),
                movers=w.movers.Select(m=>new MovingInfo{name=m.name,home=m.home,position=m.motion.position,frozen=m.target.Frozen}).ToArray()};
            state.report=EFV2PuzzleBuild.Reports+"/live-inspection.json";File.WriteAllText(state.report,JsonUtility.ToJson(data,true));
        }
    }
}
