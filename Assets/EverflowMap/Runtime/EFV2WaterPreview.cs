using UnityEngine;
using UnityEngine.InputSystem;

namespace EverflowMap
{
    // Only placed in the isolated presentation test; never another input owner in the map.
    public sealed class EFV2WaterPreview : MonoBehaviour
    {
        public EFV2WaterIntegration water;
        void Update()
        {
            var k=Keyboard.current;if(k==null)return;
            if(k.digit1Key.wasPressedThisFrame)water.previewSupplied=!water.previewSupplied;
            if(k.digit2Key.wasPressedThisFrame)foreach(var s in water.segments)s.direction=-s.direction;
            if(k.digit3Key.wasPressedThisFrame)water.previewFrozen=!water.previewFrozen;
            if(k.digit4Key.wasPressedThisFrame){water.previewSupplied=true;water.previewFrozen=false;water.ResetVisuals();}
        }
        void OnGUI(){GUI.Label(new Rect(20,20,820,40),"WATER TEST   1 supply   2 reverse world flow   3 freeze/thaw   4 reset");}
    }
}
