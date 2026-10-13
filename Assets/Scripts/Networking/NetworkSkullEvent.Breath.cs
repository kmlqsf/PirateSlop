using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkSkullEvent
    {
        readonly SyncVar<byte> breathPhase = new();
        readonly SyncVar<NetworkObject> breathTarget = new();
        readonly SyncVar<Vector3> breathEndpoint = new();
        readonly SyncVar<bool> breathContact = new();
        readonly System.Collections.Generic.HashSet<NetworkShip> breathStruckShips = new();
        readonly System.Collections.Generic.HashSet<CombatHealth> breathStruckCrew = new();
        float breathPhaseUntil, nextBreathAttack, nextBreathTick, nextBreathIgnite;
        int previousBreathShip = -1;
        NetworkShip cachedBreathShip;
        Collider cachedBreathSurface;
        float nextBreathSurface;
        NetworkPlayer breathCrewTarget;
        public bool BreathWarning => !Cleared && breathPhase.Value == 1;
        public bool Breathing => !Cleared && breathPhase.Value == 2;
        public Vector3 BreathEndpoint => breathEndpoint.Value;
        public bool BreathContact => breathContact.Value;

        Vector3 ShipBreathPoint(NetworkShip ship)
        {
            if (!IsBreathCrew(breathCrewTarget, ship)) breathCrewTarget = FindBreathCrew(ship);
            if (breathCrewTarget != null)
                return breathCrewTarget.transform.position + Vector3.up * 1.1f;
            if (cachedBreathShip == ship && cachedBreathSurface != null && cachedBreathSurface.enabled && Time.time < nextBreathSurface)
                return cachedBreathSurface.ClosestPointOnBounds(Mouth.position);
            cachedBreathShip = ship; cachedBreathSurface = null; nextBreathSurface = Time.time + .5f;
            var destruction = ship.GetComponent<ShipDestruction>();
            Vector3 point = ship.transform.position + ship.transform.up * 7f;
            float nearest = float.PositiveInfinity;
            foreach (var collider in ship.GetComponentsInChildren<Collider>())
            {
                if (!collider.enabled || collider.isTrigger || collider.attachedRigidbody != ship.Body || destruction != null && destruction.SectionFor(collider) == null) continue;
                var candidate = collider.ClosestPointOnBounds(Mouth.position);
                float distance = (candidate - Mouth.position).sqrMagnitude;
                if (distance < nearest) { point = candidate; nearest = distance; cachedBreathSurface = collider; }
            }
            return point;
        }

        static bool IsBreathCrew(NetworkPlayer player, NetworkShip ship)
        {
            return player != null && player.IsSpawned && !player.Eliminated.Value && !player.DeveloperNoclip &&
                player.Motor != null && !player.Motor.IsDead && player.Passenger != null && player.Passenger.Ship == ship.Body;
        }

        NetworkPlayer FindBreathCrew(NetworkShip ship)
        {
            NetworkPlayer choice = null;
            float nearest = float.PositiveInfinity;
            foreach (var player in NetworkPlayer.Active)
            {
                if (!IsBreathCrew(player, ship)) continue;
                float distance = (player.transform.position - Mouth.position).sqrMagnitude;
                if (distance < nearest) { nearest = distance; choice = player; }
            }
            return choice;
        }

        bool SkullShipTooClose(NetworkShip ship, float margin = 0f)
        {
            if (ship == null || !ship.IsSpawned || ship.IsSinking) return false;
            var local = ship.transform.InverseTransformPoint(transform.position);
            var half = ship.HullHalfExtents;
            var point = ship.transform.TransformPoint(new Vector3(Mathf.Clamp(local.x, -half.x, half.x), 0, Mathf.Clamp(local.z, -half.y, half.y)));
            float radius = PlatformRadius + Catalog.SkullBreathClearance + margin;
            return Vector3.ProjectOnPlane(point - transform.position, Vector3.up).sqrMagnitude <= radius * radius;
        }

        void AimSkullBreath()
        {
            var target = breathTarget.Value != null ? breathTarget.Value.GetComponent<NetworkShip>() : null;
            if (target != null && SkullShipTooClose(target, 8f))
            {
                var delta = ShipBreathPoint(target) - Mouth.position;
                var local = transform.InverseTransformDirection(delta);
                float goal = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
                serverYaw = Mathf.MoveTowardsAngle(serverYaw, goal, (Breathing ? 28f : 75f) * Time.deltaTime);
            }
            else serverYaw = Mathf.Repeat(serverYaw + Catalog.SkullRotationSpeed * Time.deltaTime, 360f);
        }

        void StopSkullBreath()
        {
            breathPhase.Value = 0;
            breathTarget.Value = null;
            breathCrewTarget = null;
            breathContact.Value = false;
            nextBreathAttack = Time.time + Catalog.SkullBreathCooldown;
        }

        void TickSkullBreath()
        {
            if (Cleared) { if (breathPhase.Value != 0) StopSkullBreath(); return; }
            if (breathPhase.Value == 0)
            {
                if (Time.time < nextBreathAttack) return;
                NetworkShip choice = null;
                float best = float.PositiveInfinity;
                foreach (var ship in NetworkShip.ActiveShips)
                {
                    if (!SkullShipTooClose(ship)) continue;
                    float score = (ship.transform.position - transform.position).sqrMagnitude;
                    if (FindBreathCrew(ship) == null) score += 2000000f;
                    if (ship.NetworkObject.ObjectId == previousBreathShip) score += 1000000f;
                    if (score < best) { choice = ship; best = score; }
                }
                if (choice == null) return;
                breathTarget.Value = choice.NetworkObject;
                breathCrewTarget = FindBreathCrew(choice);
                breathPhase.Value = 1;
                breathPhaseUntil = Time.time + Catalog.SkullBreathWarning;
                return;
            }
            var target = breathTarget.Value != null ? breathTarget.Value.GetComponent<NetworkShip>() : null;
            if (!SkullShipTooClose(target, 8f)) { StopSkullBreath(); return; }
            var targetPoint = ShipBreathPoint(target);
            if (breathPhase.Value == 1)
            {
                var forward = Vector3.ProjectOnPlane(targetPoint - Mouth.position, Vector3.up);
                if (Time.time >= breathPhaseUntil && Vector3.Angle(Mouth.forward, forward) < 10f)
                {
                    breathPhase.Value = 2;
                    breathEndpoint.Value = targetPoint;
                    breathContact.Value = false;
                    breathPhaseUntil = Time.time + Catalog.SkullBreathSeconds;
                    breathStruckShips.Clear(); breathStruckCrew.Clear();
                    nextBreathTick = nextBreathIgnite = 0f;
                    previousBreathShip = target.NetworkObject.ObjectId;
                }
                return;
            }
            if (Time.time >= breathPhaseUntil) { StopSkullBreath(); return; }
            if (Time.time < nextBreathTick) return;
            nextBreathTick = Time.time + .1f;
            Vector3 start = Mouth.position + Mouth.forward * 3f;
            Vector3 direction = (targetPoint - start).normalized;
            Vector3 flat = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
            float turn = Vector3.SignedAngle(Mouth.forward, flat, Vector3.up);
            var limited = Quaternion.AngleAxis(Mathf.Clamp(turn, -12f, 12f), Vector3.up) * Mouth.forward;
            direction = (limited * new Vector2(direction.x, direction.z).magnitude + Vector3.up * direction.y).normalized;
            float range = PlatformRadius + Catalog.SkullBreathClearance + 45f;
            RaycastHit contact = default;
            foreach (var hit in Physics.SphereCastAll(start, 2.5f, direction, range, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.collider.transform.IsChildOf(transform) && hit.distance < range) { contact = hit; range = hit.distance; }
            breathEndpoint.Value = contact.collider != null ? contact.point : start + direction * range;
            breathContact.Value = contact.collider != null;
            if (contact.collider == null || Time.time < nextBreathIgnite) return;
            nextBreathIgnite = Time.time + 1f;
            var struck = contact.collider.GetComponentInParent<NetworkShip>();
            if (struck != null)
            {
                if (breathStruckShips.Add(struck)) struck.Ignite(contact.point, null, .5f, contact.collider, contact.normal, true);
            }
            foreach (var collider in Physics.OverlapSphere(contact.point, 3f, ~0, QueryTriggerInteraction.Ignore))
            {
                var health = collider.GetComponentInParent<CombatHealth>();
                if (health == null || health.IsDead || health.GetComponent<NetworkPlayer>() == null) continue;
                if (breathStruckCrew.Add(health))
                {
                    health.Damage(10f);
                    health.GetComponent<NetworkHealth>()?.Ignite(null, 2f, 3f);
                }
            }
        }

    }
}
