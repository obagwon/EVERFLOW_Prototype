using UnityEngine;

namespace EverflowMap
{
    [DisallowMultipleComponent]
    public sealed class EFWindmillInterior : MonoBehaviour
    {
        public Transform sails, driveWheel;
        public EF1123Motion outletGate;
        public GameObject outletWater;
        float angularSpeed;

        public bool Restored => outletGate.AtOpenPose;

        internal void Tick(float delta, bool powered)
        {
            if (!powered) return;
            angularSpeed = Mathf.MoveTowards(angularSpeed, 66f, delta * 44f);
            sails.Rotate(Vector3.forward, angularSpeed * delta, Space.Self);
            driveWheel.Rotate(Vector3.right, -angularSpeed * delta * .55f, Space.Self);
            if (Restored && !outletWater.activeSelf) outletWater.SetActive(true);
        }

        internal string Mission(int stage) => stage switch
        {
            0 => "순환 장치의 끊긴 축을 이으세요.",
            1 => "수조에 물이 차도록 누수를 막으세요.",
            2 => "풍차 날개에 바람을 보내세요.",
            _ => Restored ? "계곡으로 나가는 물길을 확인하세요." : "순환 장치가 다시 돌아갑니다."
        };

        internal string Instruction(int stage, bool tankFull) => stage switch
        {
            0 => "1 BLOOM · 축 사이에 있는 초록 싹을 클릭하세요.",
            1 => "2 FREEZE · 왼쪽 수조의 누수 지점을 얼리세요.",
            2 => tankFull ? "3 FLOW · 창 너머 날개 쪽으로 바람을 보내세요." : "얼음이 누수를 막았습니다. 수위가 오르는 중입니다.",
            _ => Restored ? "물길이 열렸습니다. 창 오른쪽에서 흐르는 물을 보세요." : "날개가 돌고 출수문이 열리는 중입니다."
        };
    }
}
