using System;
using System.Collections.Generic;
using System.Linq;
using EverflowPuzzleLab;
using UnityEngine;
using UnityEngine.InputSystem;

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
        public string GoalMarker {get;private set;}="A · 첫 둔덕";
        Font font;
        GUIStyle body,small,title,large,button,helpBody;
        string previousEvent="",toast="";
        float toastUntil;
        readonly Color ink=new Color(.94f,.97f,.95f),muted=new Color(.67f,.76f,.76f),panel=new Color(.025f,.055f,.07f,.94f);
        readonly Color[] colors={new Color(.57f,.88f,.39f),new Color(.47f,.83f,.96f),new Color(.46f,.69f,1)};
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
            SkinWorldSigns();
        }
        void Update()
        {
            if(Keyboard.current!=null&&Keyboard.current.f1Key.wasPressedThisFrame)HelpOpen=!HelpOpen;
        }
        void SetGoal(string titleText,string detail,string marker,Vector3 point)
        {GoalTitle=titleText;GoalDetail=detail;GoalMarker=marker;GoalPosition=point;}
        public void Refresh()
        {
            if(world==null||!world.Built)return;
            var w=world;string area=w.CurrentCheckpoint!=null?w.CurrentCheckpoint.label:"S";
            Area=Place(area);IsUnderground=area.StartsWith("D")||area.StartsWith("RESTORED");
            if(w.Restored)
                SetGoal(w.GardenReached?"정원이 되살아났습니다":"열린 수문을 지나 정원으로",
                    w.GardenReached?"자유롭게 탐험하세요. Enter: 처음부터 다시 시작":"D4 출구를 통과해 난간이 있는 회랑을 걸어가세요.","W · 복원된 정원",new Vector3(-84,-17.3f,0));
            else if(IsUnderground)
            {
                bool bench=w.returnWalk.activeSelf;
                if(!bench)
                    SetGoal("D3 작업대에 접근하세요","발코니 덩굴에 BLOOM 또는 아래 수조에 FREEZE로 길을 만드세요.","D3 · 작업대",new Vector3(-40,-6.7f,-57));
                else if(!w.Supply)
                {
                    if(w.upperIce.Frozen)
                        SetGoal("수로 안의 얼음을 녹이세요","물에 FREEZE를 다시 사용해 해동하거나, BLOOM으로 우회 수로를 여세요.","D5 · 수로 안의 물",w.upperIce.transform.position);
                    else if(w.channelStructure.Frozen&&!w.Aligned)
                        SetGoal("어긋난 수로의 고정을 푸세요","구조물에 FREEZE 재사용 → FLOW로 정렬 → 구조물을 다시 고정하세요.","D5 · 가동 수로",w.channelStructure.transform.position);
                    else if(w.Aligned&&!w.channelStructure.Frozen)
                        SetGoal("맞춰진 수로 구조물을 고정하세요","FREEZE로 구조물을 고정하세요. 수로 안의 물을 얼리면 공급이 막힙니다.","D5 · 수로 구조물",w.channelStructure.transform.position);
                    else
                        SetGoal("D5에서 물 공급을 연결하세요","FLOW로 수로를 맞추고 구조물에 FREEZE, 또는 서쪽 뿌리에 BLOOM을 사용하세요.",
                            w.Selected==LabAbility.Bloom?"D5 · 우회 수로":"D5 · 가동 수로",w.Selected==LabAbility.Bloom?w.bypass.transform.position:w.channelStructure.transform.position);
                }
                else if(!w.exitGate.IsOpen)
                    SetGoal(w.branch.Direction==1?"수문이 끝까지 열릴 때까지 기다리세요":"D4 출구 수문을 여세요",
                        w.branch.Direction==1?"작은 수차 방향을 유지하세요. 완전히 열리면 걸쇠가 고정됩니다.":"D2를 작은 수차 방향으로 돌리거나 수문 아래 뿌리에 BLOOM을 사용하세요.","D4 · 출구 수문",w.exitGate.root.transform.position);
                else if(w.branch.Direction!=2)
                    SetGoal("D2를 주 출구 방향으로 돌리세요","FLOW로 중앙 분기대를 조준하고 주 출구를 선택·확정하세요.","D2 · 중앙 분기대",w.branch.transform.position);
                else if(w.mainIce.Frozen)
                    SetGoal("주 물길을 막는 얼음을 녹이세요","중앙 수조의 물에 FREEZE를 다시 사용하세요.\n조준 후 X로도 해동할 수 있습니다.","D2 · 중앙 수조의 물",w.mainIce.transform.position);
                else
                    SetGoal("물레방아에 물이 도착합니다","현재 물길을 유지하세요. 연속 공급이 완료되면 물레방아가 복원됩니다.","W · 물레방아",w.wheel.position);
            }
            else if(area.StartsWith("TOWER"))
                SetGoal("탑 안의 계단을 따라 내려가세요","아래 터널을 지나 지하 발코니 D1로 이동하세요.","D1 · 지하 발코니",new Vector3(-48,-2,-38.7f));
            else if(area.StartsWith("G"))
                SetGoal(w.ruinGate.IsOpen?"열린 석문을 통과하세요":"유적의 석문을 여세요",
                    w.ruinGate.IsOpen?"문 뒤 탑 안의 계단과 터널이 지하로 이어집니다.":"분기에 FLOW로 작은 수차에 물을 보내거나 문 아래 뿌리에 BLOOM을 사용하세요.","G · 석문",new Vector3(-29.75f,16,-61.5f));
            else if(area.StartsWith("B FAR")||area.StartsWith("C HIGH"))
                SetGoal("계단을 올라 고지 유적으로","B와 C는 같은 유적에서 만납니다. 다른 경로까지 완료할 필요는 없습니다.","G · 고지 유적",new Vector3(-35,16,-60.5f));
            else if(area.StartsWith("B"))
                SetGoal("끊긴 길의 건너편으로","뿌리에 BLOOM 또는 풍차에 FLOW → 돌 발판에 FREEZE를 사용하세요.","B · 건너편 선반",new Vector3(-25,7,-51));
            else if(area.StartsWith("C"))
            {
                var leaf=Target("C GROWABLE LEAF");
                SetGoal(leaf!=null&&!leaf.Grown?"이동할 잎을 크게 키우세요":"잎을 타고 위쪽 선반으로",
                    leaf!=null&&!leaf.Grown?"잎에 BLOOM을 사용한 뒤 올라타세요. FLOW로 바람 방향을 바꿀 수 있습니다.":"바람에 FLOW로 위쪽 선반을 선택하세요. FREEZE로 잎을 멈추고 안전하게 내리세요.","C · 위쪽 선반",new Vector3(-62,12.5f,-59));
            }
            else if(area.StartsWith("A COMMON"))
                SetGoal("B 또는 C로 유적을 향하세요","풍차가 있는 B 또는 큰 잎이 있는 C로 가세요.\n중간 연결길에서 경로를 바꿀 수 있습니다.","G · 고지 유적",new Vector3(-45,16,-61));
            else
                SetGoal("첫 둔덕에 올라가세요","뿌리에 BLOOM으로 계단을 만들거나, 잎에 올라 물살에 FLOW → 잎에 FREEZE를 사용하세요.","A · 첫 둔덕",new Vector3(-45,6.7f,-28));
            CircuitLines[0]=w.Supply?"공급 연결됨":w.upperIce.Frozen?"공급 막힘 · 내부 물 해동":w.channelStructure.Frozen&&!w.Aligned?"공급 없음 · 수로 고정 해제":"공급 없음 · D5 연결 필요";
            CircuitLines[1]="D2 방향 · "+(w.branch.Direction<0?"미선택":w.branch.Direction==0?"정비 부두":w.branch.Direction==1?"작은 수차":"주 출구");
            CircuitLines[2]=w.exitGate.IsOpen?"D4 수문 열림 · 걸쇠 유지":"D4 수문 닫힘";
            CircuitLines[3]=w.mainIce.Frozen?"주 물길 얼음 · 해동 필요":"주 물길 얼음 차단 없음";
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
            if(text.Contains("자동 회수"))return "구체가 돌아옵니다 · 완료된 효과는 유지됩니다";
            if(text.Contains("새 대상으로"))return "구체가 새 대상으로 이동합니다 · 이전 효과 유지";
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
        }
        void Fill(Rect rect,Color color){var old=GUI.color;GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=old;}
        void Panel(Rect rect,Color accent){Fill(rect,panel);Fill(new Rect(rect.x,rect.y,3,rect.height),accent);}
        void Text(Rect rect,string text,GUIStyle style,Color? color=null)
        {var old=GUI.color;var textColor=style.normal.textColor;GUI.color=Color.white;if(color.HasValue)style.normal.textColor=color.Value;GUI.Label(rect,text,style);style.normal.textColor=textColor;GUI.color=old;}
        void OnGUI()
        {
            if(world==null||!world.Built||font==null)return;
            Styles();float scale=Mathf.Min(Screen.width/1600f,Screen.height/900f),width=Screen.width/scale,height=Screen.height/scale;
            var old=GUI.matrix;GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,Vector3.one*scale);
            try {Draw(width,height);}finally{GUI.matrix=old;GUI.color=Color.white;}
        }
        void Draw(float width,float height)
        {
            var w=world;
            if(w.OpeningActive&&!w.Paused)
            {
                Panel(new Rect(width*.5f-330,height-144,660,100),colors[2]);
                Text(new Rect(width*.5f-310,height-132,620,42),"EVERFLOW",large);
                Text(new Rect(width*.5f-310,height-84,620,30),"유적의 석문을 열고, 멈춘 물레방아에 흐름을 돌려주세요",body);
                return;
            }
            if(!w.InputActive&&!w.Paused)
            {
                Panel(new Rect(width*.5f-330,height-144,660,100),colors[2]);
                Text(new Rect(width*.5f-310,height-132,620,42),"멈췄던 흐름이 돌아왔습니다",large);
                Text(new Rect(width*.5f-310,height-84,620,30),"연출이 끝나면 D4의 열린 문을 지나 정원으로 걸어가세요",body);
                return;
            }
            Panel(new Rect(24,24,550,148),colors[2]);
            Text(new Rect(42,35,514,25),Area,small);
            Text(new Rect(42,66,514,38),GoalTitle,title);
            Text(new Rect(42,110,514,54),GoalDetail,body);
            Panel(new Rect(width-272,24,248,30),muted);
            Text(new Rect(width-260,27,230,25),"F1  조작·구체 도움말",small);
            if(IsUnderground&&!w.Restored)
            {
                Panel(new Rect(width-330,61,306,196),colors[2]);
                Text(new Rect(width-312,73,270,27),"물레방아까지의 물길",body);
                for(int i=0;i<4;i++)
                {
                    bool good=i==0?w.Supply:i==1?w.branch.Direction==2:i==2?w.exitGate.IsOpen:!w.mainIce.Frozen;
                    Text(new Rect(width-312,109+i*32,272,29),(good?"✓ ":"! ")+CircuitLines[i],small,good?new Color(.65f,1,.69f):new Color(1,.82f,.45f));
                }
                if(w.Arriving){Fill(new Rect(width-312,242,270,5),muted);Fill(new Rect(width-312,242,270*Mathf.Clamp01(w.StableFor/w.successSeconds),5),colors[2]);}
            }
            DrawAbilities(width,height);
            Panel(new Rect(24,height-102,380,78),muted);
            Text(new Rect(38,height-94,352,26),"WASD 이동 · Space 점프 · 마우스 시점",small);
            Text(new Rect(38,height-62,352,26),"R 가까운 복귀 · Esc 일시정지",small);
            if(Time.unscaledTime<toastUntil&&!HelpOpen)
            {Panel(new Rect(24,height-149,456,43),colors[(int)w.Selected]);Text(new Rect(38,height-142,430,32),toast,small);}
            float cx=width*.5f,cy=height*.5f;
            Color reticle=w.Hovered==null?ink:ContextValid?colors[(int)w.Selected]:new Color(1,.55f,.4f);
            Fill(new Rect(cx-5,cy-1,10,2),reticle);Fill(new Rect(cx-1,cy-5,2,10),reticle);
            if(w.PendingFlow!=null&&!HelpOpen)
            {
                float x=width-554,y=height-450;
                Panel(new Rect(x,y,530,222),colors[2]);
                Text(new Rect(x+18,y+10,496,30),TargetName(w.PendingFlow)+" · 흐름 방향 선택",title);
                Text(new Rect(x+18,y+46,496,28),"E / Tab / 휠로 변경 → 같은 대상 좌클릭으로 확정",small);
                for(int i=0;i<w.PendingFlow.directions.Length;i++)
                {
                    bool current=i==w.FlowChoice;
                    if(current)Fill(new Rect(x+14,y+80+i*37,502,34),new Color(.11f,.22f,.31f));
                    Text(new Rect(x+24,y+83+i*37,480,31),(current?"▶  ":"    ")+ShortDirection(w.PendingFlow.directions[i]),body,current?colors[2]:ink);
                }
                Text(new Rect(x+18,y+194,496,26),"능력을 바꾸면 방향 선택을 취소합니다",small);
            }
            else if(w.Hovered!=null&&!HelpOpen)
            {
                float x=cx-265,y=height-346;
                Panel(new Rect(x,y,530,118),ContextValid?colors[(int)w.Selected]:new Color(1,.55f,.4f));
                Text(new Rect(x+18,y+8,494,33),ContextTitle,title);
                Text(new Rect(x+18,y+45,494,26),ContextState,small);
                Text(new Rect(x+18,y+78,494,33),ContextAction,body);
            }
            else if(!HelpOpen) {Panel(new Rect(cx-250,height-265,500,37),muted);Text(new Rect(cx-235,height-259,470,28),"1 / 2 / 3 선택 · 장치를 조준해 좌클릭",small);}
            if(w.Hovered==null&&w.PendingFlow==null&&!HelpOpen)DrawGoalMarker(width,height);
            if(HelpOpen)DrawHelp(width,height);
            if(w.Paused)
            {
                Fill(new Rect(0,0,width,height),new Color(0,.02f,.03f,.78f));
                Panel(new Rect(cx-250,cy-130,500,260),colors[2]);
                Text(new Rect(cx-220,cy-106,440,45),"일시정지",large);
                Text(new Rect(cx-220,cy-52,440,40),"Esc로 계속하기 · F1으로 조작 도움말",body);
                if(GUI.Button(new Rect(cx-210,cy+6,420,54),"계속하기",button))w.SetPaused(false);
                if(w.Restored&&GUI.Button(new Rect(cx-210,cy+76,420,54),"처음부터 다시 시작",button))w.Restart();
                if(HelpOpen)DrawHelp(width,height);
            }
        }
        public string AbilityDescription => world.Selected==LabAbility.Bloom?"식물을 성장시켜 길을 만듭니다":
            world.Selected==LabAbility.Freeze?"물을 얼리거나 움직이는 구조물을 고정합니다":"물살과 바람의 방향을 바꿉니다";
        public static Rect AbilityPanelRect(float width,float height)=>new Rect(width*.5f-280,height-216,560,192);
        void DrawAbilities(float width,float height)
        {
            Rect bounds=AbilityPanelRect(width,height);
            Panel(bounds,colors[(int)world.Selected]);
            for(int i=0;i<3;i++)
            {
                bool selected=(int)world.Selected==i;
                float x=width*.5f+(i-1)*160;
                float size=selected?82:74;
                var p=world.presentation;
                if(p!=null&&p.abilityIcons.Length>i&&p.abilityIcons[i]!=null)
                {
                    var previous=GUI.color;
                    GUI.color=selected?Color.white:new Color(.78f,.78f,.78f,1);
                    var uv=p.iconUV.Length>i?p.iconUV[i]:new Rect(0,0,1,1);
                    GUI.DrawTextureWithTexCoords(new Rect(x-size*.5f,height-202+(82-size)*.5f,size,size),p.abilityIcons[i].texture,uv,true);
                    GUI.color=previous;
                }
                Text(new Rect(x-63,height-116,132,29),(i+1)+"  "+((LabAbility)i).ToString().ToUpper(),body,selected?Color.white:muted);
                if(selected)Fill(new Rect(x-59,height-86,118,2),colors[i]);
                var phase=world.Slots[i].phase;
                string state=phase==EFV2OrbPhase.Flying?"이동 중":phase==EFV2OrbPhase.Placed?"배치됨":phase==EFV2OrbPhase.Returning?"돌아오는 중":"준비";
                Text(new Rect(x-50,height-80,122,23),state,small);
            }
            Text(new Rect(bounds.x+20,height-54,bounds.width-40,28),AbilityDescription,body);
        }
        void DrawGoalMarker(float width,float height)
        {
            var vp=world.player.view.WorldToViewportPoint(GoalPosition);
            float x=vp.x*width,y=(1-vp.y)*height;
            bool edge=vp.z<0||x<60||x>width-60||y<180||y>height-400;
            if(vp.z<0){x=width-x;y=height-y;}
            x=Mathf.Clamp(x,80,width-205);y=Mathf.Clamp(y,190,height-400);
            if(x<590&&y<180||x>width-340&&y<270)return;
            float distance=Vector3.Distance(world.player.transform.position,GoalPosition);
            if(distance<2.5f)return;
            string arrow=edge?(x<width*.25f?"← ":x>width*.75f?"→ ":y<height*.5f?"↑ ":"↓ "):"◇ ";
            Panel(new Rect(x-10,y-13,195,49),colors[2]);
            Text(new Rect(x,y-9,185,24),arrow+GoalMarker,small);
            Text(new Rect(x+18,y+13,165,22),$"목표까지 {distance:0}m",small);
        }
        void DrawHelp(float width,float height)
        {
            float x=width-426,y=Mathf.Min(280,height-560);
            Panel(new Rect(x,y,402,510),colors[1]);
            Text(new Rect(x+20,y+14,364,36),"세 구체로 길을 만드세요",title);
            Text(new Rect(x+20,y+62,364,395),
                "1  BLOOM · 뿌리·덩굴·잎 성장\n\n2  FREEZE · 구조물 고정 / 물 동결\n재사용 또는 X: 고정 해제·해동\n물 동결은 물길을 막을 수 있습니다.\n\n3  FLOW · 물살·바람 방향 변경\n클릭 → E / Tab / 휠 → 클릭 확정\n\n구체는 능력마다 하나입니다.\n다른 유효 대상에 사용: 바로 이동\n배치한 구체에서 "+world.recallDistance.ToString("0")+"m 멀어짐: 자동 복귀\n완료된 효과는 그대로 유지됩니다.\n\nR · 가까운 안전 지점에서 재시도",helpBody);
            Text(new Rect(x+20,y+471,364,26),"F1 닫기 · 세 능력은 시작부터 모두 사용 가능",small);
        }
    }
}
