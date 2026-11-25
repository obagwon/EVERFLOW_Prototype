using UnityEngine;

namespace EverflowMap
{
    // 줄감개의 줄 통에서 다리 끝까지 줄을 그려 주는 스크립트.
    // 줄감개가 돌거나 다리가 올라가도 매 프레임 따라가요.
    // 사용: 빈 오브젝트(예: A2 Rope)에 붙이고 Spool, Target을 넣으세요. LineRenderer는 자동으로 붙어요.
    [ExecuteAlways]
    [RequireComponent(typeof(LineRenderer))]
    public sealed class EFRopeLink : MonoBehaviour
    {
        [Tooltip("줄 감는 통의 중심. EF_A2_Drum 안의 RopeSpool 을 넣으세요. 이 오브젝트의 X축(빨간 화살표)이 통의 회전축이어야 해요.")]
        public Transform spool;

        [Tooltip("줄 통 반지름 (블렌더 스크립트가 출력한 값, 기본 0.42m)")]
        public float spoolRadius = 0.42f;

        [Tooltip("줄이 묶이는 곳. 다리 끝(경첩 반대쪽)에 빈 오브젝트를 만들어 다리의 자식으로 넣고 여기에 연결하세요.")]
        public Transform target;

        [Tooltip("줄 굵기 (m)")]
        public float width = 0.06f;

        [Tooltip("줄이 아래로 처지는 정도 (줄 길이 대비). 팽팽하면 0")]
        [Range(0, 0.2f)] public float sag = 0.02f;

        [Min(2)] public int segments = 12;

        LineRenderer line;

        void OnEnable()
        {
            line = GetComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = segments + 1;
            line.startWidth = line.endWidth = width;
            line.numCapVertices = 2;
            line.textureMode = LineTextureMode.Tile;
        }

        void LateUpdate()
        {
            if (spool == null || target == null) return;
            if (line == null) OnEnable();
            if (line.positionCount != segments + 1) line.positionCount = segments + 1;
            line.startWidth = line.endWidth = width;

            Vector3 axis = spool.right;
            Vector3 center = spool.position;
            Vector3 end = target.position;

            // 통 표면에서 줄이 빠져나가는 점: 축에 수직인 면 위에서 목표 쪽 방향
            Vector3 toTarget = end - center;
            Vector3 radial = Vector3.ProjectOnPlane(toTarget, axis);
            if (radial.sqrMagnitude < 1e-6f) radial = Vector3.up;
            Vector3 start = center + radial.normalized * spoolRadius;

            float length = Vector3.Distance(start, end);
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                Vector3 p = Vector3.Lerp(start, end, t);
                p += Vector3.down * (4f * t * (1f - t) * sag * length);   // 가운데가 살짝 처짐
                line.SetPosition(i, p);
            }
        }
    }
}
