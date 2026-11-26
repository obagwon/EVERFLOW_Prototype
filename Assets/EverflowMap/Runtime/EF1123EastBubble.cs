using System;
using UnityEngine;

namespace EverflowMap
{
    [DisallowMultipleComponent]
    public sealed class EF1123EastBubble : MonoBehaviour
    {
        [Serializable]
        public struct RiseLeg
        {
            public Transform end;
            [Min(.05f)] public float seconds;
        }

        public EF1123Target target;
        public BoxCollider deck;
        public Renderer bubble;
        public GameObject frost;
        public ParticleSystem burst;
        public Transform origin;
        public RiseLeg[] route;
        [Min(.1f)] public float freezeSeconds = 2.5f;

        static readonly int Rim = Shader.PropertyToID("_FrSolidColor");
        static readonly int RimAmount = Shader.PropertyToID("_FrSolidColorActive");
        static readonly int RimEmission = Shader.PropertyToID("_FrEmissionMultiply");
        static readonly int RimOpacityPower = Shader.PropertyToID("_OpacityFrPower");
        static readonly int Center = Shader.PropertyToID("_CenterColor");
        static readonly int Opacity = Shader.PropertyToID("_OpacityMin");
        static readonly int Smoothness = Shader.PropertyToID("_Smoothness");
        static readonly int Drift = Shader.PropertyToID("_HueShiftSpeed");
        static readonly int Wobble = Shader.PropertyToID("_WPOIntensity");

        MaterialPropertyBlock appearance;
        int leg;
        float legTime, freezeRemaining, spawnDelay;
        public bool IsAvailable => deck.gameObject.activeSelf;
        public bool IsFrozen => freezeRemaining > 0f;

        void Awake()
        {
            appearance = new MaterialPropertyBlock();
            deck.gameObject.SetActive(false);
            deck.enabled = false;
        }

        internal bool TryFreeze()
        {
            if (!IsAvailable || IsFrozen) return false;
            freezeRemaining = freezeSeconds;
            deck.enabled = true;
            SetAppearance(true);
            return true;
        }

        internal void Tick(EF1123World world, float delta)
        {
            if (!world.eastSpring.IsFull) return;
            if (!IsAvailable)
            {
                spawnDelay -= delta;
                if (spawnDelay > 0f) return;
                leg = 0;
                legTime = 0f;
                deck.transform.position = origin.position;
                deck.gameObject.SetActive(true);
                SetAppearance(false);
                burst.transform.position = origin.position;
                burst.Play();
                burst.Emit(5);
                return;
            }

            bool aboard = world.player.TrackGroundSupport(deck);
            if (IsFrozen)
            {
                float stoppedTime = Mathf.Min(freezeRemaining, delta);
                freezeRemaining -= stoppedTime;
                delta -= stoppedTime;
                if (IsFrozen) return;
                SetAppearance(false);
            }

            // At thaw, only the rider who already has this ground support keeps it.
            deck.enabled = aboard;
            while (delta > 0f && leg < route.Length)
            {
                var step = route[leg];
                float advance = Mathf.Min(delta, step.seconds - legTime);
                legTime += advance;
                delta -= advance;
                Vector3 from = leg == 0 ? origin.position : route[leg - 1].end.position;
                deck.transform.position = Vector3.Lerp(from, step.end.position, legTime / step.seconds);
                if (legTime < step.seconds) break;
                leg++;
                legTime = 0f;
            }

            if (leg == route.Length)
            {
                burst.transform.position = bubble.bounds.center;
                burst.Play();
                burst.Emit(18);
                deck.enabled = false;
                deck.gameObject.SetActive(false);
                spawnDelay = .55f;
            }
            else if (aboard)
                world.player.FollowSupportDuringCutaway();
        }

        void SetAppearance(bool frozen)
        {
            frost.SetActive(frozen);
            appearance.SetColor(Rim, frozen ? new Color(.60f, .92f, 1f) : new Color(.37f, .82f, 1f));
            appearance.SetFloat(RimAmount, frozen ? 1f : .72f);
            appearance.SetFloat(RimEmission, frozen ? .85f : .60f);
            appearance.SetFloat(RimOpacityPower, frozen ? 2f : 1.7f);
            appearance.SetColor(Center, frozen ? new Color(.76f, .93f, 1f) : new Color(.41f, .80f, .97f));
            appearance.SetFloat(Opacity, frozen ? .55f : .19f);
            appearance.SetFloat(Smoothness, frozen ? .26f : .9f);
            appearance.SetFloat(Drift, 0f);
            appearance.SetFloat(Wobble, 0f);
            bubble.SetPropertyBlock(appearance);
        }
    }
}
