using FishNet.Object;
using PirateSlop.Networking;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PirateSlop
{
    [DefaultExecutionOrder(-79)]
    public sealed class ClamPlayerInteraction : MonoBehaviour
    {
        NetworkWeapon network;
        AdvancedPlayerController motor;
        ClamLeverTarget hovered, held;
        int consumedFrame = -1;
        float pull, nextSend, grabStarted, messageUntil;
        uint attempt;
        bool confirmed, waitForRelease, ownsLock;
        string message;
        public bool IsHolding => held != null;
        public bool ConsumedInput => consumedFrame == Time.frameCount;
        void Awake() { network = GetComponent<NetworkWeapon>(); motor = GetComponent<AdvancedPlayerController>(); }

        void Update()
        {
            hovered = null;
            if (network == null || !network.IsOwner || !network.IsClientInitialized) return;
            var mouse = Mouse.current; var keys = Keyboard.current;
            if (mouse != null && !mouse.leftButton.isPressed) waitForRelease = false;
            if (mouse == null || keys == null || motor.PlayerCamera == null || motor.IsDead || motor.IsDowned || motor.IsFrozen || motor.IsClimbing ||
                SessionController.MenuOpen || DeveloperMenu.IsOpen || RoguelikeUpgradeUI.BlocksInput || network.LootHandsBusy ||
                Cursor.lockState != CursorLockMode.Locked || motor.LocomotionLocked && !ownsLock)
            { Release(); return; }
            if (held != null)
            {
                consumedFrame = Time.frameCount;
                if (held.Owner == null || !held.Owner.IsSpawned || !held.CanReach(network) || motor.IsSwimming ||
                    !mouse.leftButton.isPressed || keys.qKey.wasPressedThisFrame || keys.escapeKey.wasPressedThisFrame)
                { Release(); return; }
                if (!confirmed)
                {
                    if (Time.unscaledTime - grabStarted > 1.5f) { Notify("Сервер не подтвердил захват"); waitForRelease = true; Release(); }
                    return;
                }
                if (held.Owner.Cooldown(held.Side) > 0 || held.Owner.SuccessfulPairs >= held.Owner.RequiredPairs)
                { waitForRelease = true; Release(); return; }
                var state = held.Owner.Hold(held.Side);
                if (state.Actor != network.NetworkObject && Time.unscaledTime - grabStarted > .8f)
                { waitForRelease = true; Release(); return; }
                float delta = -mouse.delta.ReadValue().y * .004f;
                pull = Mathf.Clamp01(pull + delta);
                motor.SetInteractionCameraPull(pull * .65f);
                if (Time.unscaledTime >= nextSend)
                {
                    network.ClamInput(held.Owner.NetworkObject, held.Side, true, pull, attempt);
                    nextSend = Time.unscaledTime + .05f;
                }
                return;
            }
            if (!motor.InputActive || WhalePlayerInteraction.InputBlocked) return;
            var ray = new Ray(motor.PlayerCamera.transform.position, motor.PlayerCamera.transform.forward);
            float nearest = float.PositiveInfinity;
            foreach (var target in ClamLeverTarget.Active)
            {
                if (target == null || target.Owner == null || !target.Owner.IsSpawned || target.gameObject.scene != gameObject.scene ||
                    !target.Aim(ray, out float distance) || distance >= nearest) continue;
                hovered = target; nearest = distance;
            }
            if (hovered == null) return;
            if (keys.eKey.wasPressedThisFrame)
            {
                consumedFrame = Time.frameCount;
                network.PressClamLever(hovered.Owner.NetworkObject, hovered.Side); return;
            }
            if (!mouse.leftButton.isPressed || waitForRelease || motor.IsSwimming) return;
            consumedFrame = Time.frameCount;
            held = hovered; confirmed = false; pull = held.Owner.HandlePull(held.Side); message = null;
            grabStarted = Time.unscaledTime; nextSend = grabStarted + .05f;
            motor.ShipActivityLocked = ownsLock = true;
            network.ClamInput(held.Owner.NetworkObject, held.Side, true, 0, ++attempt, true);
        }

        public void GripResult(NetworkObject target, int side, uint request, bool accepted, string reason)
        {
            if (held == null || held.Owner.NetworkObject != target || held.Side != side || request != attempt) return;
            if (accepted) { confirmed = true; return; }
            Notify(string.IsNullOrEmpty(reason) ? "Не удалось взяться за ручку" : reason);
            waitForRelease = true; Release();
        }
        public void Notify(string value) { message = value; messageUntil = Time.unscaledTime + 3f; }
        void Release()
        {
            if (held != null && held.Owner != null && network != null && network.IsSpawned)
                network.ClamInput(held.Owner.NetworkObject, held.Side, false, 0, attempt);
            held = null; confirmed = false; pull = 0;
            if (ownsLock && motor != null) { motor.ShipActivityLocked = false; motor.SetInteractionCameraPull(0); }
            ownsLock = false;
        }
        void LateUpdate() { if (held != null && confirmed && held.Owner.Cooldown(held.Side) <= 0) held.SetHandle(pull); }
        void OnDisable() { Release(); }
        void OnGUI()
        {
            if (network == null || !network.IsOwner || RoguelikeUpgradeUI.WindowOpen) return;
            string hint = messageUntil > Time.unscaledTime ? message : held != null
                ? confirmed ? $"Ручка захвачена · мышь вниз: {pull:P0} · отпустить ЛКМ / Q" : "Захватываем ручку…"
                : hovered != null ? motor.IsSwimming ? "E — подняться на деревянный настил" : hovered.CanReach(network) ? hovered.Hint : "Подойдите к ручке механизма" : null;
            if (!string.IsNullOrEmpty(hint)) PirateHudStyle.Panel(new Rect(Screen.width * .5f - 325, Screen.height * .72f, 650, 34), hint);
        }
    }
}
