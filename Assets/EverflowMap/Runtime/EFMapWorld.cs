using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using EFInput = EverflowMap.EFMapInput;

namespace EverflowMap
{
    [Serializable]
    public sealed class EFMapStage
    {
        public string hint;
        public Transform spawn;
        [Tooltip("P1-P5: far-side passage center. P6: reference only; never completes the ending by proximity.")]
        public Transform arrival;
        [Tooltip("Full size in arrival-local space. Keep the near boundary beyond the obstacle and include jump/landing height.")]
        public Vector3 arrivalSize = new Vector3(8, 6, 8);
        public EFMapNode[] nodes = Array.Empty<EFMapNode>();
        public EFMapNode[] requirements = Array.Empty<EFMapNode>();
        public EFMapAnimation[] restoration = Array.Empty<EFMapAnimation>();
    }
    [DefaultExecutionOrder(-200)]
    public sealed class EFMapWorld : MonoBehaviour
    {
        public EFMapPlayer player;
        public EFMapStage[] stages = Array.Empty<EFMapStage>();
        public EFMapNode[] debris = Array.Empty<EFMapNode>();
        public EFMapNode gate4;
        public Renderer sideWater;
        public Transform wheel;
        public Vector3 wheelAxis = Vector3.forward;
        public GameObject[] finalFlow = Array.Empty<GameObject>();
        public Camera endingCamera;
        public Camera openingCamera;
        public bool[] Completed { get; private set; }
        public int StageIndex { get; private set; }
        public string Message { get; set; }
        public bool Ended { get; private set; }
        public bool Paused { get; private set; }
        public bool CinematicActive => cinematicCamera != null;
        public bool GameInputActive => !Paused && !Ended && !CinematicActive;
        public bool WantsCursorLocked => GameInputActive;
        public bool WheelRunning { get; private set; }
        public Vector3 Checkpoint { get; private set; }
        public float FallFloor => StageIndex == 3 ? -5 : Mathf.Max(-10, Checkpoint.y - 10);
        public float PlaySeconds => Time.time - startedAt;
        Quaternion wheelHome;
        float waterProgress, endingAt, startedAt;
        bool endingStarted;
        float openingUntil;
        Camera cinematicCamera;
        bool endingShown;
        float timeScaleBeforePause = 1;
        GUIStyle label;
        void Awake() { Completed = new bool[6]; wheelHome = wheel.localRotation; }
        void Start()
        {
            StageIndex = 0; Checkpoint = stages[0].spawn.position;
            player.Teleport(Checkpoint); Message = stages[0].hint; startedAt = Time.time;
            foreach (var go in finalFlow) go.SetActive(false);
            endingCamera.enabled = false;
            if (openingCamera != null)
            {
                openingUntil = Time.time + 3;
                BeginCinematic(openingCamera);
            }
            UpdateCursor();
        }
        void Update()
        {
            if (EFInput.KeyDown(KeyCode.Escape) && !Ended) SetPaused(!Paused);
            if (Paused) return;
            if (cinematicCamera == openingCamera && openingCamera != null)
            {
                if (Time.time < openingUntil) return;
                EndCinematic();
            }
            if (EFInput.KeyDown(KeyCode.R) && GameInputActive) Retry();
            sideWater.enabled = gate4.Done;
            if (Ended) return;
            if (StageIndex == 5 && ConditionsMet(5))
            {
                if (!endingStarted) { endingStarted = true; waterProgress = 0; Message = "The waterway is connected. Water is reaching the wheel."; }
                waterProgress += Time.deltaTime;
                for (int i = 0; i < finalFlow.Length; i++) finalFlow[i].SetActive(waterProgress >= i * .4f);
                if (waterProgress >= finalFlow.Length * .4f + .5f && !WheelRunning)
                {
                    WheelRunning = true; Completed[5] = true;
                    foreach (var restore in stages[5].restoration) restore.Begin();
                    endingAt = Time.time;
                    Message = "EVERFLOW — the world flows again.";
                }
            }
            else if (endingStarted && !WheelRunning)
            { endingStarted = false; foreach (var flow in finalFlow) flow.SetActive(false); }
            if (WheelRunning)
            {
                wheel.Rotate(wheelAxis, -25 * Time.deltaTime, Space.Self);
                if (Time.time - endingAt > 4 && !endingShown)
                {
                    endingShown = true; Ended = true; BeginCinematic(endingCamera);
                }
            }
        }
        void LateUpdate()
        {
            // Execution order -200: after player movement, before the player's
            // LateUpdate ability ray/click. A cast on the arrival frame is unlocked.
            if (GameInputActive && StageIndex < 5 && IsAtArrival(StageIndex))
            {
                // Passage is evidence of traversal; never recheck pose/use history here.
                if (!TryArrive(StageIndex)) TryPassage(StageIndex);
            }
            if (Ended && WheelRunning) wheel.Rotate(wheelAxis, -25 * Time.deltaTime, Space.Self);
        }
        public void BeginCinematic(Camera camera)
        {
            if (camera == null || camera == player.view) return;
            if (cinematicCamera != null) cinematicCamera.enabled = false;
            cinematicCamera = camera;
            player.view.enabled = player.follow.enabled = player.enabled = false;
            camera.enabled = true;
            UpdateCursor();
        }
        public void EndCinematic()
        {
            if (cinematicCamera != null) cinematicCamera.enabled = false;
            cinematicCamera = null;
            player.view.enabled = player.follow.enabled = player.enabled = true;
            player.follow.Snap();
            UpdateCursor();
        }
        public void ResumeExploration()
        {
            if (!Ended) return;
            Ended = false;
            EndCinematic();
        }
        public void SetPaused(bool paused)
        {
            if (Paused == paused || Ended) return;
            if (paused) { timeScaleBeforePause = Time.timeScale; Time.timeScale = 0; }
            else { Time.timeScale = timeScaleBeforePause; player.follow.DiscardLookDelta(); }
            Paused = paused;
            UpdateCursor();
        }
        void UpdateCursor()
        {
            Cursor.lockState = WantsCursorLocked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !WantsCursorLocked;
        }
        void OnApplicationFocus(bool focused)
        {
            // Batch probes have no focused game window. Interactive play pauses on focus loss.
            if (!focused && !Application.isBatchMode) SetPaused(true);
        }
        void OnDisable()
        {
            if (Paused) { Time.timeScale = timeScaleBeforePause; Paused = false; }
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
        public bool ConditionsMet(int index)
        {
            if (index < 0 || index >= stages.Length) return false;
            if (index == 0 && (!player.FlowerUsed || !player.VineUsed || player.Riding)) return false;
            if (index == 1 && player.SteppedDebris.Any(v => !v)) return false;
            if (index == 3 && (!player.SlideUsed || player.Riding)) return false;
            return stages[index].requirements.All(n => n != null && n.Satisfied);
        }
        public bool TryArrive(int index)
        {
            if (!CanCompleteTraversal(index) || !ConditionsMet(index) || !IsAtArrival(index)) return false;
            return CompleteTraversal(index);
        }
        public bool IsAtArrival(int index)
        {
            if (index < 0 || index >= stages.Length || stages[index].arrival == null) return false;
            var stage = stages[index];
            return new Bounds(Vector3.zero, stage.arrivalSize).Contains(stage.arrival.InverseTransformPoint(player.transform.position));
        }
        bool CanCompleteTraversal(int index) => Completed != null && index >= 0 && index < stages.Length - 1 &&
            index == StageIndex && !Completed[index];
        public bool TryPassage(int index)
        {
            if (!CanCompleteTraversal(index) || !IsAtArrival(index)) return false;
            return CompleteTraversal(index);
        }
        bool CompleteTraversal(int index)
        {
            Completed[index] = true;
            foreach (var action in stages[index].restoration) action.Begin();
            if (index < 5)
            {
                StageIndex++; Checkpoint = stages[StageIndex].spawn.position;
                Message = stages[StageIndex].hint;
            }
            return true;
        }
        public void SetCheckpoint(Vector3 position)
        {
            Checkpoint = position; Message = "Checkpoint saved. R retries this puzzle; previous restorations remain.";
        }
        public void Respawn()
        {
            foreach (var node in stages[StageIndex].nodes) node.ResetRide();
            player.Teleport(Checkpoint); Message = "Returned to checkpoint. Puzzle states preserved.";
        }
        public void Retry()
        {
            if (Completed[StageIndex]) return;
            foreach (var node in stages[StageIndex].nodes) node.ResetNode();
            foreach (var checkpoint in GetComponentsInChildren<EFMapCheckpoint>()) checkpoint.ResetIfStage(StageIndex);
            if (StageIndex == 0) player.FlowerUsed = player.VineUsed = false;
            if (StageIndex == 1) Array.Clear(player.SteppedDebris, 0, player.SteppedDebris.Length);
            if (StageIndex == 3) player.SlideUsed = false;
            Checkpoint = stages[StageIndex].spawn.position; Respawn();
        }
        public void RestartWorld() { SetPaused(false); SceneManager.LoadScene(gameObject.scene.path, LoadSceneMode.Single); }
        public void ReturnToTitle() { SetPaused(false); SceneManager.LoadScene("Assets/EverflowMVP/Scenes/Everflow_Title.unity", LoadSceneMode.Single); }
        void OnGUI()
        {
            if (Completed == null) return;
            if (label == null) label = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
            float width = Mathf.Min(920, Screen.width - 20);
            const string controls = "WASD move | Mouse look | C recenter | Esc pause | Space jump/release | E ride | 1/2/3 ability | Click cast | R retry";
            float controlsHeight = Mathf.Max(26, label.CalcHeight(new GUIContent(controls), width - 24));
            float messageY = 42 + controlsHeight + 4;
            float messageHeight = Mathf.Max(40, label.CalcHeight(new GUIContent(Message ?? ""), width - 24));
            GUI.Box(new Rect(10, 10, width, messageY + messageHeight), "");
            GUI.Label(new Rect(22, 15, width - 24, 26), $"EVERFLOW | P{StageIndex + 1} | {Completed.Count(v => v)}/6 | {player.selected.ToString().ToUpperInvariant()}", label);
            GUI.Label(new Rect(22, 42, width - 24, controlsHeight), controls, label);
            GUI.Label(new Rect(22, messageY, width - 24, messageHeight), Message, label);
            if (GameInputActive)
                GUI.Label(new Rect(20, Screen.height - 200 * Mathf.Min(Screen.width / 1920f, Screen.height / 1080f) - 80, width, 65),
                    (player.Hovered == null ? "" : player.Hovered.Status + "\n") + player.AimReason, label);
            if (GameInputActive)
            {
                var oldColor = GUI.color;
                GUI.color = player.Hovered == null ? Color.white : player.AimReady ? Color.cyan : Color.yellow;
                GUI.DrawTexture(new Rect(Screen.width * .5f - 5, Screen.height * .5f - 1, 10, 2), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(Screen.width * .5f - 1, Screen.height * .5f - 5, 2, 10), Texture2D.whiteTexture);
                GUI.color = oldColor;
            }
            if (Paused)
            {
                float x = Screen.width / 2 - 140, y = Screen.height / 2 - 100;
                GUI.Box(new Rect(x, y, 280, 220), "PAUSED");
                if (GUI.Button(new Rect(x + 25, y + 40, 230, 42), "Resume (Esc)")) SetPaused(false);
                if (GUI.Button(new Rect(x + 25, y + 96, 230, 42), "Restart world")) RestartWorld();
                if (GUI.Button(new Rect(x + 25, y + 152, 230, 42), "Title")) ReturnToTitle();
                return;
            }
            if (!Ended) return;
            GUI.Box(new Rect(Screen.width / 2 - 225, Screen.height / 2 - 65, 450, 220), "");
            GUI.Label(new Rect(Screen.width / 2 - 200, Screen.height / 2 - 45, 400, 50), "EVERFLOW — THE WORLD FLOWS AGAIN", label);
            if (GUI.Button(new Rect(Screen.width / 2 - 195, Screen.height / 2 + 15, 185, 42), "Restart world")) RestartWorld();
            if (GUI.Button(new Rect(Screen.width / 2 + 10, Screen.height / 2 + 15, 185, 42), "Title")) ReturnToTitle();
            if (GUI.Button(new Rect(Screen.width / 2 - 195, Screen.height / 2 + 72, 390, 42), "Continue exploring")) ResumeExploration();
        }
    }
}
