using UnityEngine;
namespace EverflowMap
{
    // Run before the existing rig/input controller. The world remains the only pause/cursor owner.
    [DefaultExecutionOrder(-200)]
    public sealed class EFV2CameraInputGate : MonoBehaviour
    {
        public EFV2PuzzleWorld world;
        void Update()
        {
            if(world!=null&&world.Built&&!world.AutomatedCheck&&!Application.isFocused&&!world.Paused)
                world.SetPaused(true);
        }
    }
}
