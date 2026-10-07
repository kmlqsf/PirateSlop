using PirateSlop.Networking;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PirateSlop.Ships
{
    [DefaultExecutionOrder(-26)]
    public sealed class ShipSlotMachinePlayer : MonoBehaviour
    {
        NetworkPlayer player;
        PlayerInventory inventory;
        int consumedFrame = -1;
        ShipSlotMachine held;
        float pendingPull, nextSend;
        public bool IsHolding => held != null;
        public ShipSlotMachine HeldMachine => held;
        public bool ConsumedInput => consumedFrame == Time.frameCount;
        readonly RaycastHit[] hits = new RaycastHit[24];
        void Awake() { player = GetComponent<NetworkPlayer>(); inventory = GetComponent<PlayerInventory>(); }
        void Update()
        {
            if (player == null || !player.IsOwner || !player.IsSpawned) return;
            if (player.Motor.IsDead || player.Motor.IsDowned || player.Motor.IsFrozen || player.Motor.IsSwimming || player.Motor.IsClimbing || player.Motor.LocomotionLocked && held == null ||
                SessionController.MenuOpen || DeveloperMenu.IsOpen || RoguelikeUpgradeUI.BlocksInput || Keyboard.current == null || Mouse.current == null)
            { Release(); return; }
            if (held != null)
            {
                consumedFrame = Time.frameCount;
                var state = held.Ship != null ? held.Ship.SlotState : default;
                if (!held.gameObject.activeInHierarchy || held.Ship == null || !held.Ship.IsSpawned || state.Phase != 1 ||
                    state.Payer != player.ParticipantId.Value || !Mouse.current.leftButton.isPressed || Keyboard.current.qKey.wasPressedThisFrame || !held.CanReach(player, true))
                { Release(); return; }
                player.Motor.ShipActivityLocked = true;
                pendingPull -= Mouse.current.delta.ReadValue().y * .004f;
                if (Time.unscaledTime >= nextSend)
                {
                    held.Ship.PullSlotLever(pendingPull, true);
                    pendingPull = 0; nextSend = Time.unscaledTime + .05f;
                }
                return;
            }
            if (!player.Motor.InputActive) return;
            var other = GetComponent<ShipV3PlayerInteraction>();
            if (other != null && other.ConsumedInput) return;
            var camera = player.Motor.PlayerCamera;
            if (camera == null || Keyboard.current == null || Mouse.current == null) return;
            ShipSlotMachine nearby = null;
            float best = float.PositiveInfinity;
            foreach (var candidate in ShipSlotMachine.Active)
            {
                if (candidate == null || candidate.Ship == null) continue;
                var state = candidate.Ship.SlotState;
                if ((state.Phase != 1 && state.Phase != 4) || state.Payer != player.ParticipantId.Value || !candidate.CanReach(player, true)) continue;
                float distance = (candidate.transform.position - transform.position).sqrMagnitude;
                if (distance < best) { best = distance; nearby = candidate; }
            }
            if (nearby != null && Keyboard.current.qKey.wasPressedThisFrame)
            {
                consumedFrame = Time.frameCount;
                nearby.Ship.UseSlotMachine(2, inventory.SelectedSlot); return;
            }
            if (nearby != null && nearby.Ship.SlotState.Phase == 1 && Mouse.current.leftButton.wasPressedThisFrame)
            {
                consumedFrame = Time.frameCount;
                held = nearby; pendingPull = 0; nextSend = Time.unscaledTime + .05f;
                player.Motor.ShipActivityLocked = true;
                nearby.Ship.PullSlotLever(0, true); return;
            }
            bool blocked = FirearmTrace.Cast(gameObject, camera.transform.position, camera.transform.position + camera.transform.forward * 3f, out var obstruction);
            int count = Physics.SphereCastNonAlloc(camera.transform.position, .12f, camera.transform.forward, hits, 3f, ~0, QueryTriggerInteraction.Collide);
            ShipSlotMachineTarget target = null;
            float nearest = 3f;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                var candidate = hit.collider.GetComponent<ShipSlotMachineTarget>();
                if (candidate == null || candidate.Lever || candidate.Machine == null || hit.distance >= nearest ||
                    blocked && obstruction.distance + .15f < hit.distance || !candidate.Machine.CanReach(player, candidate.Lever)) continue;
                nearest = hit.distance; target = candidate;
            }
            if (target == null) return;
            var machine = target.Machine;
            ContextPrompt.Offer(machine.IntakeHint(), 82);
            if (!Keyboard.current.eKey.wasPressedThisFrame) return;
            consumedFrame = Time.frameCount;
            machine.Ship.UseSlotMachine(0, inventory.SelectedSlot);
        }
        void Release()
        {
            if (held == null) return;
            if (held.Ship != null && held.Ship.IsSpawned) held.Ship.PullSlotLever(0, false);
            held = null; pendingPull = 0;
            if (player != null) player.Motor.ShipActivityLocked = false;
        }
        void OnDisable() { Release(); }
    }
}
