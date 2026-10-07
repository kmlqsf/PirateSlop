using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace PirateSlop.Networking
{
    public enum InventoryItem { None = -1, Fish = 0, Pistol = 1, Rod = 2, Cannon = 3, Cannonball = 4, Mallet = 5, Plank = 6, Sabre = 7, FireCannonball = 8, IceCannonball = 9, PushCannonball = 10, BoomerangCannonball = 11, Rum = 12, Wine = 13, Musket = 14, DoubleBarrel = 15, BombParrot = 16, GrapplingHook = 17, BoardingHook = 18, Pufferfish = 19, Swordfish = 20, HolyGrenade = 21, Spyglass = 22, VortexBottle = 23, FogBottle = 24, Barricade = 25, Lantern = 26 }
    [DefaultExecutionOrder(80)]
    public sealed partial class NetworkFish : NetworkBehaviour
    {
        public static readonly System.Collections.Generic.List<NetworkFish> ServerItems = new();
        public bool OnShip => platformId.Value != 0;
        public NetworkShip SupportingShip
        {
            get
            {
                if (platformId.Value == 0) return null;
                if (platform.Value != null) return platform.Value.GetComponent<NetworkShip>();
                foreach (var ship in NetworkShip.ActiveShips)
                    if (ship != null && ship.ParticipantId.Value == platformId.Value) return ship;
                return null;
            }
        }
        public override void OnStopServer() { ServerItems.Remove(this); base.OnStopServer(); }
        void Awake()
        {
            WaterImpactBody.Ensure(gameObject);
            if (LivingFish) movementBounds = LootPlacement.VisualBounds(this);
            var body = GetComponent<Rigidbody>();
            if (body != null && GetComponent<NetworkLooseCannonball>() == null) { body.isKinematic = true; body.useGravity = false; }
        }
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
        bool visualReady;
        int visualPlatformId;
        Transform visualSupport;
        Vector3 visualPosition;
        Quaternion visualRotation;
        NetworkFishProjectile projectile;
        void OnDisable()
        {
            visualReady = false;
            motionStarted = -1f;
            RestoreBodyMotion();
        }
        public bool Available => IsSpawned && !taken && !diving.Value && !(GetComponent<SlotPrizeFlight>()?.Flying ?? false) && !(GetComponent<NetworkFishProjectile>()?.Flying ?? false) && !(GetComponent<NetworkHolyGrenade>()?.Busy ?? false) && !(GetComponent<NetworkVortexBottle>()?.Flying ?? false) && !(GetComponent<NetworkFogBottle>()?.Flying ?? false);
        float expires;
        float nextFlop;
        bool airborne;
        Vector3 velocity;
        NetworkShip resolvedPlatform;
        public void Place(NetworkObject support, Vector3 point, Quaternion orientation)
        {
            var body = GetComponent<Rigidbody>();
            if (body != null && GetComponent<NetworkLooseCannonball>() == null) { body.isKinematic = true; body.useGravity = false; }
            platform.Value = support;
            platformId.Value = support != null ? support.GetComponent<NetworkShip>().ParticipantId.Value : 0;
            worldPosition.Value = point;
            position.Value = support != null ? support.transform.InverseTransformPoint(point) : point;
            rotation.Value = support != null ? Quaternion.Inverse(support.transform.rotation) * orientation : orientation;
            transform.SetPositionAndRotation(point, orientation);
        }
        public override void OnStartServer()
        {
            ServerItems.Add(this);
            base.OnStartServer(); expires = float.PositiveInfinity;
            taken = false; airborne = false; escapeNet = null; escapeShip = null; diving.Value = false;
            nextFlop = Time.time + Random.Range(3f, 6f);
        }
        public bool Take()
        {
            if (!IsServerInitialized || !Available) return false;
            monkeyCarrier.Value = null;
            taken = true;
            ServerManager.Despawn(NetworkObject);
            return true;
        }
        void LateUpdate()
        {
            if (PresentMonkeyCarry()) return;
            if (!IsSpawned || (Item == InventoryItem.Cannonball && GetComponent<NetworkLooseCannonball>() != null)) return;
            if (platformId.Value == 0) resolvedPlatform = null;
            if (platform.Value != null) resolvedPlatform = platform.Value.GetComponent<NetworkShip>();
            if (resolvedPlatform == null && platformId.Value > 0)
                foreach (var ship in FindObjectsByType<NetworkShip>(FindObjectsInactive.Exclude))
                    if (ship.ParticipantId.Value == platformId.Value) { resolvedPlatform = ship; break; }
            if (platformId.Value > 0 && resolvedPlatform == null)
            {
                if (IsServerInitialized) { ServerManager.Despawn(NetworkObject); return; }
                transform.position = worldPosition.Value;
                return;
            }
            var support = resolvedPlatform != null ? resolvedPlatform.transform : null;
            if (projectile == null) projectile = GetComponent<NetworkFishProjectile>();
            bool smooth = !IsServerInitialized && visualReady && visualSupport == support && visualPlatformId == platformId.Value && (projectile == null || !projectile.Flying) && !(GetComponent<NetworkVortexBottle>()?.Flying ?? false) && !(GetComponent<NetworkFogBottle>()?.Flying ?? false) && (visualPosition - position.Value).sqrMagnitude < 4f;
            float blend = smooth ? 1f - Mathf.Exp(-22f * Time.deltaTime) : 1f;
            visualPosition = Vector3.Lerp(visualPosition, position.Value, blend);
            visualRotation = smooth ? Quaternion.Slerp(visualRotation, rotation.Value, blend) : rotation.Value;
            visualReady = true; visualSupport = support; visualPlatformId = platformId.Value;
            transform.SetPositionAndRotation(support != null ? support.TransformPoint(visualPosition) : visualPosition,
                support != null ? support.rotation * visualRotation : visualRotation);
            if (IsServerInitialized && LivingFish) SimulateFlop(Time.deltaTime);
            if (IsSpawned && IsClientInitialized && LivingFish) AnimateBody();
            if (IsServerInitialized && IsSpawned && Time.time > expires) ServerManager.Despawn(NetworkObject);
        }
        [ObserversRpc(RunLocally = true)]
        void FlopSoundObserversRpc(SoundCue cue, Vector3 point) => GameAudio.Play(cue, point, cue == SoundCue.Splash ? 1f : .6f);
    }
}
