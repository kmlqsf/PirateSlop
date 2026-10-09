using FishNet.Object;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkPlayer
    {
        const float KickCooldown = 3f, KickReach = 1.9f, KickWindup = .16f, KickDamage = 5f;
        float nextLocalKick, nextServerKick, kickImpactAt = -1f;
        Vector3 kickDirection;
        readonly Collider[] kickTargets = new Collider[128];

        bool CanKick => IsSpawned && motor != null && !motor.IsDead && !motor.IsDowned && !motor.IsFrozen &&
            !Eliminated.Value && !motor.IsSwimming && !motor.IsClimbing && !motor.LocomotionLocked &&
            motor.ActiveParrot == null && !(GetComponent<NetworkEquipment>()?.IsBusy ?? false) &&
            !(GetComponent<NetworkFishing>() is { } fishing && (fishing.IsFishing || fishing.IsEating || fishing.CarryingCatch));

        void UpdateKick()
        {
            if (IsServerInitialized && kickImpactAt >= 0f && Time.time >= kickImpactAt)
            {
                kickImpactAt = -1f;
                if (CanKick) ResolveKick();
            }
            if (!IsOwner || !CanKick || !motor.InputActive || SessionController.MenuOpen ||
                PlayerInventory.LootWindowOpen || (GetComponent<PlayerInventory>()?.ControlFocused ?? false) ||
                Time.time < nextLocalKick || Keyboard.current == null || !Keyboard.current.xKey.wasPressedThisFrame) return;
            nextLocalKick = Time.time + KickCooldown;
            kickDirection = motor.AimDirection;
            PresentKick();
            KickServerRpc(motor.AimDirection);
        }

        [ServerRpc]
        void KickServerRpc(Vector3 direction)
        {
            if (!CanKick || Time.time < nextServerKick || !float.IsFinite(direction.sqrMagnitude) || direction.sqrMagnitude < .5f) return;
            direction.Normalize();
            var flat = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (flat.sqrMagnitude > .01f && Vector3.Dot(flat.normalized, motor.transform.forward) < .5f) return;
            nextServerKick = Time.time + KickCooldown;
            kickDirection = direction;
            kickImpactAt = Time.time + KickWindup;
            KickObserversRpc();
        }

        void PresentKick()
        {
            GetComponent<PlayerKickVisual>()?.Play();
            GameAudio.Play(SoundCue.KickSwing, transform.position + Vector3.up);
        }

        [ObserversRpc(RunLocally = true)]
        void KickObserversRpc()
        {
            if (!IsOwner) PresentKick();
        }

        void ResolveKick()
        {
            Physics.SyncTransforms();
            Vector3 origin = transform.position + Vector3.up * .95f;
            int count = Physics.OverlapSphereNonAlloc(origin, KickReach, kickTargets, ~0, QueryTriggerInteraction.Collide);
            Component target = null;
            Vector3 contact = origin;
            float nearest = KickReach;
            for (int i = 0; i < count; i++)
            {
                var collider = kickTargets[i];
                if (collider == null || collider.transform.IsChildOf(transform)) continue;
                if (collider.isTrigger && collider.GetComponentInParent<PirateSlop.Ships.ShipMonkeyHitbox>() == null) continue;
                Component candidate = collider.GetComponentInParent<NetworkPlayer>();
                if (candidate is NetworkPlayer player && (player == this || player.Motor == null || player.Motor.IsDead || player.Eliminated.Value)) continue;
                if (candidate == null) candidate = collider.GetComponentInParent<NetworkFish>();
                if (candidate is NetworkFish loot && (!loot.Available || loot.MonkeyCarried ||
                    (loot.GetComponent<NetworkLooseCannonball>()?.IsHeld ?? false) || (loot.GetComponent<NetworkFishProjectile>()?.Stuck ?? false))) continue;
                if (candidate == null) candidate = collider.GetComponentInParent<CannonCarriage>();
                if (candidate == null) candidate = collider.GetComponentInParent<PirateSlop.Ships.ShipMonkeyHitbox>();
                if (candidate is PirateSlop.Ships.ShipMonkeyHitbox monkeyHitbox && monkeyHitbox.Monkey == null) continue;
                if (candidate == null) continue;
                Vector3 point = collider.ClosestPoint(origin);
                Vector3 delta = point - origin;
                if (delta.sqrMagnitude < .0025f) { point = collider.bounds.center; delta = point - origin; }
                float distance = delta.magnitude;
                if (distance < .05f || distance >= nearest || Vector3.Dot(delta / distance, kickDirection) < .65f) continue;
                bool blocked = false;
                foreach (var hit in Physics.RaycastAll(origin, delta / distance, distance, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.transform.IsChildOf(transform) || hit.transform.IsChildOf(candidate.transform)) continue;
                    blocked = true; break;
                }
                if (blocked) continue;
                nearest = distance; target = candidate; contact = point;
            }
            var push = Vector3.ProjectOnPlane(kickDirection, Vector3.up);
            if (push.sqrMagnitude < .01f) push = transform.forward;
            push.Normalize();
            SoundCue cue;
            if (target is NetworkPlayer victim)
            {
                if (victim.Motor.IsFrozen || victim.Motor.IsDowned) return;
                victim.ReleaseServerInteractions();
                victim.GetComponent<CombatHealth>()?.Damage(KickDamage, gameObject);
                if (!victim.Motor.IsDead)
                {
                    if (TryKickOverboard(victim, push, out var fallVelocity)) victim.KnockDown(fallVelocity, 2.8f);
                    else victim.KickPushObserversRpc(push * 11.3f + Vector3.up * 3.6f);
                }
                cue = SoundCue.KickBody;
            }
            else if (target is NetworkFish item)
            {
                if (!item.Kick(push * 3.2f + Vector3.up * 1.2f)) return;
                cue = SoundCue.KickObject;
            }
            else if (target is CannonCarriage carriage)
            {
                if (!carriage.Kick(push)) return;
                cue = SoundCue.KickCannon;
            }
            else if (target is PirateSlop.Ships.ShipMonkeyHitbox monkeyHitbox)
            {
                if (!monkeyHitbox.Monkey.ReceiveKick(gameObject, push)) return;
                cue = SoundCue.KickBody;
            }
            else return;
            KickHitObserversRpc(cue, contact);
        }

        bool TryKickOverboard(NetworkPlayer victim, Vector3 direction, out Vector3 velocity)
        {
            velocity = Vector3.zero;
            if (!victim.Motor.IsGrounded || victim.Motor.IsSwimming || victim.Motor.IsClimbing) return false;
            var body = victim.Passenger != null ? victim.Passenger.Ship : null;
            if (body == null) return false;
            var ship = body.GetComponent<NetworkShip>();
            if (ship == null) return false;
            Vector3 up = body.transform.up;
            Vector3 outward = Vector3.ProjectOnPlane(direction, up).normalized;
            if (outward.sqrMagnitude < .5f) return false;
            Vector3 feet = victim.transform.position;
            var controller = victim.GetComponent<CharacterController>();
            float radius = controller != null ? controller.radius * Mathf.Max(victim.transform.lossyScale.x, victim.transform.lossyScale.z) : .5f;
            float edgeDistance = radius + 2f;
            if (!KickDeckSupport(feet, up, victim, out var standingShip) || standingShip != ship) return false;
            if (KickDeckSupport(feet + outward * edgeDistance, up, victim, out _) ||
                KickDeckSupport(feet + outward * (edgeDistance + .75f), up, victim, out _) ||
                KickDeckSupport(feet + outward * (edgeDistance + 2.5f), up, victim, out _)) return false;
            Vector3 side = Vector3.Cross(up, outward) * radius * .65f;
            if (KickDeckSupport(feet + outward * edgeDistance + side, up, victim, out _) &&
                KickDeckSupport(feet + outward * edgeDistance - side, up, victim, out _)) return false;
            float clearance = .2f;
            foreach (var hit in Physics.RaycastAll(feet + up * .45f, outward, edgeDistance + .6f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(victim.transform) || hit.transform.IsChildOf(transform) ||
                    hit.collider.GetComponentInParent<NetworkPlayer>() != null || hit.collider.GetComponentInParent<NetworkFish>() != null) continue;
                if (hit.collider.GetComponentInParent<NetworkShip>() != ship || hit.collider.GetComponentInParent<SimpleCannon>() != null) return false;
                var bounds = hit.collider.bounds;
                float top = Vector3.Dot(bounds.center - feet, up) + Vector3.Dot(bounds.extents, new Vector3(Mathf.Abs(up.x), Mathf.Abs(up.y), Mathf.Abs(up.z)));
                var batch = hit.collider.GetComponent<PirateSlop.Ships.ShipV3CollisionBatch>();
                if (batch != null)
                {
                    Vector3 localHit = batch.transform.InverseTransformPoint(hit.point);
                    float best = float.MaxValue;
                    for (int i = 0; i < batch.SourceBounds.Length && i < batch.Sources.Length; i++)
                    {
                        var source = batch.Sources[i];
                        if (source == null || !source.gameObject.activeInHierarchy) continue;
                        var sourceBounds = batch.SourceBounds[i];
                        float score = (sourceBounds.ClosestPoint(localHit) - localHit).sqrMagnitude + (sourceBounds.center - localHit).sqrMagnitude * .00001f;
                        if (score >= best) continue;
                        best = score;
                        top = Vector3.Dot(batch.transform.TransformPoint(sourceBounds.center) - feet, up) +
                            Mathf.Abs(Vector3.Dot(batch.transform.TransformVector(Vector3.right * sourceBounds.extents.x), up)) +
                            Mathf.Abs(Vector3.Dot(batch.transform.TransformVector(Vector3.up * sourceBounds.extents.y), up)) +
                            Mathf.Abs(Vector3.Dot(batch.transform.TransformVector(Vector3.forward * sourceBounds.extents.z), up));
                    }
                }
                if (top > 1.6f) return false;
                clearance = Mathf.Max(clearance, top + .12f);
            }
            float lift = Mathf.Max(6.5f, Mathf.Sqrt(2f * Mathf.Abs(Physics.gravity.y) * (clearance + .75f)));
            velocity = outward * 5f + Vector3.up * lift;
            return true;
        }

        bool KickDeckSupport(Vector3 point, Vector3 up, NetworkPlayer victim, out NetworkShip ship)
        {
            ship = null;
            float nearest = .95f;
            bool found = false;
            foreach (var hit in Physics.RaycastAll(point + up * .35f, -up, nearest, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(victim.transform) || hit.transform.IsChildOf(transform) ||
                    hit.collider.GetComponentInParent<NetworkPlayer>() != null || hit.collider.GetComponentInParent<NetworkFish>() != null ||
                    hit.collider.GetComponentInParent<SimpleCannon>() != null || Vector3.Dot(hit.normal, up) < .55f || hit.distance >= nearest) continue;
                nearest = hit.distance;
                ship = hit.collider.GetComponentInParent<NetworkShip>();
                found = true;
            }
            return found;
        }

        [ObserversRpc(RunLocally = true)]
        void KickPushObserversRpc(Vector3 velocity)
        {
            if (motor == null || motor.IsDead) return;
            motor.ActiveCannon?.ReleaseControl();
            motor.ActiveHarpoon?.ReleaseControl();
            GetComponent<NetworkWeapon>()?.ReleaseGrapple();
            motor.BellPullLocked = motor.ShipActivityLocked = motor.SailPullLocked = motor.PickupLocked = false;
            motor.ApplyKickPush(velocity);
        }

        [ObserversRpc(RunLocally = true)]
        void KickHitObserversRpc(SoundCue cue, Vector3 point)
        {
            GameAudio.Play(cue, point);
            if (IsOwner) FirstPersonFeedback.Kick(point, -kickDirection, .035f);
        }
    }
}
