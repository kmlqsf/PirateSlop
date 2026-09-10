using System.Collections.Generic;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class BotCaptain
    {
        readonly BotNavigation navigation = new();
        static readonly Dictionary<SimpleCannon, NetworkPlayer> gunWorkers = new();
        SimpleCannon workingCannon;
        float nextWork, nextShotAim;
        Vector3 placement;
        Quaternion placementRotation;
        bool hasPlacement;
        readonly Dictionary<SimpleCannon, float> blockedGuns = new();
        float workStarted;
        public static void ResetWork() { gunWorkers.Clear(); expeditions.Clear(); }
        public void Stop() => ReleaseWork();
        void ReleaseCannon()
        {
            if (workingCannon != null)
            {
                if (workingCannon.Operator == player.Motor) workingCannon.ReleaseControl();
                if (gunWorkers.TryGetValue(workingCannon, out var worker) && worker == player) gunWorkers.Remove(workingCannon);
            }
            workingCannon = null;
        }
        void ReleaseWork()
        {
            ReleaseCannon(); navigation.Clear(); hasPlacement = false;
            CancelTrip();
        }
        NetworkShip Enemy(NetworkShip ship, float range)
        {
            NetworkShip nearest = null;
            foreach (var other in NetworkShip.ActiveShips)
            {
                if (other == null || other == ship || other.IsSinking || other.TeamId.Value == ship.TeamId.Value || other.TeamId.Value <= 0) continue;
                float distance = FlatDistance(ship.transform.position, other.transform.position);
                if (distance < range) { range = distance; nearest = other; }
            }
            return nearest;
        }
        bool NavigateCombat(NetworkShip ship)
        {
            var enemy = Enemy(ship, 180f);
            if (enemy == null) return false;
            Vector3 delta = enemy.transform.position - ship.transform.position;
            float bearing = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
            float yaw = ship.transform.eulerAngles.y;
            float left = bearing + 90f, right = bearing - 90f;
            float desired = Mathf.Abs(Mathf.DeltaAngle(yaw, left)) < Mathf.Abs(Mathf.DeltaAngle(yaw, right)) ? left : right;
            var battery = ship.GetComponent<NetworkCannon>();
            if (battery != null && battery.Crate != null)
                foreach (var cannon in battery.Crate.Cannons)
                {
                    if (!cannon.gameObject.activeInHierarchy) continue;
                    Vector3 facing = ship.transform.InverseTransformDirection(cannon.transform.forward);
                    desired = bearing - Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;
                    break;
                }
            if (delta.magnitude > 105f) desired = Mathf.MoveTowardsAngle(bearing, desired, 55f);
            float turn = Mathf.DeltaAngle(yaw, desired);
            rudder = Mathf.Clamp(turn / 30f, -1f, 1f);
            deployment = delta.magnitude < 85f ? .1f : .4f;
            if (!ClearCourse(ship, ship.transform.position, ship.transform.forward, yaw, 45f)) deployment = 0f;
            return true;
        }
        PlayerCommand Work(NetworkShip ship)
        {
            var idle = new PlayerCommand { Yaw = player.transform.eulerAngles.y };
            var weapon = player.GetComponent<NetworkWeapon>();
            var inventory = player.GetComponent<PlayerInventory>();
            var cannons = ship.GetComponent<NetworkCannon>();
            if (cannons == null || cannons.Crate == null) return WalkDeck(ship);
            if (TryStartTrip(ship)) { ReleaseCannon(); return SupplyTrip(ship); }
            if (workingCannon != null && (!workingCannon.gameObject.activeInHierarchy || (workingCannon.Operator != null && workingCannon.Operator != player.Motor))) ReleaseCannon();
            if (workingCannon == null && Time.time >= nextWork && (inventory.CannonSlots == 0 || cannons.Crate.Cannons.FindAll(c => c.gameObject.activeInHierarchy).Count >= 2))
            {
                nextWork = Time.time + 1f;
                foreach (var cannon in cannons.Crate.Cannons)
                {
                    if (!cannon.gameObject.activeInHierarchy || cannon.Operator != null || cannon.Network.HasBoarding(cannon.Index)) continue;
                    if (blockedGuns.TryGetValue(cannon, out float until) && Time.time < until) continue;
                    if (gunWorkers.TryGetValue(cannon, out var worker) && worker != null && worker.IsSpawned && !worker.Motor.IsDead && worker != player) continue;
                    workingCannon = cannon; gunWorkers[cannon] = player; workStarted = Time.time; navigation.Clear(); break;
                }
            }
            if (workingCannon == null)
            {
                if (inventory.CannonSlots == 0)
                {
                    if (!cannons.Crate.KitAvailable) return WalkDeck(ship);
                    if (weapon.BotTakeKit(cannons)) { navigation.Clear(); return idle; }
                    return navigation.Move(player, cannons.Crate.Kit.transform.position, ship.transform);
                }
                if (!hasPlacement && Time.time >= nextWork - .5f) hasPlacement = FindPlacement(ship, out placement, out placementRotation);
                if (!hasPlacement) return WalkDeck(ship);
                if (weapon.BotPlaceCannon(cannons, placement, placementRotation)) { hasPlacement = false; navigation.Clear(); nextWork = 0f; return idle; }
                return navigation.Move(player, ship.transform.TransformPoint(placement - placementRotation * Vector3.forward * 1.5f), ship.transform);
            }
            var gun = workingCannon;
            if (!gun.IsLoaded && gun.Operator == player.Motor) gun.ReleaseControl();
            if (Time.time - workStarted > 35f && !gun.InBreechRange(player.Motor))
            { blockedGuns[gun] = Time.time + 30f; ReleaseCannon(); return idle; }
            if (!gun.IsLoaded)
            {
                if (gun.Operator == player.Motor) gun.ReleaseControl();
                bool ammo = false;
                for (int slot = 0; slot < 6; slot++) if (inventory.BallCount(slot) > 0 && inventory.BallItem(slot) != InventoryItem.BoardingHook && inventory.BallItem(slot) != InventoryItem.BoomerangCannonball) ammo = true;
                if (!ammo)
                {
                    if (weapon.BotSupplyBalls(cannons)) { navigation.Clear(); workStarted = Time.time; return idle; }
                    return navigation.Move(player, cannons.Crate.Supply.transform.position, ship.transform);
                }
                if (weapon.BotLoadCannon(gun)) { navigation.Clear(); return idle; }
                return navigation.Move(player, gun.transform.position - gun.transform.forward * 1.4f, ship.transform);
            }
            if (!gun.InBreechRange(player.Motor)) return navigation.Move(player, gun.transform.position - gun.transform.forward * 1.4f, ship.transform);
            if (!gun.TakeControl(player.Motor)) { ReleaseCannon(); return idle; }
            workStarted = Time.time;
            if (Time.time < nextShotAim || gun.IsLoading) return idle;
            nextShotAim = Time.time + .25f;
            var target = Enemy(ship, 135f);
            if (target != null && AimShot(gun, target, out float elevation, out float traverse))
            {
                gun.Aim(player.Motor, elevation, traverse);
                cannons.PublishBotAim(gun);
                if (!gun.IsIgnited) gun.Fire(player.gameObject);
            }
            return idle;
        }
        bool FindPlacement(NetworkShip ship, out Vector3 point, out Quaternion rotation)
        {
            point = default; rotation = Quaternion.identity;
            var enemy = Enemy(ship, 200f);
            int preferred = enemy != null && ship.transform.InverseTransformPoint(enemy.transform.position).x < 0f ? -1 : 1;
            foreach (int side in new[] { preferred, -preferred })
                foreach (float z in new[] { -10f, -5f, 0f, 5f, 10f })
                {
                    Vector3 origin = ship.transform.TransformPoint(new Vector3(side * 4.7f, 10f, z));
                    foreach (var hit in Physics.RaycastAll(origin, -ship.transform.up, 9f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (hit.rigidbody != ship.Body || Vector3.Dot(hit.normal, ship.transform.up) < .95f || hit.collider.GetComponentInParent<SimpleCannon>() != null) continue;
                        var local = ship.transform.InverseTransformPoint(hit.point);
                        if (local.y < 3.5f || local.y > 7f) continue;
                        var facing = ship.transform.rotation * Quaternion.Euler(0, side * 90f, 0);
                        bool free = true;
                        foreach (var collider in Physics.OverlapBox(hit.point + ship.transform.up * .65f, new Vector3(.85f, .5f, 1.5f), facing, ~0, QueryTriggerInteraction.Ignore))
                            if (collider.GetComponentInParent<AdvancedPlayerController>() == null) { free = false; break; }
                        if (!free) continue;
                        point = local; rotation = Quaternion.Euler(0, side * 90f, 0); return true;
                    }
                }
            return false;
        }
        bool AimShot(SimpleCannon gun, NetworkShip target, out float elevation, out float traverse)
        {
            elevation = gun.Elevation; traverse = gun.Traverse;
            var own = player.Ship;
            float speed = gun.LaunchSpeed * 1.15f;
            Vector3 ownVelocity = own.Motor.CannonPointVelocity(gun.Muzzle.position);
            Vector3 targetVelocity = target.Motor.CannonPointVelocity(target.transform.position);
            float delay = gun.IsIgnited ? gun.FuseSeconds * (1f - gun.FuseProgress) : gun.FuseSeconds;
            float travel = FlatDistance(gun.Muzzle.position, target.transform.position) / speed;
            Vector3 aim = target.transform.position + Vector3.up * 2.5f + targetVelocity * (delay + travel) - ownVelocity * (delay + travel);
            Vector3 local = gun.transform.InverseTransformDirection(aim - gun.Muzzle.position);
            traverse = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            if (Mathf.Abs(traverse) > gun.MaxTraverse) return false;
            float error = float.PositiveInfinity;
            for (float angle = gun.MinElevation; angle <= gun.MaxElevation; angle += 1f)
            {
                Vector3 direction = gun.transform.TransformDirection(Quaternion.Euler(-angle, traverse, 0f) * Vector3.forward);
                Vector3 p = gun.ShotPosition + ownVelocity * delay, v = direction * speed + ownVelocity;
                for (float t = .05f; t <= 6f; t += .05f)
                {
                    Vector3 next = CannonShotDamage.StepVelocity(v, .05f); p += (v + next) * .025f; v = next;
                    Vector3 predicted = target.transform.position + targetVelocity * (delay + t) + Vector3.up * 2.5f;
                    float miss = (p - predicted).sqrMagnitude;
                    if (miss < error) { error = miss; elevation = angle; }
                    if (p.y < target.transform.position.y - 1f) break;
                }
            }
            if (error > 25f) return false;
            Vector3 position = gun.ShotPosition;
            Vector3 velocity = gun.transform.TransformDirection(Quaternion.Euler(-elevation, traverse, 0f) * Vector3.forward) * speed + ownVelocity;
            for (float t = 0; t < 6f; t += .1f)
            {
                Vector3 next = CannonShotDamage.StepVelocity(velocity, .1f), delta = (velocity + next) * .05f;
                foreach (var hit in Physics.SphereCastAll(position, .15f, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.GetComponentInParent<AdvancedPlayerController>() != null || hit.collider.GetComponentInParent<Cannonball>() != null || hit.transform.IsChildOf(gun.transform)) continue;
                    var ship = hit.collider.GetComponentInParent<NetworkShip>();
                    if (ship == target) return true;
                    if (ship == own && t > .2f) continue;
                    return false;
                }
                position += delta; velocity = next;
                if (position.y < own.transform.position.y - 1f) break;
            }
            return true;
        }
    }
}
