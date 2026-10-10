using PirateSlop.Networking;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PirateSlop.Ships
{
    [DefaultExecutionOrder(-27)]
    public sealed class ShipBilgePumpPlayer : MonoBehaviour
    {
        NetworkPlayer player;
        ShipBilgePump held;
        int heldSequence, consumedFrame = -1;
        float nextSend, acquiredAt, cycleStarted;
        public bool IsHolding => held != null;
        public bool ConsumedInput => consumedFrame == Time.frameCount;
        void Awake() => player = GetComponent<NetworkPlayer>();
        void Update()
        {
            if (player == null || !player.IsOwner || !player.IsSpawned) return;
            var motor = player.Motor;
            var keys = Keyboard.current;
            if (motor == null || motor.IsDead || motor.IsDowned || motor.IsFrozen || held == null && (motor.IsSwimming && !ShipWaterInterior.Contains(motor.transform.position) || motor.IsClimbing) ||
                motor.LocomotionLocked && held == null || SessionController.MenuOpen || DeveloperMenu.IsOpen || RoguelikeUpgradeUI.BlocksInput || keys == null)
            { Release(); return; }
            if (held != null)
            {
                OfferFlooding(held, false);
                consumedFrame = Time.frameCount;
                if (held.Ship == null || !held.Ship.IsSpawned || !held.CanReach(player, true) || !keys.eKey.isPressed || keys.qKey.wasPressedThisFrame || keys.escapeKey.wasPressedThisFrame)
                { Release(); return; }
                var state = held.Ship.PumpState;
                if (state.Sequence != heldSequence || state.Holder != player.Owner.ClientId + 1 && Time.unscaledTime - acquiredAt > 1.5f)
                { Release(); return; }
                motor.ShipActivityLocked = true;
                float start = state.Holder == player.Owner.ClientId + 1 ? state.ReleasedAt : cycleStarted;
                held.Predict(held.CyclePull(held.Clock - start));
                if (Time.unscaledTime >= nextSend)
                {
                    held.Ship.PullPump(0f, true, heldSequence);
                    nextSend = Time.unscaledTime + .1f;
                }
                return;
            }
            if (!motor.InputActive || motor.PlayerCamera == null) return;
            var camera = motor.PlayerCamera.transform;
            var ray = new Ray(camera.position, camera.forward);
            ShipBilgePump target = null;
            float nearest = float.PositiveInfinity;
            foreach (var candidate in ShipBilgePump.Active)
            {
                if (candidate == null || candidate.Ship == null || !candidate.Ship.IsSpawned ||
                    !candidate.AimedAt(ray, out float distance) || distance >= nearest || !candidate.CanReach(player)) continue;
                target = candidate;
                nearest = distance;
            }
            if (target == null) return;
            if (target.Ship == null || !target.Ship.IsSpawned) return;
            var current = target.Ship.PumpState;
            bool canReach = target.CanReach(player);
            OfferFlooding(target, canReach && current.Holder == 0 && target.Pull(current) <= .005f);
            if (!canReach) return;
            if (current.Holder != 0 || target.Pull(current) > .005f) return;
            if (!keys.eKey.isPressed) return;
            held = target;
            heldSequence = current.Sequence;
            cycleStarted = held.Clock;
            acquiredAt = Time.unscaledTime;
            nextSend = acquiredAt + .05f;
            consumedFrame = Time.frameCount;
            motor.ShipActivityLocked = true;
            player.Passenger?.Attach(held.Ship.GetComponent<Rigidbody>());
            held.Predict(0f);
            held.Ship.PullPump(0f, true, heldSequence);
        }
        static void OfferFlooding(ShipBilgePump pump, bool canPump)
        {
            var flooding = pump.Ship != null ? pump.Ship.GetComponent<ShipFlooding>() : null;
            if (flooding == null) return;
            string prompt = $"Затопление трюма: {flooding.Level * 100f:0}%";
            if (canPump) prompt += "\nУдерживайте E — откачивать воду";
            ContextPrompt.Offer(prompt, 83);
        }
        void Release()
        {
            if (held == null) return;
            if (held.Ship != null && held.Ship.IsSpawned) held.Ship.PullPump(0f, false, heldSequence);
            held.PredictReturn();
            held = null;
            if (player != null && player.Motor != null) player.Motor.ShipActivityLocked = false;
        }
        void OnDisable() => Release();
    }
}
