using System;
using System.Collections.Generic;
using EverflowPuzzleLab;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EverflowMap
{
    [Serializable]
    public sealed class EF1123Course
    {
        public string title;
        public string starId;
        public EF1123Target[] steps;
        public Transform star;
        public EF1123Motion returnGate;
        public EF1123Motion returnExit;
        [NonSerialized] public int stage;
        [NonSerialized] public bool collected;
        [NonSerialized] public Vector3 starPosition;
    }

    [Serializable]
    public sealed class EF1123SafePoint
    {
        public Transform surface;
        public int course = -1, stage;
        public bool resetFerry;
    }

    [DefaultExecutionOrder(-150)]
    public sealed class EF1123World : MonoBehaviour
    {
        static readonly Color[] AbilityColors = {new(.56f,.93f,.3f),new(.35f,.86f,1),new(1,.63f,.23f)};
        static readonly string[] AbilityLabels = {"BLOOM", "FREEZE", "FLOW"};
        const float CastRange = 18, RecallRange = 24;
        public EFMapPlayer player;
        public Transform jumpPad, jumpDestination, flowerTuft;
        public EF1123SafePoint[] checkpoints;
        public EF1123Course[] courses;
        public EF1123Target[] tutorials;
        public EF1123Motion[] motions;
        public EF1123Motion[] windmillDoors;
        public Transform[] orbs;
        public Renderer[] water;
        public Renderer[] deepWater;
        public Material windTrailMaterial;
        public EFV2FallRecovery fallRecovery;
        public Bounds[] fallZones;
        public Vector3 finishCenter;

        public int Count => collectedStars.Count;
        public bool Finished { get; private set; }
        public bool AbilityMode { get; private set; }
        public LabAbility Selected { get; private set; }
        public EF1123Target Hovered { get; private set; }
        public string Message { get; private set; }
        public int RetryCount { get; private set; }

        readonly EFV2OrbSlot<EF1123Target>[] slots = {new(), new(), new()};
        readonly HashSet<string> collectedStars = new(StringComparer.Ordinal);
        EF1123SafePoint checkpoint;
        Pose startingPose;
        EF1123Target pendingFlow, held, flight;
        Font font;
        MaterialPropertyBlock properties;
        bool paused;
        int tutorialStage;
        float motionTime, nextCast;
        Vector3 tuftRest;
        LineRenderer windTrail;
        Vector3 windOrigin, windAxis;
        float windRemaining;

        void Start()
        {
            startingPose = new Pose(player.transform.position, player.transform.rotation);
            foreach (var motion in motions) motion.Initialize();
            foreach (var course in courses) course.starPosition = course.star.position;
            properties = new MaterialPropertyBlock();
            font = LabSignFont.Get();
            var trail = new GameObject("FLOW gust trail");
            trail.transform.SetParent(transform, false);
            windTrail = trail.AddComponent<LineRenderer>();
            windTrail.sharedMaterial = windTrailMaterial;
            windTrail.positionCount = 9;
            windTrail.startWidth = .13f;
            windTrail.endWidth = .015f;
            windTrail.enabled = false;
            foreach (var sign in GetComponentsInChildren<TextMesh>(true)) LabSignFont.Apply(sign);
            if (flowerTuft != null) tuftRest = flowerTuft.position;
            player.TraversalFinished += EndTraversal;
            player.cinemachineRig.ResetView(true);
            SetInput();
            var tutorial = tutorials[0];
            Report(Near(tutorial.station.position + Vector3.up, tutorial.stationRadius, 2.1f)
                ? "전망대에서 풍차를 보세요. 뿌리 길을 만들어 계곡으로 내려갑니다."
                : "코스의 조작 발판에서 가까운 장치에 능력을 사용하세요.");
        }

        void OnDestroy()
        {
            if (player != null) player.TraversalFinished -= EndTraversal;
        }

        void SetInput()
        {
            bool walking = !paused && !AbilityMode && held == null;
            player.enabled = walking;
            player.cinemachineRig.SetInputActive(!paused && !AbilityMode);
            player.cinemachineRig.SetViewFrozen(paused || AbilityMode);
            Cursor.lockState = paused || AbilityMode ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = paused || AbilityMode;
        }

        void Update()
        {
            if (EFMapInput.KeyDown(KeyCode.Escape))
            {
                if (AbilityMode) LeaveAbility(); else { paused = !paused; SetInput(); }
                return;
            }
            if (paused) return;
            if (EFMapInput.KeyDown(KeyCode.R)) { Retry(); return; }
            if (EFMapInput.KeyDown(KeyCode.C) && !AbilityMode) player.cinemachineRig.ResetView(true);
            if (!player.Traversing)
            {
                if (EFMapInput.KeyDown(KeyCode.Alpha1)) Select(LabAbility.Bloom);
                if (EFMapInput.KeyDown(KeyCode.Alpha2)) Select(LabAbility.Freeze);
                if (EFMapInput.KeyDown(KeyCode.Alpha3)) Select(LabAbility.Flow);
            }
            if (AbilityMode) return;

            float dt = Time.deltaTime;
            motionTime += dt;
            foreach (var motion in motions) motion.Tick(dt);
            TickOrbs(dt);
            TickWind(dt);
            foreach (var r in water)
            {
                r.GetPropertyBlock(properties);
                properties.SetFloat("_WaterTime", motionTime);
                properties.SetVector("_FlowOffset", new Vector4(motionTime * .12f, motionTime * .025f, 0, 0));
                r.SetPropertyBlock(properties);
            }
            UpdateStations();
            UpdateRewards();
            if (!Finished && Count == courses.Length && Near(finishCenter, 1.35f, 1.5f))
            {
                Finished = true;
                Report("풍차 입구 도착 · 별 6개를 모두 모았습니다.");
            }
            if (fallRecovery.ShouldRecover(player, 1.6f, out _) || InCourseFallZone() || InDeepWater())
            { Retry(); return; }
            if (held == null && !player.Traversing)
            {
                if (EFMapInput.KeyDown(KeyCode.E)) TryGrab();
                if (courses[5].stage == 1 && EFMapInput.KeyDown(KeyCode.Space) &&
                    Near(jumpPad.position + Vector3.up, .95f, .8f) && player.Grounded)
                    LaunchFlower();
            }
        }

        void LateUpdate()
        {
            if (flowerTuft != null)
            {
                bool carrying = flight != null || held != null;
                flowerTuft.position = carrying ? player.transform.position + Vector3.up * 1.9f : tuftRest;
                flowerTuft.gameObject.SetActive(carrying || courses[3].stage == 2);
            }
            if (!AbilityMode) return;
            Physics.SyncTransforms();
            var pointer = Mouse.current != null
                ? Mouse.current.position.ReadValue()
                : new Vector2(Screen.width * .5f, Screen.height * .5f);
            var ray = player.view.ScreenPointToRay(pointer);
            Hovered = null;
            var hits = Physics.RaycastAll(ray, 65, ~(1 << 2), QueryTriggerInteraction.Collide);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                var target = hit.collider.GetComponentInParent<EF1123Target>();
                if (target != null) { Hovered = target; break; }
                if (!hit.collider.isTrigger) break;
            }
            if (!EFMapInput.Click) return;
            if (Hovered == null) { Report("능력 표식을 선택하세요."); return; }
            if (!CanCast(Hovered, out string reason)) { Report(reason); return; }
            if (Selected == LabAbility.Flow && pendingFlow != Hovered)
            {
                pendingFlow = Hovered;
                Report("표식의 바람 방향을 확인하고 같은 대상을 다시 클릭하세요.");
                return;
            }
            if (Time.time < nextCast) return;
            int i = (int)Selected;
            if (!slots[i].Launch(Hovered, 0, false)) return;
            orbs[i].position = player.transform.position + Vector3.up * .65f;
            nextCast = Time.time + .2f;
            LeaveAbility();
        }

        bool CanCast(EF1123Target target, out string reason)
        {
            if (target.ability != Selected)
                reason = AbilityLabels[(int)target.ability] + " 대상입니다.";
            else if (Vector3.Distance(player.transform.position, target.transform.position) > CastRange)
                reason = "사거리 밖입니다. 가까이 이동하세요.";
            else if (target.tutorial ? tutorialStage != target.order : courses[target.course].stage != target.order)
                reason = "앞 단계의 장치를 먼저 작동시키세요.";
            else if (target.requiredMotion != null && !target.requiredMotion.AtOpenPose)
                reason = "씨앗이 흙받이에 안착할 때까지 기다리세요.";
            else if (target.station != null && !Near(target.station.position + Vector3.up, target.stationRadius, 2.1f))
                reason = "조작 발판 가까이에서 능력을 사용하세요.";
            else if (target.grabFirst && held != target)
                reason = "꽃씨 옆에서 E로 손잡이를 잡으세요.";
            else
            {
                Vector3 from = player.transform.position + Vector3.up * .5f;
                Vector3 delta = target.transform.position - from;
                if (Physics.Raycast(from, delta.normalized, delta.magnitude - .04f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                    reason = "지형이나 장치가 가립니다. 조작 위치를 옮기세요.";
                else reason = null;
            }
            return reason == null;
        }

        void Select(LabAbility next)
        {
            Selected = next; AbilityMode = true;
            Hovered = pendingFlow = null;
            Report("커서로 " + AbilityLabels[(int)next] + " 대상을 선택하세요.");
            SetInput();
        }

        void LeaveAbility()
        {
            AbilityMode = false; Hovered = pendingFlow = null;
            SetInput();
        }

        void TickOrbs(float delta)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                Vector3 home = player.transform.position + Vector3.up * .8f +
                    (player.transform.right * ((i - 1) * .36f)) - player.transform.forward * .65f;
                Vector3 goal = slot.target == null ? home : slot.target.transform.position;
                if (slot.RecallIfFar(Vector3.Distance(player.transform.position, goal), RecallRange)) goal = home;
                orbs[i].position = Vector3.MoveTowards(orbs[i].position, goal, delta * 32);
                if (Vector3.Distance(orbs[i].position, goal) > .07f) continue;
                if (slot.Arrive(slot.revision)) Apply(slot.target);
                if (slot.phase == EFV2OrbPhase.Returning) slot.Returned();
            }
        }

        void Apply(EF1123Target target)
        {
            if (target.ability == LabAbility.Flow)
            {
                windOrigin = target.transform.position;
                windAxis = target.windDirection.normalized;
                windRemaining = .85f;
            }
            if (target.timedPlatform != null && !target.timedPlatform.IsAligned)
            {
                Report("잎이 착지턱과 맞는 순간 다시 얼리세요.");
                return;
            }
            if (target.flyTo != null)
            {
                held = null; SetInput();
                flight = target;
                if (!player.BeginTraversal(target.flyTo.position + Vector3.up * 1.08f, 2.45f, .95f))
                { flight = null; Report("꽃씨를 다시 잡아주세요."); }
                else Report("바람을 타고 섬으로 활공합니다.");
                return;
            }
            Advance(target);
        }

        void Advance(EF1123Target target)
        {
            if (target.tutorial) tutorialStage++;
            else courses[target.course].stage++;
            foreach (var motion in target.motions) motion.Open();
            foreach (var item in target.hide) item.SetActive(false);
            foreach (var item in target.reveal) item.SetActive(true);
            Report(target.solution);
        }

        void TryGrab()
        {
            var course = courses[3];
            if (course.stage != 2 || !player.Grounded) return;
            var target = course.steps[2];
            if (!Near(target.station.position + Vector3.up, 1.4f, 1)) return;
            held = target;
            SetInput();
            Report("꽃씨를 잡았습니다. 3 → FLOW 표식 두 번 클릭.");
        }

        void LaunchFlower()
        {
            if (player.BeginTraversal(jumpDestination.position + Vector3.up * 1.08f, 1.35f, 1.95f))
                Report("꽃이 큰 도약을 만들어 냅니다.");
        }

        void EndTraversal(bool reached)
        {
            var target = flight;
            flight = null;
            if (!reached) { Retry(); return; }
            if (target != null) Advance(target);
        }

        void UpdateStations()
        {
            if (!player.Grounded) return;
            foreach (var point in checkpoints)
                if ((point.course < 0 || courses[point.course].stage >= point.stage) &&
                    Near(point.surface.position + Vector3.up, 1.35f, .8f))
                    checkpoint = point;
        }

        bool InDeepWater()
        {
            Vector3 feet = player.transform.position - Vector3.up;
            foreach (var surface in deepWater)
            {
                if (!surface.gameObject.activeInHierarchy) continue;
                Bounds b = surface.bounds;
                if (feet.y < b.max.y - .36f && feet.y > b.max.y - 12 &&
                    feet.x > b.min.x + .1f && feet.x < b.max.x - .1f &&
                    feet.z > b.min.z + .1f && feet.z < b.max.z - .1f) return true;
            }
            return false;
        }

        bool InCourseFallZone()
        {
            foreach (var zone in fallZones)
                if (zone.Contains(player.transform.position)) return true;
            return false;
        }

        void UpdateRewards()
        {
            foreach (var course in courses)
            {
                if (course.collected) continue;
                course.star.Rotate(Vector3.up, 42 * Time.deltaTime, Space.World);
                course.star.position = course.starPosition + Vector3.up * (Mathf.Sin(motionTime * 2.5f) * .12f);
                if (course.stage != course.steps.Length || !Near(course.starPosition, 1.12f, 1.05f)) continue;
                if (!collectedStars.Add(course.starId)) continue;
                course.collected = true;
                course.star.gameObject.SetActive(false);
                if (course.returnGate != null) course.returnGate.Open();
                if (course.returnExit != null) course.returnExit.Open();
                if (Count == courses.Length)
                {
                    foreach (var gate in windmillDoors) gate.Open();
                    Report("별 6/6 · 풍차 문이 열렸습니다. 풍차 입구로 이동하세요.");
                }
                else Report(course.title + " · 별 " + Count + "/6 · 복귀로 열림");
            }
        }

        bool Near(Vector3 point, float radius, float height)
        {
            Vector3 diff = player.transform.position - point;
            return Mathf.Abs(diff.y) <= height && diff.x*diff.x + diff.z*diff.z <= radius*radius;
        }

        void TickWind(float dt)
        {
            windRemaining -= dt;
            windTrail.enabled = windRemaining > 0;
            if (!windTrail.enabled) return;
            float progress = 1 - windRemaining / .85f;
            for (int i = 0; i < windTrail.positionCount; i++)
            {
                float t = i / (windTrail.positionCount - 1f);
                windTrail.SetPosition(i, windOrigin + windAxis * (progress * 3 + t * 2.4f) +
                    Vector3.up * (.1f + Mathf.Sin((t + progress) * 5) * .18f));
            }
        }

        void Retry()
        {
            RetryCount++;
            flight = held = null;
            foreach (var slot in slots) slot.Recall();
            if (checkpoint != null && checkpoint.resetFerry && !courses[3].collected && courses[3].stage == 1)
            {
                var ferry = courses[3].steps[0];
                foreach (var m in ferry.motions) m.ResetPose();
                foreach (var item in ferry.hide) item.SetActive(true);
                foreach (var item in ferry.reveal) item.SetActive(false);
                courses[3].stage = 0;
            }
            LeaveAbility();
            var returnPose = startingPose;
            if (checkpoint != null)
            {
                var surface = checkpoint.surface;
                returnPose = new Pose(surface.position + Vector3.up * 1.08f, surface.rotation);
            }
            player.transform.rotation = returnPose.rotation;
            player.Teleport(returnPose.position);
            Report("안전한 곳으로 돌아왔습니다. 획득한 별은 유지됩니다.");
        }

        void Report(string text) { Message = text; }

        void OnGUI()
        {
            if (font == null) return;
            float w = Screen.width, h = Screen.height;
            var text = new GUIStyle(GUI.skin.label) {font = font, fontSize = 15, wordWrap = true};
            text.normal.textColor = new Color(.94f,.97f,.88f);
            var title = new GUIStyle(text) {fontSize=20, fontStyle=FontStyle.Bold};
            var hint = new GUIStyle(text) {fontSize=13};
            var shade = new Color(.055f,.12f,.1f,.89f);
            Paint(new Rect(16,16,Mathf.Min(310,w-32),83),shade);
            GUI.Label(new Rect(29,21,275,31), "별   " + Count + " / " + courses.Length, title);
            GUI.Label(new Rect(29,53,275,39), Finished ? "풍차 입구 도착" :
                Count == courses.Length ? "열린 풍차 입구로 이동하세요" : "계곡의 퍼즐을 풀어 풍차 문을 여세요", hint);
            float width = Mathf.Min(570,w-32);
            Paint(new Rect(16,h-87,width,66),shade);
            GUI.Label(new Rect(28,h-83,width-23,22),"1  BLOOM     2  FREEZE     3  FLOW     E  잡기     R  복귀",hint);
            string content = paused ? "일시 정지   Esc로 계속" : Message;
            if (AbilityMode && Hovered != null) content = Hovered.title + " · " +
                (CanCast(Hovered, out string reason) ? (pendingFlow == Hovered ? "한 번 더 클릭" : "클릭하여 사용") : reason);
            GUI.Label(new Rect(28,h-62,width-23,39),content,text);
            if (AbilityMode)
            {
                Color color = AbilityColors[(int)Selected];
                Paint(new Rect(16,h-90,width,3),color);
                var mouse = Mouse.current;
                if (mouse != null)
                {
                    var p = mouse.position.ReadValue();
                    Paint(new Rect(p.x-6,h-p.y,12,2),color);
                    Paint(new Rect(p.x,h-p.y-5,2,12),color);
                }
            }
        }

        void Paint(Rect rect, Color color)
        {
            var before = GUI.color; GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = before;
        }
    }
}

