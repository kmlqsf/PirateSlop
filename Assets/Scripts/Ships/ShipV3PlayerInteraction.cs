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
                var grip = held.Kind == ShipV3TargetKind.Door ? held.Ship.DoorGrip : held.Kind == ShipV3TargetKind.Dispenser ? held.Ship.DispenserGrip : held.Ship.BellGrip;
                if (!held.gameObject.activeInHierarchy || grip == null || !mouse.leftButton.isPressed || keys.qKey.wasPressedThisFrame || held.Kind == ShipV3TargetKind.Dispenser && held.Ship.DispenserCooling || Vector3.Distance(transform.position + Vector3.up, grip.position) > 3.5f)
                { Release(); return; }
                motor.ShipActivityLocked = true;
                pending += mouse.delta.ReadValue();
                if (Time.unscaledTime >= nextSend)
                {
                    if (held.Kind == ShipV3TargetKind.Door) held.Ship.DragDoor((-pending.x + pending.y) * .5f, true);
                    else if (held.Kind == ShipV3TargetKind.Dispenser) held.Ship.PullDispenser(-pending.y * .004f, true);
                    else held.Ship.PullBell(pending, true);
                    pending = Vector2.zero; nextSend = Time.unscaledTime + .05f;
                }
                ContextPrompt.Offer(held.Kind == ShipV3TargetKind.Bell ? "Удерживайте ЛКМ и двигайте мышь — качать язычок" : held.Kind == ShipV3TargetKind.Dispenser ? "Удерживайте ЛКМ и тяните мышь вниз — выдать ядро" : "Удерживайте ЛКМ и двигайте мышь — открывать или закрывать", 80);
                return;
            }
            hovered = null;
            if (!motor.InputActive || motor.LocomotionLocked) return;
            hovered = FindTarget();
            var nearbyDice = FindDiceTable();
            if (nearbyDice != null && keys.fKey.wasPressedThisFrame)
            {
                diceShip = nearbyDice; joinPending = true; joinedAt = Time.time; pending = Vector2.zero;
                motor.ShipActivityLocked = true;
                diceShip.JoinDice();
                return;
            }
            if (hovered == null || hovered.Ship == null || !hovered.Ship.IsSpawned)
            {
                if (nearbyDice != null) ContextPrompt.Offer("F — сыграть в кости", 75);
                return;
            }
            if (hovered.Kind == ShipV3TargetKind.Lantern)
            {
                ContextPrompt.Offer("E — включить или выключить фонарь", 75);
                if (keys.eKey.wasPressedThisFrame) hovered.Ship.ToggleLantern(hovered.Index);
            }
            else if (hovered.Kind == ShipV3TargetKind.Dice)
            {
                if (nearbyDice != null) ContextPrompt.Offer("F — сыграть в кости", 75);
            }
            else
            {
                if (hovered.Kind == ShipV3TargetKind.Dispenser && hovered.Ship.DispenserCooling)
                { ContextPrompt.Offer("Рычаг возвращается — подождите", 75); return; }
                ContextPrompt.Offer(hovered.Kind == ShipV3TargetKind.Bell ? "ЛКМ — взять верёвку рынды" : hovered.Kind == ShipV3TargetKind.Dispenser ? "ЛКМ — взять рычаг выдачи ядер" : "Удерживать ЛКМ — двигать дверь", 75);
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    held = hovered; pending = Vector2.zero;
                    motor.ShipActivityLocked = true;
                    if (held.Kind == ShipV3TargetKind.Door) held.Ship.DragDoor(0, true);
                    else if (held.Kind == ShipV3TargetKind.Dispenser) held.Ship.PullDispenser(0, true);
                    else held.Ship.PullBell(Vector2.zero, true);
                }
            }
        }
        ShipV3Features FindDiceTable()
        {
            ShipV3Features closest = null;
            float distance = float.MaxValue;
            foreach (var ship in ShipV3Features.Active)
            {
                if (ship == null || !ship.IsSpawned || !ship.CanReachDice(player)) continue;
                float candidate = (ship.DiceTable.position - transform.position).sqrMagnitude;
                if (candidate >= distance) continue;
                distance = candidate; closest = ship;
            }
            return closest;
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
            if (result != null) return result;
            float best = float.MaxValue;
            foreach (var target in ShipV3InteractionTarget.Active)
            {
                if (target == null || target.Ship == null || !target.Ship.IsSpawned ||
                    target.Kind != ShipV3TargetKind.Bell && target.Kind != ShipV3TargetKind.Dispenser) continue;
                if (!ShipGripAim.CanAim(camera, transform, target.transform, .38f, out float score) || score >= best) continue;
                result = target; best = score;
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
                else if (held.Kind == ShipV3TargetKind.Dispenser) held.Ship.PullDispenser(0, false);
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
