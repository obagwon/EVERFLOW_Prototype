using UnityEngine;

namespace EverflowMap
{
    // 복원되면 차오르는 물 (호수) / 쏟아지는 물 (폭포). EFRestoration 이 Set(0~1) 로 움직여요.
    [DisallowMultipleComponent]
    public sealed class EFRestoreWater : MonoBehaviour
    {
        [Tooltip("어느 구역이 살아날 때 차오를지 (EFRestoration 의 Regions 번호)")]
        public int region;
        public bool waterfall;
        [Tooltip("호수: 마른 상태일 때 수면이 내려가 있는 깊이 (m)")]
        public float drop = 1.2f;
        [Tooltip("다 차오르는 데 걸리는 시간")]
        public float seconds = 3f;

        Renderer rend;
        MaterialPropertyBlock block;
        Vector3 home;
        bool ready;
        float shown = -1;

        void Init()
        {
            if (ready) return;
            rend = GetComponent<Renderer>();
            block = new MaterialPropertyBlock();
            home = transform.localPosition;
            ready = true;
        }

        public void Set(float v)
        {
            Init();
            v = Mathf.Clamp01(v);
            if (Mathf.Approximately(v, shown)) return;
            shown = v;
            float s = Mathf.SmoothStep(0, 1, v);
            rend.GetPropertyBlock(block);
            block.SetFloat("_Wet", v > .001f ? 1 : 0);
            if (waterfall) block.SetFloat("_Arrival", s);
            else block.SetFloat("_WorldUV", 1);
            rend.SetPropertyBlock(block);
            if (!waterfall) transform.localPosition = home - Vector3.up * (drop * (1 - s));
        }
    }
}
