using UnityEngine;

namespace EverflowMap
{
    [DefaultExecutionOrder(-100)]
    public sealed class EFWindmillEpilogue : MonoBehaviour
    {
        public EF1123World world;
        public Transform sails, overlook;
        public Transform[] blossoms;
        public ParticleSystem meadowWind;
        const float Duration = 6f;
        Vector3[] blossomSizes;
        float elapsed, rotationSpeed;
        public bool Playing { get; private set; }

        internal void Play()
        {
            blossomSizes = new Vector3[blossoms.Length];
            for (int i = 0; i < blossoms.Length; i++)
            {
                blossomSizes[i] = blossoms[i].localScale;
                blossoms[i].localScale = Vector3.zero;
                blossoms[i].gameObject.SetActive(true);
            }
            meadowWind.Play();
            Playing = true;
            world.effects.ShowCutaway(overlook, Duration);
        }

        void Update()
        {
            if (world.Paused || (!Playing && !world.Finished)) return;
            rotationSpeed = Mathf.MoveTowards(rotationSpeed, 30f, 13f * Time.deltaTime);
            sails.Rotate(Vector3.forward, rotationSpeed * Time.deltaTime, Space.Self);
            if (!Playing) return;

            elapsed += Time.deltaTime;
            for (int i = 0; i < blossoms.Length; ++i)
            {
                float t = Mathf.Clamp01((elapsed - 1f - i * .13f) / 1.4f);
                blossoms[i].localScale = blossomSizes[i] * Mathf.SmoothStep(0,1,t);
            }
            if (elapsed < Duration || world.Cinematic) return;
            Playing = false;
            world.EndWindmillGarden();
        }
    }
}
