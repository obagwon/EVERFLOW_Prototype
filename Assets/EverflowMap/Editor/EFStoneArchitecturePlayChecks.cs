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
    public static class EFStoneArchitecturePlayChecks
    {
        const string Key="EVERFLOW.Stone.Check";
        static EFStoneArchitecturePlayChecks()
        {EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false)){SessionState.SetBool(Key,false);new GameObject("Stone architecture physical checks").AddComponent<EFStoneArchitectureProbe>();}};}
        public static void Run()
        {if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stop Play first.");SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();}
        public static void Baseline()
        {SessionState.SetBool("EVERFLOW.Stone.Baseline",true);Run();}
    }
    public sealed class EFStoneArchitectureProbe:MonoBehaviour
    {
        const string Folder="docs/play-review-20261109/stone-architecture";
        EFV2PuzzleWorld world;EFV2StoneArchitecture architecture;Keyboard keyboard;readonly StringBuilder report=new StringBuilder();int checks,failures;
        void Check(bool value,string text){checks++;if(!value)failures++;report.AppendLine((value?"PASS ":"FAIL ")+text);}
        void Own(){if(keyboard!=null&&keyboard.added)keyboard.MakeCurrent();}
        void OnEnable(){InputSystem.onAfterUpdate+=Own;Application.logMessageReceived+=Log;}
        void OnDisable(){InputSystem.onAfterUpdate-=Own;Application.logMessageReceived-=Log;}
        void Log(string text,string trace,LogType type){if(type==LogType.Exception||type==LogType.Error||type==LogType.Assert){failures++;report.AppendLine("UNITY ERROR "+text);}}
        IEnumerator Frames(int count){for(int i=0;i<count;i++)yield return null;}
        void View(Vector3 p,Vector3 target)
        {world.player.view.transform.SetPositionAndRotation(p,Quaternion.LookRotation(target-p));world.player.view.fieldOfView=58;}
        IEnumerator Walk(Vector3 from,Vector3 to,string label,bool place=true)
        {
            report.AppendLine("ISOLATED WALK "+label+" start placement="+from+" goal="+to+" (no jump, normal CharacterController)");
            if(place)world.player.Teleport(from+Vector3.up*1.25f);var direction=Vector3.ProjectOnPlane(to-from,Vector3.up).normalized;
            View(from-direction*6+Vector3.up*4,to+Vector3.up);yield return new WaitForSeconds(.5f);
            int version=world.player.TeleportVersion;float start=Time.unscaledTime;
            while(Time.unscaledTime-start<15)
            {
                var delta=Vector3.ProjectOnPlane(to-world.player.transform.position,Vector3.up);if(delta.magnitude<.32f)break;
                var forward=Vector3.ProjectOnPlane(world.player.view.transform.forward,Vector3.up).normalized;var right=Vector3.Cross(Vector3.up,forward);var keys=new System.Collections.Generic.List<Key>();
                float f=Vector3.Dot(delta.normalized,forward),r=Vector3.Dot(delta.normalized,right);
                if(f>.3f)keys.Add(Key.W);if(f<-.3f)keys.Add(Key.S);if(r>.35f)keys.Add(Key.D);if(r<-.35f)keys.Add(Key.A);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys.ToArray()));yield return null;
            }
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return new WaitForSeconds(.3f);
            var feet=world.player.transform.position-Vector3.up;
            Check(Vector3.ProjectOnPlane(feet-to,Vector3.up).magnitude<.5f&&Mathf.Abs(feet.y-to.y)<.55f,label+" reachable without jump, feet="+feet.ToString("F3"));
            Check(world.player.TeleportVersion==version,label+" no automatic fall recovery during normal traversal");
        }
        IEnumerator Cases()
        {
            report.AppendLine("Isolated start placements and fixed observation cameras; real synthetic WASD through the normal motor. Separate full input routes validate puzzle progression.");
            Check(architecture.Flights==8&&architecture.DressedDecks==16&&architecture.Masonry.Count==24,"Eight fixed flights and sixteen decks dressed; grown root paths excluded");
            Check(architecture.Masonry.All(c=>c.sharedMesh==c.GetComponent<MeshFilter>().sharedMesh&&(!c.enabled||c.GetComponent<MeshRenderer>().enabled)&&!c.convex),"Solid dressed stone uses its visible mesh");
            Check(architecture.Masonry.All(c=>Vector3.Distance(c.transform.lossyScale,Vector3.one)<.001f),"Generated world meshes retain unit scale under scaled deck parents");
            Check(architecture.Anchors.Count>=25&&architecture.Anchors.All(a=>a.surface!=null&&a.foot.y<=a.top.y),"Foundations/upper corridor corbels terminate on measured existing ground/wall surfaces");
            var all=world.GetComponentsInChildren<Transform>(true);
            foreach(string name in new[]{"A grown root staircase","B grown root bridge","D3 grown vine path"})
            {var root=all.First(t=>t.name==name);Check(root.GetComponentsInChildren<EFV2StoneArchitecture>().Length==0&&!root.GetComponentsInChildren<Transform>(true).Any(t=>t.name.StartsWith("Ruins masonry")),name+" retains original ability-owned growth geometry");}
            var water=FindFirstObjectByType<EFV2WaterIntegration>();var main=water.segments.Single(s=>s.label=="W_H_to_G_Fall");var v=main.preparedMesh.vertices;var uv=main.preparedMesh.uv2;
            var waterPoints=Enumerable.Range(0,v.Length).Where(i=>uv[i].y<.5f).Select(i=>main.filter.transform.TransformPoint(v[i])).Where(p=>p.z> -41&&p.z< -39&&p.y>2.8f&&p.y<3).ToArray();
            Check(waterPoints.Length>0&&waterPoints.All(p=>!Physics.OverlapSphere(p,.055f,~(1<<2),QueryTriggerInteraction.Ignore).Any(c=>architecture.Masonry.Contains(c as MeshCollider))),"Switching bridge piers and arches leave the natural creek cross-section clear");
            Check(!world.returnWalk.activeSelf,"Conditional D3 return route remains inactive before workbench arrival");
            yield return Walk(new Vector3(-25,6.1f,-52),new Vector3(-34.5f,15.15f,-58.8f),"B stone ascent");
            yield return Walk(new Vector3(-61,11.9f,-60),new Vector3(-57,15.15f,-62),"C stone ascent");
            yield return Walk(new Vector3(-60,6.1f,-40),new Vector3(-28,6.1f,-40),"B-C arched switching bridge");
            yield return Walk(EFV2UndergroundWorks.D5StairFoot,EFV2UndergroundWorks.D5StairTop,"D3-D5 stone access");
            yield return Walk(EFV2UndergroundWorks.D5StairTop,EFV2UndergroundWorks.D5GalleryTurn,"D5 service-junction turn after ascent (no reposition)",false);
            yield return Walk(new Vector3(-70.1f,-5.95f,-65),new Vector3(-74,-8.9f,-64.8f),"D5 bypass stone descent");
            world.returnWalk.SetActive(true);yield return Walk(new Vector3(-45.15f,-7.45f,-59.2f),new Vector3(-50,-11.5f,-59.2f),"Conditional D3 return stairs (forced availability fixture)");
            yield return Walk(new Vector3(-58.6f,-5.95f,-63.9f),new Vector3(-53.4f,-5.95f,-63.9f),"D5 unchanged dry channel crossing");
            yield return Walk(new Vector3(-82.3f,-11.9f,-47),new Vector3(-82.3f,-17.35f,-12),"Stone exterior descent");
            var views=new[]{new[]{new Vector3(-15,17,-36),new Vector3(-30,10,-55)},new[]{new Vector3(-46,12,-25),new Vector3(-44,5,-40)},new[]{new Vector3(-71,17,-52),new Vector3(-59,13,-61)},new[]{new Vector3(-35,-1,-54),new Vector3(-54,-6,-65)}};
            string[] names={"after-b-stairs","after-switching-bridge","after-c-landing","after-d5"};
            for(int i=0;i<views.Length;i++){View(views[i][0],views[i][1]);yield return new WaitForSeconds(.5f);ScreenCapture.CaptureScreenshot(Folder+"/"+names[i]+".png");yield return new WaitForSeconds(.5f);}
            int count=architecture.Masonry.Count;world.Restart();yield return Frames(8);
            Check(!world.returnWalk.activeSelf&&architecture.Masonry.Count==count&&FindObjectsByType<EFV2StoneArchitecture>(FindObjectsSortMode.None).Length==1,"Restart resets conditional route without duplicating masonry");
        }
        IEnumerator Baseline()
        {
            report.AppendLine("Comparison fixture: new masonry colliders and renderers disabled, original imported terrain and original stair boxes/ramp unchanged.");
            foreach(var c in architecture.Masonry){c.enabled=false;c.GetComponent<MeshRenderer>().enabled=false;}
            yield return Walk(new Vector3(-61,11.9f,-60),new Vector3(-57,15.15f,-62),"C original physics baseline");
            yield return Walk(new Vector3(-40,-7.45f,-62),new Vector3(-46,-5.95f,-66),"D3-D5 original physics baseline");
        }
        IEnumerator Start()
        {
            Directory.CreateDirectory(Folder);yield return Frames(12);world=FindFirstObjectByType<EFV2PuzzleWorld>();architecture=world.GetComponentInChildren<EFV2StoneArchitecture>();world.AutomatedCheck=true;world.SetPaused(false);while(world.OpeningActive)yield return null;
            world.player.cinemachineRig.brain.enabled=false;world.player.cinemachineRig.SetInputActive(false);keyboard=InputSystem.AddDevice<Keyboard>("StoneWalkKeyboard");
            SessionState.SetString(EFV2PuzzlePlayChecks.ModeKey,"stone-architecture");SessionState.SetFloat(EFV2PuzzlePlayChecks.ModeKey+".deadline",(float)EditorApplication.timeSinceStartup+180);
            bool baseline=SessionState.GetBool("EVERFLOW.Stone.Baseline",false);SessionState.EraseBool("EVERFLOW.Stone.Baseline");
            EFDemoReview.Start("../play-review-20261109/stone-architecture/"+(baseline?"baseline-comparison":"stone-structure-review"));var stack=new System.Collections.Generic.Stack<IEnumerator>();stack.Push(baseline?Baseline():Cases());
            while(stack.Count>0)
            {bool more;object current=null;try{more=stack.Peek().MoveNext();if(more)current=stack.Peek().Current;}catch(Exception e){failures++;report.AppendLine("EXCEPTION "+e);break;}if(!more){stack.Pop();continue;}if(current is IEnumerator child){stack.Push(child);continue;}yield return current;}
            report.AppendLine("TOTAL "+checks+" checks, "+failures+" failures.");File.WriteAllText(Folder+"/"+(baseline?"baseline-checks.txt":"checks.txt"),DateTime.UtcNow.ToString("O")+"\n"+report);Debug.Log(report.ToString());
            EFDemoReview.Stop();SessionState.EraseString(EFV2PuzzlePlayChecks.ModeKey);if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);world.AutomatedCheck=false;Destroy(gameObject);
        }
    }
}
