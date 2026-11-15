using System;
using System.Collections.Generic;
using EverflowPuzzleLab;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using Input = EverflowGraybox.EFInput;

namespace EverflowMap
{
    public enum EFV2InteractionMode { Exploration, Ability, Paused, Cinematic }
    // Aim after CinemachineBrain's default-order LateUpdate, using this frame's rendered view.
    // All Awake calls still finish before any Start, so scene composition precedes gameplay.
    [DefaultExecutionOrder(300), DisallowMultipleComponent]
    public sealed class EFV2PuzzleWorld : MonoBehaviour
    {
        public EFMapPlayer player;
        public EFV2ScenePresentation presentation;
        public Transform spawnPoint, mapRoot;
        public string[] solidMeshNames = Array.Empty<string>();
        public float fallHeight = -42, recallDistance = 24, castRange = 18, orbSpeed = 32, successSeconds = 3;
        public LabTarget branch, bypass, upperIce, mainIce, channelStructure;
        public LabMover channel, dockLeaf;
        public LabGate ruinGate, exitGate;
        public Transform wheel;
        public Transform connectionPoint;
        public EFV2ChannelPose channelPose;
        public GameObject[] supplyWater, bypassWater, dockWater, turbineWater, mainWater, exitWater, leakWater, gardenWater;
        public GameObject returnWalk;
        public CinemachineCamera finaleCamera;
        public TextMesh supplySign, branchSign, gateSign, arrivalSign;
        public GameObject[] ruinWater;
        public List<LabTarget> targets = new List<LabTarget>();
        public List<LabMover> movers = new List<LabMover>();
        public List<LabGate> gates = new List<LabGate>();
        public List<EFV2Checkpoint> checkpoints = new List<EFV2Checkpoint>();
        public List<EFV2Plant> plants = new List<EFV2Plant>();
        public bool Built { get; private set; }
        public bool AutomatedCheck { get; set; }
        public bool Paused { get; private set; }
        public bool AbilityMode { get; private set; }
        public bool SimulationPaused => Paused || AbilityMode;
        public EFV2InteractionMode Mode => Paused ? EFV2InteractionMode.Paused : OpeningActive || cinematicUntil > 0 ? EFV2InteractionMode.Cinematic : AbilityMode ? EFV2InteractionMode.Ability : EFV2InteractionMode.Exploration;
        public string AimMessage => aim;
        EFV2Guidance guidance;
        readonly Dictionary<ParticleSystem, bool> pausedParticles = new Dictionary<ParticleSystem, bool>();
        readonly List<AudioSource> pausedAudio = new List<AudioSource>();
        bool savedListenerPause;
        public bool Aligned => channel != null && Vector3.Distance(channel.motion.position, connectionPoint.position) <= .4f && channelPose.Aligned;
        public bool Supply { get; private set; }
        public bool Arriving { get; private set; }
        public bool Restored => arrival.success;
        public bool GardenReached { get; private set; }
        public float StableFor => arrival.stableFor;
        public float Elapsed => Time.unscaledTime - started;
        public string Objective
        {
            get
            {
                string area=CurrentCheckpoint!=null?CurrentCheckpoint.label:"S";
                if(Restored)return GardenReached?"복원된 정원을 계속 탐험하세요. Enter: 처음부터 다시 시작":"열린 D4 문을 지나 안전 회랑을 따라 W 정원으로 걸어가세요.";
                if(area.StartsWith("D"))return Supply?exitGate.Latched?"D2를 주 출구로 돌리고 물길의 얼음을 녹이세요.":"D2에서 작은 수차를 선택하거나 D4 뿌리를 키우세요.":area.StartsWith("D1")?"D3 작업대에 접근하세요: 발코니 덩굴 또는 아래 수조의 얼음길":"D5 수로를 FLOW로 정렬하고 FREEZE로 고정하거나 서쪽 우회 뿌리를 키우세요.";
                if(area.StartsWith("G") || area.StartsWith("TOWER"))return ruinGate.Latched?"석문을 통과해 탑 안의 계단과 터널로 D1까지 내려가세요.":"G 석문: 수차에 FLOW로 물을 보내거나 문 아래 뿌리에 BLOOM";
                if(area.StartsWith("B") || area.StartsWith("C"))return "B: 뿌리 다리/돌 발판 · C: 성장한 잎/바람 → 높은 G 유적";
                if(area.StartsWith("A COMMON"))return "둔덕 건너편 B 또는 C를 선택하세요. 연결길로 경로를 바꿀 수 있습니다.";
                return "A 둔덕: BLOOM 뿌리 계단 또는 FLOW 잎 운반 → FREEZE 고정";
            }
        }
        public string LastEvent { get; private set; } = "유적의 석문을 열고 지하 물길을 연결하세요.";
        public string BlockReason => !Supply ? (upperIce.Frozen ? "D5 내부 물이 얼었습니다. FREEZE로 해동하세요." : channelStructure.Frozen && !Aligned ? "D5가 어긋난 채 고정되었습니다. 고정을 풀고 다시 정렬하세요." : "D5 누수: 수로를 정렬·고정하거나 우회 문을 여세요.") : branch.Direction != 2 ? "D2 물이 주 출구를 향하지 않습니다." : !exitGate.IsOpen ? "D4 수문이 닫혔습니다. 작은 수차 또는 뿌리를 이용하세요." : mainIce.Frozen ? "주 물길의 얼음이 물을 막습니다. FREEZE로 해동하세요." : "W에 물 공급 중";
        public EFV2OrbSlot<LabTarget>[] Slots { get; } = new[] { new EFV2OrbSlot<LabTarget>(), new EFV2OrbSlot<LabTarget>(), new EFV2OrbSlot<LabTarget>() };
        public Transform[] orbs = new Transform[3];
        public LabAbility Selected { get; private set; }
        public LabTarget Hovered { get; private set; }
        public LabTarget PendingFlow { get; private set; }
        public int FlowChoice { get; private set; }
        public bool OpeningActive => presentation != null && presentation.IntroActive;
        public bool InputActive => Mode == EFV2InteractionMode.Exploration;
        public bool AbilityInputActive => Mode == EFV2InteractionMode.Ability;
        public EFV2Checkpoint CurrentCheckpoint { get; private set; }
        public event Action<string> Recorded;
        readonly LabArrivalState arrival = new LabArrivalState();
        float cinematicUntil, started, nextCast;
        Vector3 safePosition;
        Quaternion safeRotation;
        string aim = "";
        Font font;
        public Font PresentationFont => font;
        int deaths;

        void Awake()
        {
            if (player == null || mapRoot == null || spawnPoint == null || player.cinemachineRig == null)
                throw new InvalidOperationException("V2 puzzle requires cloned map/player/Cinemachine/spawn references.");
            if (player.world != null || player.follow != null || !player.puzzleLabLocomotion)
                throw new InvalidOperationException("V2 must have one input, respawn and camera owner.");
            EFV2PuzzleAssembly.Build(this);
            Built = true;
            guidance=gameObject.AddComponent<EFV2Guidance>();guidance.world=this;
        }
        void Start()
        {
            started = Time.unscaledTime;
            safePosition = spawnPoint.position; safeRotation = spawnPoint.rotation;
            player.transform.rotation = safeRotation; player.Teleport(safePosition);
            SetPaused(false);
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial", "Noto Sans CJK KR" },18);
            if(presentation!=null)presentation.Begin();
            Record("세 능력을 모두 사용할 수 있습니다. 첫 둔덕으로 이동하세요.");
        }
        public void Record(string message) { LastEvent = message; Recorded?.Invoke(message); Debug.Log("[EVERFLOW V2] " + message); }

        void Update()
        {
            if (!Built) return;
            if (EFMapInput.KeyDown(KeyCode.Escape))
            {
                if(AbilityMode) CancelAbility(); else SetPaused(!Paused);
                return; // One Esc belongs to exactly one mode.
            }
            if (Paused) return;
            if (OpeningActive) return;
            if(cinematicUntil<=0)
            {
                if (Input.KeyDown(KeyCode.Alpha1)) Select(LabAbility.Bloom);
                if (Input.KeyDown(KeyCode.Alpha2)) Select(LabAbility.Freeze);
                if (Input.KeyDown(KeyCode.Alpha3)) Select(LabAbility.Flow);
            }
            if(AbilityMode)
            {
                if (PendingFlow != null && (Input.KeyDown(KeyCode.E) || Pressed(Key.Tab))) CycleFlow(1);
                if (PendingFlow != null && Mouse.current != null && Mathf.Abs(Mouse.current.scroll.ReadValue().y) > .01f)
                    CycleFlow(Mouse.current.scroll.ReadValue().y > 0 ? 1 : -1);
                return;
            }
            TickCircuit(Time.deltaTime);
            TickOrbs(Time.deltaTime);
            if (cinematicUntil > 0)
            {
                float t=1-Mathf.Clamp01((cinematicUntil-Time.time)/4);
                finaleCamera.transform.position=Vector3.Lerp(new Vector3(-87,-5,-38),new Vector3(-94,-7,-2),Mathf.SmoothStep(0,1,t));
                finaleCamera.transform.LookAt(Vector3.Lerp(new Vector3(-86,-13,-30),wheel.position,t));
                if (Time.time >= cinematicUntil) EndCinematic();
                return;
            }
            if (Input.KeyDown(KeyCode.R)) Respawn();
            if (Restored && Input.KeyDown(KeyCode.Return)) Restart();
            bool inTower = player.transform.position.x > -32 && player.transform.position.x < -27 && player.transform.position.z < -61 && player.transform.position.z > -71;
            if (player.transform.position.y < fallHeight || (!inTower && player.transform.position.y < safePosition.y - 8)) Respawn();
        }
        static bool Pressed(Key key) => Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;
        public void Select(LabAbility ability)
        {
            if(!InputActive && !AbilityInputActive)return;
            Selected=ability;PendingFlow=null;Hovered=null;
            if(!AbilityMode)
            {
                AbilityMode=true;
                SuspendPresentation();
                ApplyInputState();
            }
            aim="커서로 대상을 선택하세요. Esc: 취소";
        }
        public void CancelAbility()
        {
            if(!AbilityMode)return;
            AbilityMode=false;PendingFlow=null;Hovered=null;
            HideFlowArrows();ResumePresentation();ApplyInputState();
        }
        public void ChooseFlow(int choice)
        {
            if(AbilityInputActive && PendingFlow!=null && choice>=0 && choice<PendingFlow.directions.Length)FlowChoice=choice;
        }
        public bool ConfirmFlow()=>AbilityInputActive && PendingFlow!=null && TryCast(PendingFlow,FlowChoice,false);
        void HideFlowArrows()
        {
            foreach(var t in targets)foreach(var arrow in t.arrows)if(arrow!=null)arrow.gameObject.SetActive(false);
        }
        public void CycleFlow(int delta)
        {
            if (PendingFlow == null || PendingFlow.directions.Length == 0) return;
            FlowChoice = (FlowChoice + delta + PendingFlow.directions.Length) % PendingFlow.directions.Length;
        }
        public void TickCircuit(float dt)
        {
            Supply = EFV2CircuitRules.Supply(Aligned, channelStructure.Frozen, upperIce.Frozen, bypass.Grown);
            exitGate.supplied = Supply;
            dockLeaf.powered = Supply && branch.Direction == 0;
            Arriving = LabCircuitRules.Arriving(Supply,branch.Direction,exitGate.IsOpen,mainIce.Frozen);
            Show(supplyWater, Aligned && channelStructure.Frozen && !upperIce.Frozen);
            Show(bypassWater,bypass.Grown);
            Show(leakWater,!Aligned || !channelStructure.Frozen);
            Show(dockWater,Supply && branch.Direction == 0);
            Show(turbineWater,Supply && branch.Direction == 1);
            Show(mainWater,Supply && branch.Direction == 2 && !mainIce.Frozen);
            Show(exitWater,Arriving);
            Show(ruinWater,ruinGate.flow.Direction==0);
            if(supplySign!=null)
            {
                supplySign.text=bypass.Grown?"D5 BYPASS OPEN - WATER SUPPLIED":upperIce.Frozen?"D5 INTERNAL ICE - THAW WATER":Aligned&&channelStructure.Frozen?"D5 ALIGNED + FIXED - WATER SUPPLIED":channelStructure.Frozen?"D5 FIXED OFF SOCKET - RELEASE & REALIGN":"D5 LEAK - ALIGN & FREEZE STRUCTURE";
                supplySign.color=Supply?Color.green:upperIce.Frozen?Color.cyan:Color.yellow;
            }
            if(branchSign!=null) branchSign.text="D2 WATER -> "+(branch.Direction<0?"NO DIRECTION":branch.Direction==0?"DOCK LEAF":branch.Direction==1?"D4 SMALL WHEEL":"MAIN EXIT")+(!Supply?"\nNO SUPPLY FROM D5":"");
            if(gateSign!=null) {gateSign.text=exitGate.Latched?"D4 LATCHED OPEN - SWITCH D2 TO MAIN EXIT":"D4 CLOSED - SEND WATER TO WHEEL OR BLOOM ROOT";gateSign.color=exitGate.Latched?Color.green:Color.yellow;}
            if(arrivalSign!=null) {arrivalSign.text=mainIce.Frozen?"MAIN WATER BLOCKED BY ICE - THAW":Arriving?$"W WATER ARRIVING {StableFor:0.0}/{successSeconds:0.0}s":"W NO WATER - CHECK D5 / D2 / D4 / ICE";arrivalSign.color=mainIce.Frozen?Color.cyan:Arriving?Color.green:Color.yellow;}
            bool was = Restored;
            arrival.Tick(dt,Arriving,successSeconds);
            if (Arriving || Restored) wheel.Rotate(Vector3.right, 65 * dt, Space.World);
            Show(gardenWater,Restored);
            foreach(var plant in plants) plant.SetRestored(Restored);
            if (!was && Restored) BeginCinematic();
        }
        static void Show(GameObject[] items,bool visible) { if(items!=null) foreach(var item in items) if(item!=null && item.activeSelf!=visible) item.SetActive(visible); }
        void TickOrbs(float dt)
        {
            for (int i=0;i<3;i++)
            {
                var slot=Slots[i];
                Vector3 home=player.transform.position+Vector3.up*1.2f+player.transform.right*((i-1)*.42f);
                if ((slot.phase==EFV2OrbPhase.Placed || slot.phase==EFV2OrbPhase.Flying) && (slot.target==null || !slot.target.gameObject.activeInHierarchy)) slot.Recall();
                Vector3 goal=slot.target!=null ? slot.target.transform.position+Vector3.up*.55f : home;
                if(slot.RecallIfFar(Vector3.Distance(player.transform.position,goal),recallDistance))
                {goal=home;Record(((LabAbility)i)+" 거리 자동 회수: 완료된 효과 유지");}
                orbs[i].position=Vector3.MoveTowards(orbs[i].position,goal,orbSpeed*dt);
                if(slot.phase==EFV2OrbPhase.Flying && Vector3.Distance(orbs[i].position,goal)<.06f)
                {
                    int rev=slot.revision;
                    if(slot.Arrive(rev))
                    {
                        if(slot.release) slot.target.Undo((LabAbility)i); else slot.target.Apply((LabAbility)i,slot.direction);
                        Record(((LabAbility)i)+" → "+slot.target.State);
                    }
                }
                if(slot.phase==EFV2OrbPhase.Returning && Vector3.Distance(orbs[i].position,home)<.06f) slot.Returned();
            }
        }
        void LateUpdate()
        {
            if(!Built || !AbilityInputActive) return;
            Physics.SyncTransforms();
            Hovered=null; aim="빈 공간입니다. 사용할 대상을 선택하세요.";
            bool overUI=guidance!=null && guidance.BlocksWorldPointer(Mouse.current!=null?Mouse.current.position.ReadValue():Vector2.zero);
            if(UnityEngine.EventSystems.EventSystem.current!=null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())overUI=true;
            var ray=player.view.ScreenPointToRay(Mouse.current!=null?Mouse.current.position.ReadValue():new Vector2(Screen.width*.5f,Screen.height*.5f));
            var hits=Physics.RaycastAll(ray,90,~(1<<2),QueryTriggerInteraction.Collide);
            Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            foreach(var hit in hits)
            {
                if(overUI){aim="안내 패널 위입니다. 월드의 대상을 선택하세요.";break;}
                if(hit.collider.transform.IsChildOf(player.transform)) continue;
                var target=EFV2TargetProxy.Resolve(hit.collider);
                if(hit.collider.isTrigger && target==null) continue;
                if(target!=null)
                {
                    Hovered=target;
                    aim=target.State+"\n"+(CanUse(target,Selected,out var reason) ? Selected==LabAbility.Freeze ? (target.Frozen ? "좌클릭: 해동 / 고정 해제" : target.water ? "좌클릭: 물 동결 (물 공급 차단)" : "좌클릭: 구조물 고정") : "좌클릭: "+Selected : reason);
                }
                else aim="가림: "+hit.collider.name+" · 옆으로 이동해 대상을 조준하세요.";
                break;
            }
            foreach(var t in targets)
                for(int i=0;i<t.arrows.Length;i++) if(t.arrows[i]!=null)
                {
                    t.arrows[i].gameObject.SetActive(Selected==LabAbility.Flow && (t==PendingFlow || t==Hovered && CanUse(t,Selected,out _)));
                    if(t==PendingFlow) t.arrows[i].localScale=Vector3.one*(i==FlowChoice?1.25f:.8f);
                }
            if(overUI){if(EFMapInput.Click && guidance!=null)guidance.HandleAbilityClick(Mouse.current.position.ReadValue());return;}
            if(Selected==LabAbility.Freeze && Pressed(Key.X) && Hovered!=null && Hovered.Frozen) {TryCast(Hovered,0,true);return;}
            if(!EFMapInput.Click) return;
            if(Hovered==null){Record(aim);return;}
            if(Selected!=LabAbility.Flow) TryCast(Hovered,0,Selected==LabAbility.Freeze && Hovered.Frozen);
            else if(PendingFlow!=Hovered)
            {
                if(CanUse(Hovered,Selected,out var reason))
                {PendingFlow=Hovered;FlowChoice=Hovered.Direction<0?0:Hovered.Direction;Record("FLOW: 방향 버튼 또는 E / Tab / 휠로 선택 후 확정");}
                else Record(reason);
            }
            else if(TryCast(Hovered,FlowChoice,false)) PendingFlow=null;
        }
        public bool CanUse(LabTarget target,LabAbility ability,out string reason)
        {
            reason=null;
            if(!InputActive && !AbilityInputActive) reason="일시 정지 / 복원 연출 중";
            else if(target==null || !targets.Contains(target) || !target.gameObject.activeInHierarchy) reason="유효한 대상이 없습니다.";
            else if(!target.Accepts(ability)) reason="이 대상은 "+ability+" 능력을 받지 않습니다.";
            else if(Vector3.Distance(player.transform.position,target.transform.position)>castRange) reason="사거리 밖 · "+castRange+"m 안으로 접근하세요.";
            else
            {
                Vector3 from=player.transform.position+Vector3.up*.6f,delta=target.transform.position-from;
                foreach(var h in Physics.RaycastAll(from,delta.normalized,delta.magnitude-.05f,~(1<<2),QueryTriggerInteraction.Ignore))
                    if(!h.collider.transform.IsChildOf(player.transform) && EFV2TargetProxy.Resolve(h.collider)!=target)
                    { reason="몸 앞이 "+h.collider.name+"에 가렸습니다.";break; }
            }
            return reason==null;
        }
        public bool TryCast(LabTarget target,int choice,bool release)
        {
            if(!AbilityInputActive)return false;
            if(!CanUse(target,Selected,out var reason)) {Record(reason);return false;}
            if(Selected==LabAbility.Flow && (choice<0 || choice>=target.directions.Length)) return false;
            if(Time.unscaledTime<nextCast) return false;
            var slot=Slots[(int)Selected];
            bool transferred=slot.target!=null && slot.target!=target;
            if(!slot.Launch(target,choice,release)) return false;
            nextCast=Time.unscaledTime+.18f;
            CancelAbility(); // Resume before the next world tick; arrival applies the effect, never preview.
            if(transferred) Record(Selected+" 새 대상으로 자동 이동 · 이전 효과 유지");
            return true;
        }
        public void Reach(EFV2Checkpoint checkpoint)
        {
            if(!InputActive) return;
            if(checkpoint.garden && Restored && !GardenReached)
            {GardenReached=true;Record("정원에 도착했습니다. 계속 탐험하거나 Enter로 다시 시작하세요.");}
            if(CurrentCheckpoint==checkpoint) return;
            if(CurrentCheckpoint!=null && Vector3.Distance(player.transform.position,checkpoint.safePosition)>Vector3.Distance(player.transform.position,safePosition)-.35f) return;
            CurrentCheckpoint=checkpoint;safePosition=checkpoint.safePosition;safeRotation=checkpoint.transform.rotation;
            if(checkpoint.workbench) returnWalk.SetActive(true);
            Record("안전 지점: "+checkpoint.label+" · R로 여기서 재시도");
        }
        public void Respawn()
        {
            CancelAbility();
            deaths++;PendingFlow=null;
            foreach(var slot in Slots) slot.Recall();
            // A remote/mid-rail frozen platform must be recoverable without erasing another area's work.
            if(CurrentCheckpoint!=null && CurrentCheckpoint.localMover!=null)
            {
                var m=CurrentCheckpoint.localMover;
                m.target.Undo(LabAbility.Freeze);m.flow.ResetState();m.ResetMotion();
            }
            player.transform.rotation=safeRotation;player.Teleport(safePosition);
            Physics.SyncTransforms();
            Record("가까운 안전 지점으로 복귀 · 다른 구역의 완료 상태 보존");
        }
        void BeginCinematic()
        {
            PendingFlow=null;player.enabled=false;player.cinemachineRig.SetInputActive(false);
            finaleCamera.Priority=100;finaleCamera.PreviousStateIsValid=false;
            cinematicUntil=Time.time+4;
            Record("물레방아 복원! D4 출구 회랑을 따라 정원으로 걸어가세요.");
        }
        void EndCinematic()
        {
            cinematicUntil=0;finaleCamera.Priority=-10;player.cinemachineRig.virtualCamera.PreviousStateIsValid=false;
            ApplyInputState();
            Record("조작 복귀 · D4의 열린 문을 지나 물레 옆 정원으로");
        }
        public void SetPaused(bool paused)
        {
            if(AbilityMode)CancelAbility();
            if(paused && !Paused)SuspendPresentation();
            if(!paused && Paused)ResumePresentation();
            Paused=paused;
            ApplyInputState();
        }
        void ApplyInputState()
        {
            Time.timeScale=SimulationPaused?0:1;
            player.enabled=InputActive;
            player.cinemachineRig.SetInputActive(InputActive);
            player.cinemachineRig.SetViewFrozen(SimulationPaused);
            Cursor.lockState=SimulationPaused?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=SimulationPaused;
        }
        void SuspendPresentation()
        {
            savedListenerPause=AudioListener.pause;AudioListener.pause=true;
            pausedParticles.Clear();pausedAudio.Clear();
            foreach(var p in FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
                if(p.isPlaying){pausedParticles[p]=true;p.Pause(false);}
            foreach(var a in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                if(a.ignoreListenerPause && a.isPlaying){pausedAudio.Add(a);a.Pause();}
        }
        void ResumePresentation()
        {
            AudioListener.pause=savedListenerPause;
            foreach(var entry in pausedParticles)if(entry.Key!=null && entry.Key.isPaused)entry.Key.Play(false);
            foreach(var a in pausedAudio)if(a!=null)a.UnPause();
            pausedParticles.Clear();pausedAudio.Clear();
        }
        public void Restart()
        {
            CancelAbility();
            if(presentation!=null)presentation.Finish();
            foreach(var t in targets) t.ResetState();
            foreach(var m in movers) m.ResetMotion();
            foreach(var g in gates) g.ResetGate();
            foreach(var lift in GetComponentsInChildren<LabRootLift>(true)) lift.transform.position=lift.closed;
            channelPose.ResetPose();arrival.Reset();Supply=Arriving=GardenReached=false;
            returnWalk.SetActive(false);cinematicUntil=0;finaleCamera.Priority=-10;
            foreach(var slot in Slots) {slot.Recall();slot.Returned();}
            foreach(var plant in plants) plant.SetRestored(false);
            wheel.rotation=EFV2PuzzleAssembly.WheelInitialRotation;
            CurrentCheckpoint=null;safePosition=spawnPoint.position;safeRotation=spawnPoint.rotation;PendingFlow=null;
            for(int i=0;i<orbs.Length;i++)orbs[i].position=safePosition+Vector3.up*1.2f+Vector3.right*((i-1)*.42f);
            started=Time.unscaledTime;deaths=0;nextCast=0;Selected=LabAbility.Bloom;
            player.transform.rotation=safeRotation;player.Teleport(safePosition);SetPaused(false);TickCircuit(0);
            Record("다시 시작 · 모든 퍼즐과 구체가 초기 상태로 복구되었습니다.");
        }
        void OnApplicationFocus(bool focus) {if(!focus && Built && Application.isPlaying && !Application.isBatchMode && !AutomatedCheck) SetPaused(true);}
        void OnDisable()
        {
            if(SimulationPaused)ResumePresentation();
            if(player!=null && player.cinemachineRig!=null)player.cinemachineRig.SetViewFrozen(false);
            Time.timeScale=1;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        }
    }
}
