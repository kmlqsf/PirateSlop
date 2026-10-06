using FishNet.Object.Synchronizing;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkLootChest
    {
        readonly SyncVar<Vector3> rewardStart = new();
        readonly SyncVar<Vector3> rewardEnd = new();
        readonly SyncVar<float> rewardDuration = new(2.5f);
        readonly SyncVar<float> rewardAge = new();
        float rewardStarted, receivedRewardAge = -1f, receivedRewardAt;

        public void PlaceOnDeck(NetworkShip targetShip, Vector3 point)
        {
            if (targetShip == null) return;
            LaunchReward(point, point, .5f, targetShip.transform.rotation, targetShip);
            phase.Value = SeaLootState.Ready;
        }

        public void LaunchReward(Vector3 start, Vector3 end, float duration, Quaternion rotation, NetworkShip targetShip = null)
        {
            kind.Value = SeaLootKind.FloatingReward;
            eventPoint.Value = end;
            deck = targetShip;
            support.Value = targetShip != null ? targetShip.NetworkObject : null;
            supportId.Value = targetShip != null ? targetShip.ParticipantId.Value : 0;
            anchor.Value = targetShip != null ? targetShip.transform.InverseTransformPoint(end) : end;
            rewardStart.Value = start;
            rewardEnd.Value = end;
            rewardDuration.Value = Mathf.Max(.5f, duration);
            rewardAge.Value = 0f;
            rewardStarted = Time.time;
            facing.Value = targetShip != null ? Quaternion.Inverse(targetShip.transform.rotation) * rotation : rotation;
            phase.Value = SeaLootState.Flying;
            transform.SetPositionAndRotation(start, rotation);
        }

        void TickRewardFlight()
        {
            if (phase.Value != SeaLootState.Flying) return;
            if (supportId.Value != 0 && (deck == null || !deck.IsSpawned || deck.IsSinking))
            {
                Vector3 end = deck != null ? deck.transform.TransformPoint(anchor.Value) : rewardEnd.Value;
                end.y = OceanSurface.Instance != null ? OceanSurface.Instance.Height(end) : eventPoint.Value.y;
                rewardEnd.Value = anchor.Value = end;
                facing.Value = transform.rotation;
                support.Value = null;
                supportId.Value = 0;
                deck = null;
            }
            float elapsed = Time.time - rewardStarted;
            rewardAge.Value = Mathf.Floor(elapsed * 10f) / 10f;
            if (elapsed < rewardDuration.Value) return;
            phase.Value = SeaLootState.Ready;
            Vector3 landing = RewardDestination();
            eventPoint.Value = landing;
            transform.position = landing;
            RewardLandedRpc(landing);
        }

        Vector3 RewardDestination() => deck != null && supportId.Value != 0 ? deck.transform.TransformPoint(anchor.Value) : rewardEnd.Value;

        Vector3 RewardFlightPoint()
        {
            if (receivedRewardAge != rewardAge.Value)
            {
                receivedRewardAge = rewardAge.Value;
                receivedRewardAt = Time.time;
            }
            float elapsed = IsServerInitialized ? Time.time - rewardStarted : receivedRewardAge + Time.time - receivedRewardAt;
            float t = Mathf.Clamp01(elapsed / rewardDuration.Value);
            Vector3 end = RewardDestination();
            float height = Mathf.Clamp(Vector3.Distance(rewardStart.Value, end) * .12f, 3f, 12f);
            return Vector3.Lerp(rewardStart.Value, end, t) + Vector3.up * (4f * height * t * (1f - t));
        }

        public static bool TryFindRewardDeckPoint(NetworkShip ship, NetworkLootChest prefab, out Vector3 point)
            => TryFindDeckPoint(ship, prefab, out point, false);

        public static bool TryFindBowDeckPoint(NetworkShip ship, NetworkLootChest prefab, out Vector3 point)
            => TryFindDeckPoint(ship, prefab, out point, true);

        static bool TryFindDeckPoint(NetworkShip ship, NetworkLootChest prefab, out Vector3 point, bool bow)
        {
            point = default;
            if (ship == null) return false;
            var floors = new System.Collections.Generic.List<(Collider Collider, Bounds Bounds, int Priority)>();
            var destruction = ship.GetComponent<ShipDestruction>();
            foreach (var collider in ship.GetComponentsInChildren<Collider>())
            {
                if (collider.isTrigger || collider.GetComponent<PirateSlop.Ships.ShipV3CollisionBatch>() != null) continue;
                var section = destruction != null ? destruction.SectionFor(collider) : null;
                if (!collider.enabled && (section == null || section.CollisionBatch == null)) continue;
                int priority = RewardDeckPriority(collider);
                if (priority >= 10) continue;
                floors.Add((collider, DeckBounds(ship, collider), priority));
            }
            floors.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            float bowLimit = float.NegativeInfinity;
            if (bow)
            {
                foreach (var floor in floors) bowLimit = Mathf.Max(bowLimit, floor.Bounds.max.z);
                bowLimit -= 5.5f;
                floors.Sort((a, b) => b.Bounds.max.z.CompareTo(a.Bounds.max.z));
            }
            var box = prefab != null ? prefab.GetComponent<BoxCollider>() : null;
            var chestBounds = box != null ? new Bounds(box.center, box.size) : new Bounds(Vector3.up * .5f, new Vector3(1.2f, 1f, .8f));
            if (prefab != null && prefab.Lid != null)
                foreach (var lidBox in prefab.Lid.GetComponentsInChildren<BoxCollider>())
                    for (int i = 0; i < 8; i++)
                    {
                        Vector3 corner = lidBox.center + Vector3.Scale(lidBox.size * .5f, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                        chestBounds.Encapsulate(prefab.transform.InverseTransformPoint(lidBox.transform.TransformPoint(corner)));
                    }
            Vector3 halfSize = prefab != null ? Vector3.Scale(chestBounds.extents, prefab.transform.localScale) : chestBounds.extents;
            Vector3 boxCenter = prefab != null ? Vector3.Scale(chestBounds.center, prefab.transform.localScale) : chestBounds.center;
            var offsets = new[] { new Vector2(.35f, -.35f), new Vector2(-.35f, -.35f), new Vector2(.35f, .35f), new Vector2(-.35f, .35f), Vector2.zero };
            foreach (var floor in floors)
            {
                var localBounds = floor.Bounds;
                if (bow && localBounds.max.z < bowLimit) continue;
                foreach (var offset in offsets)
                {
                    Vector3 sample = localBounds.center + new Vector3(offset.x * localBounds.size.x, localBounds.extents.y + 3f, offset.y * localBounds.size.z);
                    if (bow) sample.z = localBounds.max.z - Mathf.Min(localBounds.extents.z, 1.4f) - Mathf.Abs(offset.y) * 3f;
                    var ray = new Ray(ship.transform.TransformPoint(sample), -ship.transform.up);
                    if (!TryDeckHit(ship, ray, localBounds.size.y + 6f, out var hit)) continue;
                    Vector3 candidate = hit.point + ship.transform.up * .03f;
                    if (bow && (ship.transform.InverseTransformPoint(hit.point).z < bowLimit || !DeckSupportsChest(ship, candidate, halfSize))) continue;
                    Vector3 center = candidate + ship.transform.rotation * boxCenter + ship.transform.up * .08f;
                    bool clear = true;
                    foreach (var obstacle in Physics.OverlapBox(center, halfSize * .95f, ship.transform.rotation, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (obstacle == hit.collider && obstacle.GetComponent<PirateSlop.Ships.ShipV3CollisionBatch>() == null) continue;
                        if (Vector3.Dot(obstacle.bounds.max - hit.point, ship.transform.up) <= .15f) continue;
                        clear = false;
                        break;
                    }
                    if (!clear) continue;
                    point = candidate;
                    return true;
                }
            }
            return false;
        }

        static Bounds DeckBounds(NetworkShip ship, Collider floor)
        {
            Bounds bounds;
            Matrix4x4 matrix;
            if (floor is MeshCollider mesh && mesh.sharedMesh != null)
            {
                bounds = mesh.sharedMesh.bounds;
                matrix = ship.transform.worldToLocalMatrix * floor.transform.localToWorldMatrix;
            }
            else if (floor is BoxCollider box)
            {
                bounds = new Bounds(box.center, box.size);
                matrix = ship.transform.worldToLocalMatrix * floor.transform.localToWorldMatrix;
            }
            else
            {
                bounds = floor.bounds;
                matrix = ship.transform.worldToLocalMatrix;
            }
            var local = new Bounds(matrix.MultiplyPoint3x4(bounds.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
                local.Encapsulate(matrix.MultiplyPoint3x4(bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f))));
            return local;
        }

        static bool TryDeckHit(NetworkShip ship, Ray ray, float distance, out RaycastHit floor)
        {
            floor = default;
            bool found = false;
            foreach (var hit in Physics.RaycastAll(ray, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!hit.transform.IsChildOf(ship.transform) || hit.distance >= distance || Vector3.Dot(hit.normal, ship.transform.up) < .7f || RewardDeckPriority(hit.collider, hit.point) >= 10) continue;
                floor = hit; distance = hit.distance; found = true;
            }
            return found;
        }

        static bool DeckSupportsChest(NetworkShip ship, Vector3 point, Vector3 halfSize)
        {
            foreach (float x in new[] { -halfSize.x, halfSize.x })
                foreach (float z in new[] { -halfSize.z, halfSize.z })
                {
                    Vector3 from = point + ship.transform.rotation * new Vector3(x, .25f, z);
                    if (!TryDeckHit(ship, new Ray(from, -ship.transform.up), .4f, out _)) return false;
                }
            return true;
        }

        static int RewardDeckPriority(Collider collider, Vector3? point = null)
        {
            var batch = collider.GetComponent<PirateSlop.Ships.ShipV3CollisionBatch>();
            var section = batch != null && point.HasValue ? batch.Resolve(point.Value) : null;
            if (batch != null && !point.HasValue)
            {
                int priority = 10;
                foreach (var source in batch.Sources) if (source != null) priority = Mathf.Min(priority, RewardDeckPriority(source));
                return priority;
            }
            if (section == null)
            {
                var owner = collider.GetComponentInParent<ShipDestruction>();
                section = owner != null ? owner.SectionFor(collider) : collider.GetComponentInParent<ShipDamageSection>();
            }
            if (section != null && section.Owner != null && section.Owner.Definition(section.SectionId).Type != ShipSectionType.Deck) return 10;
            string name = section != null ? section.name : collider.name;
            if (name.Contains("FloorMid")) return 0;
            if (name.Contains("FloorFront")) return 1;
            if (name.Contains("FloorBackTop")) return 2;
            if (name.Contains("FloorBackBottom")) return 10;
            if (name.Contains("Deck") || name.Contains("Floor")) return 3;
            return 10;
        }

        [FishNet.Object.ObserversRpc(RunLocally = true)]
        void RewardLandedRpc(Vector3 point)
        {
            if (!IsClientInitialized) return;
            if (supportId.Value == 0)
            {
                float duration = Mathf.Max(.5f, rewardDuration.Value);
                Vector3 destination = RewardDestination();
                float height = Mathf.Clamp(Vector3.Distance(rewardStart.Value, destination) * .12f, 3f, 12f);
                Vector3 incoming = (destination - rewardStart.Value) / duration - Vector3.up * (4f * height / duration);
                WaterImpactPhysics.Report(point, incoming, 45f, .65f, WaterImpactKind.Object, gameObject);
                GameAudio.Play(SoundCue.WaterSplash, point);
            }
            else GameAudio.Play(SoundCue.Impact, point);
        }
    }
}
