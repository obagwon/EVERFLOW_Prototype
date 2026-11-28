using System.Collections;
using UnityEngine;

namespace EverflowMap
{
    public sealed class EF1123WindChimes : MonoBehaviour
    {
        [System.Serializable]
        public sealed class Chime
        {
            public EF1123Target target;
            public Transform hanger;
            public Light glow;
            public GameObject litMarker;
            public GameObject gateMarker;
            public GameObject gateBlade;
            internal bool rung;
        }

        public Chime left, right;
        public Chime third;
        public EF1123Target shutterControl;
        public EF1123Motion gate;
        public Transform gateCameraPose;
        public Transform gateBladeCameraPose;
        public EF1123Audio audioFeedback;
        public EF1123Target gateWindTarget;
        public EF1123Motion gateRotor;
        public GameObject gateReadySignal;
        public Transform gateReward;

        bool AllRung => left.rung && right.rung && (third?.target == null || third.rung);

        internal string CastBlock(EF1123Target target) =>
            target == gateWindTarget && !AllRung ? "종 세 개를 울려 풍차 날개를 완성하세요." : null;

        internal bool BlocksReward(Transform reward) =>
            gateWindTarget != null && reward == gateReward && !gate.AtOpenPose;

        // A custom blade shot takes precedence over the chime's usual impact cutaway.
        internal bool Ring(EF1123Target target, EF1123Effects effects)
        {
            if (target == gateWindTarget)
            {
                if (!AllRung || gateRotor.IsOpen) return false;
                gateRotor.Open();
                audioFeedback?.Play(EF1123Sound.Door);
                OpenGate(effects);
                return true;
            }

            Chime chime;
            EF1123Sound note;
            if (target == left.target) { chime = left; note = EF1123Sound.ChimeLow; }
            else if (target == right.target) { chime = right; note = EF1123Sound.ChimeHigh; }
            else if (target == third?.target) { chime = third; note = EF1123Sound.ChimeThird; }
            else if (target == shutterControl)
            {
                audioFeedback?.Play(EF1123Sound.Mechanism);
                return false;
            }
            else return false;

            if (!Ring(chime, note)) return false;
            if (chime.gateBlade != null && effects != null && gateBladeCameraPose != null)
            {
                ShowBlade(chime.gateBlade.transform, effects);
                return true;
            }
            if (chime.gateBlade != null) chime.gateBlade.SetActive(true);
            OnChimesReady(effects);
            return false;
        }

        void OnChimesReady(EF1123Effects effects)
        {
            if (!AllRung) return;
            if (gateWindTarget != null)
            {
                gateReadySignal.SetActive(true);
                audioFeedback?.Play(EF1123Sound.Confirm);
                return;
            }
            OpenGate(effects);
        }

        void ShowBlade(Transform blade, EF1123Effects effects)
        {
            Vector3 seat = blade.localPosition;
            Quaternion rotation = blade.localRotation;
            Vector3 scale = blade.localScale;
            Coroutine assembly = null;
            bool fitted = false;
            effects.ShowCutaway(gateBladeCameraPose, 2f,
                () => assembly = StartCoroutine(Assemble()), Finish);

            void Fit()
            {
                if (fitted) return;
                fitted = true;
                blade.SetLocalPositionAndRotation(seat, rotation);
                blade.localScale = scale;
                blade.gameObject.SetActive(true);
                OnChimesReady(effects);
            }

            void Finish()
            {
                if (!fitted && assembly != null) StopCoroutine(assembly);
                Fit();
            }

            IEnumerator Assemble()
            {
                const float lead = .4f, duration = .73f;
                for (float elapsed = 0; elapsed < lead; elapsed += ShotDelta())
                    yield return null;

                Vector3 start = seat + new Vector3(0, .62f, -.72f);
                Quaternion turned = rotation * Quaternion.Euler(-18, -38, 16);
                Vector3 socket = blade.parent.TransformPoint(seat + new Vector3(0, .6f, -.23f));
                effects.WindmillSparks(socket, 14);
                for (float elapsed = 0; elapsed < duration; elapsed += ShotDelta())
                {
                    float t = Mathf.SmoothStep(0, 1, elapsed / duration);
                    blade.SetLocalPositionAndRotation(Vector3.Lerp(start, seat, t), Quaternion.Slerp(turned, rotation, t));
                    blade.localScale = Vector3.Lerp(scale * .08f, scale, t);
                    if (!blade.gameObject.activeSelf) blade.gameObject.SetActive(true);
                    yield return null;
                }

                Fit();
                effects.WindmillSparks(socket, 10);
                if (!AllRung) audioFeedback?.Play(EF1123Sound.Mechanism, .62f);
            }

            float ShotDelta() => effects.world.Paused ? 0 : Time.deltaTime;
        }

        void OpenGate(EF1123Effects effects)
        {
            if (effects != null) effects.ShowDoorOpening(gateCameraPose, gate);
            else gate.Open();
        }

        bool Ring(Chime chime, EF1123Sound note)
        {
            if (chime.rung) return false;
            chime.rung = true;
            chime.litMarker.SetActive(true);
            if (chime.gateMarker != null) chime.gateMarker.SetActive(true);
            audioFeedback?.Play(note);
            StartCoroutine(Swing(chime));
            return true;
        }

        static IEnumerator Swing(Chime chime)
        {
            const float duration = 1.45f;
            Quaternion rest = chime.hanger.localRotation;
            for (float elapsed = 0; elapsed < duration; elapsed += Time.deltaTime)
            {
                float fade = 1 - elapsed / duration;
                float angle = 25 * Mathf.Sin(elapsed * 14) * fade * fade;
                chime.hanger.localRotation = rest * Quaternion.Euler(0, 0, angle);
                chime.glow.intensity = .7f + fade * fade * 2.4f;
                yield return null;
            }
            chime.hanger.localRotation = rest;
            chime.glow.intensity = .7f;
        }
    }
}
