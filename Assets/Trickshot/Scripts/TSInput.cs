using UnityEngine;
using UnityEngine.InputSystem;

namespace Trickshot
{
    /// <summary>
    /// Thin wrapper around the Input System so the rest of the game never touches devices directly.
    /// Rebind keys here, or swap this for an InputActionAsset later.
    /// </summary>
    public static class TSInput
    {
        // Mouse delta arrives in pixels; this brings it to roughly "degrees at sensitivity 1".
        const float MouseDeltaScale = 0.1f;

        public static Vector2 Move
        {
            get
            {
                Keyboard k = Keyboard.current;
                if (k == null) return Vector2.zero;
                float x = (k.dKey.isPressed ? 1f : 0f) - (k.aKey.isPressed ? 1f : 0f);
                float y = (k.wKey.isPressed ? 1f : 0f) - (k.sKey.isPressed ? 1f : 0f);
                return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
            }
        }

        public static Vector2 LookDelta
        {
            get
            {
                Mouse m = Mouse.current;
                return m != null ? m.delta.ReadValue() * MouseDeltaScale : Vector2.zero;
            }
        }

        public static bool JumpPressed => Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        public static bool SprintHeld => Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
        public static bool RespawnPressed => Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
        public static bool PausePressed => Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        public static bool SensitivityDownPressed => Keyboard.current != null && Keyboard.current.leftBracketKey.wasPressedThisFrame;
        public static bool SensitivityUpPressed => Keyboard.current != null && Keyboard.current.rightBracketKey.wasPressedThisFrame;

        public static bool FirePressed => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        public static bool AimHeld => Mouse.current != null && Mouse.current.rightButton.isPressed;
    }
}
