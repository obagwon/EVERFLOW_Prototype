using UnityEngine;

namespace EverflowPuzzleLab
{
    [ExecuteAlways, DefaultExecutionOrder(300)]
    public sealed class LabInfoBoard : MonoBehaviour
    {
        public LabArea area;
        public TextMesh title, goal, methods, controls, status;
        public TextMesh circuitStatus;
        public Renderer[] detailRenderers=new Renderer[0];
        public float detailDistance=30;
        void OnEnable() { Refresh(); }
        void LateUpdate() { Refresh(); }
        static string Mark(bool value) => value ? "연결" : "끊김";
        public void Refresh()
        {
            foreach(var text in GetComponentsInChildren<TextMesh>(true)) LabSignFont.Apply(text);
            if(area==null || status==null) return;
            string completion=area.Complete?"성공 · 목표 도달":"진행 중 · 목표 미도달";
            if(area.canal!=null) completion=area.canal.Success?"복원 완료":"복원 진행 중";
            else if(area.goalGate!=null) completion+="  |  문 "+(area.goalGate.Latched?"걸쇠 유지":area.goalGate.OpenAmount.ToString("P0"));
            status.text=completion;
            if(circuitStatus!=null && area.canal!=null)
            {
                var c=area.canal;
                string direction=c.branch.Direction==0?"부두":c.branch.Direction==1?"작은 수차":c.branch.Direction==2?"주 출구":"미선택";
                circuitStatus.text="직접 "+Mark(c.Connected)+" · 내부 물 "+(c.upperIce.Frozen?"얼음":"흐름")+" · 우회 "+(c.bypassRoot.Grown?"열림":"닫힘")+"\n"+
                    "공급 "+Mark(c.Supply)+" · 분기 "+direction+" · 문 "+(c.gate.Latched?"걸쇠":c.gate.IsOpen?"열림":"닫힘")+"\n"+
                    "주 얼음 "+(c.mainIce.Frozen?"있음":"없음")+" · W 도착 "+(c.Arriving?"확인":"없음")+" · "+c.StableFor.ToString("0.0")+" / "+c.successSeconds.ToString("0.0")+"초";
            }
            // Far boards retain only the title; the common layout is fully visible at each start.
            var camera=Application.isPlaying && area.lab!=null && area.lab.player!=null ? area.lab.player.view : null;
            bool details=camera==null || Vector3.Distance(camera.transform.position,transform.position)<=detailDistance;
            foreach(var item in detailRenderers) if(item!=null) item.enabled=details;
        }
    }
}
