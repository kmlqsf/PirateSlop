using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace PirateSlop.Networking
{
    public struct HullDamage
    {
        public int Id;
        public Vector3 Point, Normal, BackPoint, BackNormal;
        public bool HasBack;
        public int Strikes;
        public float Health;
    }

    public sealed class ShipRepair : NetworkBehaviour
    {
        public GameObject BreachPrefab;
        public GameObject[] PatchPrefabs;
        public NetworkFish StartingPlank, StartingMallet;
        readonly SyncList<HullDamage> damage = new();
        readonly Dictionary<int, GameObject> visuals = new();
        readonly Dictionary<int, int> visibleStrikes = new();
        int nextId;
        CombatHealth health;
        void Awake() { health = GetComponent<CombatHealth>(); }
        public override void OnStartServer()
        {
            base.OnStartServer();
            SpawnSupply(StartingPlank, new Vector3(-1.5f, 4.36f, -10f), Quaternion.identity);
            SpawnSupply(StartingMallet, new Vector3(-.8f, 4.4f, -10f), Quaternion.Euler(90, 0, 0));
        }
        void SpawnSupply(NetworkFish prefab, Vector3 point, Quaternion rotation)
        {
            if (prefab == null) return;
            var item = Instantiate(prefab, transform.TransformPoint(point), transform.rotation * rotation);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(item.gameObject, gameObject.scene);
            item.Place(NetworkObject, item.transform.position, item.transform.rotation);
            ServerManager.Spawn(item.NetworkObject);
        }
        public void AddImpact(Collider surface, Vector3 point, Vector3 normal, float amount)
        {
            if (!IsServerInitialized || health.IsDead || amount <= 0f) return;
            Vector3 localPoint = transform.InverseTransformPoint(point);
            Vector3 localNormal = transform.InverseTransformDirection(normal);
            for (int i = damage.Count - 1; i >= 0; i--)
            {
                var existing = damage[i];
                if ((existing.Point - localPoint).sqrMagnitude > .65f * .65f || Vector3.Dot(existing.Normal, localNormal) < .5f) continue;
                if (existing.Strikes >= 3) { damage.RemoveAt(i); continue; }
                existing.Health += Mathf.Min(amount, health.Current);
                damage[i] = existing;
                return;
            }
            if (damage.Count >= 48)
                for (int i = 0; i < damage.Count; i++) if (damage[i].Strikes >= 3) { damage.RemoveAt(i); break; }
            var wound = new HullDamage { Id = ++nextId, Point = transform.InverseTransformPoint(point), Normal = transform.InverseTransformDirection(normal), Health = Mathf.Min(amount, health.Current) };
            if (surface.Raycast(new Ray(point - normal * 1.8f, normal), out var back, 1.75f))
            {
                wound.HasBack = true;
                wound.BackPoint = transform.InverseTransformPoint(back.point);
                wound.BackNormal = transform.InverseTransformDirection(back.normal);
            }
            damage.Add(wound);
        }
        public bool FindDamage(Vector3 point, out int id, out int strikes)
        {
            id = -1; strikes = 0; float best = .8f * .8f;
            foreach (var wound in damage)
            {
                if (wound.Strikes >= 3) continue;
                float distance = (transform.TransformPoint(wound.Point) - point).sqrMagnitude;
                if (wound.HasBack) distance = Mathf.Min(distance, (transform.TransformPoint(wound.BackPoint) - point).sqrMagnitude);
                if (distance >= best) continue;
                best = distance; id = wound.Id; strikes = wound.Strikes;
            }
            return id >= 0;
        }
        public void Strike(int id, Vector3 point) => StrikeServerRpc(id, transform.InverseTransformPoint(point));
        [ServerRpc(RequireOwnership = false)]
        void StrikeServerRpc(int id, Vector3 localPoint, NetworkConnection sender = null)
        {
            var player = sender != null ? SessionController.Instance.GetPlayer(sender.ClientId) : null;
            if (player == null || health.IsDead || !float.IsFinite(localPoint.sqrMagnitude)) return;
            var equipment = player.GetComponent<NetworkWeapon>();
            var hands = player.GetComponent<MalletHands>();
            if (equipment == null || hands == null || !equipment.CanRepair) return;
            Vector3 point = transform.TransformPoint(localPoint);
            Vector3 origin = player.transform.position + Vector3.up * (player.Motor.IsCrouched ? .8f : 1.5f);
            Vector3 delta = point - origin;
            if (delta.sqrMagnitude > 3.5f * 3.5f || !FindDamage(point, out int nearestId, out _) || nearestId != id) return;
            RaycastHit nearest = default; float distance = delta.magnitude + .08f;
            foreach (var hit in Physics.RaycastAll(origin, delta.normalized, distance, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(player.transform) && hit.distance < distance) { nearest = hit; distance = hit.distance; }
            if (nearest.collider == null || nearest.collider.GetComponentInParent<ShipRepair>() != this || Vector3.Distance(nearest.point, point) > .15f) return;
            for (int i = 0; i < damage.Count; i++)
            {
                var wound = damage[i];
                if (wound.Id != id || wound.Strikes >= 3) continue;
                if (!hands.AcceptAuthorityStrike()) return;
                if (wound.Strikes == 0 && !equipment.ConsumeRepairPlank()) return;
                wound.Strikes++;
                if (wound.Strikes == 3)
                {
                    float restored = Mathf.Min(120f, wound.Health);
                    health.Heal(restored);
                    wound.Health -= restored;
                    if (wound.Health > .01f) wound.Strikes = 0;
                }
                damage[i] = wound;
                StrikeObserversRpc(player.NetworkObject, localPoint);
                return;
            }
        }
        [ObserversRpc(RunLocally = true)]
        void StrikeObserversRpc(NetworkObject player, Vector3 point)
        {
            if (player != null) player.GetComponent<MalletHands>()?.PlayStrike();
            GameAudio.Play(SoundCue.Place, transform.TransformPoint(point));
        }
        void Update()
        {
            if (!IsSpawned) return;
            foreach (var wound in damage)
            {
                if (visibleStrikes.TryGetValue(wound.Id, out int previous) && previous == wound.Strikes) continue;
                if (visuals.TryGetValue(wound.Id, out var old)) Destroy(old);
                var root = new GameObject("HullDamage_" + wound.Id);
                root.transform.SetParent(transform, false);
                CreateFace(root.transform, wound.Point, wound.Normal, wound.Strikes);
                if (wound.HasBack) CreateFace(root.transform, wound.BackPoint, wound.BackNormal, wound.Strikes);
                visuals[wound.Id] = root; visibleStrikes[wound.Id] = wound.Strikes;
            }
            if (visuals.Count <= damage.Count) return;
            var removed = new List<int>();
            foreach (var entry in visuals)
            {
                bool found = false;
                foreach (var wound in damage) if (wound.Id == entry.Key) { found = true; break; }
                if (!found) { Destroy(entry.Value); removed.Add(entry.Key); }
            }
            foreach (int id in removed) { visuals.Remove(id); visibleStrikes.Remove(id); }
        }
        void CreateFace(Transform parent, Vector3 point, Vector3 normal, int strikes)
        {
            if (BreachPrefab != null && strikes < 3)
            {
                var breach = Instantiate(BreachPrefab, parent);
                breach.transform.localPosition = point + normal * .035f;
                breach.transform.localRotation = Quaternion.FromToRotation(Vector3.up, normal);
            }
            if (strikes == 0 || PatchPrefabs == null || PatchPrefabs.Length == 0) return;
            for (int i = 0; i < Mathf.Min(strikes, 3); i++)
            {
                var plank = Instantiate(PatchPrefabs[i % PatchPrefabs.Length], parent);
                var facing = Quaternion.FromToRotation(Vector3.up, normal);
                plank.transform.localRotation = facing * Quaternion.Euler(0, (i - 1) * 8f, 0);
                plank.transform.localPosition = point + normal * .075f + facing * new Vector3(0, 0, (i - 1) * .19f);
                plank.transform.localScale = new Vector3(.85f, 1f, 1f);
            }
        }
    }
}
