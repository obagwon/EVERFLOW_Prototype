using UnityEngine;

namespace EverflowMap
{
    // BLOOM 으로 자라는 식물: 처음엔 시든 색, 장치가 열리면(자라면) 원래 색으로.
    // 머티리얼을 바꾸지 않고 MaterialPropertyBlock 으로 _BaseColor 만 곱해서 바꿔요.
    // Source 를 비우면 오브젝트가 켜질 때부터 초록으로 바뀌어요 (D 잎 계단처럼 퍼즐이 켜서 보여 주는 경우).
    [DisallowMultipleComponent]
    public sealed class EFBloomTint : MonoBehaviour
    {
        public EF1123Motion source;
        public Color withered = new Color(0.55f, 0.42f, 0.25f, 1f);
        public Color grown = Color.white;
        [Min(.05f)] public float seconds = 1.0f;

        Renderer[] renderers;
        MaterialPropertyBlock block;
        float t = -1;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int LegacyColor = Shader.PropertyToID("_Color");

        void OnEnable()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            block ??= new MaterialPropertyBlock();
            t = 0; Apply(0);
        }

        void Update()
        {
            bool open = source == null || source.IsOpen;
            float target = open ? 1 : 0;
            if (Mathf.Approximately(t, target)) return;
            t = Mathf.MoveTowards(t, target, Time.deltaTime / seconds);
            Apply(t);
        }

        void Apply(float k)
        {
            var c = Color.Lerp(withered, grown, Mathf.SmoothStep(0, 1, k));
            foreach (var r in renderers)
            {
                if (r == null || r is LineRenderer) continue;
                r.GetPropertyBlock(block);
                block.SetColor(BaseColor, c);
                block.SetColor(LegacyColor, c);
                r.SetPropertyBlock(block);
            }
        }
    }
}
