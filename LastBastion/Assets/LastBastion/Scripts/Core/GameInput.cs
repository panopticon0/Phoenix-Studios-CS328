using UnityEngine;
#if !ENABLE_LEGACY_INPUT_MANAGER && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LastBastion
{
    /// <summary>Keys the game listens for.</summary>
    public enum GKey { W, A, S, D, Up, Down, Left, Right, Space, Escape, Q, F, H, Digit1, Digit2, Digit3, F9, F10 }

    /// <summary>
    /// Thin input wrapper so the game runs whether the project uses the old Input Manager,
    /// the new Input System package, or both (Unity 6 default).
    /// </summary>
    public static class GameInput
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        static KeyCode Map(GKey k)
        {
            switch (k)
            {
                case GKey.W: return KeyCode.W;
                case GKey.A: return KeyCode.A;
                case GKey.S: return KeyCode.S;
                case GKey.D: return KeyCode.D;
                case GKey.Up: return KeyCode.UpArrow;
                case GKey.Down: return KeyCode.DownArrow;
                case GKey.Left: return KeyCode.LeftArrow;
                case GKey.Right: return KeyCode.RightArrow;
                case GKey.Space: return KeyCode.Space;
                case GKey.Escape: return KeyCode.Escape;
                case GKey.Q: return KeyCode.Q;
                case GKey.F: return KeyCode.F;
                case GKey.H: return KeyCode.H;
                case GKey.Digit1: return KeyCode.Alpha1;
                case GKey.Digit2: return KeyCode.Alpha2;
                case GKey.Digit3: return KeyCode.Alpha3;
                case GKey.F9: return KeyCode.F9;
                case GKey.F10: return KeyCode.F10;
            }
            return KeyCode.None;
        }

        public static Vector2 MousePosition => Input.mousePosition;
        public static bool LeftPressed => Input.GetMouseButtonDown(0);
        public static bool RightPressed => Input.GetMouseButtonDown(1);
        public static bool MiddleHeld => Input.GetMouseButton(2);
        public static float Scroll => Input.mouseScrollDelta.y;
        public static bool KeyPressed(GKey k) => Input.GetKeyDown(Map(k));
        public static bool KeyHeld(GKey k) => Input.GetKey(Map(k));
#elif ENABLE_INPUT_SYSTEM
        static Key Map(GKey k)
        {
            switch (k)
            {
                case GKey.W: return Key.W;
                case GKey.A: return Key.A;
                case GKey.S: return Key.S;
                case GKey.D: return Key.D;
                case GKey.Up: return Key.UpArrow;
                case GKey.Down: return Key.DownArrow;
                case GKey.Left: return Key.LeftArrow;
                case GKey.Right: return Key.RightArrow;
                case GKey.Space: return Key.Space;
                case GKey.Escape: return Key.Escape;
                case GKey.Q: return Key.Q;
                case GKey.F: return Key.F;
                case GKey.H: return Key.H;
                case GKey.Digit1: return Key.Digit1;
                case GKey.Digit2: return Key.Digit2;
                case GKey.Digit3: return Key.Digit3;
                case GKey.F9: return Key.F9;
                case GKey.F10: return Key.F10;
            }
            return Key.None;
        }

        public static Vector2 MousePosition => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        public static bool LeftPressed => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        public static bool RightPressed => Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
        public static bool MiddleHeld => Mouse.current != null && Mouse.current.middleButton.isPressed;

        public static float Scroll
        {
            get
            {
                if (Mouse.current == null) return 0f;
                float y = Mouse.current.scroll.ReadValue().y;
                // Some platforms report 120 per notch; normalise to ~1 per notch.
                return Mathf.Abs(y) > 10f ? y / 120f : y;
            }
        }

        public static bool KeyPressed(GKey k) => Keyboard.current != null && Keyboard.current[Map(k)].wasPressedThisFrame;
        public static bool KeyHeld(GKey k) => Keyboard.current != null && Keyboard.current[Map(k)].isPressed;
#else
        public static Vector2 MousePosition => Vector2.zero;
        public static bool LeftPressed => false;
        public static bool RightPressed => false;
        public static bool MiddleHeld => false;
        public static float Scroll => 0f;
        public static bool KeyPressed(GKey k) => false;
        public static bool KeyHeld(GKey k) => false;
#endif
    }
}
