using UnityEngine;

namespace EverflowMap
{
    // 숨겨진 압력발판: 덮고 있던 것(낙엽 더미)이 사라진 뒤 플레이어가 올라서면 눌리고, Opens 의 장치를 열어요.
    [DisallowMultipleComponent]
    public sealed class EFPressurePlate : MonoBehaviour
    {
        public EF1123World world;
        [Tooltip("이게 켜져 있는 동안은 눌리지 않음 (예: D dancing pollen 낙엽 더미)")]
        public GameObject cover;
        [Tooltip("눌리면 여는 장치들 (비워 두면 눌리기만 함)")]
        public EF1123Motion[] opens = new EF1123Motion[0];
        [Tooltip("켜면 한 번 눌리면 계속 눌린 상태")]
        public bool latch = true;
        public float radius = .8f;
        public float sink = .08f;

        Vector3 home;
        bool pressed, done;
        float k;

        void Start() { home = transform.localPosition; if (world == null) world = FindAnyObjectByType<EF1123World>(); }

        void Update()
        {
            if (world == null || world.player == null) return;
            Vector3 p = world.player.transform.position, c = transform.position;
            bool on = (cover == null || !cover.activeInHierarchy) && world.player.Grounded &&
                      new Vector2(p.x - c.x, p.z - c.z).sqrMagnitude <= radius * radius && Mathf.Abs(p.y - c.y) < 1.6f;
            pressed = on || (latch && done);
            if (on && !done)
            {
                done = true;
                foreach (var m in opens) if (m != null) m.Open();
            }
            k = Mathf.MoveTowards(k, pressed ? 1 : 0, Time.deltaTime * 6);
            transform.localPosition = home - Vector3.up * (sink * k);
        }
    }
}
