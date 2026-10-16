using System.Linq;
using UnityEngine;

namespace EverflowGraybox
{
    public class EFGrayboxEnvironment : MonoBehaviour
    {
        public EFWorld world;
        public GameObject[] restoration;
        public GameObject[] exitPaths;
        public Transform wheel;
        public GameObject waterFlow;
        public Camera overview;
        public Transform plaza;
        public bool Finished { get; private set; }
        public bool OverviewActive => overview.enabled;
        Quaternion wheelHome;
        float endingTime;
        GUIStyle label;

        void Start() { wheelHome = wheel.localRotation; ResetPresentation(); }
        void Update()
        {
            for (int i = 0; i < 6; i++)
            {
                restoration[i].SetActive(world.Completed[i]);
                exitPaths[i].SetActive(world.Completed[i]);
            }
            bool all = world.Completed.All(c => c);
            waterFlow.SetActive(all);
            if (all) wheel.Rotate(Vector3.forward, -12 * Time.deltaTime, Space.Self);
            if (!OverviewActive && world.player.transform.position.y < world.stages[world.StageIndex].spawn.position.y - 9)
                world.Respawn();
            Vector3 p = world.player.transform.position - plaza.position;
            if (all && !Finished && Mathf.Abs(p.x) < 12 && p.z > -3 && p.z < 9 && Mathf.Abs(p.y) < 3)
            {
                Finished = true;
                endingTime = Time.time;
                SetOverview(true);
            }
            if (!Finished && (Application.isEditor || Debug.isDebugBuild) && EFInput.KeyDown(KeyCode.F8))
                SetOverview(!OverviewActive);
        }
        public void SetOverview(bool active)
        {
            overview.enabled = active;
            world.player.view.enabled = !active;
            world.player.follow.enabled = !active;
            world.player.enabled = !active;
        }
        public void ResetPresentation()
        {
            Finished = false;
            wheel.localRotation = wheelHome;
            waterFlow.SetActive(false);
            foreach (var go in restoration) go.SetActive(false);
            foreach (var go in exitPaths) go.SetActive(false);
            SetOverview(false);
        }
        void OnGUI()
        {
            if (world.Completed == null) return;
            if (label == null) label = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
            GUI.Box(new Rect(10, 10, Mathf.Min(960, Screen.width - 20), 142), "");
            GUI.Label(new Rect(22, 18, 900, 24), $"EVERFLOW | P{world.StageIndex + 1:00} | {world.Completed.Count(x => x)}/6 restored", label);
            GUI.Label(new Rect(22, 45, 900, 25), "WASD move | RMB orbit | Space jump/release | E ride | 1 Bloom  2 Freeze  3 Pulse | Click cast", label);
            GUI.Label(new Rect(22, 73, 900, 24), "R retry current puzzle | Walk along the restored path" + ((Application.isEditor || Debug.isDebugBuild) ? " | F8 overview" : ""), label);
            GUI.Label(new Rect(22, 101, 900, 40), world.Message, label);
            GUI.Label(new Rect(20, Screen.height - 64, Screen.width - 40, 60),
                world.player.Hovered != null ? world.player.Hovered.Status : world.stages[world.StageIndex].hint, label);
            if (OverviewActive)
            {
                for (int i = 0; i < 6; i++)
                {
                    Vector3 s = overview.WorldToScreenPoint(world.stages[i].spawn.position);
                    GUI.Label(new Rect(s.x - 20, Screen.height - s.y, 180, 24), $"P{i + 1:00}" + (world.Completed[i] ? " RESTORED" : ""), label);
                }
                if (Finished)
                {
                    GUI.Label(new Rect(Screen.width / 2 - 150, 175, 350, 30), "EVERFLOW — THE WORLD FLOWS AGAIN", label);
                    if (Time.time - endingTime > 3 && GUI.Button(new Rect(Screen.width / 2 - 80, 215, 160, 36), "Restart world")) world.RestartWorld();
                }
            }
        }
    }
}
