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
        static readonly KeyCode[] WindKeys = {KeyCode.W, KeyCode.D, KeyCode.S, KeyCode.A};
        static readonly Key[] WindArrows = {Key.UpArrow, Key.RightArrow, Key.DownArrow, Key.LeftArrow};
        const float CastRange = 18, RecallRange = 24;
        const float WindAlignment = .707f, WindDuration = .85f;
        const float HazardGrace = .22f, SafeGroundInterval = .28f;
        const int WalkableMask = ~(1 << 2);
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
        public EF1123PlayUI playUI;
        public EF1123Effects effects;
        public EFWindmillInterior interior;
        public EFV2TargetMeshCache modelAimMeshes;
        public Shader windArrowShader;

        public int Count => collectedStars.Count;
        public string CollectibleName => playUI == null ? "별" : "숨결 조각";
        public bool Finished { get; private set; }
        public bool AbilityMode { get; private set; }
        public LabAbility Selected { get; private set; }
        public EF1123Target Hovered { get; private set; }
        public string Message { get; private set; }
        public int RetryCount { get; private set; }
        public bool Paused => paused;
        public bool Cinematic { get; private set; }
        public int TutorialStage => tutorialStage;
        public bool HoldingFlower => held != null;
        public EF1123Target PendingFlow => pendingFlow;
        public bool ChoosingWind => AbilityMode && Selected == LabAbility.Flow && windCompass != null;
        public bool UsesModelAim => modelAim != null;
        public int HoveredWindDirection { get; private set; } = -1;
        public float MessageUntil { get; private set; }
        public string AimNotice => Time.unscaledTime < aimNoticeUntil ? aimNotice : null;
        public bool HasWindReceiver { get; private set; }
        public bool CanGrabFlower => flowerTuft != null && courses[3].stage == 2 && held == null &&
            !player.Traversing && player.Grounded && Near(courses[3].steps[2].station.position + Vector3.up, 1.4f, 1);
        public bool CanLaunchJumpFlower => jumpPad != null && courses[5].stage == 1 && player.Grounded &&
            Near(jumpPad.position + Vector3.up, .95f, .8f);

        readonly EFV2OrbSlot<EF1123Target>[] slots = {new(), new(), new()};
        readonly EF1123ModelAim.Contact?[] orbContacts = new EF1123ModelAim.Contact?[3];
        readonly EF1123Target[] windPreview = new EF1123Target[4];
        readonly HashSet<string> collectedStars = new(StringComparer.Ordinal);
        EF1123ModelAim modelAim;
        EF1123ModelAim.Contact? hoverContact;
        EF1123WindCompass windCompass;
        EF1123SafePoint checkpoint;
        Pose startingPose;
        Pose? recentSafeGround;
        CharacterController body;
        EF1123Target pendingFlow, held, flight;
        Font font;
        MaterialPropertyBlock properties;
        bool paused;
        int tutorialStage;
        float motionTime, nextCast, hazardTime, safeGroundTime;
        Vector3 tuftRest;
        LineRenderer windTrail;
        Vector3 windFacing, windOrigin, windAxis;
        float windRemaining, windLength;
        float aimNoticeUntil;
        string aimNotice;

        void Start()
        {
            startingPose = new Pose(player.transform.position, player.transform.rotation);
            body = player.GetComponent<CharacterController>();
            if (modelAimMeshes != null) modelAim = new EF1123ModelAim(this, modelAimMeshes);
            if (playUI != null && windArrowShader != null) windCompass = new EF1123WindCompass(transform, windArrowShader);
            foreach (var motion in motions) motion.Initialize();
            if (interior == null)
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
            if (interior != null)
            {
                Report("풍차가 멈췄습니다. 초록 싹을 키워 끊긴 축을 이어보세요.");
                return;
            }
            var tutorial = tutorials[0];
            Report(Near(tutorial.station.position + Vector3.up, tutorial.stationRadius, 2.1f)
                ? (playUI != null ? "WASD 이동 · 마우스 시점 · 1로 뿌리 성장" :
                    "전망대에서 풍차를 보세요. 뿌리 길을 만들어 계곡으로 내려갑니다.")
                : "코스의 조작 발판에서 가까운 장치에 능력을 사용하세요.");
        }

        void OnDestroy()
        {
            if (player != null) player.TraversalFinished -= EndTraversal;
            windCompass?.Dispose();
        }

        void SetInput()
        {
            bool ended = Finished && playUI != null;
            bool walking = !paused && !AbilityMode && !Cinematic && !ended && held == null;
            player.enabled = walking;
            player.cinemachineRig.SetInputActive(!paused && !AbilityMode && !Cinematic && !ended);
            player.cinemachineRig.SetViewFrozen(paused || AbilityMode || ended);
            bool showCursor = paused || AbilityMode || ended;
            Cursor.lockState = showCursor ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = showCursor;
        }

        public void SetPaused(bool value)
        {
            paused = value;
            SetInput();
        }

        internal void SetCinematic(bool value)
        {
            Cinematic = value;
            SetInput();
        }

        void Update()
        {
            if (Finished && playUI != null) return;
            if (playUI != null && playUI.HasControls && !Cinematic && EFMapInput.KeyDown(KeyCode.F1))
            {
                if (AbilityMode) LeaveAbility();
                playUI.ToggleControls();
                return;
            }
            if (EFMapInput.KeyDown(KeyCode.Escape))
            {
                if (playUI != null && playUI.ControlsOpen) playUI.ToggleControls();
                else if (AbilityMode) LeaveAbility();
                else SetPaused(!paused);
                return;
            }
            if (paused) return;
            if (EFMapInput.KeyDown(KeyCode.R)) { Retry(); return; }
            if (EFMapInput.KeyDown(KeyCode.C) && !AbilityMode && !Cinematic) player.cinemachineRig.ResetView(true);
            if (!player.Traversing && !Cinematic)
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
            if (Cinematic)
            {
                player.FollowSupportDuringCutaway();
                return;
            }
            if (UpdateRecovery(dt)) return;
            UpdateStations();
            if (interior != null)
            {
                interior.Tick(dt, courses[0].stage == courses[0].steps.Length);
                return;
            }
            UpdateRewards();
            if (!Finished && Count == courses.Length && Near(finishCenter, 1.35f, 1.5f))
            {
                Finished = true;
                Report(playUI != null ? "풍차 입구 도착 · 이제 안쪽 순환 장치를 되살릴 차례입니다." :
                    "풍차 입구 도착 · 별 6개를 모두 모았습니다.");
                if (playUI != null) SetInput();
                effects?.Finish();
                return;
            }
            if (held == null && !player.Traversing)
            {
                if (EFMapInput.KeyDown(KeyCode.E)) TryGrab();
                if (EFMapInput.KeyDown(KeyCode.Space) && CanLaunchJumpFlower)
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
            if (ChoosingWind)
            {
                HasWindReceiver = false;
                for (int direction = 0; direction < windPreview.Length; direction++)
                {
                    windPreview[direction] = WindReceiver(direction);
                    HasWindReceiver |= windPreview[direction] != null;
                }
                int hoveredDirection = windCompass.Show(this, ray);
                if (hoveredDirection >= 0 && hoveredDirection != HoveredWindDirection) aimNoticeUntil = 0f;
                HoveredWindDirection = hoveredDirection;
                var keyboard = Keyboard.current;
                for (int direction = 0; direction < WindKeys.Length; direction++)
                    if (EFMapInput.KeyDown(WindKeys[direction]) ||
                        (keyboard != null && keyboard[WindArrows[direction]].wasPressedThisFrame))
                    {
                        SendWind(direction);
                        return;
                    }
                if (EFMapInput.Click)
                {
                    if (HoveredWindDirection >= 0) SendWind(HoveredWindDirection);
                    else RejectAim("바람을 보낼 화살표를 클릭하세요.");
                }
                return;
            }
            var previousTarget = Hovered;
            hoverContact = null;
            if (UsesModelAim)
            {
                Hovered = modelAim.Pick(ray, 65f, Selected, out var contact);
                if (Hovered != null) hoverContact = contact;
            }
            else Hovered = PickMarker(ray);
            if (Hovered != null && Hovered != previousTarget) aimNoticeUntil = 0f;
            if (!EFMapInput.Click) return;
            if (Hovered == null)
            {
                RejectAim(UsesModelAim ? "장치에 닿지 않았어요. 빛나는 모델 표면을 클릭하세요." : "능력 표식을 선택하세요.");
                return;
            }
            if (!CanCast(Hovered, out string reason)) { RejectAim(reason); return; }
            if (Selected == LabAbility.Flow && pendingFlow != Hovered)
            {
                pendingFlow = Hovered;
                effects?.ChooseFlow();
                Report("표식의 바람 방향을 확인하고 같은 대상을 다시 클릭하세요.");
                return;
            }
            int i = (int)Selected;
            if (Time.time < nextCast || !slots[i].Launch(Hovered, 0, false))
            {
                RejectAim("정령이 돌아오는 중입니다. Esc로 조준을 닫고 잠시 기다리세요.");
                return;
            }
            orbContacts[i] = hoverContact;
            orbs[i].position = player.transform.position + Vector3.up * .65f;
            effects?.Launch(orbs[i]);
            nextCast = Time.time + .2f;
            LeaveAbility();
        }

        static EF1123Target PickMarker(Ray ray)
        {
            var hits = Physics.RaycastAll(ray, 65f, WalkableMask, QueryTriggerInteraction.Collide);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                var target = hit.collider.GetComponentInParent<EF1123Target>();
                if (target != null) return target;
                if (!hit.collider.isTrigger) break;
            }
            return null;
        }

        public Vector3 WindDirection(int choice)
        {
            var right = Vector3.Cross(Vector3.up, windFacing);
            return choice switch { 0 => windFacing, 1 => right, 2 => -windFacing, _ => -right };
        }

        public EF1123Target PreviewWindReceiver(int choice) => windPreview[choice];

        public string WindSetupHint()
        {
            EF1123Target nearby = null;
            float nearest = 81f;
            foreach (var course in courses)
                for (int step = course.stage; step < course.steps.Length; step++)
                {
                    var target = course.steps[step];
                    if (target.ability != LabAbility.Flow || !target.isActiveAndEnabled) continue;
                    var anchor = target.station != null ? target.station.position + Vector3.up : target.transform.position;
                    float distance = (anchor - player.transform.position).sqrMagnitude;
                    if (distance >= nearest) continue;
                    nearby = target;
                    nearest = distance;
                }
            if (nearby == null) return "근처에 반응할 장치가 없어요.";
            if (!CanCast(nearby, out string reason)) return reason;
            return "Esc로 나와 " + (nearby.grabFirst ? "착지할 곳" : "장치") + " 쪽을 바라보세요.";
        }

        public EF1123Target WindReceiver(int choice)
        {
            Vector3 direction = WindDirection(choice);
            EF1123Target receiver = null;
            float nearest = float.PositiveInfinity;
            foreach (var course in courses)
            {
                if (course.stage >= course.steps.Length) continue;
                var target = course.steps[course.stage];
                if (target.ability != LabAbility.Flow || !target.isActiveAndEnabled) continue;
                Vector3 toward = Horizontal(target.transform.position - player.transform.position);
                if (toward.sqrMagnitude >= nearest) continue;
                Vector3 travel = toward;
                if (target.flyTo != null)
                    travel = Horizontal(target.flyTo.position - player.transform.position);
                else if (target.windMovesFirstMotion && target.motions.Length != 0)
                {
                    var motion = target.motions[0];
                    var parent = motion.transform.parent;
                    travel = Horizontal(parent == null ? motion.offset : parent.TransformVector(motion.offset));
                    // Objects within arm's reach can be pushed along their track from the side.
                    if (toward.sqrMagnitude > 1.25f * 1.25f && Vector3.Dot(direction, toward.normalized) < WindAlignment)
                        continue;
                }
                if (travel.sqrMagnitude < .0025f) travel = Horizontal(target.windDirection);
                if (Vector3.Dot(direction, travel.normalized) < WindAlignment || !CanCast(target, out _)) continue;
                receiver = target;
                nearest = toward.sqrMagnitude;
            }
            return receiver;
        }

        public void SendWind(int choice)
        {
            if (!ChoosingWind || choice < 0 || choice >= WindKeys.Length || Time.time < nextCast) return;
            Physics.SyncTransforms();
            var direction = WindDirection(choice);
            var target = WindReceiver(choice);
            var origin = player.transform.position + Vector3.up * .55f;
            const float visibleRange = 6f;
            float length = Physics.Raycast(origin, direction, out var wall, visibleRange,
                WalkableMask, QueryTriggerInteraction.Ignore) ? wall.distance : visibleRange;
            nextCast = Time.time + .2f;
            LeaveAbility();
            StartWind(origin, direction, length);
            if (target != null) Apply(target, false);
            else
            {
                effects?.ProjectileArrived(LabAbility.Flow);
                Report("바람을 보냈습니다. 장치나 이동할 곳 쪽으로 바람을 보내세요.");
            }
        }

        static Vector3 Horizontal(Vector3 vector) => Vector3.ProjectOnPlane(vector, Vector3.up);

        public bool CanCast(EF1123Target target, out string reason)
        {
            bool pickModel = UsesModelAim && target.aimModels.Length > 0 && target.ability != LabAbility.Flow;
            Vector3? hitPoint = pickModel && target == Hovered ? hoverContact?.Point : null;
            if (target.ability != Selected)
                reason = AbilityLabels[(int)target.ability] + " 대상입니다.";
            else if ((!pickModel || hitPoint.HasValue) &&
                     Vector3.Distance(player.transform.position, hitPoint ?? target.transform.position) > CastRange)
                reason = "사거리 밖입니다. 가까이 이동하세요.";
            else if (target.tutorial ? tutorialStage != target.order : courses[target.course].stage != target.order)
                reason = "앞 단계의 장치를 먼저 작동시키세요.";
            else if (target.requiredMotion != null && !target.requiredMotion.AtOpenPose)
                reason = interior != null ? "수조에 물이 찰 때까지 기다리세요." : "씨앗이 흙받이에 안착할 때까지 기다리세요.";
            else if (target.station != null && !Near(target.station.position + Vector3.up, target.stationRadius, 2.1f))
                reason = "조작 발판 가까이에서 능력을 사용하세요.";
            else if (target.grabFirst && held != target)
                reason = "꽃씨 옆에서 E로 손잡이를 잡으세요.";
            else
            {
                Vector3 from = player.transform.position + Vector3.up * .5f;
                if (pickModel)
                    reason = (hitPoint.HasValue ? modelAim.CanReach(target, from, hitPoint.Value) :
                        modelAim.HasReachableModel(target, from, CastRange))
                        ? null : "모델이 가려져 있거나 너무 멉니다. 가까이에서 시점을 옮겨보세요.";
                else
                {
                    Vector3 delta = target.transform.position - from;
                    reason = Physics.Raycast(from, delta.normalized, delta.magnitude - .04f, WalkableMask, QueryTriggerInteraction.Ignore)
                        ? "지형이나 장치가 가립니다. 조작 위치를 옮기세요." : null;
                }
            }
            return reason == null;
        }

        void Select(LabAbility next)
        {
            Selected = next; AbilityMode = true;
            Hovered = pendingFlow = null;
            hoverContact = null;
            aimNoticeUntil = 0f;
            HoveredWindDirection = -1;
            if (!ChoosingWind) windCompass?.Hide();
            windRemaining = 0f;
            windTrail.enabled = false;
            windFacing = Horizontal(player.transform.forward).normalized;
            effects?.Select();
            Report(ChoosingWind ? "캐릭터가 보는 방향을 기준으로 바람을 고르세요." :
                "커서로 " + AbilityLabels[(int)next] + " 대상을 선택하세요.");
            SetInput();
        }

        void LeaveAbility()
        {
            AbilityMode = false; Hovered = pendingFlow = null;
            hoverContact = null;
            aimNoticeUntil = 0f;
            HoveredWindDirection = -1;
            windCompass?.Hide();
            SetInput();
        }

        void TickOrbs(float delta)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                Vector3 home = player.transform.position + Vector3.up * .8f +
                    (player.transform.right * ((i - 1) * .36f)) - player.transform.forward * .65f;
                var contact = orbContacts[i];
                Vector3 goal = slot.target == null ? home : contact?.Point ?? slot.target.transform.position;
                if (slot.RecallIfFar(Vector3.Distance(player.transform.position, goal), RecallRange)) goal = home;
                orbs[i].position = Vector3.MoveTowards(orbs[i].position, goal, delta * 32);
                if (Vector3.Distance(orbs[i].position, goal) > .07f) continue;
                if (slot.Arrive(slot.revision)) Apply(slot.target, hitPoint: contact?.Point);
                if (slot.phase == EFV2OrbPhase.Returning)
                {
                    slot.Returned();
                    orbContacts[i] = null;
                }
            }
        }

        void Apply(EF1123Target target, bool startWindAtTarget = true, Vector3? hitPoint = null)
        {
            effects?.ProjectileArrived(target.ability);
            if (target.ability == LabAbility.Flow && startWindAtTarget)
                StartWind(target.transform.position, target.windDirection.normalized, 5.4f);
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
                else
                {
                    effects?.Impact(target);
                    Report("바람을 타고 섬으로 활공합니다.");
                }
                return;
            }
            Advance(target, hitPoint);
        }

        void Advance(EF1123Target target, Vector3? hitPoint = null)
        {
            if (target.tutorial) tutorialStage++;
            else courses[target.course].stage++;
            foreach (var motion in target.motions) motion.Open();
            foreach (var item in target.hide) item.SetActive(false);
            foreach (var item in target.reveal) item.SetActive(true);
            Report(target.solution);
            if (target.flyTo == null) effects?.Impact(target, hitPoint);
        }

        void TryGrab()
        {
            if (!CanGrabFlower) return;
            held = courses[3].steps[2];
            SetInput();
            effects?.ChooseFlow();
            Report(windCompass != null ? "꽃씨를 잡았습니다. 3 → 섬 쪽으로 바람 보내기." :
                "꽃씨를 잡았습니다. 3 → FLOW 표식 두 번 클릭.");
        }

        void LaunchFlower()
        {
            if (player.BeginTraversal(jumpDestination.position + Vector3.up * 1.08f, 1.35f, 1.95f))
            {
                effects?.Jump();
                Report("꽃이 큰 도약을 만들어 냅니다.");
            }
        }

        void EndTraversal(bool reached)
        {
            var target = flight;
            flight = null;
            if (!reached) { RecoverFromFall(); return; }
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
            Vector3 feet = FeetPosition();
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
                effects?.Reward(course.starPosition, Count == courses.Length);
                if (Count == courses.Length)
                {
                    foreach (var gate in windmillDoors) gate.Open();
                    Report(CollectibleName + " 6/6 · 풍차 문이 열렸습니다.");
                }
                else Report(course.title + " · " + CollectibleName + " " + Count + "/6 · 복귀로 열림");
            }
        }

        bool Near(Vector3 point, float radius, float height)
        {
            Vector3 diff = player.transform.position - point;
            return Mathf.Abs(diff.y) <= height && diff.x*diff.x + diff.z*diff.z <= radius*radius;
        }

        void StartWind(Vector3 origin, Vector3 axis, float length)
        {
            windOrigin = origin;
            windAxis = axis;
            windLength = length;
            windRemaining = WindDuration;
            TickWind(0);
        }

        void TickWind(float dt)
        {
            windRemaining -= dt;
            windTrail.enabled = windRemaining > 0;
            if (!windTrail.enabled) return;
            float progress = 1 - windRemaining / WindDuration;
            for (int i = 0; i < windTrail.positionCount; i++)
            {
                float t = i / (windTrail.positionCount - 1f);
                windTrail.SetPosition(i, windOrigin + windAxis * (windLength * (progress * .55f + t * .45f)) +
                    Vector3.up * (.1f + Mathf.Sin((t + progress) * 5) * .18f));
            }
        }

        bool UpdateRecovery(float delta)
        {
            if (player.Traversing)
            {
                hazardTime = safeGroundTime = 0;
                return false;
            }

            var feet = FeetPosition();
            // A platform can sit beyond the map's original bounds in the editing scene.
            bool outside = !player.Grounded && fallRecovery.ShouldRecover(player, 1.6f, out _) &&
                !Physics.Raycast(feet + Vector3.up * .18f, Vector3.down, 2.4f,
                    WalkableMask, QueryTriggerInteraction.Ignore);
            bool hazard = outside || InCourseFallZone() || InDeepWater();
            hazardTime = hazard ? hazardTime + delta : 0;
            if (!hazard && checkpoint == null && player.Grounded)
                RememberSafeGround(feet, delta);
            else
                safeGroundTime = 0;

            if (hazardTime < HazardGrace) return false;
            RecoverFromFall();
            return true;
        }

        Vector3 FeetPosition() => body.transform.TransformPoint(body.center - Vector3.up * (body.height * .5f));

        void RememberSafeGround(Vector3 feet, float delta)
        {
            safeGroundTime += delta;
            if (safeGroundTime < SafeGroundInterval) return;
            safeGroundTime = 0;

            // Remember ground with room to stand, away from edges and moving devices.
            float radius = body.radius * .72f;
            if (!HasStaticFloor(feet) ||
                !HasStaticFloor(feet + Vector3.right * radius) || !HasStaticFloor(feet - Vector3.right * radius) ||
                !HasStaticFloor(feet + Vector3.forward * radius) || !HasStaticFloor(feet - Vector3.forward * radius))
                return;

            recentSafeGround = new Pose(player.transform.position + Vector3.up * .03f, player.transform.rotation);
        }

        static bool HasStaticFloor(Vector3 feet)
        {
            return Physics.Raycast(feet + Vector3.up * .18f, Vector3.down, out var floor, .46f,
                WalkableMask, QueryTriggerInteraction.Ignore) &&
                floor.normal.y > .60f && floor.collider.GetComponentInParent<EF1123Motion>() == null;
        }

        void RecoverFromFall()
        {
            effects?.Fall();
            ReturnToSafety(true);
        }

        public void Retry() => ReturnToSafety(false);

        void ReturnToSafety(bool automatic)
        {
            if (Finished && playUI != null) return;
            RetryCount++;
            hazardTime = safeGroundTime = 0;
            flight = held = null;
            paused = false;
            effects?.CancelCue();
            foreach (var slot in slots) slot.Recall();
            if (interior == null && checkpoint != null && checkpoint.resetFerry && !courses[3].collected && courses[3].stage == 1)
            {
                var ferry = courses[3].steps[0];
                foreach (var m in ferry.motions) m.ResetPose();
                foreach (var item in ferry.hide) item.SetActive(true);
                foreach (var item in ferry.reveal) item.SetActive(false);
                courses[3].stage = 0;
            }
            LeaveAbility();
            var returnPose = automatic ? recentSafeGround ?? startingPose : startingPose;
            if (checkpoint != null)
            {
                var surface = checkpoint.surface;
                returnPose = new Pose(surface.position + Vector3.up * 1.08f, surface.rotation);
            }
            player.transform.rotation = returnPose.rotation;
            player.Teleport(returnPose.position);
            Report(interior != null ? "입구로 돌아왔습니다. 수리 진행은 유지됩니다." :
                "안전한 곳으로 돌아왔습니다. 획득한 " + CollectibleName + "은 유지됩니다.");
            effects?.Recovery();
        }

        void Report(string text)
        {
            Message = text;
            MessageUntil = Time.unscaledTime + 3.6f;
        }

        void RejectAim(string text)
        {
            aimNotice = text;
            aimNoticeUntil = Time.unscaledTime + 2.2f;
            if (playUI == null) Report(text);
        }

        void OnGUI()
        {
            if (font == null || playUI != null) return;
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

