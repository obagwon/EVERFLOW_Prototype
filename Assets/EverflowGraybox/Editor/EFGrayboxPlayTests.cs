using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EverflowGraybox.Editor
{
    public static class EFGrayboxPlayTests
    {
        [MenuItem("Tools/EVERFLOW/Graybox/Run Physical Smoke Tests")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new Exception("Stop Play mode first");
            EditorSceneManager.OpenScene(EFGrayboxBuilder.ScenePath);
            EFGrayboxBuilder.Validate();
            SessionState.SetBool("GrayboxTests", true);
            EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod]
        static void Register()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("GrayboxTests",false)) return;
                SessionState.SetBool("GrayboxTests",false);
                var go = new GameObject("Graybox physical test runner");
                go.AddComponent<EFGrayboxPlayProbe>();
            };
        }
    }
    public class EFGrayboxPlayProbe : MonoBehaviour
    {
        EFWorld world;
        EFPlayer player;
        int assertions;
        string report = "Unity Play Mode physical smoke tests\nAbilities are invoked by API; navigation uses the real EFPlayer and CharacterController.\n";
        void Check(bool result, string message)
        {
            report += (result ? "PASS " : "FAIL ") + message + "\n";
            Debug.Log((result ? "GRAYBOX TEST PASS " : "GRAYBOX TEST FAIL ") + message);
            if (!result) throw new Exception(message);
            assertions++;
        }
        void Pose(int stage, Vector3 local)
        {
            world.EnterStage(stage);
            player.Teleport(world.stages[stage].spawn.parent.TransformPoint(local),false);
            player.follow.yaw = world.stages[stage].spawn.parent.eulerAngles.y;
            player.follow.Snap(false);
        }
        EFNode Node(string name) => GameObject.Find(name).GetComponent<EFNode>();
        Vector3 Local(int stage) => world.stages[stage].spawn.parent.InverseTransformPoint(player.transform.position);
        IEnumerator Frames(int count, Vector2 movement = default, KeyCode press = KeyCode.None)
        {
            EFInput.TestMovement = movement; EFInput.TestPress = press;
            for (int i = 0; i < count; i++) { yield return null; EFInput.TestPress = KeyCode.None; }
            EFInput.TestMovement = Vector2.zero;
        }
        IEnumerator WalkTo(Vector3 target)
        {
            Vector3 delta = Vector3.zero;
            for (int i = 0; i < 900; i++)
            {
                if (world.environment.Finished) break;
                delta = Vector3.ProjectOnPlane(target - player.transform.position,Vector3.up);
                if (delta.magnitude < .2f) break;
                Vector3 forward = Vector3.ProjectOnPlane(player.view.transform.forward,Vector3.up).normalized;
                Vector3 right = Vector3.Cross(Vector3.up,forward);
                EFInput.TestMovement = new Vector2(Vector3.Dot(delta.normalized,right),Vector3.Dot(delta.normalized,forward));
                yield return null;
            }
            EFInput.TestMovement = Vector2.zero;
            Check(world.environment.Finished || delta.magnitude < .2f,
                world.environment.Finished ? "Final walk enters the plaza ending area: " + player.transform.position :
                "Walk connection to " + target + "; actual " + player.transform.position);
        }
        void FaceStage(int stage)
        {
            player.follow.yaw = world.stages[stage].spawn.parent.eulerAngles.y;
            player.follow.pitch = 27;
            player.follow.Snap(false);
        }
        void Capture(string name, Camera camera)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var rt = new RenderTexture(1600,1000,24);
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            var image = new Texture2D(1600,1000,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,1600,1000),0,0); image.Apply();
            Directory.CreateDirectory("docs/graybox-captures");
            File.WriteAllBytes("docs/graybox-captures/" + name + ".png",image.EncodeToPNG());
            camera.targetTexture = null; RenderTexture.active = null; rt.Release(); Destroy(rt); Destroy(image);
        }
        IEnumerator Tests()
        {
            yield return null;
            world = EFWorld.Current; player = world.player;
            EFInput.TestMode = true; Time.captureDeltaTime = 1f/60;
            yield return Frames(5);
            Capture("start",player.view); Capture("overview-initial",world.environment.overview);
            Check(world.StageIndex == 0 && world.Completed.Length == 6, "Initial stage and completion array");
            world.EnterOnFoot(2);
            Check(world.StageIndex == 0, "Out-of-order entry rejected");
            world.TryComplete(0);
            Check(!world.Completed[0], "Untouched P01 cannot complete");
            world.RestartWorld();
            yield return WalkTo(new Vector3(-10,0,-60));
            yield return WalkTo(new Vector3(-50,0,-60));
            yield return WalkTo(new Vector3(-50,0,-51));

            // Flower launch uses the shipped velocity and real CharacterController collisions.
            Pose(0,new Vector3(0,1.6f,6));
            Node("P1_Flower").Apply(Ability.Bloom);
            yield return Frames(12);
            yield return Frames(80,Vector2.zero,KeyCode.Space);
            Check(player.FlowerUsed && Local(0).y > 3.8f && Local(0).z > 10 && Local(0).z < 16,
                "P01 flower reaches upper ledge: " + Local(0));
            EFNode vine = Node("P1_Vine"); vine.Apply(Ability.Bloom);
            bool boarded = false;
            for (int i = 0; i < 360 && !boarded; i++)
            {
                EFInput.TestPress = KeyCode.E; yield return null;
                boarded = player.IsRiding;
            }
            EFInput.TestPress = KeyCode.None;
            Check(boarded, "P01 vine boards from upper ledge");
            for (int i = 0; i < 240; i++)
            {
                Vector3 handle = vine.transform.parent.InverseTransformPoint(vine.HandlePosition);
                if (handle.z > 23.5f && vine.SwingVelocity.z > 0) break;
                yield return null;
            }
            report += "Vine release position=" + Local(0) + " handle=" + vine.HandlePosition + " velocity=" + vine.SwingVelocity + "\n";
            yield return Frames(1,Vector2.zero,KeyCode.Space);
            for (int sample = 0; sample < 10; sample++)
            {
                yield return Frames(10);
                report += "Vine flight " + sample + ": " + Local(0) + " " + player.Mode + " " + world.Message + "\n";
            }
            Check(Local(0).z > 27 && Local(0).z < 39 && Local(0).y > 3.8f, "P01 vine lands beyond the gap: " + Local(0));
            yield return Frames(45,Vector2.up);
            Check(world.Completed[0], "P01 physical exit triggers completion");

            // P02 fixed stone and slate are tested with normal jumps, not position interpolation.
            yield return WalkTo(new Vector3(-50,3,-17));
            yield return WalkTo(new Vector3(-50,3,-4));
            Check(world.StageIndex == 1,"P01 to P02 advances on foot");
            FaceStage(1);
            EFNode stone = Node("P2_FallingStone"); stone.ResetNode();
            stone.Apply(Ability.Freeze);
            yield return Frames(1,Vector2.zero,KeyCode.R);
            Check(!stone.Frozen && world.Completed[0],"R recovers an incorrectly frozen stone without clearing P01");
            yield return WalkTo(new Vector3(-50,3,-4));
            stone.ResetNode();
            // Freeze when the top is one metre above the starting platform.
            for (int i = 0; i < 600 && (stone.transform.localPosition.y < .7f || stone.transform.localPosition.y > 1); i++) yield return null;
            stone.Apply(Ability.Freeze);
            yield return Frames(5);
            yield return Frames(37,Vector2.up,KeyCode.Space);
            yield return Frames(10);
            Check(player.FallingUsed && Local(1).z > 6 && Local(1).z < 10 && Local(1).y > 1.7f,
                "P02 normal jump onto frozen falling stone: " + Local(1));
            yield return Frames(48,Vector2.up,KeyCode.Space);
            yield return Frames(12);
            Check(Local(1).z > 11 && Local(1).z < 15 && Local(1).y > 3,
                "P02 stone to middle ledge: " + Local(1));
            var slate = Node("P2_Slate");
            for (int i = 0; i < 720 && !slate.Aligned; i++) yield return null;
            slate.Apply(Ability.Freeze);
            yield return Frames(200,Vector2.up);
            Check(world.Completed[1], "P02 frozen slate supports crossing and completion");

            yield return WalkTo(new Vector3(-50,5,40));
            yield return WalkTo(new Vector3(-36,5,40));
            Check(world.StageIndex == 2,"P02 to P03 advances on foot");
            FaceStage(2);
            var machine = Node("P3_Machine"); machine.Apply(Ability.Pulse);
            for (int i = 0; i < 720 && !machine.Aligned; i++) yield return null;
            machine.Apply(Ability.Freeze);
            yield return Frames(305,Vector2.up);
            Check(world.Completed[2], "P03 rotated powered bridge crosses physically");

            yield return WalkTo(new Vector3(13,5,40));
            Check(world.StageIndex == 3,"P03 to P04 advances on foot");
            FaceStage(3);
            Node("P4_Sluice").Apply(Ability.Pulse); world.RefreshPower();
            var water = Node("P4_Water"); water.Tick(0); water.Apply(Ability.Freeze);
            yield return Frames(1,Vector2.zero,KeyCode.E);
            Check(player.IsRiding, "P04 leaf boards");
            yield return Frames(230);
            Check(player.SlideUsed && Local(3).z > 32 && Local(3).z < 52 && Local(3).y > 3,
                "P04 rotated slide lands and brakes: " + Local(3));
            yield return Frames(60,Vector2.up);
            Check(world.Completed[3], "P04 landing completes");
            Capture("p04-landing",player.view);

            yield return WalkTo(new Vector3(66,1,40));
            yield return WalkTo(new Vector3(66,1,27));
            yield return WalkTo(new Vector3(55,1,27));
            yield return WalkTo(new Vector3(55,1,18));
            Check(world.StageIndex == 4,"P04 to P05 advances on foot");
            FaceStage(4);
            Node("P5_Source").Apply(Ability.Pulse); world.RefreshPower();
            Check(!Node("P5_Door").Powered,"P05 ungrown root blocks power");
            Node("P5_Root").Apply(Ability.Bloom); world.RefreshPower();
            yield return Frames(14,Vector2.right);
            yield return Frames(70,Vector2.up);
            yield return Frames(14,Vector2.left);
            yield return Frames(230,Vector2.up);
            Check(world.Completed[4],"P05 rotated root and powered door cross physically: " + Local(4));

            yield return WalkTo(new Vector3(55,1,-24));
            yield return WalkTo(new Vector3(36,1,-24));
            Check(world.StageIndex == 5,"P05 to P06 advances on foot");
            FaceStage(5);
            Node("P6_Root").Apply(Ability.Bloom); Node("P6_Source").Apply(Ability.Pulse); world.RefreshPower();
            var rotor = Node("P6_Rotor");
            for (int i=0;i<720 && !rotor.Aligned;i++) yield return null;
            rotor.Apply(Ability.Freeze); world.RefreshPower();
            Check(Node("P6_Core").Powered,"P06 grown roots and powered aligned frozen rotor supply core");
            yield return Frames(14,Vector2.right);
            yield return Frames(65,Vector2.up);
            yield return Frames(14,Vector2.left);
            yield return Frames(280,Vector2.up);
            Check(Local(5).z > 36 && Local(5).y > 1,"P06 both bridges physically support player: " + Local(5));
            Node("P6_Core").Apply(Ability.Pulse);
            // Walk around the original solid core using the final platform.
            yield return Frames(22,Vector2.right);
            yield return Frames(82,Vector2.up);
            Check(world.Completed[5],"P06 final input and exit complete");
            yield return Frames(3);
            Check(world.environment.waterFlow.activeSelf,"Six completions activate inlet and outlet");
            var before = world.environment.wheel.rotation;
            yield return Frames(30);
            Check(Quaternion.Angle(before,world.environment.wheel.rotation)>1,"Wheel rotates after six completions");
            var checkpoint = world.Checkpoint;
            player.Teleport(player.transform.position+Vector3.down*30,false);
            yield return Frames(3);
            Check(Vector3.Distance(player.transform.position,checkpoint)<1 && !player.IsRiding,"Fall respawn clears ride and preserves checkpoint");
            Check(world.Completed[0] && world.Completed[5],"Fall preserves completed areas");
            // Respawn above left us at the P06 middle checkpoint. Use the restored bridges again.
            yield return WalkTo(new Vector3(2.2f,1,-24));
            yield return WalkTo(new Vector3(2.2f,1,-26.2f));
            yield return WalkTo(new Vector3(-12,1,-26.2f));
            yield return WalkTo(new Vector3(-12,1,-24));
            yield return WalkTo(new Vector3(-12,3,-6));
            yield return WalkTo(new Vector3(0,3,-6));
            yield return WalkTo(new Vector3(0,3,0));
            yield return Frames(5);
            Check(world.environment.Finished && world.environment.OverviewActive && !player.enabled,"Safe plaza arrival starts ending overview");
            Capture("overview-restored",world.environment.overview);
            world.RestartWorld(); yield return Frames(5);
            Check(Array.TrueForAll(world.Completed,c=>!c) && !world.environment.Finished && player.enabled,"Full restart clears completion/ending and restores input");
            Check(!Node("P6_Core").Restored && !Node("P4_Water").Frozen && !Node("P1_Flower").Grown,"Full restart resets puzzle effects");
            Check(!world.environment.waterFlow.activeSelf && Array.TrueForAll(world.environment.restoration,g=>!g.activeSelf),"Full restart clears environmental restoration");
        }
        IEnumerator Start()
        {
            // Flatten nested coroutines so failures write a report and exit instead of hanging batch mode.
            var stack = new System.Collections.Generic.Stack<IEnumerator>(); stack.Push(Tests());
            bool failed = false;
            while (stack.Count > 0)
            {
                object next = null;
                try
                {
                    if (!stack.Peek().MoveNext()) { stack.Pop(); continue; }
                    next = stack.Peek().Current;
                }
                catch (Exception e) { report += e + "\n"; Debug.LogException(e); failed = true; break; }
                if (next is IEnumerator nested) stack.Push(nested); else yield return next;
            }
            EFInput.TestMode = false; Time.captureDeltaTime = 0;
            Directory.CreateDirectory("docs/graybox-captures");
            File.WriteAllText("docs/graybox-captures/play-test-results.txt",report + $"Assertions passed: {assertions}; result: {(failed ? "FAIL" : "PASS")}\n");
            if (Application.isBatchMode) EditorApplication.Exit(failed ? 1 : 0);
            else EditorApplication.ExitPlaymode();
        }
    }
}
