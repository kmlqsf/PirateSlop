using PirateSlop;
using PirateSlop.Networking;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class AdvancedPlayerController
{
    [SerializeField, Min(0f)] float spectatorDelay = 1.5f;
    [SerializeField, Min(1f)] float spectatorSpeed = 25f;
    bool deathSpectating;
    public bool FreeSpectating { get; private set; }
    float spectatorStarted, spectatorYaw, spectatorPitch, spectatorFov;
    Vector3 spectatorPosition;

    void UpdateDeathSpectator()
    {
        if (playerCamera == null) return;
        if (!deathSpectating)
        {
            deathSpectating = true;
            spectatorStarted = Time.unscaledTime;
            spectatorPosition = playerCamera.transform.position;
            spectatorYaw = playerCamera.transform.eulerAngles.y;
            spectatorPitch = Mathf.DeltaAngle(0, playerCamera.transform.eulerAngles.x);
            spectatorFov = playerCamera.fieldOfView;
            SpectatorTarget = null;
        }
        var self = GetComponent<NetworkPlayer>();
        bool free = self != null && self.Eliminated.Value;
        if (free && !FreeSpectating)
        {
            spectatorPosition = playerCamera.transform.position;
            spectatorYaw = playerCamera.transform.eulerAngles.y;
            spectatorPitch = Mathf.DeltaAngle(0, playerCamera.transform.eulerAngles.x);
            SpectatorTarget = null;
        }
        FreeSpectating = free;
        bool ready = Time.unscaledTime - spectatorStarted >= spectatorDelay;
        if (!free && ready && (SpectatorTarget == null || !SpectatorTarget.IsSpawned || SpectatorTarget.Eliminated.Value || SpectatorTarget.Motor == null || SpectatorTarget.Motor.IsDead))
        {
            CycleSpectatorTarget();
            spectatorYaw = 0f;
            spectatorPitch = 15f;
        }
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        if (DeveloperMenu.IsOpen || BotDebugPanel.ConsumedInput || SessionController.MenuOpen) return;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) SetCursor(false);
        bool locked = Cursor.lockState == CursorLockMode.Locked;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            if (locked && !free && ready) { CycleSpectatorTarget(); spectatorYaw = 0f; spectatorPitch = 15f; }
            SetCursor(true);
        }
        if (!locked || (!free && !ready)) return;
        if (mouse != null)
        {
            var look = mouse.delta.ReadValue() * mouseSensitivity;
            spectatorYaw = Mathf.Repeat(spectatorYaw + look.x, 360f);
            spectatorPitch = Mathf.Clamp(spectatorPitch - look.y, -85f, 85f);
        }
        if (!free || keyboard == null) return;
        float x = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
        float z = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
        float y = (keyboard.spaceKey.isPressed || keyboard.eKey.isPressed ? 1f : 0f) - (keyboard.leftCtrlKey.isPressed || keyboard.qKey.isPressed ? 1f : 0f);
        var rotation = Quaternion.Euler(spectatorPitch, spectatorYaw, 0);
        var move = rotation * new Vector3(x, 0, z) + Vector3.up * y;
        float speed = spectatorSpeed * (keyboard.leftShiftKey.isPressed ? 3f : 1f);
        spectatorPosition += Vector3.ClampMagnitude(move, 1f) * speed * Time.unscaledDeltaTime;
    }

    void PositionDeathSpectator()
    {
        if (!deathSpectating) return;
        if (FreeSpectating)
        {
            playerCamera.transform.SetPositionAndRotation(spectatorPosition, Quaternion.Euler(spectatorPitch, spectatorYaw, 0));
            return;
        }
        if (SpectatorTarget == null || SpectatorTarget.Motor == null || SpectatorTarget.Motor.IsDead) return;
        var target = SpectatorTarget.Motor;
        var graphics = SpectatorTarget.transform.Find("PlayerGraphics");
        var pivot = (graphics != null ? graphics.position : SpectatorTarget.transform.position) + Vector3.up * (target.IsCrouched ? .85f : 1.65f);
        var rotation = Quaternion.Euler(spectatorPitch, SpectatorTarget.transform.eulerAngles.y + spectatorYaw, 0);
        var direction = rotation * Vector3.back;
        float distance = 3f;
        foreach (var hit in Physics.SphereCastAll(pivot, .15f, direction, distance, ~0, QueryTriggerInteraction.Ignore))
            if (!hit.transform.IsChildOf(SpectatorTarget.transform) && !hit.transform.IsChildOf(transform)) distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - .05f));
        playerCamera.transform.SetPositionAndRotation(pivot + direction * distance, rotation);
    }

    void ResetDeathSpectator()
    {
        if (!deathSpectating) return;
        deathSpectating = FreeSpectating = false;
        SpectatorTarget = null;
        if (playerCamera != null) playerCamera.fieldOfView = spectatorFov;
    }
}
