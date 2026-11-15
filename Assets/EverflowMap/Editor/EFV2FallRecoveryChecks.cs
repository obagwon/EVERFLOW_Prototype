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
    public static class EFV2FallRecoveryChecks
    {
        const string Key="EVERFLOW.FallRecovery.Check";
        static EFV2FallRecoveryChecks()
        {
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Key,false))return;
                SessionState.SetBool(Key,false);new GameObject("Isolated physical fall recovery checks").AddComponent<EFV2FallRecoveryProbe>();
            };
        }
        [MenuItem("Tools/EVERFLOW/V2 Puzzles/Check Internal Landings And Exterior Falls")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stop Play first.");
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=EFV2PuzzleBuild.Scene)throw new Exception("Open V2 puzzle scene.");
            SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
    }

    public sealed class EFV2FallRecoveryProbe:MonoBehaviour
    {
        const string Folder="docs/play-review-20261109/fall-recovery";
        EFV2PuzzleWorld world;EFMapPlayer player;Keyboard keyboard;
        readonly StringBuilder report=new StringBuilder();int checks,failures;bool recording;
        void Check(bool pass,string message){checks++;if(!pass)failures++;report.AppendLine((pass?"PASS ":"FAIL ")+message);}
        void OnEnable(){InputSystem.onAfterUpdate+=OwnKeyboard;Application.logMessageReceived+=OnLog;}
        void OnDisable(){InputSystem.onAfterUpdate-=OwnKeyboard;Application.logMessageReceived-=OnLog;}
        void OwnKeyboard(){if(keyboard!=null&&keyboard.added)keyboard.MakeCurrent();}
        void OnLog(string message,string trace,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){failures++;report.AppendLine("UNITY ERROR "+message);}}
        IEnumerator Frames(int count){for(int i=0;i<count;i++)yield return null;}
        IEnumerator Tap(Key key)
        {InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));yield return Frames(2);InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return Frames(3);}
        void SeedSafe(string label)
        {
            world.CancelAbility();world.SetPaused(false);
            var checkpoint=world.checkpoints.First(c=>c.label==label);
            player.transform.rotation=checkpoint.transform.rotation;player.Teleport(checkpoint.safePosition);world.Reach(checkpoint);
        }
        string States()=>string.Join(";",world.targets.Select(t=>t.label+":"+t.Grown+":"+t.Frozen+":"+t.Direction));
        IEnumerator Land(string safe,Vector3 start,string label,bool pause=false)
        {
            SeedSafe(safe);float legacyLimit=world.CurrentCheckpoint.safePosition.y-8;
            var hit=Physics.RaycastAll(start,Vector3.down,55,~(1<<2),QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance).First();
            report.AppendLine("DROP "+label+" from "+start+" to "+hit.collider.name+" y="+hit.point.y.ToString("F2")+"; previous relative limit="+legacyLimit.ToString("F2"));
            player.Teleport(start);int version=player.TeleportVersion;float began=Time.unscaledTime;bool crossed=false;
            if(pause)
            {
                yield return new WaitForSeconds(.15f);yield return Tap(Key.Digit1);
                var position=player.transform.position;yield return new WaitForSecondsRealtime(.4f);
                Check(world.AbilityMode&&Time.timeScale==0&&position==player.transform.position&&player.TeleportVersion==version,"Ability selection freezes airborne player without recovery");
                yield return Tap(Key.Escape);
                Check(!world.AbilityMode&&!world.Paused&&Time.timeScale==1,"One Esc resumes the same physical fall");
            }
            while(Time.unscaledTime-began<5&&player.TeleportVersion==version)
            {
                crossed|=player.transform.position.y<legacyLimit;
                if(player.Grounded&&Time.unscaledTime-began>.2f)break;
                yield return null;
            }
            Check(player.TeleportVersion==version,label+" lands without teleport");
            Check(crossed&&player.Grounded&&Mathf.Abs(player.transform.position.y-1-hit.point.y)<.35f,label+" crosses old 8m limit and lands on the real visible floor (feet="+(player.transform.position.y-1).ToString("F2")+")");
            ScreenCapture.CaptureScreenshot(Folder+"/"+label+".png");yield return new WaitForSeconds(.3f);
        }
        IEnumerator Exterior(Vector3 start,string expected)
        {
            SeedSafe("G RUIN");player.Teleport(start);int version=player.TeleportVersion;float began=Time.unscaledTime;
            while(player.TeleportVersion==version&&Time.unscaledTime-began<4)yield return null;
            Check(player.TeleportVersion==version+1&&world.LastFallRecoveryReason==expected,expected+" recovers once after an actual gravity step");
            yield return new WaitForSeconds(.4f);
            Check(player.TeleportVersion==version+1&&player.Grounded,expected+" settles at checkpoint without repeated recovery");
        }
        IEnumerator Cases()
        {
            report.AppendLine("ISOLATED PHYSICS REGRESSION: checkpoint and drop start positions are placed explicitly. Falls/landings use the normal player motor, gravity, collisions and world recovery. This is not a continuous puzzle playthrough.");
            var states=States();
            var envelope=world.fallRecovery.playableXZBounds;
            var solids=FindObjectsByType<Collider>(FindObjectsSortMode.None).Where(c=>c.enabled&&!c.isTrigger&&c.gameObject.layer!=2).ToArray();
            Check(solids.All(c=>c.bounds.min.x>=envelope.xMin&&c.bounds.max.x<=envelope.xMax&&c.bounds.min.z>=envelope.yMin&&c.bounds.max.z<=envelope.yMax),"All active solid geometry lies inside explicit exterior recovery regions");
            // Start below the overlying terrain/roof: the high checkpoint is still remembered.
            yield return Land("G RUIN",new Vector3(-48,0,-38.7f),"G-to-D1",true);
            yield return Land("D1 BALCONY",new Vector3(-71,1,-55.5f),"D1-to-dry-hub");
            yield return Land("G RUIN",new Vector3(-84,-4,0),"G-to-garden-floor");
            Check(world.CurrentCheckpoint.label=="G RUIN"&&!world.GardenReached,"Unrestored final floor permits landing without replacing the return checkpoint or completing the garden");
            int gardenVersion=player.TeleportVersion;yield return Tap(Key.R);
            Check(player.TeleportVersion==gardenVersion+1&&Vector3.Distance(player.transform.position,world.CurrentCheckpoint.safePosition)<.4f,"R returns from unrestored garden floor to the prior safe area");
            foreach(var sample in new[]{(new Vector3(-107,4,-40),"West exterior"),(new Vector3(17,4,-40),"East exterior"),(new Vector3(-45,4,-97),"North exterior"),(new Vector3(-45,4,17),"South exterior"),(new Vector3(-5,-44,-10),"Below-map safety plane")})
                yield return Exterior(sample.Item1,sample.Item2);
            Check(States()==states,"Falls/recovery preserve existing target growth, freeze and flow states");
            int version=player.TeleportVersion;yield return Tap(Key.R);
            Check(player.TeleportVersion==version+1,"Manual R recovery still uses existing input");
            world.Restart();yield return Frames(5);
            Check(world.LastFallRecoveryReason==""&&world.InputActive&&Time.timeScale==1,"Restart clears fall diagnostics and restores exploration");
        }
        IEnumerator Start()
        {
            Directory.CreateDirectory(Folder);yield return Frames(12);
            world=FindFirstObjectByType<EFV2PuzzleWorld>();player=world.player;
            world.AutomatedCheck=true;world.SetPaused(false);while(world.OpeningActive)yield return null;
            keyboard=InputSystem.AddDevice<Keyboard>("FallRecoveryKeyboard");
            SessionState.SetString(EFV2PuzzlePlayChecks.ModeKey,"fall-recovery");SessionState.SetFloat(EFV2PuzzlePlayChecks.ModeKey+".deadline",(float)EditorApplication.timeSinceStartup+120);
            EFDemoReview.Start("../play-review-20261109/fall-recovery/physical-falls");recording=true;
            var stack=new System.Collections.Generic.Stack<IEnumerator>();stack.Push(Cases());
            while(stack.Count>0)
            {
                bool more;object current=null;
                try{more=stack.Peek().MoveNext();if(more)current=stack.Peek().Current;}
                catch(Exception e){failures++;report.AppendLine("EXCEPTION "+e);break;}
                if(!more){stack.Pop();continue;}
                if(current is IEnumerator child){stack.Push(child);continue;}yield return current;
            }
            report.AppendLine("TOTAL "+checks+" checks, "+failures+" failures.");
            File.WriteAllText(Folder+"/checks.txt",DateTime.UtcNow.ToString("O")+"\n"+report);Debug.Log(report.ToString());
            if(recording)EFDemoReview.Stop();SessionState.EraseString(EFV2PuzzlePlayChecks.ModeKey);
            if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);world.AutomatedCheck=false;Destroy(gameObject);
        }
    }
}
