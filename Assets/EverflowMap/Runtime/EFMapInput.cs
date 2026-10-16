using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace EverflowMap
{
    // Map-only additions; the original MVP/graybox pointer controls stay intact.
    public static class EFMapInput
    {
#if UNITY_EDITOR
        public static Vector2 TestLookDelta;
        public static bool TestClick;
#endif
        public static bool KeyDown(KeyCode key)
        {
#if UNITY_EDITOR
            if (EverflowGraybox.EFInput.TestMode) return EverflowGraybox.EFInput.TestPress == key;
#endif
            if (key != KeyCode.C && key != KeyCode.Escape) return EverflowGraybox.EFInput.KeyDown(key);
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current == null) return false;
            return Keyboard.current[key == KeyCode.C ? Key.C : Key.Escape].wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(key);
#else
            return false;
#endif
        }
        public static Vector2 LookDelta
        {
            get
            {
#if UNITY_EDITOR
                if (EverflowGraybox.EFInput.TestMode) return TestLookDelta;
#endif
#if ENABLE_INPUT_SYSTEM
                return Mouse.current == null ? Vector2.zero : Mouse.current.delta.ReadValue();
#elif ENABLE_LEGACY_INPUT_MANAGER
                return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 10;
#else
                return Vector2.zero;
#endif
            }
        }
        public static bool Click
        {
            get
            {
#if UNITY_EDITOR
                if (EverflowGraybox.EFInput.TestMode) return TestClick;
#endif
                return EverflowGraybox.EFInput.Click;
            }
        }
    }
}
