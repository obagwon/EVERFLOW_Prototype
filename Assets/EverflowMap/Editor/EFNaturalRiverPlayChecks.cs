using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace EverflowMap.Editor
{
    [InitializeOnLoad]
    public static class EFNaturalRiverPlayChecks
    {
        const string Key="EVERFLOW.NaturalRiver.Check";
        static EFNaturalRiverPlayChecks()
        {
            EditorApplication.playModeStateChanged+=state=>
            {if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false)){SessionState.SetBool(Key,false);new GameObject("Natural river physical and visual checks").AddComponent<EFNaturalRiverProbe>();}};
        }
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stop Play first.");
            SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
    }
    public sealed class EFNaturalRiverProbe:MonoBehaviour
    {
        const string Folder=EFNaturalRiverTools.Reports;
        EFV2PuzzleWorld world;EFV2WaterIntegration water;EFMapPlayer player;Keyboard keyboard;
        readonly StringBuilder report=new StringBuilder();int checks,failures;
        void Check(bool value,string message){checks++;if(!value)failures++;report.AppendLine((value?"PASS ":"FAIL ")+message);}
        void OnEnable(){InputSystem.onAfterUpdate+=OwnKeyboard;Application.logMessageReceived+=Log;}
        void OnDisable(){InputSystem.onAfterUpdate-=OwnKeyboard;Application.logMessageReceived-=Log;}
        void OwnKeyboard(){if(keyboard!=null&&keyboard.added)keyboard.MakeCurrent();}
        void Log(string text,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){failures++;report.AppendLine("UNITY ERROR "+text);}}
        IEnumerator Frames(int n){for(int i=0;i<n;i++)yield return null;}
        IEnumerator Tap(Key key){InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));yield return Frames(2);InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return Frames(3);}
        float Ground(Vector3 point)
        {
            var hits=Physics.RaycastAll(point+Vector3.up*.7f,Vector3.down,2,~(1<<2),QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance).ToArray();
            return hits.Length>0?hits[0].point.y:float.NaN;
        }
        void View(Vector3 p,Vector3 target)
        {player.cinemachineRig.brain.enabled=false;player.cinemachineRig.SetInputActive(false);player.view.transform.SetPositionAndRotation(p,Quaternion.LookRotation(target-p));player.view.fieldOfView=58;}
        IEnumerator Cases()
        {
            report.AppendLine("ISOLATED NATURAL RIVER CHECK: start player/camera explicitly placed. Real Input System W/ability keys, normal motor and cooked floor colliders. Fixed observation camera is not a camera regression or continuous puzzle playthrough.");
            var grounds=world.mapRoot.GetComponentsInChildren<MeshFilter>(true).Where(f=>AssetDatabase.GetAssetPath(f.sharedMesh).StartsWith(EFNaturalRiverTools.Folder)).ToArray();
            Check(grounds.Length>=5,"Carved floor overrides installed");
            foreach(var f in grounds)Check(f.GetComponents<MeshCollider>().Any(c=>c.enabled&&c.sharedMesh==f.sharedMesh&&!c.convex),f.name+" visible floor and collision use the same carved mesh");
            Check(water.segments.Length==39&&water.segments.Count(s=>!s.suppressPresentation)==15,"Original 39 water segments / 15 visible network owners retained");
            Check(water.segments.All(s=>s.filter.GetComponents<Collider>().Length==0),"Water surfaces remain non-solid");
            var source=water.segments.Single(s=>s.label=="W_H_to_G_Fall");var mesh=source.preparedMesh;var v=mesh.vertices;int rows=mesh.uv2.TakeWhile(uv=>uv.y<.5f).Count()/17;
            var points=Enumerable.Range(0,rows).Select(i=>source.filter.transform.TransformPoint(v[i*17+8])).ToArray();
            Check(points.Zip(points.Skip(1),(a,b)=>b.y<=a.y+.002f).All(value=>value),"Connected main stream never rises upstream-to-downstream");
            var selected=Enumerable.Range(1,rows-2).Where(i=>points[i].y>2.849f&&points[i].y<2.901f&&points[i].z>-55.5f&&points[i].z<-34).ToArray();
            int step=Mathf.Max(1,selected.Length/8);
            for(int sample=0;sample<selected.Length;sample+=step)
            {
                int i=selected[sample];var p=points[i];var direction=Vector3.ProjectOnPlane(points[i+1]-points[i-1],Vector3.up).normalized;var right=Vector3.Cross(Vector3.up,direction);
                float half=Vector3.Distance(source.filter.transform.TransformPoint(v[i*17]),source.filter.transform.TransformPoint(v[i*17+16]))*.5f;
                float bed=Ground(p),left=Ground(p-right*(half+.5f)),bank=Ground(p+right*(half+.5f));
                Check(!float.IsNaN(bed)&&bed<p.y-.18f,"Real creek bed below water at "+p.ToString("F2")+" depth="+(p.y-bed).ToString("F3"));
                Check(left>p.y+.055f&&bank>p.y+.055f,"Both earth banks contain water at "+p.ToString("F2"));
            }
            player.transform.rotation=Quaternion.Euler(0,-90,0);player.Teleport(new Vector3(-43,4.1f,-50.5f));
            View(new Vector3(-37,9,-50.5f),new Vector3(-46,2.9f,-50.5f));yield return new WaitForSeconds(.6f);
            int version=player.TeleportVersion;float started=Time.unscaledTime,minimum=player.transform.position.y-1;int grounded=0,moving=0;
            while(player.transform.position.x>-49.15f&&Time.unscaledTime-started<5)
            {InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));minimum=Mathf.Min(minimum,player.transform.position.y-1);if(player.Grounded)grounded++;moving++;yield return null;}
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return new WaitForSeconds(.25f);
            Check(player.transform.position.x<=-49.15f&&player.Grounded,"Real W walk crosses both banks and the shallow creek without jump");
            Check(minimum<2.79f&&grounded>moving*.75f,"Walk physically enters the lowered bed and climbs out (minimum feet="+minimum.ToString("F3")+")");
            Check(player.TeleportVersion==version,"Creek crossing triggers no fall recovery");
            ScreenCapture.CaptureScreenshot(Folder+"/ford-crossing.png");
            yield return Tap(Key.Digit2);float clock=source.clock;yield return new WaitForSecondsRealtime(.3f);
            Check(world.AbilityMode&&clock==source.clock,"Ability selection pauses new river animation through existing clock");
            yield return Tap(Key.Escape);yield return new WaitForSeconds(.15f);
            Check(!world.AbilityMode&&!world.Paused&&source.clock>clock,"Cancel resumes the existing water clock");
            View(new Vector3(-29,13,-34),new Vector3(-40,3,-47));yield return new WaitForSeconds(.4f);ScreenCapture.CaptureScreenshot(Folder+"/after-overview.png");yield return Frames(3);
            View(new Vector3(-38,6.5f,-41),new Vector3(-44.1f,2.9f,-46));yield return new WaitForSeconds(.4f);ScreenCapture.CaptureScreenshot(Folder+"/after-bend.png");yield return Frames(3);
            world.Restart();yield return Frames(6);
            Check(FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Count(r=>r.name=="Sparse bank pebbles")==1,"Restart retains one bank detail set without duplicate geometry");
        }
        IEnumerator Start()
        {
            Directory.CreateDirectory(Folder);yield return Frames(12);world=FindFirstObjectByType<EFV2PuzzleWorld>();water=FindFirstObjectByType<EFV2WaterIntegration>();player=world.player;
            world.AutomatedCheck=true;world.SetPaused(false);while(world.OpeningActive)yield return null;
            keyboard=InputSystem.AddDevice<Keyboard>("NaturalRiverKeyboard");
            SessionState.SetString(EFV2PuzzlePlayChecks.ModeKey,"natural-river");SessionState.SetFloat(EFV2PuzzlePlayChecks.ModeKey+".deadline",(float)EditorApplication.timeSinceStartup+120);
            EFDemoReview.Start("../play-review-20261109/natural-river/river-review");
            var stack=new System.Collections.Generic.Stack<IEnumerator>();stack.Push(Cases());
            while(stack.Count>0)
            {
                bool more;object current=null;try{more=stack.Peek().MoveNext();if(more)current=stack.Peek().Current;}catch(Exception e){failures++;report.AppendLine("EXCEPTION "+e);break;}
                if(!more){stack.Pop();continue;}if(current is IEnumerator child){stack.Push(child);continue;}yield return current;
            }
            report.AppendLine("TOTAL "+checks+" checks, "+failures+" failures.");File.WriteAllText(Folder+"/checks.txt",DateTime.UtcNow.ToString("O")+"\n"+report);Debug.Log(report.ToString());
            EFDemoReview.Stop();SessionState.EraseString(EFV2PuzzlePlayChecks.ModeKey);if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);world.AutomatedCheck=false;Destroy(gameObject);
        }
    }
}
