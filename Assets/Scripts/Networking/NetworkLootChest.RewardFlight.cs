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
        {
            point = default;
            if (ship == null) return false;
            var colliders = ship.GetComponentsInChildren<Collider>();
            System.Array.Sort(colliders, (a, b) => RewardDeckPriority(a).CompareTo(RewardDeckPriority(b)));
            var box = prefab != null ? prefab.GetComponent<BoxCollider>() : null;
            Vector3 halfSize = box != null ? Vector3.Scale(box.size, prefab.transform.localScale) * .5f : new Vector3(.6f, .5f, .4f);
            Vector3 boxCenter = box != null ? Vector3.Scale(box.center, prefab.transform.localScale) : Vector3.up * .5f;
            var offsets = new[] { new Vector2(.35f, -.35f), new Vector2(-.35f, -.35f), new Vector2(.35f, .35f), new Vector2(-.35f, .35f), Vector2.zero };
            foreach (var floor in colliders)
            {
                if (!floor.enabled || floor.isTrigger || RewardDeckPriority(floor) >= 10) continue;
                var bounds = floor.bounds;
                var localBounds = new Bounds(ship.transform.InverseTransformPoint(bounds.center), Vector3.zero);
                for (int i = 0; i < 8; i++)
                    localBounds.Encapsulate(ship.transform.InverseTransformPoint(bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f))));
                foreach (var offset in offsets)
                {
                    Vector3 sample = localBounds.center + new Vector3(offset.x * localBounds.size.x, localBounds.extents.y + 3f, offset.y * localBounds.size.z);
                    var ray = new Ray(ship.transform.TransformPoint(sample), -ship.transform.up);
                    if (!floor.Raycast(ray, out var hit, localBounds.size.y + 6f) || Vector3.Dot(hit.normal, ship.transform.up) < .7f) continue;
                    Vector3 candidate = hit.point + ship.transform.up * .03f;
                    Vector3 center = candidate + ship.transform.rotation * boxCenter + ship.transform.up * .08f;
                    bool clear = true;
                    foreach (var obstacle in Physics.OverlapBox(center, halfSize * .95f, ship.transform.rotation, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (obstacle == floor) continue;
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

        static int RewardDeckPriority(Collider collider)
        {
            var section = collider.GetComponentInParent<ShipDamageSection>();
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
                CombatVfx.Splash(point);
                GameAudio.Play(SoundCue.WaterSplash, point);
            }
            else GameAudio.Play(SoundCue.Impact, point);
        }
    }
}
