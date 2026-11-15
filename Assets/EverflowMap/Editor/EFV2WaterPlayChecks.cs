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
    public static class EFV2WaterPlayChecks
    {
        const string Key="EVERFLOW.WaterChecks";
        static EFV2WaterPlayChecks()
        {
            EditorApplication.playModeStateChanged+=s=>
            {
                string mode=SessionState.GetString(Key,"");if(mode.Length==0)return;
                if(s==PlayModeStateChange.EnteredPlayMode)new GameObject("Water presentation checks - forced state, not traversal").AddComponent<EFV2WaterProbe>().integrated=mode=="integrated";
                if(s==PlayModeStateChange.EnteredEditMode){SessionState.EraseString(Key);if(!File.Exists(EFV2WaterTools.Reports+"/runtime-"+mode+".txt"))File.WriteAllText(EFV2WaterTools.Reports+"/runtime-"+mode+".txt","INCOMPLETE - Play interrupted.\n");}
            };
            EditorApplication.update+=()=>
            {
                if(SessionState.GetString(Key,"").Length==0)return;
                Application.runInBackground=true;EditorApplication.QueuePlayerLoopUpdate();
                if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+".deadline",float.MaxValue))
                {Finish(SessionState.GetString(Key,""),"FAIL timed out; not a successful Play test.\n");}
            };
        }
        public static void Run(bool integrated)
        {
            EditorSceneManager.OpenScene(integrated?EFV2PuzzleBuild.Scene:EFV2WaterTools.TestScene);
            string mode=integrated?"integrated":"isolated";string report=EFV2WaterTools.Reports+"/runtime-"+mode+".txt";
            if(File.Exists(report))File.Move(report,report+"."+DateTime.UtcNow.ToString("yyyyMMddHHmmss")+".previous");
            SessionState.SetString(Key,mode);SessionState.SetFloat(Key+".deadline",(float)EditorApplication.timeSinceStartup+120);
            EditorApplication.ExecuteMenuItem("Window/General/Game");EditorApplication.isPaused=false;EditorApplication.EnterPlaymode();
        }
        public static void Finish(string mode,string report)
        {
            File.WriteAllText(EFV2WaterTools.Reports+"/runtime-"+mode+".txt",DateTime.UtcNow.ToString("O")+"\n"+report);
            SessionState.EraseString(Key);EditorApplication.ExitPlaymode();
        }
    }
    public sealed class EFV2WaterProbe:MonoBehaviour
    {
        public bool integrated;
        EFV2WaterIntegration water;
        EFV2PuzzleWorld world;
        Camera viewCamera;
        Texture2D first,second;
        readonly StringBuilder report=new StringBuilder();
        readonly System.Collections.Generic.List<string> errors=new System.Collections.Generic.List<string>();
        int passes,failures;
        void OnEnable(){Application.logMessageReceived+=Log;}
        void OnDisable(){Application.logMessageReceived-=Log;}
        void Log(string message,string stack,LogType t){if((t==LogType.Error||t==LogType.Exception||t==LogType.Assert)&&errors.Count<12&&!errors.Contains(message))errors.Add(message);}
        void Check(bool pass,string text){report.AppendLine((pass?"PASS ":"FAIL ")+text);if(pass)passes++;else failures++;File.WriteAllText(EFV2WaterTools.Reports+"/progress-"+(integrated?"integrated":"isolated")+".txt",report.ToString());}
        IEnumerator Capture(string name,bool a)
        {
            yield return new WaitForEndOfFrame();
            var texture=ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(EFV2WaterTools.Reports+"/"+name+".png",texture.EncodeToPNG());
            if(a){if(first!=null)Destroy(first);first=texture;}else{if(second!=null)Destroy(second);second=texture;}
            yield return null;
        }
        float Difference()
        {
            var a=first.GetPixels32();var b=second.GetPixels32();double delta=0;
            for(int i=0;i<a.Length;i++)delta+=Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b);
            return (float)(delta/a.Length/3/255);
        }
        EFV2WaterSegment S(string n)=>water.segments.Single(s=>s.label==n);
        void FixedCamera(Vector3 p,Vector3 target)
        {
            if(viewCamera==null){foreach(var c in FindObjectsByType<Camera>(FindObjectsSortMode.None))c.enabled=false;viewCamera=new GameObject("Water verification viewCamera - forced view only").AddComponent<Camera>();viewCamera.nearClipPlane=.1f;viewCamera.farClipPlane=250;}
            viewCamera.transform.position=p;viewCamera.transform.LookAt(target);
        }
        IEnumerator Start()
        {
            Application.runInBackground=true;Time.timeScale=1;
            yield return null;
            water=FindFirstObjectByType<EFV2WaterIntegration>();world=FindFirstObjectByType<EFV2PuzzleWorld>();
            if(world!=null){world.AutomatedCheck=true;world.SetPaused(false);}
            while(world!=null&&world.OpeningActive)yield return null;
            yield return new WaitForSeconds(.8f);
            report.AppendLine(integrated?"INTEGRATED PLAY: explicit forced puzzle state and verification viewCamera. This is NOT input traversal.":"ISOLATED PLAY: real URP Game View, actual copied canal mesh; presentation interface state controlled by test.");
            Check(water!=null,"Water component present");
            Check(!EditorUtility.scriptCompilationFailed&&!ShaderUtil.ShaderHasError(water.waterMaterial.shader)&&water.waterMaterial.shader.isSupported,"C# and EVERFLOW shader compile/support");
            Check(water.waterMaterial.shader.name=="EVERFLOW/Water/CoolWater","Dedicated shader/material references");
            Check(water.segments.All(s=>s.filter.sharedMesh==s.preparedMesh&&s.renderer.sharedMaterial==(s.materialOverride!=null?s.materialOverride:water.waterMaterial)),"All configured surfaces use their dedicated mesh/material override");
            Check(water.segments.All(s=>s.renderer.GetComponents<Collider>().Length==0),"No solid colliders added to water surfaces");
            yield return Safe(integrated?Integrated():Isolated());
            foreach(var e in errors)report.AppendLine("UNITY ERROR "+e);failures+=errors.Count;
            report.AppendLine($"{passes} passes, {failures} failures.");
            EFV2WaterPlayChecks.Finish(integrated?"integrated":"isolated",report.ToString());
        }
        IEnumerator Safe(IEnumerator root)
        {
            var stack=new System.Collections.Generic.Stack<IEnumerator>();stack.Push(root);
            while(stack.Count>0)
            {
                object current=null;bool next=false;Exception error=null;
                try{next=stack.Peek().MoveNext();if(next)current=stack.Peek().Current;}catch(Exception e){error=e;}
                if(error!=null){Check(false,"Test exception: "+error);yield break;}
                if(!next){stack.Pop();continue;}
                if(current is IEnumerator child)stack.Push(child);else yield return current;
            }
        }
        IEnumerator Isolated()
        {
            var s=water.segments[0];
            yield return Capture("test-flow-a",true);var offset=s.offset;
            yield return new WaitForSeconds(.65f);yield return Capture("test-flow-b",false);
            float movement=Difference();Check(s.wet&&Vector3.Dot(s.offset-offset,s.direction)>0,"Advection travels in configured world downstream direction");
            Check(movement>.00005f,"Rendered flowing water changes pixels; mean RGB delta="+movement);
            water.previewFrozen=true;yield return null;yield return null;
            float clock=s.clock;offset=s.offset;
            yield return Capture("test-ice-a",true);yield return new WaitForSeconds(.6f);yield return Capture("test-ice-b",false);
            float frozenDifference=Difference();Check(s.clock==clock&&s.offset==offset,"Freeze stops shared wave/foam/caustic clock and offset");
            Check(frozenDifference<.0001f,"Frozen rendered image stable; mean RGB delta="+frozenDifference);
            water.previewFrozen=false;s.direction=-s.direction;offset=s.offset;
            yield return null;
            Check(Vector3.Distance(offset,s.offset)<.2f,"Direction change retains continuous phase instead of resetting UVs");
            yield return new WaitForSeconds(1.2f);Check(s.clock>clock&&Vector3.Dot(s.velocity,s.direction)>0,"Thaw resumes animation; reversed world flow takes effect");
            yield return Capture("test-reversed",true);
            water.previewSupplied=false;yield return null;yield return null;
            Check(!s.wet&&!s.renderer.enabled&&s.arrival==0,"No supply hides water and all foam effects");yield return Capture("test-dry",false);
            Check(Difference()>.0001f,"Supplied surface visible relative to dry canal");
            water.previewSupplied=true;water.ResetVisuals();yield return new WaitForSeconds(.12f);
            Check(s.arrival>0&&s.arrival<1,"Supply start has a moving spatial arrival front");yield return Capture("test-arrival",true);
            yield return new WaitForSeconds(.5f);Check(s.arrival==1,"Supply front settles to full flow");
            viewCamera=Camera.main;viewCamera.transform.position=new Vector3(4,2,-6);viewCamera.transform.LookAt(Vector3.zero);yield return new WaitForSeconds(.2f);yield return Capture("test-low-viewCamera",true);
            viewCamera.transform.position=new Vector3(-13,15,15);viewCamera.transform.LookAt(Vector3.zero);yield return new WaitForSeconds(.2f);yield return Capture("test-distant-viewCamera",false);
            Check(!ShaderUtil.ShaderHasError(water.waterMaterial.shader),"Shader remains valid across viewCamera angles/distances (captures require visual review)");
        }
        IEnumerator Integrated()
        {
            Check(water.segments.Length==42,"39 original water/foam meshes and three local D5 joins are integrated");
            var trough=S("W_D5_ChannelInterior");
            Check(trough.filter.transform.parent==world.channelPose.model&&!trough.wet&&S("W_D5_HeadboxOverflow").wet,
                "Misaligned D5 drains at the forebay and keeps the moving trough dry");
            Check(!world.Supply&&!S("W_D2_Exit").wet&&S("W_D2_Basin").wet,"Initial supply absent, exit dry, existing basin retains standing water");
            float clock=S("W_A_Pond").clock;
            var leaf=world.targets.First(t=>t.label=="A FLOATING LEAF");leaf.Apply(LabAbility.Freeze);
            yield return new WaitForSeconds(.15f);
            Check(!S("W_A_Pond").frozen&&S("W_A_Pond").clock>clock,"Platform structure FREEZE does not freeze pond");
            world.bypass.Apply(LabAbility.Bloom);yield return new WaitForSeconds(.7f);
            Check(world.Supply&&S("W_Bypass_1").wet&&!S("W_D5_Fixed").wet,"BLOOM bypass reads existing circuit supply/visibility");
            Check(S("W_D2_Basin").velocity.sqrMagnitude<.000001f,"Unselected D2 does not imply a flowing outlet");
            world.branch.Apply(LabAbility.Flow,0);yield return new WaitForSeconds(.6f);
            Check(S("W_D2_Dock").wet&&!S("W_D2_Wheel").wet&&!S("W_D2_Exit").wet,"D2 dock branch only");
            world.branch.Apply(LabAbility.Flow,1);yield return new WaitForSeconds(3);
            Check(S("W_D2_Wheel").wet&&!S("W_D2_Dock").wet&&world.exitGate.IsOpen,"D2 wheel branch opens existing D4 gate");
            world.branch.Apply(LabAbility.Flow,2);yield return new WaitForSeconds(.6f);
            Check(S("W_D2_Exit").wet&&S("W_Exit_Tunnel").wet&&world.Arriving,"D2 main branch and D4 latch supply actual downstream result");
            FixedCamera(new Vector3(-62,-8,-47.5f),new Vector3(-56,-11.8f,-54));yield return new WaitForSeconds(.2f);yield return Capture("map-d2-flow",true);
            world.mainIce.Apply(LabAbility.Freeze);yield return null;yield return null;
            var basin=S("W_D2_Basin");clock=basin.clock;var offset=basin.offset;
            yield return new WaitForSeconds(.4f);
            Check(basin.frozen&&basin.clock==clock&&basin.offset==offset&&world.mainIce.iceSupport.enabled,"D2 water FREEZE stops all shader animation, preserves ice collider");
            Check(!world.Arriving&&!S("W_D2_Exit").wet&&!S("W_Exit_Tunnel").wet,"Ice block removes actual downstream water effects");yield return Capture("map-d2-ice",false);
            world.mainIce.Undo(LabAbility.Freeze);yield return new WaitForSeconds(.5f);
            Check(!basin.frozen&&basin.clock>clock&&!world.mainIce.iceSupport.enabled&&world.Arriving,"Thaw resumes actual supply and removes ice collision");
            world.Restart();yield return null;yield return null;
            Check(!world.Supply&&!world.mainIce.Frozen&&!S("W_Bypass_1").wet&&S("W_A_Pond").clock<.2f,"Puzzle restart resets visual phase and circuit-owned wet state");
            world.channel.motion.position=world.channel.destinations[0];world.channelPose.model.rotation=world.channelPose.aligned;
            yield return new WaitForSeconds(.15f);
            Check(!world.Supply&&trough.wet&&S("W_D5_IntakeMouth").wet&&!S("W_D5_Fall").wet,
                "Aligned loose trough shows the intake and inner water without downstream supply");
            world.channelStructure.Apply(LabAbility.Freeze);
            yield return new WaitForSeconds(.6f);
            Check(world.Supply&&S("W_D5_Fixed").wet&&!S("W_Bypass_1").wet,"Aligned/fixed D5 uses existing supply result");
            world.upperIce.Apply(LabAbility.Freeze);yield return new WaitForSeconds(.2f);
            Check(!world.Supply&&S("W_D5_Fixed").frozen&&trough.frozen&&!S("W_D5_Fall").wet&&world.upperIce.iceSupport.enabled,"D5 internal water freezes inside the trough and stops downstream water");
            world.upperIce.Undo(LabAbility.Freeze);world.branch.Apply(LabAbility.Flow,2);world.exitGate.root.Apply(LabAbility.Bloom);yield return new WaitForSeconds(6);
            Check(world.Supply&&world.Restored&&S("W_R_WheelPool").wet&&S("W_R_IslandFall").wet,"Thawed D5, root gate, main branch restore waterwheel and garden");
            FixedCamera(new Vector3(-94,-14,2),new Vector3(-84,-17.8f,-5));yield return new WaitForSeconds(.2f);yield return Capture("map-restored-pool",true);
            FixedCamera(new Vector3(-93,-23,12),new Vector3(-86,-23,2.65f));yield return new WaitForSeconds(.2f);yield return Capture("map-restored-waterfall",true);
            Check(water.segments.All(s=>s.kind!=EFV2WaterKind.Waterfall||s.direction==Vector3.down),"Vertical falls have world-Y downward advection, separate projection");
            Check(water.segments.All(s=>s.networkSurface||s.kind==EFV2WaterKind.Foam||s.filter.sharedMesh.colors.All(c=>c==Color.white)),"Network vertex masks or legacy white colors match the assigned shader");
            if(water.segments.Any(s=>s.networkSurface))
            {
                Check(water.segments.All(s=>s.networkSurface),"Every original water/foam segment assigned to the continuous network");
                Check(water.segments.Count(s=>!s.suppressPresentation)==18,"18 independent owners preserve supply, branch and ice visibility");
                Check(water.segments.All(s=>!s.renderer.enabled||!s.suppressPresentation),"Covered legacy renderers stay hidden during runtime");
                Check(water.segments.Where(s=>!s.suppressPresentation).All(s=>!ShaderUtil.ShaderHasError(s.renderer.sharedMaterial.shader)),"Continuous network shaders compile after actual runtime rendering");
                Check(FindObjectsByType<EFWaterImpact>(FindObjectsInactive.Include,FindObjectsSortMode.None).All(s=>s.spray.particleCount<=48),"Impact emitters respect bounded particle counts");
            }
            world.Restart();yield return new WaitForSeconds(.1f);
            Check(!world.Restored&&!world.Supply&&!S("W_R_WheelPool").wet,"Ending restart restores dry garden paths");
        }
    }
}
