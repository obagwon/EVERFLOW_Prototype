using System;
using System.Collections.Generic;
using System.Linq;
using EverflowPuzzleLab;
using UnityEngine;
using UnityEngine.InputSystem;
using RouteArea = EverflowMap.EFV2RouteGuide.Area;
using RouteAccess = EverflowMap.EFV2RouteGuide.Access;

namespace EverflowMap
{
    // Presentation only: no quest completion flags, ability writes, colliders or camera control.
    [DefaultExecutionOrder(800), DisallowMultipleComponent]
    public sealed class EFV2Guidance:MonoBehaviour
    {
        public EFV2PuzzleWorld world;
        public bool HelpOpen {get;private set;}
        public string GoalTitle {get;private set;}="첫 둔덕에 올라가세요";
        public string GoalDetail {get;private set;}="";
        public string Area {get;private set;}="S · 시작 전망대";
        public string ContextTitle {get;private set;}="";
        public string ContextAction {get;private set;}="";
        public string ContextState {get;private set;}="";
        public bool ContextValid {get;private set;}
        public string[] CircuitLines {get;private set;}=new string[4];
        public bool IsUnderground {get;private set;}
        public Vector3 GoalPosition {get;private set;}
        public string GoalMarker {get;private set;}="";
        readonly EFV2RouteGuide route=new EFV2RouteGuide();
        LabTarget aRoot,aFlow,bRoot,bFlow,cLeaf,cFlow,vine;
        bool reachedMound,reachedUpperApproach,reachedRuins,reachedBalcony;
        int destination;
        Font font;
        GUIStyle body,small,title,large,button,helpBody,iconLabel,hintLabel;
        string previousEvent="",toast="";
        float toastUntil;
        readonly Color ink=new Color(.94f,.97f,.95f),muted=new Color(.67f,.76f,.76f),panel=new Color(.025f,.055f,.07f,.94f);
        readonly Color[] colors=EFV2AbilityTargets.Colors;
        readonly Dictionary<TextMesh,string> signs=new Dictionary<TextMesh,string>();
        static readonly Dictionary<string,string> names=new Dictionary<string,string>
        {
            {"A ROOT STAIRS","뿌리 계단"},{"A POND CURRENT","연못의 물살"},{"A FLOATING LEAF","떠 있는 잎"},
            {"B WINDMILL","돌 발판의 풍차"},{"B MOVING STONE","움직이는 돌 발판"},{"B ROOT BRIDGE","뿌리 다리"},
            {"C WIND","잎을 운반하는 바람"},{"C GROWABLE LEAF","성장하는 잎"},{"G WATER BRANCH","유적의 물 분기"},{"G RUIN GATE ROOT","석문 아래 뿌리"},
            {"D3 VINE ACCESS","작업대로 가는 덩굴"},{"D2 MAIN WATER / ICE BRIDGE","중앙 수조의 물"},
            {"D2 THREE WAY BRANCH","중앙 물 분기대"},{"D2 DOCK LEAF - AFTER SUPPLY","정비 부두의 잎"},
            {"D5 ALIGNING WIND","가동 수로의 바람"},{"D5 CHANNEL STRUCTURE","가동 수로 구조물"},
            {"D5 INTERNAL WATER - NOT STRUCTURE","수로 안의 물"},{"D5 BYPASS ROOT","우회 수로의 뿌리"},{"D4 EXIT GATE ROOT","출구 수문 아래 뿌리"}
        };
        public static string TargetName(LabTarget t)=>t!=null&&names.TryGetValue(t.label,out var n)?n:"장치";
        public static string ShortDirection(string s)=>s.Split('/')[0].Trim();
        public static string Place(string s)
        {
            if(s==null)return "S · 시작 전망대";
            if(s.StartsWith("TOWER"))return "G → D1 · 탑 내부 계단";
            switch(s)
            {
                case "S / A SHORE":return "A · 연못 물가";
                case "A COMMON MOUND":return "A · 첫 둔덕";
                case "B ENTRANCE":return "B · 뿌리 테라스";
                case "B FAR BANK":return "B · 건너편 선반";
                case "C ENTRANCE":return "C · 바람 정원";
                case "C HIGH LEDGE":return "C · 위쪽 선반";
                case "G RUIN":return "G · 고지 유적";
                case "D1 BALCONY":return "D1 · 지하 발코니";
                case "D1 MID STAIRS":case "D1 STAIR LANDING":return "D1 → D2 · 지하 계단";
                case "D2 CENTRAL HUB":return "D2 · 중앙 분기대";
                case "D3 WORKBENCH":return "D3 · 작업대";
                case "D5 SERVICE LEDGE":return "D5 · 수로 정비 통로";
                case "D4 EXIT CONTROL":return "D4 · 출구 수문";
                case "RESTORED GARDEN":return "W · 복원된 정원";
                default:return "S · 시작 전망대";
            }
        }
        LabTarget Target(string label)=>world.targets.FirstOrDefault(t=>t.label==label);
        void Start()
        {
            if(world==null)world=GetComponent<EFV2PuzzleWorld>();
            font=world.PresentationFont;
            aRoot=Target("A ROOT STAIRS");aFlow=Target("A POND CURRENT");
            bRoot=Target("B ROOT BRIDGE");bFlow=Target("B WINDMILL");
            cLeaf=Target("C GROWABLE LEAF");cFlow=Target("C WIND");vine=Target("D3 VINE ACCESS");
            SkinWorldSigns();
        }
        void Update()
        {
            if(Keyboard.current!=null&&Keyboard.current.f1Key.wasPressedThisFrame)HelpOpen=!HelpOpen;
        }
        internal void ResetJourney()
        {
            reachedMound=reachedUpperApproach=reachedRuins=reachedBalcony=false;
        }
        void SetGoal(string titleText,string detail,int waypoint=-1)
        {GoalTitle=titleText;GoalDetail=detail;destination=waypoint;}
        RouteAccess AvailableConnections()
        {
            var access=RouteAccess.Walk;
            if(aRoot.Grown)access|=RouteAccess.ARoot;
            if(aFlow.Direction==0)access|=RouteAccess.ALeaf;
            if(bRoot.Grown)access|=RouteAccess.BRoot;
            if(bFlow.Direction==0)access|=RouteAccess.BStone;
            if(cLeaf.Grown&&cFlow.Direction==0)access|=RouteAccess.CLeaf;
            if(world.ruinGate.IsOpen)access|=RouteAccess.RuinGate;
            if(vine.Grown)access|=RouteAccess.Vine;
            if(world.mainIce.Frozen)access|=RouteAccess.Ice;
            if(world.returnWalk.activeSelf)access|=RouteAccess.ReturnWalk;
            if(world.exitGate.IsOpen)access|=RouteAccess.ExitGate;
            return access;
        }
        static string LocationName(RouteArea area,float floor)
        {
            if(area==RouteArea.Tower)
            {
                int level=Mathf.Clamp(Mathf.RoundToInt((floor+3)/3)+1,1,7);
                return $"G · 탑 내부 / {level}층 부근";
            }
            switch(area)
            {
                case RouteArea.Shore:return "A · 연못 물가";
                case RouteArea.Mound:return "A · 첫 둔덕";
                case RouteArea.Link:return "B ↔ C · 연결길";
                case RouteArea.B:return "B · 뿌리 테라스";
                case RouteArea.BFar:return "B · 건너편 선반";
                case RouteArea.C:return "C · 바람 정원";
                case RouteArea.CHigh:return "C · 위쪽 선반";
                case RouteArea.Ruins:return "G · 고지 유적";
                case RouteArea.Tunnel:return "D1 · 지하 터널";
                case RouteArea.Balcony:return "D1 · 지하 발코니 / 상층";
                case RouteArea.LowerStairs:return "D1 → D2 · 수조행 계단";
                case RouteArea.Basin:return "D2 · 중앙 수조 / 하층";
                case RouteArea.Bench:return "D3 · 작업대 / 중층";
                case RouteArea.Service:return "D5 · 수로 정비 통로 / 상층";
                case RouteArea.ExitApproach:return "D4 · 수문행 회랑 / 하층";
                case RouteArea.Exit:return "D4 · 출구 수문 / 하층";
                case RouteArea.GardenWalk:return "W · 정원행 회랑";
                case RouteArea.Garden:return "W · 복원 정원";
                default:return "S · 시작 전망대";
            }
        }
        void RefreshJourney()
        {
            Vector3 feet=world.player.transform.position-Vector3.up;
            RouteAccess access=AvailableConnections();
            bool located=route.Locate(feet,access,out RouteArea area,out float floor,out string between);
            Area=located?between??LocationName(area,floor):"현재 구역 · 연결길 밖";
            IsUnderground=located&&area>=RouteArea.Tunnel&&area<=RouteArea.Exit;
            if(located)
            {
                reachedMound|=area>=RouteArea.Mound;
                reachedUpperApproach|=area==RouteArea.BFar||area>=RouteArea.CHigh;
                reachedRuins|=area>=RouteArea.Ruins;
                reachedBalcony|=area>=RouteArea.Balcony;
            }
            RefreshGoal(area,access);
            GoalMarker="";
            GoalPosition=default;
            if(route.Next(destination,access,feet,out var point))
            {GoalMarker=point.Name;GoalPosition=point.Position;}
        }
        void RefreshGoal(RouteArea area,RouteAccess access)
        {
            var w=world;
            if(w.Restored)
                SetGoal(w.GardenReached?"정원이 되살아났습니다":"열린 수문을 지나 정원으로",
                    w.GardenReached?"자유롭게 탐험하세요. Enter: 처음부터 다시 시작":"D4 출구를 통과해 난간이 있는 회랑을 걸어가세요.",w.GardenReached?-1:route.Garden);
            else if(reachedBalcony||w.returnWalk.activeSelf)
            {
                if(!w.returnWalk.activeSelf)
                {
                    bool lower=area==RouteArea.LowerStairs||area==RouteArea.Basin||area==RouteArea.ExitApproach||area==RouteArea.Exit;
                    bool iceOpen=(access&RouteAccess.Ice)!=0;
                    bool vineOpen=(access&RouteAccess.Vine)!=0;
                    if(iceOpen&&(lower||!vineOpen))
                        SetGoal("D3 작업대에 접근하세요","얼음길을 건너 정비 부두 계단으로 올라가세요.",route.Bench);
                    else if(!lower&&vineOpen)
                        SetGoal("D3 작업대에 접근하세요",iceOpen?"자란 덩굴이나 얼음길을 따라 작업대로 이동하세요.":"자란 덩굴을 따라 작업대로 내려가세요.",route.Bench);
                    else
                        SetGoal("D3 작업대에 접근하세요",lower?"수조에 FREEZE로 얼음길을 만들거나 위층의 발코니 덩굴을 이용하세요.":"발코니 덩굴에 BLOOM 또는 아래 수조에 FREEZE로 길을 만드세요.",
                            lower?route.Basin:route.Balcony);
                }
                else if(!w.Supply)
                {
                    int supplyApproach=area==RouteArea.Service?route.Bypass:route.Alignment;
                    if(w.upperIce.Frozen)
                        SetGoal("수로 안의 얼음을 녹이세요","물에 FREEZE를 다시 사용해 해동하거나, BLOOM으로 우회 수로를 여세요.",supplyApproach);
                    else if(w.channelStructure.Frozen&&!w.Aligned)
                        SetGoal("어긋난 수로의 고정을 푸세요","구조물을 해제·정렬·고정하거나 서쪽 우회 뿌리를 키우세요.",supplyApproach);
                    else if(w.Aligned&&!w.channelStructure.Frozen)
                        SetGoal("맞춰진 수로 구조물을 고정하세요","FREEZE로 구조물을 고정하거나 서쪽 우회 뿌리를 키우세요.",supplyApproach);
                    else
                        SetGoal("D5에서 물 공급을 연결하세요","FLOW로 수로를 맞추고 구조물에 FREEZE, 또는 서쪽 뿌리에 BLOOM을 사용하세요.",
                            supplyApproach);
                }
                else if(!w.exitGate.IsOpen)
                    SetGoal(w.branch.Direction==1?"수문이 끝까지 열릴 때까지 기다리세요":"D4 출구 수문을 여세요",
                        w.branch.Direction==1?"작은 수차 방향을 유지하세요. 완전히 열리면 걸쇠가 고정됩니다.":"D2를 작은 수차 방향으로 돌리거나 수문 아래 뿌리에 BLOOM을 사용하세요.",
                        area==RouteArea.Exit||area==RouteArea.ExitApproach?route.Exit:route.Basin);
                else if(w.branch.Direction!=2)
                    SetGoal("D2를 주 출구 방향으로 돌리세요","FLOW로 중앙 분기대를 조준하고 주 출구를 선택·확정하세요.",route.Basin);
                else if(w.mainIce.Frozen)
                    SetGoal("주 물길을 막는 얼음을 녹이세요","중앙 수조의 물에 FREEZE를 다시 사용하세요.\n조준 후 X로도 해동할 수 있습니다.",route.Basin);
                else
                    SetGoal("물레방아에 물이 도착합니다","현재 물길을 유지하세요. 연속 공급이 완료되면 물레방아가 복원됩니다.");
            }
            else if(area==RouteArea.Tower||area==RouteArea.Tunnel)
                SetGoal("탑 아래 지하 발코니로","계단을 내려가 아래 터널을 따라 D1로 이동하세요.",route.Balcony);
            else if(reachedRuins||w.ruinGate.IsOpen)
                SetGoal(w.ruinGate.IsOpen?"열린 석문을 통과하세요":"유적의 석문을 여세요",
                    w.ruinGate.IsOpen?"문 뒤 탑 안의 계단과 터널이 지하로 이어집니다.":"분기에 FLOW로 작은 수차에 물을 보내거나 문 아래 뿌리에 BLOOM을 사용하세요.",w.ruinGate.IsOpen?route.Balcony:route.Gate);
            else if(reachedUpperApproach)
                SetGoal("계단을 올라 고지 유적으로","B와 C는 같은 고지 유적으로 이어집니다.",route.Ruins);
            else if(area==RouteArea.B)
                SetGoal("끊긴 길의 건너편으로","뿌리에 BLOOM 또는 풍차에 FLOW → 돌 발판에 FREEZE를 사용하세요.",
                    (access&(RouteAccess.BRoot|RouteAccess.BStone))!=0?route.BFar:route.BNear);
            else if(area==RouteArea.C)
            {
                SetGoal(cLeaf.Grown?"잎을 타고 위쪽 선반으로":"이동할 잎을 크게 키우세요",
                    cLeaf.Grown?"바람에 FLOW로 위쪽 선반을 선택하세요. FREEZE로 잎을 멈추고 안전하게 내리세요.":"잎에 BLOOM을 사용한 뒤 올라타세요. FLOW로 바람 방향을 바꿀 수 있습니다.",
                    (access&RouteAccess.CLeaf)!=0?route.CHigh:route.CBoard);
            }
            else if(reachedMound)
                SetGoal("B 또는 C로 유적을 향하세요","풍차가 있는 B 또는 큰 잎이 있는 C로 가세요.\n중간 연결길에서 경로를 바꿀 수 있습니다.",
                    area==RouteArea.Start||area==RouteArea.Shore?route.Mound:-1);
            else
                SetGoal("첫 둔덕에 올라가세요","뿌리에 BLOOM으로 계단을 만들거나, 잎에 올라 물살에 FLOW → 잎에 FREEZE를 사용하세요.",
                    (access&(RouteAccess.ARoot|RouteAccess.ALeaf))!=0?route.Mound:route.Shore);
        }
        public void Refresh()
        {
            if(world==null||!world.Built||aRoot==null)return;
            var w=world;
            RefreshJourney();
            CircuitLines[0]=w.Supply?"공급 연결됨":w.upperIce.Frozen?"공급 막힘 · 내부 물 해동":w.channelStructure.Frozen&&!w.Aligned?"공급 없음 · 수로 고정 해제":"공급 없음 · D5 연결 필요";
            CircuitLines[1]="D2 방향 · "+(w.branch.Direction<0?"미선택":w.branch.Direction==0?"정비 부두":w.branch.Direction==1?"작은 수차":"주 출구");
            CircuitLines[2]=w.exitGate.IsOpen?"D4 수문 열림 · 걸쇠 유지":"D4 수문 닫힘";
            CircuitLines[3]=w.mainIce.Frozen?(w.returnWalk.activeSelf?"주 물길 얼음 · 해동 필요":"수조 얼음길 열림"):"주 물길 얼음 차단 없음";
            ContextTitle=ContextAction=ContextState="";ContextValid=false;
            var feedback=w.GetComponent<EFDemoFeedback>();
            if(feedback!=null&&feedback.NoticeUntil>Time.time)
            {toast=feedback.Notice;toastUntil=Time.unscaledTime+.15f;}
            var target=w.Hovered;
            if(target!=null)
            {
                ContextTitle=TargetName(target);ContextValid=w.CanUse(target,w.Selected,out var reason);
                var states=new List<string>();
                if(target.acceptsBloom)states.Add(target.Grown?"성장 완료":"성장 가능");
                if(target.acceptsFreeze)states.Add(target.Frozen?(target.water?"얼음 · 물길 차단":"고정됨"):(target.water?"물 · 동결 가능":"구조물 · 고정 가능"));
                if(target.acceptsFlow)states.Add(target.Direction<0?"방향 미선택":"현재: "+ShortDirection(target.directions[target.Direction]));
                ContextState=string.Join(" · ",states);
                if(feedback!=null&&target==w.channelStructure)ContextState=feedback.D5Status;
                ContextAction=!ContextValid?FriendlyFailure(reason):w.Selected==LabAbility.Flow?(w.PendingFlow==target?"E / Tab / 휠로 선택 · 좌클릭으로 확정":"좌클릭으로 방향 선택 시작"):
                    w.Selected==LabAbility.Bloom?(target.Grown?"이미 성장했습니다 · 다른 대상에도 재사용할 수 있습니다":"좌클릭 · BLOOM으로 성장"):
                    target.Frozen?(target.water?"좌클릭 / X · 얼음 해동":"좌클릭 / X · 구조물 고정 해제"):
                    target.water?"좌클릭 · 물을 얼림 (물길이 막힙니다)":"좌클릭 · 구조물 고정 (물은 흐릅니다)";
                if(!ContextValid&&reason!=null&&reason.Contains("능력을 받지"))
                    ContextAction="사용 가능한 능력: "+string.Join(" / ",new[]{target.acceptsBloom?"1 BLOOM":null,target.acceptsFreeze?"2 FREEZE":null,target.acceptsFlow?"3 FLOW":null}.Where(s=>s!=null));
            }
            if(previousEvent!=w.LastEvent)
            {previousEvent=w.LastEvent;if(w.LastEvent.StartsWith("다시 시작"))HelpOpen=false;toast=FriendlyEvent(w.LastEvent);toastUntil=Time.unscaledTime+3.5f;}
        }
        string FriendlyFailure(string text)
        {
            if(string.IsNullOrEmpty(text))return "대상을 조준하세요";
            if(text.Contains("사거리"))return $"사거리 밖 · {world.castRange:0}m 안으로 접근하세요";
            if(text.Contains("가렸"))return "몸 앞이 막혔습니다 · 옆으로 이동해 다시 조준하세요";
            if(text.Contains("능력"))return "이 대상에는 선택한 능력을 사용할 수 없습니다";
            return "지금은 사용할 수 없습니다";
        }
        string FriendlyEvent(string text)
        {
            if(text.StartsWith("다시 시작"))return "처음부터 다시 시작했습니다";
            if(text.Contains("안전 지점:"))return "안전 지점 저장 · R로 가까운 곳에서 다시 시도";
            if(text.Contains("자동 회수"))return "완료된 효과는 그대로 유지됩니다";
            if(text.Contains("새 대상으로"))return "새 대상에 능력을 사용합니다 · 이전 효과 유지";
            if(text.Contains("가까운 안전"))return "안전 지점으로 복귀했습니다 · 다른 구역의 효과는 유지됩니다";
            if(text.Contains("FLOW:"))return "방향을 고른 뒤 같은 대상을 다시 클릭해 확정하세요";
            if(text.Contains(" → "))
            {
                var t=world.targets.FirstOrDefault(x=>text.Contains(x.label));
                return t==null?"능력을 적용했습니다":TargetName(t)+" · "+(text.StartsWith("Flow")?"흐름 방향 변경":text.StartsWith("Bloom")?"성장 완료":t.Frozen?(t.water?"동결 완료":"고정 완료"):(t.water?"해동 완료":"고정 해제 완료"));
            }
            if(text.Contains("사거리")||text.Contains("가렸")||text.Contains("능력을 받지"))return FriendlyFailure(text);
            if(text.Contains("물레방아 복원"))return "물레방아가 복원되었습니다";
            if(text.Contains("조작 복귀"))return "조작이 돌아왔습니다 · D4 출구로 이동하세요";
            if(text.Contains("정원에 도착"))return "정원 도착 · 계속 탐험하거나 Enter로 다시 시작";
            return "세 능력을 모두 사용할 수 있습니다 · F1 도움말";
        }
        void LateUpdate()
        {
            Refresh();
            if(world==null||!world.Built)return;
            foreach(var pair in signs)if(pair.Key!=null)pair.Key.text=pair.Value;
            world.supplySign.text="D5 · "+CircuitLines[0];
            world.branchSign.text=CircuitLines[1];
            world.gateSign.text=world.exitGate.IsOpen?"D4 · 수문 열림 / 주 출구로 전환":"D4 · 수차 또는 뿌리로 수문 개방";
            world.arrivalSign.text=world.mainIce.Frozen?"얼음이 주 물길을 막고 있습니다":world.Arriving?$"물레방아 공급 중 {world.StableFor:0.0} / {world.successSeconds:0}초":"주 출구로 물을 보내세요";
        }
        void SkinWorldSigns()
        {
            var map=new Dictionary<string,string>
            {
                {"S  /  RESTORE THE VALLEY","유적의 석문을 열고\n물레방아의 흐름을 복원하세요"},
                {"B  ROOT TERRACE  >>>","풍차: B 뿌리 테라스\n큰 잎: C 바람 정원"},
                {"B: ROOT BRIDGE OR WINDMILL + FROZEN STONE","뿌리 다리 또는 풍차와 돌 발판"},
                {"C: BLOOM LEAF -> FLOW TO HIGH LEDGE","잎 성장 → 바람 방향 선택 → 선반 착지"},
                {"B <-> C  /  SWITCH ROUTES","B ↔ C · 경로 연결길"},
                {"G: FLOW TO WHEEL OR BLOOM ROOT","수차에 물 보내기 / 뿌리 성장\n끝까지 열린 문은 걸쇠로 유지"},
                {"D1  /  BROKEN D5 ABOVE - CLOSED D4 ACROSS THE HALL","D3 작업대에서 위쪽 D5 수로를 연결하세요"},
                {"D3: VINE FROM BALCONY OR FREEZE BASIN","공급 전: 덩굴 또는 얼음길\n부두 잎은 공급 복구 후 지름길"},
                {"W  /  THE WATERWHEEL","W · 물레방아와 정원"},
                {"MOUND","둔덕"},{"SHORE","물가"},{"FAR BANK","건너편"},{"RETURN","입구로"},
                {"HIGH LEDGE","위쪽 선반"},{"OPEN DOOR","작은 수차"},{"DIVERT","유출"},
                {"DOCK LEAF","정비 부두"},{"TO WATERWHEEL","주 출구"},{"ALIGN CHANNEL","수로 정렬"},{"MISALIGN","수로 분리"}
            };
            foreach(var label in world.GetComponentsInChildren<TextMesh>(true))
            {
                string original=label.text.Split('\n')[0];var billboard=label.GetComponent<EFV2Billboard>();
                label.font=font;label.GetComponent<Renderer>().sharedMaterial=font.material;
                if(billboard!=null)
                {
                    billboard.maxDistance=billboard.target!=null?0:14;
                    if(billboard.target!=null)continue;
                }
                if(map.TryGetValue(original,out var local))signs[label]=local;
                else if(world.checkpoints.Any(c=>c.label==original))
                {signs[label]=Place(original);if(billboard!=null)billboard.maxDistance=9;}
                else if(original.Contains(" / "))signs[label]=ShortDirection(original);
            }
        }
        void Styles()
        {
            if(body!=null)return;
            body=new GUIStyle(GUI.skin.label){font=font,fontSize=19,wordWrap=true,richText=false,normal={textColor=ink}};
            small=new GUIStyle(body){fontSize=15,normal={textColor=muted}};
            title=new GUIStyle(body){fontSize=25,fontStyle=FontStyle.Bold};
            large=new GUIStyle(title){fontSize=32,alignment=TextAnchor.MiddleCenter};
            button=new GUIStyle(GUI.skin.button){font=font,fontSize=21};
            helpBody=new GUIStyle(body){fontSize=18};
            iconLabel=new GUIStyle(body){fontSize=17,alignment=TextAnchor.MiddleCenter};
            hintLabel=new GUIStyle(small){fontSize=15,alignment=TextAnchor.MiddleCenter};
        }
        void Fill(Rect rect,Color color){var old=GUI.color;GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=old;}
        void Panel(Rect rect,Color accent){Fill(rect,new Color(.08f,.18f,.12f,.55f));}
        void Text(Rect rect,string text,GUIStyle style,Color? color=null)
        {var old=GUI.color;var textColor=style.normal.textColor;GUI.color=Color.white;style.normal.textColor=new Color(0,0,0,.7f);GUI.Label(new Rect(rect.x+1,rect.y+1,rect.width,rect.height),text,style);style.normal.textColor=color??textColor;GUI.Label(rect,text,style);style.normal.textColor=textColor;GUI.color=old;}
        void OnGUI()
        {
            if(world==null||!world.Built||font==null)return;
            Styles();float scale=Mathf.Min(Screen.width/1600f,Screen.height/900f),width=Screen.width/scale,height=Screen.height/scale;
            var old=GUI.matrix;GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,Vector3.one*scale);
            try {Draw(width,height);}finally{GUI.matrix=old;GUI.color=Color.white;}
        }
        void Draw(float width,float height)
        {
            var w=world;float cx=width*.5f,cy=height*.5f;
            if(w.AbilityInputActive){DrawAbilityMode(width,height);return;}
            if(w.OpeningActive&&!w.Paused)
            {
                Text(new Rect(cx-330,height-122,660,44),"EVERFLOW",large);
                Text(new Rect(cx-330,height-74,660,30),"유적의 석문을 열고, 멈춘 물레방아에 흐름을 돌려주세요",hintLabel);return;
            }
            if(!w.InputActive&&!w.Paused)
            {
                Text(new Rect(cx-330,height-122,660,44),"멈췄던 흐름이 돌아왔습니다",large);
                Text(new Rect(cx-330,height-74,660,30),"D4의 열린 문을 지나 정원으로",hintLabel);return;
            }
            Text(new Rect(30,24,500,25),Area,small,ink);
            Text(new Rect(30,55,530,38),GoalTitle,title);
            Text(new Rect(30,98,510,54),GoalDetail,small,ink);
            Text(new Rect(width-250,24,226,26),"F1  조작 도움말",small,ink);
            if(IsUnderground&&!w.Restored)
            {
                for(int i=0;i<4;i++)Text(new Rect(width-310,66+i*28,286,27),CircuitLines[i],small,ink);
                if(w.Arriving){Fill(new Rect(width-306,188,274,3),muted);Fill(new Rect(width-306,188,274*Mathf.Clamp01(w.StableFor/w.successSeconds),3),colors[2]);}
            }
            DrawAbilities(width,height);
            Text(new Rect(26,height-62,390,25),"WASD 이동 · Space 점프 · 마우스 시점",small,ink);
            Text(new Rect(26,height-35,390,25),"R 가까운 복귀 · Esc 일시정지",small,ink);
            if(Time.unscaledTime<toastUntil&&!HelpOpen)Text(new Rect(cx-330,height-188,660,30),toast,hintLabel,ink);
            if(!HelpOpen&&!w.Paused)DrawGoalMarker();
            if(HelpOpen)DrawHelp(width,height);
            if(w.Paused)
            {
                Fill(new Rect(0,0,width,height),new Color(.02f,.07f,.045f,.65f));
                Text(new Rect(cx-220,cy-112,440,45),"일시정지",large);
                Text(new Rect(cx-220,cy-56,440,40),"Esc로 계속하기 · F1 조작 도움말",hintLabel);
                if(GUI.Button(new Rect(cx-210,cy+6,420,54),"계속하기",button))w.SetPaused(false);
                if(w.Restored&&GUI.Button(new Rect(cx-210,cy+76,420,54),"처음부터 다시 시작",button))w.Restart();
            }
        }
        public static Rect ModeHeader(float width,float height)=>new Rect(24,24,530,98);
        public static Rect ModeContext(float width,float height)=>new Rect(24,height-158,440,134);
        public static Rect ModeFlow(float width,float height)=>new Rect(width-454,height-310,430,286);
        public static Rect ModeCancel(float width,float height)=>new Rect(width-180,24,156,44);
        public static Rect FlowOption(float width,float height,int index)
        {var r=ModeFlow(width,height);return new Rect(r.x+16,r.y+78+index*42,r.width-32,36);}
        public static Rect FlowConfirm(float width,float height)
        {var r=ModeFlow(width,height);return new Rect(r.x+16,r.y+220,r.width-32,48);}
        Vector2 GuiPoint(Vector2 screen,out float width,out float height)
        {
            float scale=Mathf.Min(Screen.width/1600f,Screen.height/900f);
            width=Screen.width/scale;height=Screen.height/scale;
            return new Vector2(screen.x/scale,(Screen.height-screen.y)/scale);
        }
        public bool BlocksWorldPointer(Vector2 screen)
        {
            if(!world.AbilityInputActive)return false;
            var p=GuiPoint(screen,out var width,out var height);
            return ModeHeader(width,height).Contains(p)||ModeContext(width,height).Contains(p)||ModeCancel(width,height).Contains(p)||AbilityPanelRect(width,height).Contains(p)||
                world.PendingFlow!=null&&ModeFlow(width,height).Contains(p)||
                HelpOpen&&new Rect(width-426,Mathf.Min(280,height-560),402,510).Contains(p);
        }
        // The world's input owner dispatches these same rectangles, including synthetic Input System events.
        public void HandleAbilityClick(Vector2 screen)
        {
            var p=GuiPoint(screen,out var width,out var height);
            if(ModeCancel(width,height).Contains(p)){world.CancelAbility();return;}
            if(HelpOpen)return;
            if(world.PendingFlow==null)return;
            for(int i=0;i<world.PendingFlow.directions.Length;i++)
                if(FlowOption(width,height,i).Contains(p)){world.ChooseFlow(i);return;}
            if(FlowConfirm(width,height).Contains(p))world.ConfirmFlow();
        }
        void DrawAbilityMode(float width,float height)
        {
            var w=world;Color accent=colors[(int)w.Selected];
            var r=ModeHeader(width,height);
            Text(new Rect(r.x,r.y,r.width,34),w.Selected.ToString().ToUpper(),title,accent);
            Text(new Rect(r.x,r.y+40,r.width,26),"사용 가능한 물체가 능력 색으로 빛납니다",body);
            Text(new Rect(r.x,r.y+72,r.width,25),"좌클릭 사용 · 1 / 2 / 3 변경 · Esc 취소",small,ink);
            Fill(ModeCancel(width,height),new Color(.08f,.18f,.12f,.42f));Text(ModeCancel(width,height),"Esc  취소",iconLabel);
            bool choosingDirection=w.PendingFlow!=null&&w.Hovered==null;
            r=ModeContext(width,height);
            Text(new Rect(r.x,r.y,r.width,33),choosingDirection?TargetName(w.PendingFlow):w.Hovered!=null?ContextTitle:"빛나는 물체를 선택하세요",title);
            Text(new Rect(r.x,r.y+40,r.width,40),choosingDirection?"선택한 방향: "+ShortDirection(w.PendingFlow.directions[w.FlowChoice]):w.Hovered!=null?ContextState:"세계 정지 · 확정 전에는 적용되지 않습니다",small,ink);
            Text(new Rect(r.x,r.y+86,r.width,46),choosingDirection?"방향 선택 후 사용 확정":w.Hovered!=null?ContextAction:AbilityDescription,small,ContextValid||choosingDirection?accent:ink);
            if(w.PendingFlow!=null)
            {
                r=ModeFlow(width,height);
                Text(new Rect(r.x+16,r.y+8,r.width-32,34),TargetName(w.PendingFlow)+" · 방향",title);
                Text(new Rect(r.x+16,r.y+46,r.width-32,26),"마우스 또는 E / Tab / 휠로 선택",small,ink);
                for(int i=0;i<w.PendingFlow.directions.Length;i++)
                {
                    var option=FlowOption(width,height,i);
                    Fill(option,i==w.FlowChoice?new Color(.4f,.2f,.045f,.65f):new Color(.07f,.16f,.1f,.5f));
                    Text(option,(i==w.FlowChoice?"▶  ":"    ")+ShortDirection(w.PendingFlow.directions[i]),iconLabel,i==w.FlowChoice?accent:ink);
                }
                var confirm=FlowConfirm(width,height);Fill(confirm,new Color(.07f,.16f,.1f,.65f));Text(confirm,"선택한 방향으로 사용 확정",iconLabel);
            }
            DrawAbilities(width,height);
            if(HelpOpen)DrawHelp(width,height);
            if(Mouse.current!=null)
            {
                var point=GuiPoint(Mouse.current.position.ReadValue(),out _,out _);Color cursor=ContextValid?accent:ink;
                Fill(new Rect(point.x-7,point.y-2,14,4),panel);Fill(new Rect(point.x-2,point.y-7,4,14),panel);
                Fill(new Rect(point.x-6,point.y-1,12,2),cursor);Fill(new Rect(point.x-1,point.y-6,2,12),cursor);
            }
        }
        public string AbilityDescription => world.Selected==LabAbility.Bloom?"식물을 성장시켜 길을 만듭니다":
            world.Selected==LabAbility.Freeze?"물을 얼리거나 움직이는 구조물을 고정합니다":"물살과 바람의 방향을 바꿉니다";
        public static Rect AbilityPanelRect(float width,float height)=>new Rect(width*.5f-190,height-144,380,126);
        void DrawAbilities(float width,float height)
        {
            var bounds=AbilityPanelRect(width,height);
            for(int i=0;i<3;i++)
            {
                bool selected=world.AbilityInputActive&&(int)world.Selected==i;
                float x=width*.5f+(i-1)*126,size=selected?66:60;
                var p=world.presentation;
                if(p!=null&&p.abilityIcons.Length>i&&p.abilityIcons[i]!=null)
                {
                    var previous=GUI.color;GUI.color=Color.white;
                    var uv=p.iconUV.Length>i?p.iconUV[i]:new Rect(0,0,1,1);
                    GUI.DrawTextureWithTexCoords(new Rect(x-size*.5f,height-140+(66-size)*.5f,size,size),p.abilityIcons[i].texture,uv,true);GUI.color=previous;
                }
                Text(new Rect(x-62,height-70,124,27),(i+1)+"  "+((LabAbility)i).ToString().ToUpper(),iconLabel,selected?colors[i]:ink);
            }
            Fill(new Rect(bounds.x,height-38,bounds.width,25),new Color(.1f,.23f,.12f,.38f));
            Text(new Rect(bounds.x,height-38,bounds.width,25),world.AbilityInputActive?AbilityDescription:"1 / 2 / 3 · 능력 선택",hintLabel,ink);
        }
        void DrawGoalMarker()
        {
            if(string.IsNullOrEmpty(GoalMarker))return;
            Vector3 offset=GoalPosition-(world.player.transform.position-Vector3.up);
            Vector3 right=Vector3.ProjectOnPlane(world.player.view.transform.right,Vector3.up).normalized;
            Vector3 forward=Vector3.Cross(right,Vector3.up);
            float side=Vector3.Dot(offset,right),ahead=Vector3.Dot(offset,forward);
            string arrow=Mathf.Abs(side)>Mathf.Abs(ahead)?(side>0?"→":"←"):(ahead>0?"↑":"↓");
            Text(new Rect(30,151,500,25),$"{arrow}  다음 · {GoalMarker}  {Mathf.CeilToInt(offset.magnitude)}m",small,ink);
        }
        void DrawHelp(float width,float height)
        {
            float x=width-426,y=Mathf.Min(280,height-560);
            Panel(new Rect(x,y,402,510),colors[1]);
            Text(new Rect(x+20,y+14,364,36),"세 능력으로 길을 만드세요",title);
            Text(new Rect(x+20,y+62,364,395),
                "1 / 2 / 3 · 세계를 멈추고 능력 선택\n커서로 대상 선택 · 좌클릭 사용\nEsc · 사용 취소 후 탐색으로\n\n1  BLOOM · 뿌리·덩굴·잎 성장\n2  FREEZE · 구조물 고정 / 물 동결\n재사용 또는 X: 고정 해제·해동\n물 동결은 물길을 막을 수 있습니다.\n3  FLOW · 물살·바람 방향 변경\n대상 → 방향 버튼 → 사용 확정\nE / Tab / 휠 → 대상 재클릭도 가능\n\n빛나는 물체에 능력을 사용할 수 있습니다.\n선택한 물체를 클릭해 사용하세요.\n능력을 다시 사용해도 완료된 효과는 유지됩니다.\n\nR · 가까운 안전 지점에서 재시도",helpBody);
            Text(new Rect(x+20,y+471,364,26),"F1 닫기 · 세 능력은 시작부터 모두 사용 가능",small);
        }
    }
}
