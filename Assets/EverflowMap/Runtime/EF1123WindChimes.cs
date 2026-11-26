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
            internal bool rung;
        }

        public Chime left, right;
        public Chime third;
        public EF1123Target shutterControl;
        public EF1123Motion gate;
        public EF1123Audio audioFeedback;

        internal void Ring(EF1123Target target)
        {
            if (target == left.target) Ring(left, EF1123Sound.ChimeLow);
            else if (target == right.target) Ring(right, EF1123Sound.ChimeHigh);
            else if (target == third?.target) Ring(third, EF1123Sound.ChimeThird);
            else if (target == shutterControl)
            {
                audioFeedback?.Play(EF1123Sound.Mechanism);
                return;
            }
            else return;
            if (left.rung && right.rung && (third?.target == null || third.rung)) gate.Open();
        }

        void Ring(Chime chime, EF1123Sound note)
        {
            if (chime.rung) return;
            chime.rung = true;
            chime.litMarker.SetActive(true);
            if (chime.gateMarker != null) chime.gateMarker.SetActive(true);
            audioFeedback?.Play(note);
            StartCoroutine(Swing(chime));
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
