using Unity.Cinemachine;
using UnityEngine;

namespace EverflowMap
{
    // Presentation only: the existing world owns input, pause, cursor and puzzle state.
    [DisallowMultipleComponent, DefaultExecutionOrder(400)]
    public sealed class EFV2ScenePresentation : MonoBehaviour
    {
        public EFV2PuzzleWorld world;
        [Header("Images reused from Everflow_Map (third image represents FLOW here)")]
        public Sprite[] abilityIcons = new Sprite[3];
        public Rect[] iconUV = new Rect[3];
        [Header("Opening overview, in this map's world coordinates")]
        public Vector3 overviewPosition = new Vector3(-16, 30, 20);
        public Vector3 overviewLookAt = new Vector3(-49, -2, -35);
        [Min(0)] public float overviewSeconds = 3;
        [Min(.1f)] public float approachSeconds = 1.25f;
        [Range(20, 90)] public float overviewFov = 52;
        public bool IntroActive { get; private set; }
        public float IntroElapsed { get; private set; }
        public CinemachineCamera OpeningCamera { get; private set; }

        public void Begin()
        {
            if(!isActiveAndEnabled)return;
            if (OpeningCamera == null)
            {
                var go = new GameObject("V2 Opening overview - Cinemachine only");
                go.transform.SetParent(transform, false);
                OpeningCamera = go.AddComponent<CinemachineCamera>();
                var lens = OpeningCamera.Lens;
                lens.NearClipPlane = .1f; lens.FarClipPlane = 500; lens.FieldOfView = overviewFov;
                OpeningCamera.Lens = lens;
            }
            IntroElapsed = 0; IntroActive = true;
            OpeningCamera.transform.SetPositionAndRotation(overviewPosition,
                Quaternion.LookRotation(overviewLookAt - overviewPosition));
            OpeningCamera.PreviousStateIsValid = false;
            OpeningCamera.Priority = 200;
            world.SetPaused(world.Paused);
        }
        void Update()
        {
            if (!IntroActive || world.Paused) return;
            IntroElapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01((IntroElapsed - overviewSeconds) / approachSeconds));
            var destination = world.player.cinemachineRig.virtualCamera.State;
            OpeningCamera.transform.SetPositionAndRotation(
                Vector3.Lerp(overviewPosition, destination.GetFinalPosition(), t),
                Quaternion.Slerp(Quaternion.LookRotation(overviewLookAt - overviewPosition), destination.GetFinalOrientation(), t));
            var lens = OpeningCamera.Lens;
            lens.FieldOfView = Mathf.Lerp(overviewFov, destination.Lens.FieldOfView, t);
            OpeningCamera.Lens = lens;
            if (IntroElapsed >= overviewSeconds + approachSeconds) Finish();
        }
        public void Finish()
        {
            IntroActive = false;
            if (OpeningCamera != null) OpeningCamera.Priority = -20;
            if (world != null) world.SetPaused(world.Paused);
        }
        void OnDisable() { if (OpeningCamera != null) OpeningCamera.Priority = -20; IntroActive = false; }
    }
}
