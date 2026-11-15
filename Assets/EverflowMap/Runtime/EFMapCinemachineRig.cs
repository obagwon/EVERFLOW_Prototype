using Unity.Cinemachine;
using UnityEngine;

namespace EverflowMap
{
    // Scene-local pause/respawn bridge. Cinemachine alone positions the rendering camera.
    [DisallowMultipleComponent, DefaultExecutionOrder(-100)]
    public sealed class EFMapCinemachineRig : MonoBehaviour
    {
        public EFMapPlayer player;
        public CinemachineBrain brain;
        public CinemachineCamera virtualCamera;
        public CinemachineOrbitalFollow orbit;
        public CinemachineInputAxisController input;
        bool inputActive = true;
        int resumeFrame;
        bool viewFrozen, brainWasEnabled;

        public void SetViewFrozen(bool frozen)
        {
            if(viewFrozen==frozen || brain==null)return;
            viewFrozen=frozen;
            if(frozen){brainWasEnabled=brain.enabled;brain.enabled=false;}
            else brain.enabled=brainWasEnabled;
        }

        public void SetInputActive(bool active)
        {
            inputActive = active;
            input.enabled = false;
            // Ignore the cursor-warp delta when capture is restored.
            resumeFrame = Time.frameCount + 1;
        }

        void Update()
        {
            input.enabled = inputActive && Time.frameCount > resumeFrame;
        }

        public void ResetView(bool alignBehind = false)
        {
            if (alignBehind)
            {
                orbit.HorizontalAxis.Value = player.transform.eulerAngles.y;
                orbit.VerticalAxis.Value = 18;
            }
            virtualCamera.PreviousStateIsValid = false;
            SetInputActive(inputActive);
        }

        void OnDisable() { if (input != null) input.enabled = false;SetViewFrozen(false); }
    }
}
