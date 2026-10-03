using PirateSlop.Networking;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PirateSlop.Ships
{
    [DefaultExecutionOrder(-25)]
    public sealed class ShipV3PlayerInteraction : MonoBehaviour
    {
        NetworkPlayer player;
        AdvancedPlayerController motor;
        ShipV3InteractionTarget held, hovered;
        ShipV3Features diceShip;
        Camera diceCamera;
        float nextSend;
        Vector2 pending;
        bool joinPending;
        float joinedAt;
        readonly RaycastHit[] targetHits = new RaycastHit[32];
        void Awake() { player = GetComponent<NetworkPlayer>(); motor = GetComponent<AdvancedPlayerController>(); }
        void Update()
        {
            if (!player.IsOwner) return;
            if (motor.ShipActivityLocked && held == null && diceShip == null) Release();
            var mouse = Mouse.current;
            var keys = Keyboard.current;
            if (motor.IsDead || SessionController.MenuOpen || DeveloperMenu.IsOpen || mouse == null || keys == null) { Release(); return; }
            if (keys.f2Key.wasPressedThisFrame) { Release(); motor.ActiveHarpoon?.ReleaseControl(); player.TeleportToTestShip(); return; }
            if (diceShip != null)
            {
                int slot = diceShip.LocalDiceSlot(player.Owner.ClientId);
                if (slot < 0 && (!joinPending || Time.time - joinedAt > 1.5f)) { Release(); return; }
                if (slot >= 0)
                {
                    joinPending = false;
                    motor.ShipActivityLocked = true;
                    if (diceCamera == null)
                    {
                        var go = new GameObject("DiceTableCamera");
                        diceCamera = go.AddComponent<Camera>();
                        diceCamera.CopyFrom(motor.PlayerCamera);
                        diceCamera.depth = motor.PlayerCamera.depth + 1;
                        diceCamera.fieldOfView = 37f;
                        motor.PlayerCamera.enabled = false;
                    }
                    if (keys.fKey.wasPressedThisFrame || keys.qKey.wasPressedThisFrame || keys.escapeKey.wasPressedThisFrame || !diceShip.CanUseDice)
                    { diceShip.DiceInput(Vector2.zero, false, false, true); Release(); return; }
                    if (mouse.leftButton.isPressed) pending += mouse.delta.ReadValue() * .00055f;
                    bool gather = keys.eKey.wasPressedThisFrame;
                    if (Time.unscaledTime >= nextSend || gather || mouse.leftButton.wasReleasedThisFrame)
                    {
                        diceShip.DiceInput(pending, mouse.leftButton.isPressed, gather, false);
                        pending = Vector2.zero; nextSend = Time.unscaledTime + .05f;
                    }
                    ContextPrompt.Offer("ЛКМ — двигать и трясти стакан · E — собрать кости · отпустить ЛКМ — бросить · F — выйти\n" + diceShip.DiceResult(slot), 85);
                }
                return;
            }
            if (held != null)
            {
                var grip = held.Kind == ShipV3TargetKind.Door ? held.Ship.DoorGrip : held.Ship.BellGrip;
                if (!held.gameObject.activeInHierarchy || grip == null || !mouse.leftButton.isPressed || keys.qKey.wasPressedThisFrame || Vector3.Distance(transform.position + Vector3.up, grip.position) > 3.5f)
                { Release(); return; }
                motor.ShipActivityLocked = true;
                pending += mouse.delta.ReadValue();
                if (Time.unscaledTime >= nextSend)
                {
                    if (held.Kind == ShipV3TargetKind.Door) held.Ship.DragDoor((-pending.x + pending.y) * .5f, true);
                    else held.Ship.PullBell(pending, true);
                    pending = Vector2.zero; nextSend = Time.unscaledTime + .05f;
                }
                ContextPrompt.Offer(held.Kind == ShipV3TargetKind.Bell ? "Удерживайте ЛКМ и двигайте мышь — качать язычок" : "Удерживайте ЛКМ и двигайте мышь — открывать или закрывать", 80);
                return;
            }
            hovered = null;
            if (!motor.InputActive || motor.LocomotionLocked) return;
            hovered = FindTarget();
            if (hovered == null || hovered.Ship == null || !hovered.Ship.IsSpawned) return;
            if (hovered.Kind == ShipV3TargetKind.Lantern)
            {
                ContextPrompt.Offer("E — включить или выключить фонарь", 75);
                if (keys.eKey.wasPressedThisFrame) hovered.Ship.ToggleLantern(hovered.Index);
            }
            else if (hovered.Kind == ShipV3TargetKind.Dice)
            {
                ContextPrompt.Offer("F — сыграть в кости", 75);
                if (keys.fKey.wasPressedThisFrame)
                {
                    diceShip = hovered.Ship; joinPending = true; joinedAt = Time.time; diceShip.JoinDice();
                }
            }
            else
            {
                ContextPrompt.Offer(hovered.Kind == ShipV3TargetKind.Bell ? "ЛКМ — взять верёвку рынды" : "Удерживать ЛКМ — двигать дверь", 75);
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    held = hovered; pending = Vector2.zero;
                    motor.ShipActivityLocked = true;
                    if (held.Kind == ShipV3TargetKind.Door) held.Ship.DragDoor(0, true);
                    else held.Ship.PullBell(Vector2.zero, true);
                }
            }
        }
        ShipV3InteractionTarget FindTarget()
        {
            var camera = motor.PlayerCamera.transform;
            bool blocked = FirearmTrace.Cast(gameObject, camera.position, camera.position + camera.forward * 3f, out var direct);
            var exact = blocked ? direct.collider.GetComponentInParent<ShipV3InteractionTarget>() : null;
            if (exact != null) return exact;
            float closest = 3f;
            ShipV3InteractionTarget result = null;
            int count = Physics.SphereCastNonAlloc(camera.position, .16f, camera.forward, targetHits, 3f, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                var hit = targetHits[i];
                if (hit.transform.IsChildOf(transform) || hit.distance >= closest) continue;
                var target = hit.collider.GetComponentInParent<ShipV3InteractionTarget>();
                if (target == null || target.Ship == null || !target.Ship.IsSpawned) continue;
                if (blocked && direct.distance + .18f < hit.distance) continue;
                result = target; closest = hit.distance;
            }
            return result;
        }
        void LateUpdate()
        {
            if (diceCamera != null && diceShip != null && diceShip.DiceView != null)
                diceCamera.transform.SetPositionAndRotation(diceShip.DiceView.position, diceShip.DiceView.rotation);
        }
        void Release()
        {
            if (held != null && held.Ship != null && held.Ship.IsSpawned)
            {
                if (held.Kind == ShipV3TargetKind.Door) held.Ship.DragDoor(0, false);
                else held.Ship.PullBell(Vector2.zero, false);
            }
            if (diceShip != null && diceShip.IsSpawned) diceShip.DiceInput(Vector2.zero, false, false, true);
            held = hovered = null; diceShip = null; pending = Vector2.zero; joinPending = false;
            motor.ShipActivityLocked = false;
            if (diceCamera != null) { Destroy(diceCamera.gameObject); diceCamera = null; }
            if (motor.PlayerCamera != null) motor.PlayerCamera.enabled = true;
        }
        void OnDisable() { if (motor != null) Release(); }
    }
}
