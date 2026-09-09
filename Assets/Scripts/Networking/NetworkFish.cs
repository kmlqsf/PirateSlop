using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace PirateSlop.Networking
{
    public enum InventoryItem { None = -1, Fish = 0, Pistol = 1, Rod = 2, Cannon = 3, Cannonball = 4, Mallet = 5, Plank = 6, Sabre = 7, FireCannonball = 8, IceCannonball = 9, PushCannonball = 10, BoomerangCannonball = 11, Rum = 12, Wine = 13, Musket = 14, DoubleBarrel = 15, BombParrot = 16, GrapplingHook = 17, BoardingHook = 18 }
    public sealed class NetworkFish : NetworkBehaviour
    {
        public InventoryItem Item;
        readonly SyncVar<InventoryItem> ammoItem = new(InventoryItem.None);
        public InventoryItem CurrentItem => ammoItem.Value != InventoryItem.None ? ammoItem.Value : Item;
        public void SetAmmoItem(InventoryItem value) { if (CannonAmmo.IsBall(value)) ammoItem.Value = value; }
        public string ItemName => Item >= InventoryItem.Wine ? InventoryIcons.ItemName(Item) : Item == InventoryItem.Rum ? "ром — запас возрождений" : CannonAmmo.IsBall(CurrentItem) ? InventoryIcons.ItemName(CurrentItem) : Item == InventoryItem.Sabre ? "саблю" : Item == InventoryItem.Fish ? "рыбу" : Item == InventoryItem.Pistol ? "пистолет" : Item == InventoryItem.Rod ? "удочку" : Item == InventoryItem.Cannonball ? "ядро" : Item == InventoryItem.Mallet ? "киянку" : Item == InventoryItem.Plank ? "доску" : "разобранную пушку";
        readonly SyncVar<NetworkObject> platform = new();
        readonly SyncVar<int> platformId = new();
        readonly SyncVar<Vector3> worldPosition = new();
        readonly SyncVar<Vector3> position = new();
        readonly SyncVar<Quaternion> rotation = new(Quaternion.identity);
        bool taken;
        float expires;
        float nextFlop;
        bool airborne;
        Vector3 velocity;
        NetworkShip resolvedPlatform;
        public void Place(NetworkObject support, Vector3 point, Quaternion orientation)
        {
            platform.Value = support;
            platformId.Value = support != null ? support.GetComponent<NetworkShip>().ParticipantId.Value : 0;
            worldPosition.Value = point;
            position.Value = support != null ? support.transform.InverseTransformPoint(point) : point;
            rotation.Value = support != null ? Quaternion.Inverse(support.transform.rotation) * orientation : orientation;
            transform.SetPositionAndRotation(point, orientation);
        }
        public override void OnStartServer()
        {
            base.OnStartServer(); expires = Item == InventoryItem.Fish ? Time.time + 600f : float.PositiveInfinity;
            nextFlop = Time.time + Random.Range(10f, 30f);
        }
        public bool Take()
        {
            if (!IsServerInitialized || taken) return false;
            taken = true;
            ServerManager.Despawn(NetworkObject);
            return true;
        }
        void LateUpdate()
        {
            if (!IsSpawned || (Item == InventoryItem.Cannonball && GetComponent<NetworkLooseCannonball>() != null)) return;
            if (platformId.Value == 0) resolvedPlatform = null;
            if (platform.Value != null) resolvedPlatform = platform.Value.GetComponent<NetworkShip>();
            if (resolvedPlatform == null && platformId.Value > 0)
                foreach (var ship in FindObjectsByType<NetworkShip>(FindObjectsSortMode.None))
                    if (ship.ParticipantId.Value == platformId.Value) { resolvedPlatform = ship; break; }
            if (platformId.Value > 0 && resolvedPlatform == null)
            {
                if (IsServerInitialized) { ServerManager.Despawn(NetworkObject); return; }
                transform.position = worldPosition.Value;
                return;
            }
            var support = resolvedPlatform != null ? resolvedPlatform.transform : null;
            transform.SetPositionAndRotation(support != null ? support.TransformPoint(position.Value) : position.Value,
                support != null ? support.rotation * rotation.Value : rotation.Value);
            if (IsServerInitialized && Item == InventoryItem.Fish) SimulateFlop(Time.deltaTime);
            if (IsServerInitialized && IsSpawned && Time.time > expires) ServerManager.Despawn(NetworkObject);
        }
        void SimulateFlop(float dt)
        {
            if (!airborne)
            {
                if (Time.time < nextFlop) return;
                Vector2 sideways = Random.insideUnitCircle.normalized * Random.Range(.5f, 1.3f);
                velocity = new Vector3(sideways.x, Random.Range(3.2f, 4.8f), sideways.y);
                if (resolvedPlatform != null)
                    velocity += Vector3.ProjectOnPlane(resolvedPlatform.transform.forward, Vector3.up).normalized * resolvedPlatform.Motor.Speed;
                Vector3 start = transform.position; Quaternion facing = transform.rotation;
                resolvedPlatform = null; Place(null, start, facing); airborne = true;
                FlopSoundObserversRpc(SoundCue.FishDrop, start);
            }
            int steps = Mathf.Max(1, Mathf.CeilToInt(dt / .02f));
            float step = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                velocity += Vector3.down * (9.81f * step);
                Vector3 delta = velocity * step;
                RaycastHit nearest = default; float distance = delta.magnitude;
                foreach (var hit in Physics.SphereCastAll(transform.position, .075f, delta.normalized, distance, ~0, QueryTriggerInteraction.Ignore))
                    if (!hit.transform.IsChildOf(transform) && hit.collider.GetComponentInParent<NetworkFish>() == null && hit.distance <= distance)
                    { nearest = hit; distance = hit.distance; }
                if (nearest.collider != null)
                {
                    if (velocity.y <= 0f && nearest.normal.y > .5f)
                    {
                        var ship = nearest.collider.GetComponentInParent<NetworkShip>();
                        Place(ship != null ? ship.NetworkObject : null, nearest.point + nearest.normal * .12f, Quaternion.FromToRotation(Vector3.up, nearest.normal) * Quaternion.Euler(0, Random.Range(0, 360), 90));
                        airborne = false; nextFlop = Time.time + Random.Range(10f, 30f);
                        FlopSoundObserversRpc(SoundCue.FishDrop, transform.position);
                        return;
                    }
                    delta = delta.normalized * Mathf.Max(0, distance - .01f);
                    velocity = Vector3.Reflect(velocity, nearest.normal) * .4f;
                }
                position.Value = transform.position + delta;
                worldPosition.Value = position.Value; transform.position = position.Value;
                var ocean = OceanSurface.Instance;
                if (ocean != null && transform.position.y <= ocean.Height(transform.position))
                {
                    FlopSoundObserversRpc(SoundCue.Splash, transform.position);
                    ServerManager.Despawn(NetworkObject); return;
                }
            }
        }
        [ObserversRpc(RunLocally = true)]
        void FlopSoundObserversRpc(SoundCue cue, Vector3 point) => GameAudio.Play(cue, point, .6f);
    }
}

