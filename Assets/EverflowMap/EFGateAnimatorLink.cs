using UnityEngine;

namespace EverflowMap
{
    // 퍼즐 문(EF1123Motion)이 열리면 에셋 문의 Animator도 열리게 연결해 주는 스크립트.
    // 에셋 문(WoodenWall_Gate 등) 최상위 오브젝트에 붙이고, Source에 퍼즐 문을 넣으세요.
    [DisallowMultipleComponent]
    public sealed class EFGateAnimatorLink : MonoBehaviour
    {
        [Tooltip("퍼즐 스크립트가 여는 문 (예: PH_D_Gate - working gate)")]
        public EF1123Motion source;

        [Tooltip("비워 두면 이 오브젝트의 Animator를 자동으로 찾아요")]
        public Animator anim;

        public string openBool = "DoorOpen";
        public string closeBool = "DoorClose";

        bool last;
        bool first = true;

        void Awake()
        {
            if (anim == null) anim = GetComponent<Animator>();
        }

        void Update()
        {
            if (source == null || anim == null) return;
            bool open = source.IsOpen;
            if (!first && open == last) return;
            first = false;
            last = open;
            anim.SetBool(openBool, open);
            anim.SetBool(closeBool, !open);
        }
    }
}
