using UnityEngine;
using UnityEngine.InputSystem;

namespace PirateSlop.Networking
{
    public sealed class BotSpectatorCamera : MonoBehaviour
    {
        float yaw, pitch, speed = 12f, nextSpeed;
        void OnEnable()
        {
            yaw = transform.eulerAngles.y;
            pitch = Mathf.DeltaAngle(0, transform.eulerAngles.x);
        }
        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || BotDebugPanel.ConsumedInput || DeveloperMenu.IsOpen) return;
            if (keyboard.escapeKey.wasPressedThisFrame)
                AdvancedPlayerController.SetCursor(Cursor.lockState != CursorLockMode.Locked);
            if (SessionController.MenuOpen || Cursor.lockState != CursorLockMode.Locked) return;
            if (Time.unscaledTime >= nextSpeed)
            {
                nextSpeed = Time.unscaledTime + 1f;
                float maximum = 10f;
                foreach (var ship in NetworkShip.ActiveShips)
                    if (ship != null && ship.Motor != null) maximum = Mathf.Max(maximum, ship.Motor.MaxSpeed);
                speed = maximum * 1.2f;
            }
            if (Mouse.current != null)
            {
                var look = Mouse.current.delta.ReadValue();
                yaw += look.x * .12f;
                pitch = Mathf.Clamp(pitch - look.y * .12f, -89f, 89f);
                transform.rotation = Quaternion.Euler(pitch, yaw, 0);
            }
            float x = (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0);
            float z = (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0);
            float y = (keyboard.eKey.isPressed ? 1 : 0) - (keyboard.qKey.isPressed ? 1 : 0);
            var movement = transform.right * x + transform.forward * z + Vector3.up * y;
            transform.position += Vector3.ClampMagnitude(movement, 1f) * (speed * Time.unscaledDeltaTime);
        }
    }
}
