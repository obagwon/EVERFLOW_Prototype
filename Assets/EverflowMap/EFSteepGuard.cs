using System.Reflection;
using UnityEngine;

namespace EverflowMap
{
    // 벽타기 막기: 가파른 경사(기본 44° 이상)에 발이 닿아 있으면 점프를 못 하고 아래로 미끄러져요.
    // 플레이어(EFMapPlayer 가 있는 오브젝트)에 붙이세요. EFMapPlayer 는 고치지 않아요.
    [DefaultExecutionOrder(50)]
    [RequireComponent(typeof(CharacterController))]
    public sealed class EFSteepGuard : MonoBehaviour
    {
        [Tooltip("이보다 가파르면 서 있을 수 없음 (도)")]
        [Range(20, 80)] public float maxSlope = 44f;
        [Tooltip("가파른 곳에서 미끄러지는 속도 (m/s)")]
        public float slideSpeed = 7f;

        CharacterController cc;
        EFMapPlayer player;
        FieldInfo airGrace;
        Vector3 normal;
        float bestUp;
        bool touched;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            player = GetComponent<EFMapPlayer>();
            airGrace = typeof(EFMapPlayer).GetField("airGrace", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            // 발 쪽(캡슐 아래 반구)에 닿은 것만
            float feet = transform.position.y + cc.center.y - cc.height * .5f + cc.radius;
            if (hit.point.y > feet) return;
            if (!touched || hit.normal.y > bestUp) { bestUp = hit.normal.y; normal = hit.normal; touched = true; }
        }

        void LateUpdate()
        {
            if (player == null || !player.enabled || player.Traversing || player.Riding) { touched = false; return; }
            bool steep = touched && bestUp < Mathf.Cos(maxSlope * Mathf.Deg2Rad) && bestUp > .02f;
            touched = false;
            if (!steep) return;
            if (airGrace != null) airGrace.SetValue(player, .12f);          // 이번엔 '땅에 서 있음' 이 아니게 → 점프 불가
            Vector3 slide = Vector3.ProjectOnPlane(Vector3.down, normal).normalized;
            cc.Move(slide * slideSpeed * Time.deltaTime);
        }
    }
}
