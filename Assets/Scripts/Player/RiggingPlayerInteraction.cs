using FishNet.Object;
using PirateSlop.Networking;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PirateSlop
{
    [DefaultExecutionOrder(-80)]
    public sealed class RiggingPlayerInteraction : MonoBehaviour
    {
        NetworkWeapon network;
        AdvancedPlayerController motor;
        NetworkRiggingEvent hovered, held, nearby;
        int consumedFrame = -1;
        float nextSend, grabAt, messageUntil;
        uint attempt;
        bool confirmed, ownsLock, waitForRelease;
        string message;
        public bool ConsumedInput => consumedFrame == Time.frameCount;
        void Awake() { network = GetComponent<NetworkWeapon>(); motor = GetComponent<AdvancedPlayerController>(); }
        void Update()
        {
            hovered = nearby = null;
            if (network == null || !network.IsOwner || !network.IsClientInitialized) return;
            float localDistance = 38;
            foreach (var owner in NetworkRiggingEvent.Active)
            {
                if (owner == null || !owner.IsSpawned) continue;
                float distance = Vector3.Distance(motor.transform.position, owner.transform.position);
                if (distance < localDistance) { localDistance = distance; nearby = owner; }
            }
            var keys = Keyboard.current;
            if (keys != null && !keys.eKey.isPressed) waitForRelease = false;
            if (keys == null || motor.PlayerCamera == null || motor.IsDead || motor.IsDowned || motor.IsFrozen || motor.IsSwimming || motor.IsClimbing ||
                SessionController.MenuOpen || DeveloperMenu.IsOpen || RoguelikeUpgradeUI.BlocksInput || network.LootHandsBusy ||
                Cursor.lockState != CursorLockMode.Locked || motor.LocomotionLocked && !ownsLock)
            { Release(); return; }
            if (held != null)
            {
                consumedFrame = Time.frameCount; nearby = held;
                if (!held.IsSpawned || held.ChestAvailable || held.Failed || !held.CanReach(network) || !keys.eKey.isPressed || keys.qKey.wasPressedThisFrame || keys.escapeKey.wasPressedThisFrame)
                { Release(); return; }
                if (!confirmed && Time.unscaledTime - grabAt > 1.5f) { Reject("Сервер не подтвердил захват тормоза"); return; }
                if (confirmed && held.Operator != network.NetworkObject && Time.unscaledTime - grabAt > .6f) { Reject("Тормоз отпущен"); return; }
                if (Time.unscaledTime >= nextSend) { network.RiggingInput(held.NetworkObject, true, attempt); nextSend = Time.unscaledTime + .1f; }
                return;
            }
            if (!motor.InputActive || WhalePlayerInteraction.InputBlocked) return;
            var ray = new Ray(motor.PlayerCamera.transform.position, motor.PlayerCamera.transform.forward);
            float nearest = float.PositiveInfinity;
            foreach (var owner in NetworkRiggingEvent.Active)
            {
                if (owner == null || !owner.IsSpawned) continue;
                var delta = owner.BrakeGrip.position - ray.origin;
                float along = Vector3.Dot(delta, ray.direction);
                if (along <= 0 || along > 3.5f || along >= nearest || (delta - ray.direction * along).sqrMagnitude > .65f * .65f) continue;
                hovered = owner; nearest = along;
            }
            if (hovered == null || hovered.ChestAvailable || hovered.Failed || !keys.eKey.isPressed || waitForRelease) return;
            consumedFrame = Time.frameCount;
            held = hovered; confirmed = false; grabAt = Time.unscaledTime; nextSend = grabAt + .1f;
            motor.ShipActivityLocked = ownsLock = true;
            network.RiggingInput(held.NetworkObject, true, ++attempt, true);
        }
        public void GripResult(NetworkObject target, uint request, bool accepted, string reason)
        {
            if (held == null || held.NetworkObject != target || attempt != request) return;
            if (accepted) confirmed = true; else Reject(reason);
        }
        void Reject(string reason) { message = reason; messageUntil = Time.unscaledTime + 3; waitForRelease = true; Release(); }
        void Release()
        {
            if (held != null && network != null && network.IsSpawned) network.RiggingInput(held.NetworkObject, false, attempt);
            held = null; confirmed = false;
            if (ownsLock && motor != null) motor.ShipActivityLocked = false;
            ownsLock = false;
        }
        void OnDisable() { Release(); }
        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || network == null || !network.IsOwner || motor.IsDead || motor.IsFrozen || RoguelikeUpgradeUI.WindowOpen || SessionController.MenuOpen) return;
            if (held != null || hovered != null || Time.unscaledTime < messageUntil)
                ContextPrompt.Draw(Time.unscaledTime < messageUntil ? message : held != null ? confirmed ? $"Удерживайте E — спускать сундук: {held.Progress:P0} · отпустите E — тормоз · Q — выйти" : "Захватываем тормоз…"
                    : hovered.ChestAvailable || hovered.Failed ? hovered.Hint : "Удерживайте E — спускать сундук на канате · отпустите E — затормозить");
            if (nearby != null && !nearby.ChestAvailable)
                PirateHudStyle.Panel(new Rect(Screen.width * .5f - 330, Screen.height * .14f, 660, 34), nearby.Hint);
        }
    }
}
