using UnityEngine;
namespace EverflowMap
{
    public sealed class EFV2Plant : MonoBehaviour
    {
        public Vector3 dryScale, wetScale;
        public Color dryColor, wetColor;
        public Renderer[] foliage;
        public float delay;

        const float GrowthSeconds = .68f;
        MaterialPropertyBlock tint;
        float elapsed;

        public void ResetGrowth()
        {
            elapsed = 0;
            ApplyGrowth(0);
        }

        public void AdvanceGrowth(float dt)
        {
            float end = delay + GrowthSeconds;
            if (dt <= 0 || elapsed >= end) return;
            elapsed = Mathf.Min(elapsed + dt, end);
            if (elapsed <= delay) return;
            float progress = elapsed >= end ? 1 : Mathf.SmoothStep(0, 1, (elapsed - delay) / GrowthSeconds);
            ApplyGrowth(progress);
        }

        void ApplyGrowth(float progress)
        {
            transform.localScale = Vector3.Lerp(dryScale, wetScale, progress);
            Color color = Color.Lerp(dryColor, wetColor, progress);
            tint ??= new MaterialPropertyBlock();
            tint.SetColor("_BaseColor", color);
            tint.SetColor("_Color", color);
            foreach (var blade in foliage) blade.SetPropertyBlock(tint);
        }
    }
}
