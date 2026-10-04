using PirateSlop.Networking;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PirateSlop.Ships
{
    [DefaultExecutionOrder(-25)]
    [DisallowMultipleComponent]
    public sealed class ShipV3PlayerInteraction : MonoBehaviour
    {
        NetworkPlayer player;
        AdvancedPlayerController motor;
        ShipV3InteractionTarget held, hovered;
        ShipV3Features diceShip;
        Camera diceCamera;
        FirstPersonModelVisibility[] modelVisibility;
        float nextSend;
        Vector2 pending;
        Vector2 cupPointer, cupVelocity;
        float lastCupMotion;
        bool cupHeld;
        bool joinPending;
        float joinedAt;
        int consumedFrame = -1;
        public bool ConsumedInput => consumedFrame == Time.frameCount;
        readonly RaycastHit[] targetHits = new RaycastHit[32];
        void Awake()
        {
            player = GetComponent<NetworkPlayer>(); motor = GetComponent<AdvancedPlayerController>();
            modelVisibility = GetComponentsInChildren<FirstPersonModelVisibility>(true);
        }
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
                consumedFrame = Time.frameCount;
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
                        diceCamera.fieldOfView = 50f;
                        foreach (var visibility in modelVisibility) visibility.SetAlternateCamera(diceCamera);
                        motor.PlayerCamera.enabled = false;
                    }
                    if (keys.fKey.wasPressedThisFrame || keys.qKey.wasPressedThisFrame || keys.escapeKey.wasPressedThisFrame || !diceShip.CanUseDice)
                    { Release(); return; }
                    bool releasing = mouse.leftButton.wasReleasedThisFrame;
                    bool holding = mouse.leftButton.isPressed;
                    if (holding && !cupHeld) { cupVelocity = Vector2.zero; lastCupMotion = float.NegativeInfinity; }
                    Vector2 drag = holding || releasing ? mouse.delta.ReadValue() * .00055f : Vector2.zero;
                    if (holding || releasing) cupPointer = Vector2.ClampMagnitude(cupPointer + drag, .24f);
                    else if (diceShip.DicePhase(slot) == 5) cupPointer = Vector2.zero;
                    float deltaTime = Mathf.Max(Time.unscaledDeltaTime, .001f);
                    if (drag.sqrMagnitude > .000000001f)
                    {
                        Vector2 motion = diceShip.DiceDragVelocity(slot, drag) / deltaTime;
                        cupVelocity = Vector2.Dot(cupVelocity, motion) < 0f ? motion : Vector2.Lerp(cupVelocity, motion, 1f - Mathf.Exp(-50f * deltaTime));
                        lastCupMotion = Time.unscaledTime;
                    }
                    else if (Time.unscaledTime - lastCupMotion > .1f) cupVelocity = Vector2.zero;
                    cupVelocity = Vector2.ClampMagnitude(cupVelocity, 2.8f);
                    cupHeld = holding;
                    diceShip.PreviewDiceCup(slot, cupPointer);
                    bool gather = keys.eKey.wasPressedThisFrame;
                    if (Time.unscaledTime >= nextSend || gather || mouse.leftButton.wasReleasedThisFrame)
                    {
                        diceShip.DiceInput(cupPointer, cupVelocity, holding, gather, false);
                        nextSend = Time.unscaledTime + .05f;
                    }
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
            if (!motor.InputActive || motor.LocomotionLocked) { hovered = null; return; }
            hovered = FindTarget();
            var nearbyDice = FindDiceTable();
            if (nearbyDice != null && keys.fKey.wasPressedThisFrame)
            {
                consumedFrame = Time.frameCount;
                diceShip = nearbyDice; joinPending = true; joinedAt = Time.time; pending = Vector2.zero;
                cupPointer = cupVelocity = Vector2.zero;
                cupHeld = false; lastCupMotion = float.NegativeInfinity;
                motor.ShipActivityLocked = true;
                diceShip.JoinDice();
                return;
            }
            if (hovered == null || hovered.Ship == null || !hovered.Ship.IsSpawned)
            {
                if (nearbyDice != null) ContextPrompt.Offer("F — сыграть в кости\n" + nearbyDice.DiceSummary(), 75);
                return;
            }
            if (hovered.Kind == ShipV3TargetKind.Lantern)
            {
                ContextPrompt.Offer("E — включить или выключить фонарь", 75);
                if (keys.eKey.wasPressedThisFrame) { consumedFrame = Time.frameCount; hovered.Ship.ToggleLantern(hovered.Index); }
            }
            else if (hovered.Kind == ShipV3TargetKind.Dice)
            {
                if (nearbyDice != null) ContextPrompt.Offer("F — сыграть в кости\n" + nearbyDice.DiceSummary(), 75);
            }
            else if (hovered.Kind == ShipV3TargetKind.Candle)
            {
                if (nearbyDice == hovered.Ship)
                {
                    ContextPrompt.Offer((hovered.Ship.CandleBurning ? "E — погасить свечу" : "E — зажечь свечу") + "\nF — сыграть в кости\n" + nearbyDice.DiceSummary(), 75);
                    if (keys.eKey.wasPressedThisFrame) { consumedFrame = Time.frameCount; hovered.Ship.ToggleCandle(); }
                }
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
            foreach (var ship in ShipV3Features.Active)
            {
                if (ship == null || !ship.IsSpawned || ship.DiceCandle == null || !ship.CanReachDice(player)) continue;
                var shape = ship.DiceCandle.GetComponent<Collider>();
                var candle = ship.DiceCandle.GetComponent<ShipV3InteractionTarget>();
                Vector3 point = shape != null ? shape.bounds.center : ship.DiceCandle.position;
                float depth = Vector3.Dot(point - camera.position, camera.forward);
                if (depth < .05f || depth > 3f) continue;
                Vector3 aim = camera.position + camera.forward * depth;
                point = shape != null ? shape.ClosestPoint(aim) : point;
                if (Vector3.Distance(point, aim) < (hovered == candle ? .18f : .12f) && ship.CanSeeDice(player, point))
                    return candle;
            }
            bool blocked = FirearmTrace.Cast(gameObject, camera.position, camera.position + camera.forward * 3f, out var direct);
            var exact = blocked ? direct.collider.GetComponentInParent<ShipV3InteractionTarget>() : null;
            if (exact != null && (exact.Kind != ShipV3TargetKind.Dice && exact.Kind != ShipV3TargetKind.Candle || exact.Ship != null && exact.Ship.CanReachDice(player))) return exact;
            float closest = 3f;
            ShipV3InteractionTarget result = null;
            int count = Physics.SphereCastNonAlloc(camera.position, .16f, camera.forward, targetHits, 3f, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                var hit = targetHits[i];
                if (hit.transform.IsChildOf(transform) || hit.distance >= closest) continue;
                var target = hit.collider.GetComponentInParent<ShipV3InteractionTarget>();
                if (target == null || target.Ship == null || !target.Ship.IsSpawned) continue;
                if ((target.Kind == ShipV3TargetKind.Dice || target.Kind == ShipV3TargetKind.Candle) && !target.Ship.CanReachDice(player)) continue;
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
            if (diceCamera != null && diceShip != null)
            {
                int slot = diceShip.LocalDiceSlot(player.Owner.ClientId);
                if (slot >= 0) { diceShip.DiceCameraPose(slot, out var point, out var rotation); diceCamera.transform.SetPositionAndRotation(point, rotation); }
            }
        }
        void OnGUI()
        {
            if (!player.IsOwner || diceShip == null || SessionController.MenuOpen || DeveloperMenu.IsOpen || motor.IsDead) return;
            int slot = diceShip.LocalDiceSlot(player.Owner.ClientId);
            if (slot >= 0) ContextPrompt.Draw(diceShip.DiceInstructions(slot) + "\n" + diceShip.DiceSummary());
            else if (joinPending) ContextPrompt.Draw("Занимаем место за бочкой…");
        }
        void Release()
        {
            if (held != null && held.Ship != null && held.Ship.IsSpawned)
            {
                if (held.Kind == ShipV3TargetKind.Door) held.Ship.DragDoor(0, false);
                else if (held.Kind == ShipV3TargetKind.Dispenser) held.Ship.PullDispenser(0, false);
                else held.Ship.PullBell(Vector2.zero, false);
            }
            if (diceShip != null && diceShip.IsSpawned)
            {
                diceShip.EndDicePreview(diceShip.LocalDiceSlot(player.Owner.ClientId));
                diceShip.DiceInput(Vector2.zero, Vector2.zero, false, false, true);
            }
            held = hovered = null; diceShip = null; pending = Vector2.zero; joinPending = false;
            cupPointer = cupVelocity = Vector2.zero;
            cupHeld = false;
            motor.ShipActivityLocked = false;
            foreach (var visibility in modelVisibility) if (visibility != null) visibility.SetAlternateCamera(null);
            if (diceCamera != null) { Destroy(diceCamera.gameObject); diceCamera = null; }
            if (motor.PlayerCamera != null) motor.PlayerCamera.enabled = true;
        }
        void OnDisable() { if (motor != null) Release(); }
    }
}
