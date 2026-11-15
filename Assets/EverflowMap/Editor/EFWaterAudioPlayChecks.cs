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
    public static class EFWaterAudioPlayChecks
    {
        const string Key="EVERFLOW.WaterAudio.Check";
        static EFWaterAudioPlayChecks()
        {EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false)){SessionState.SetBool(Key,false);new GameObject("Water audio checks").AddComponent<EFWaterAudioProbe>();}};}
        public static void Run()
        {if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stop Play first.");SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();}
    }
    public sealed class EFWaterAudioProbe:MonoBehaviour
    {
        const string Folder="docs/play-review-20261109/water-audio";
        EFV2PuzzleWorld world;EFV2WaterIntegration water;EFDemoFeedback feedback;AudioSource bed;Keyboard keyboard;
        readonly StringBuilder report=new StringBuilder();int checks,failures;
        void Check(bool value,string text){checks++;if(!value)failures++;report.AppendLine((value?"PASS ":"FAIL ")+text);}
        void OnEnable(){InputSystem.onAfterUpdate+=Own;Application.logMessageReceived+=Log;}
        void OnDisable(){InputSystem.onAfterUpdate-=Own;Application.logMessageReceived-=Log;}
        void Own(){if(keyboard!=null&&keyboard.added)keyboard.MakeCurrent();}
        void Log(string text,string trace,LogType type){if(type==LogType.Exception||type==LogType.Error||type==LogType.Assert){failures++;report.AppendLine("UNITY ERROR "+text);}}
        IEnumerator Frames(int n){for(int i=0;i<n;i++)yield return null;}
        IEnumerator Tap(Key k){InputSystem.QueueStateEvent(keyboard,new KeyboardState(k));yield return Frames(2);InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return Frames(3);}
        void View(Vector3 position,Vector3 target)
        {var p=world.player;p.cinemachineRig.brain.enabled=false;p.cinemachineRig.SetInputActive(false);p.view.transform.SetPositionAndRotation(position,Quaternion.LookRotation(target-position));}
        void GeometryContracts()
        {
            var go=new GameObject("Isolated audible triangle fixture");var integration=go.AddComponent<EFV2WaterIntegration>();integration.enabled=false;
            var filter=go.AddComponent<MeshFilter>();var renderer=go.AddComponent<MeshRenderer>();
            // Two separated sheets, with a deliberately dry center inside their AABB.
            var mesh=new Mesh();mesh.vertices=new[]{new Vector3(-8,0,-1),new Vector3(-6,0,-1),new Vector3(-8,0,1),new Vector3(6,0,-1),new Vector3(8,0,-1),new Vector3(8,0,1)};
            mesh.triangles=new[]{0,2,1,3,5,4};mesh.RecalculateBounds();filter.sharedMesh=mesh;
            var segment=new EFV2WaterSegment{label="fixture",renderer=renderer,filter=filter,wet=true,speed=1,direction=Vector3.forward};integration.segments=new[]{segment};
            var sampler=new EFV2WaterAudioSampler();var gap=sampler.Query(integration,Vector3.zero,5,.55f);
            Check(renderer.bounds.Contains(Vector3.zero)&&!gap.valid,"Dry gap inside renderer bounds is silent (exact triangle distance)");
            var flat=sampler.Query(integration,new Vector3(-7.5f,.1f,0),9,.55f);
            Check(flat.valid&&Mathf.Abs(flat.point.y)<.001f&&flat.gain<.551f,"Shallow surface produces at most 55% of fall gain");
            go.transform.rotation=Quaternion.Euler(0,0,90);var ear=go.transform.TransformPoint(new Vector3(-7.5f,.1f,0));
            var fall=sampler.Query(integration,ear,9,.55f);
            Check(fall.valid&&fall.gain>flat.gain*1.7f,"Transformed vertical sheet is louder than shallow sheet");
            go.transform.position=new Vector3(30,0,0);Check(!sampler.Query(integration,ear,9,.55f).valid,"Moving transform invalidates cached world vertices");
            go.transform.position=Vector3.zero;
            segment.frozen=true;Check(!sampler.Query(integration,ear,9,.55f).valid,"Frozen water excluded");segment.frozen=false;
            segment.wet=false;Check(!sampler.Query(integration,ear,9,.55f).valid,"Dry water excluded");segment.wet=true;
            segment.suppressPresentation=true;Check(!sampler.Query(integration,ear,9,.55f).valid,"Suppressed legacy surfaces excluded");segment.suppressPresentation=false;
            mesh.uv2=Enumerable.Repeat(Vector2.up,6).ToArray();filter.sharedMesh=null;filter.sharedMesh=mesh;
            Check(!new EFV2WaterAudioSampler().Query(integration,ear,9,.55f).valid,"UV2 volume underside/skirt cannot become an audible source");
            Destroy(go);Destroy(mesh);
        }
        IEnumerator Cases()
        {
            report.AppendLine("Isolated audio/geometry fixtures and real Input System mode transitions. Fixed listener/camera viewpoints, not a puzzle playthrough or human listening evaluation.");
            GeometryContracts();yield return Frames(2);
            float ceiling=feedback.ambientVolume*Mathf.Pow(10,-feedback.waterReductionDb/20);
            Check(Mathf.Abs(20*Mathf.Log10(ceiling/feedback.ambientVolume)+12)<.01f,"Water-only maximum reduced 12 dB: "+ceiling.ToString("F6"));
            Check(feedback.effectsVolume==.55f&&feedback.ambientVolume==.18f,"Existing effect and wheel base gains unchanged");
            Check(bed.spatialBlend==1&&bed.rolloffMode==AudioRolloffMode.Custom&&bed.GetCustomCurve(AudioSourceCurveType.CustomRolloff).Evaluate(1)==1,"Spatial panning with one listener-based attenuation");
            int count=feedback.GetComponentsInChildren<AudioSource>().Length;
            Check(count==12&&feedback.GetComponentsInChildren<AudioSource>().Count(a=>a.name=="Nearby running water")==1,"One water voice in original bounded 12-source pool");
            var sampler=new EFV2WaterAudioSampler();
            var positions=new[]{new Vector3(-43,8,-58),new Vector3(-39,3,-50),new Vector3(-44,4,-47),new Vector3(30,25,30)};
            for(int i=0;i<positions.Length;i++)
            {
                View(positions[i],i==3?Vector3.zero:new Vector3(-45,3,-50));
                yield return new WaitForSeconds(.65f);
                var sample=sampler.Query(water,world.player.view.transform.position,feedback.waterAudibleRadius,feedback.shallowWaterGain);
                report.AppendLine("VIEW "+i+" ear="+positions[i]+" surface="+sample.label+" distance="+sample.distance.ToString("F3")+" gain="+sample.gain.ToString("F3")+" source volume="+bed.volume.ToString("F6"));
                Check(bed.volume<=ceiling+.00001f,"View "+i+" remains below -12 dB water ceiling");
                Check(Mathf.Abs(bed.volume-ceiling*sample.gain)<.0001f,"View "+i+" uses actual listener/surface distance");
                if(sample.valid)Check(Vector3.Distance(bed.transform.position,sample.point)<.01f,"View "+i+" emitter lies on audible water triangle");
                else Check(bed.volume==0,"Distant listener is fully silent");
                yield return new WaitForSeconds(1);
            }
            View(positions[0],new Vector3(-45,3,-55));yield return new WaitForSeconds(.5f);
            var watch=System.Diagnostics.Stopwatch.StartNew();for(int i=0;i<50;i++)sampler.Query(water,positions[0],9,.55f);watch.Stop();report.AppendLine("Exact surface query mean "+(watch.Elapsed.TotalMilliseconds/50).ToString("F3")+" ms (Editor, 50 warmed queries)");
            for(int i=0;i<4;i++)
            {
                yield return Tap(Key.Digit1);Check(world.AbilityMode&&AudioListener.pause,"Ability mode pauses audio (cycle "+i+")");
                float volume=bed.volume;int sample=bed.timeSamples;yield return new WaitForSecondsRealtime(.25f);
                Check(bed.volume==volume&&Mathf.Abs(bed.timeSamples-sample)<1024,"Paused water voice does not advance or accumulate");
                yield return Tap(Key.Escape);Check(!world.Paused&&!world.AbilityMode&&!AudioListener.pause,"Cancel restores audio without opening pause");
            }
            yield return Tap(Key.Escape);Check(world.Paused&&AudioListener.pause,"Ordinary Esc pauses water");
            yield return Tap(Key.Escape);Check(!world.Paused&&!AudioListener.pause,"Ordinary resume restores water");
            world.Respawn();yield return Frames(8);world.Restart();yield return Frames(8);
            Check(feedback.GetComponentsInChildren<AudioSource>().Length==count&&feedback.GetComponentsInChildren<AudioSource>().Count(a=>a.name=="Nearby running water")==1,"Respawn/restart create no additional audio sources");
            Check(!AudioListener.pause&&Time.timeScale==1&&bed.isPlaying,"Restart restores one running loop and simulation");
            feedback.Impact(world.player.transform.position,0,"Audio check: existing BLOOM gain",0);yield return Frames(2);
            Check(feedback.GetComponentsInChildren<AudioSource>().Any(a=>a.name.StartsWith("Effect voice")&&Mathf.Abs(a.volume-.55f*.4f)<.001f),"Ability effect gain remains .22");
        }
        IEnumerator Start()
        {
            Directory.CreateDirectory(Folder);yield return Frames(12);world=FindFirstObjectByType<EFV2PuzzleWorld>();water=FindFirstObjectByType<EFV2WaterIntegration>();feedback=FindFirstObjectByType<EFDemoFeedback>();
            world.AutomatedCheck=true;world.SetPaused(false);while(world.OpeningActive)yield return null;
            bed=feedback.GetComponentsInChildren<AudioSource>().Single(a=>a.name=="Nearby running water");keyboard=InputSystem.AddDevice<Keyboard>("WaterAudioKeyboard");
            SessionState.SetString(EFV2PuzzlePlayChecks.ModeKey,"water-audio");SessionState.SetFloat(EFV2PuzzlePlayChecks.ModeKey+".deadline",(float)EditorApplication.timeSinceStartup+120);
            EFDemoReview.Start("../play-review-20261109/water-audio/nearby-water-review");
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
