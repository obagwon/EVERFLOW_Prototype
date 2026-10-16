using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace EverflowGraybox
{
    public static class EFInput
    {
#if UNITY_EDITOR
        // Editor-only deterministic input for the physical traversal smoke tests.
        public static bool TestMode;
        public static Vector2 TestMovement;
        public static KeyCode TestPress;
#endif
        public static bool KeyDown(KeyCode key) => ReadKey(key, true);
        public static bool KeyHeld(KeyCode key) => ReadKey(key, false);
        static bool ReadKey(KeyCode key, bool pressed)
        {
#if UNITY_EDITOR
            if (TestMode) return key == TestPress;
#endif
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current == null) return false;
            Key input;
            switch (key)
            {
                case KeyCode.W: input = Key.W; break;
                case KeyCode.A: input = Key.A; break;
                case KeyCode.S: input = Key.S; break;
                case KeyCode.D: input = Key.D; break;
                case KeyCode.E: input = Key.E; break;
                case KeyCode.R: input = Key.R; break;
                case KeyCode.Space: input = Key.Space; break;
                case KeyCode.Return: input = Key.Enter; break;
                case KeyCode.Alpha1: input = Key.Digit1; break;
                case KeyCode.Alpha2: input = Key.Digit2; break;
                case KeyCode.Alpha3: input = Key.Digit3; break;
                case KeyCode.F1: input = Key.F1; break;
                case KeyCode.F2: input = Key.F2; break;
                case KeyCode.F3: input = Key.F3; break;
                case KeyCode.F4: input = Key.F4; break;
                case KeyCode.F5: input = Key.F5; break;
                case KeyCode.F6: input = Key.F6; break;
                case KeyCode.F8: input = Key.F8; break;
                default: return false;
            }
            return pressed ? Keyboard.current[input].wasPressedThisFrame : Keyboard.current[input].isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return pressed ? Input.GetKeyDown(key) : Input.GetKey(key);
#else
            return false;
#endif
        }
        public static Vector2 Move
        {
            get
            {
#if UNITY_EDITOR
                if (TestMode) return TestMovement;
#endif
                return new Vector2((KeyHeld(KeyCode.D) ? 1 : 0) - (KeyHeld(KeyCode.A) ? 1 : 0),
                    (KeyHeld(KeyCode.W) ? 1 : 0) - (KeyHeld(KeyCode.S) ? 1 : 0));
            }
        }
        public static Vector2 Pointer
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#elif ENABLE_LEGACY_INPUT_MANAGER
                return Input.mousePosition;
#else
                return Vector2.zero;
#endif
            }
        }
        public static bool Click
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
                return Input.GetMouseButtonDown(0);
#else
                return false;
#endif
            }
        }
        public static bool Orbit
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Mouse.current != null && Mouse.current.rightButton.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
                return Input.GetMouseButton(1);
#else
                return false;
#endif
            }
        }
    }
}
