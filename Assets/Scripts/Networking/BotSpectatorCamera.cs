using UnityEngine;
using UnityEngine.InputSystem;

namespace PirateSlop.Networking
{
    public sealed class BotSpectatorCamera : MonoBehaviour
    {
        float yaw, pitch, speed = 12f, nextSpeed;
        Vector3 velocity;
        float viewYaw, viewPitch;
        Camera view;
        void OnEnable()
        {
            view = GetComponent<Camera>();
            velocity = Vector3.zero;
            viewYaw = yaw = transform.eulerAngles.y;
            viewPitch = pitch = Mathf.DeltaAngle(0, transform.eulerAngles.x);
        }
        void Update()
        {
            var keyboard = Keyboard.current;
            if (view != null) view.fieldOfView = GameSettings.Fov;
            if (keyboard == null || BotDebugPanel.ConsumedInput || DeveloperMenu.IsOpen) return;
            if (keyboard.escapeKey.wasPressedThisFrame)
                AdvancedPlayerController.SetCursor(Cursor.lockState != CursorLockMode.Locked);
            if (SessionController.MenuOpen || Cursor.lockState != CursorLockMode.Locked)
            {
                velocity = Vector3.zero;
                yaw = viewYaw;
                pitch = viewPitch;
                return;
            }
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

            }
            float dt = Time.unscaledDeltaTime;
            float responseTime = GameSettings.ObserverSmoothing * .0025f;
            float blend = responseTime > 0f ? 1f - Mathf.Exp(-dt / responseTime) : 1f;
            viewPitch = Mathf.Lerp(viewPitch, pitch, blend);
            viewYaw = Mathf.LerpAngle(viewYaw, yaw, blend);
            transform.rotation = Quaternion.Euler(viewPitch, viewYaw, 0);
            float x = (keyboard[KeyBindings.Right].isPressed ? 1 : 0) - (keyboard[KeyBindings.Left].isPressed ? 1 : 0);
            float z = (keyboard[KeyBindings.Forward].isPressed ? 1 : 0) - (keyboard[KeyBindings.Back].isPressed ? 1 : 0);
            float y = (keyboard[KeyBindings.Use].isPressed ? 1 : 0) - (keyboard[KeyBindings.Cancel].isPressed ? 1 : 0);
            var movement = transform.right * x + transform.forward * z + Vector3.up * y;
            float speedScale = keyboard.leftCtrlKey.isPressed ? .25f : keyboard.leftShiftKey.isPressed ? 2f : 1f;
            velocity = Vector3.Lerp(velocity, Vector3.ClampMagnitude(movement, 1f) * (speed * speedScale), blend);
            transform.position += velocity * dt;
        }
    }
}
