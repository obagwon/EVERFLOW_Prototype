using UnityEngine;

namespace EverflowMap
{
    // 다른 장치가 다 열린 뒤에야 이 장치가 흔들리기 시작하게 하는 스크립트.
    // 예: W 도개교. 풍차(FLOW)로 다리가 내려온 다음부터 경첩에서 위아래로 흔들리고,
    //     알맞은 경사에 왔을 때 FREEZE 하면 멈춘다 (EF1123Target 의 timedPlatform).
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EF1123Motion))]
    public sealed class EFSwayGate : MonoBehaviour
    {
        [Tooltip("이 장치가 다 열려야 흔들리기 시작 (예: Opened by the windmill rope)")]
        public EF1123Motion gate;

        [Tooltip("흔들림 (도). 한 축만 쓰세요. 예: 경첩이 Z축이면 (0, 0, 18)")]
        public Vector3 sway = new Vector3(0, 0, 18);

        EF1123Motion self;

        void Awake()
        {
            self = GetComponent<EF1123Motion>();
            self.sway = Vector3.zero;          // 시작할 땐 가만히
        }

        void Update()
        {
            if (self == null || gate == null) return;
            var want = gate.AtOpenPose ? sway : Vector3.zero;   // 얼린 뒤에도 값은 두어야 부드럽게 멈춤
            if (self.sway != want) self.sway = want;
        }
    }
}
