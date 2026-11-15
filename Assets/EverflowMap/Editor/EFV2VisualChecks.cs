using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using EverflowPuzzleLab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EverflowMap.Editor
{
    [InitializeOnLoad]
    public static class EFV2VisualChecks
    {
        const string Key="EVERFLOW.VisualCapture";
        public const string Reports="docs/visual-cleanup";
        static EFV2VisualChecks()
        {
            EditorApplication.playModeStateChanged+=state=>
            {
                var mode=SessionState.GetString(Key,"");
                if(mode.Length>0&&state==PlayModeStateChange.EnteredPlayMode)new GameObject("Visual audit - forced state and camera only").AddComponent<EFV2VisualProbe>().mode=mode;
                if(mode.Length==0&&state==PlayModeStateChange.EnteredPlayMode)
                {
                    string route=SessionState.GetString(EFV2PuzzlePlayChecks.ModeKey,"");
                    if(route.StartsWith("route-"))new GameObject("Observation only - normal route camera captures").AddComponent<EFV2VisualRouteRecorder>().route=route;
                }
                if(state==PlayModeStateChange.EnteredEditMode)SessionState.EraseString(Key);
            };
            EditorApplication.update+=()=>
            {
                if(SessionState.GetString(Key,"").Length==0)return;
                Application.runInBackground=true;EditorApplication.QueuePlayerLoopUpdate();
                if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+".deadline",float.MaxValue)){SessionState.EraseString(Key);EditorApplication.ExitPlaymode();}
            };
        }
        public static void Run(bool after)
        {
            Directory.CreateDirectory(Reports);
            EditorSceneManager.OpenScene(EFV2PuzzleBuild.Scene);
            SessionState.SetString(Key,after?"after":"before");SessionState.SetFloat(Key+".deadline",(float)EditorApplication.timeSinceStartup+90);
            EditorApplication.ExecuteMenuItem("Window/General/Game");EditorApplication.isPaused=false;EditorApplication.EnterPlaymode();
        }
        public static void Finish(){SessionState.EraseString(Key);EditorApplication.ExitPlaymode();}
    }
    public sealed class EFV2VisualProbe:MonoBehaviour
    {
        public string mode;
        EFV2PuzzleWorld w;
        Camera view;
        readonly StringBuilder audit=new StringBuilder();
        IEnumerator Shot(string name,Vector3 position,Vector3 target)
        {
            view.transform.position=position;view.transform.LookAt(target);
            foreach(var b in w.GetComponentsInChildren<EFV2Billboard>(true))b.view=view;
            yield return new WaitForSecondsRealtime(.15f);yield return new WaitForEndOfFrame();
            var texture=ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(EFV2VisualChecks.Reports+"/"+mode+"-"+name+".png",texture.EncodeToPNG());Destroy(texture);
            yield return null;
        }
        IEnumerator Start()
        {
            w=FindFirstObjectByType<EFV2PuzzleWorld>();w.AutomatedCheck=true;w.SetPaused(false);
            yield return null;yield return null;
            foreach(var c in FindObjectsByType<Camera>(FindObjectsSortMode.None))c.enabled=false;
            view=new GameObject("Visual comparison camera").AddComponent<Camera>();view.fieldOfView=60;view.nearClipPlane=.1f;view.farClipPlane=400;
            w.player.cinemachineRig.SetInputActive(false);
            audit.AppendLine(DateTime.UtcNow.ToString("O")+" FORCED STATES / FIXED CAMERA. Not input traversal.");
            foreach(var m in w.mapRoot.GetComponentsInChildren<MeshFilter>(true))
                if(m.name.StartsWith("PH_"))audit.AppendLine($"MODEL {m.name} localBounds={m.sharedMesh.bounds} pivot={m.transform.position} world={m.GetComponent<Renderer>().bounds}");
            foreach(var r in w.GetComponentsInChildren<MeshRenderer>(true))
                audit.AppendLine($"FIXTURE {r.name} bounds={r.bounds} visible={r.enabled} active={r.gameObject.activeInHierarchy}");
            File.WriteAllText(EFV2VisualChecks.Reports+"/"+mode+"-geometry.txt",audit.ToString());
            if(mode=="after")File.WriteAllText(EFV2VisualChecks.Reports+"/after-cleanup.txt",string.Join("\n",EFV2VisualCleanup.LastChanges));
            yield return Shot("a-shore",new Vector3(-38,7,-8),new Vector3(-45,1,-20));
            foreach(var t in w.targets.Where(t=>t.acceptsBloom))t.Apply(LabAbility.Bloom);
            foreach(var mover in w.movers)mover.enabled=false;
            foreach(var gate in w.gates)gate.Tick(10);
            w.branch.Apply(LabAbility.Flow,2);w.TickCircuit(3.1f);
            Time.timeScale=0;
            yield return Shot("a-grown",new Vector3(-34,10,-14),new Vector3(-43,3,-23));
            yield return Shot("b-bridge",new Vector3(-18,13,-37),new Vector3(-26,6,-46));
            yield return Shot("c-leaf",new Vector3(-56,12,-35),new Vector3(-64,6,-44));
            yield return Shot("g-gate",new Vector3(-38,19,-55),new Vector3(-31,17,-62));
            yield return Shot("d1-stairs",new Vector3(-43,3,-36),new Vector3(-54,-6,-47));
            yield return Shot("d2-hub",new Vector3(-62,-8,-47.5f),new Vector3(-56,-11.8f,-54));
            yield return Shot("d5-service",new Vector3(-50,1,-57),new Vector3(-56,-5.5f,-65));
            yield return Shot("d4-open",new Vector3(-68,-8,-44),new Vector3(-77,-10,-48));
            yield return Shot("exit-corridor",new Vector3(-74,-5,-20),new Vector3(-83,-15,-30));
            yield return Shot("garden",new Vector3(-89,-13,5),new Vector3(-84,-17.3f,-1));
            File.WriteAllText(EFV2VisualChecks.Reports+"/"+mode+"-captures.txt",DateTime.UtcNow.ToString("O")+"\n11 fixed-view Game View captures saved. Explicit forced growth/gate/supply setup; no traversal claim.\n");
            Time.timeScale=1;EFV2VisualChecks.Finish();
        }
    }
    [DefaultExecutionOrder(900)]
    public sealed class EFV2VisualRouteRecorder:MonoBehaviour
    {
        public string route;
        EFV2PuzzleWorld world;
        readonly System.Collections.Generic.HashSet<string> captured=new System.Collections.Generic.HashSet<string>();
        string pending="";float due;
        int frames,nearCollision;
        void Start(){world=FindFirstObjectByType<EFV2PuzzleWorld>();}
        void LateUpdate()
        {
            if(world==null||!world.Built)return;
            frames++;
            if(frames>10&&Physics.OverlapSphere(world.player.view.transform.position,.08f,1,QueryTriggerInteraction.Ignore).Length>0)nearCollision++;
            string location=world.CurrentCheckpoint!=null?world.CurrentCheckpoint.label:"S";
            if(world.Restored)location="W_RESTORED"+(world.GardenReached?"_GARDEN":"");
            if(!captured.Contains(location)&&pending!=location){pending=location;due=Time.unscaledTime+.25f;}
            if(pending.Length>0&&Time.unscaledTime>=due)
            {
                captured.Add(pending);string safe=System.Text.RegularExpressions.Regex.Replace(pending,"[^a-zA-Z0-9_-]","_");
                ScreenCapture.CaptureScreenshot(EFV2VisualChecks.Reports+"/input-"+route+"-"+safe+".png");
                pending="";
            }
        }
        void OnDisable()
        {
            File.WriteAllText(EFV2VisualChecks.Reports+"/camera-"+route+".txt",DateTime.UtcNow.ToString("O")+$"\nObservation only; no pose, puzzle state or input writes. {frames} LateUpdate samples; {nearCollision} samples with an enabled non-trigger Default-layer collider within 0.08m of the actual camera. This proximity measurement is not an image-quality guarantee.\n");
        }
    }
}
