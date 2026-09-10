using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using UnityEngine;

namespace PirateSlop.Networking
{
    public struct CannonPlacement
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public float Elevation;
        public float Traverse;
        public bool Removed;
        public bool Mortar;
        public bool Loaded;
        public bool FireQueued;
        public InventoryItem Ammo;
        public float Fuse;
    }

    public sealed partial class NetworkCannon : NetworkBehaviour
    {
        readonly SyncVar<bool> kitTaken = new(false);
        readonly SyncList<CannonPlacement> placements = new();
        public CannonballCrate Crate { get; private set; }
        Cannonball ball;
        Rigidbody shipBody;
        int holder = -1;
        float nextSync;

        void Awake()
        {
            Crate = GetComponentInChildren<CannonballCrate>(true);
            shipBody = GetComponent<Rigidbody>();
            if (Crate != null) { ball = Crate.Supply; ball.Network = this; }
        }
        public override void OnStartServer() { base.OnStartServer(); if (Crate != null) { Crate.ResetSupply(); if (Crate.MortarPrefab != null) { placements.Add(new CannonPlacement { Position = Crate.MortarPosition, Rotation = Quaternion.identity, Elevation = 60f, Fuse = -1f, Mortar = true }); ApplyState(); } } }
        public override void OnStopServer() { if (Crate != null) Crate.ClearSpecialSupply(); base.OnStopServer(); }
        public override void OnStartClient() { base.OnStartClient(); ApplyState(); }
        public bool TakeKit()
        {
            if (!IsServerInitialized || Crate == null || kitTaken.Value || !Crate.KitAvailable) return false;
            kitTaken.Value = true; Crate.Kit.SetActive(false); return true;
        }
        public void Place(Vector3 position, Quaternion rotation)
        {
            if (!IsServerInitialized) return;
            placements.Add(new CannonPlacement { Position = position, Rotation = rotation.normalized, Elevation = 3f, Fuse = -1f });
            ApplyState();
        }
        public bool RemoveCannon(NetworkWeapon player, int index)
        {
            var cannon = Cannon(index);
            if (!IsServerInitialized || cannon == null || cannon.IsMortar || cannon.Operator != null || HasBoarding(index) || cannon.IsIgnited || cannon.IsLoading || !player.CanAddItem(InventoryItem.Cannon)) return false;
            if (cannon.IsLoaded)
            {
                if (Crate.SpecialSupplyPrefab == null) return false;
                var dropped = Instantiate(Crate.SpecialSupplyPrefab, cannon.Muzzle.position + cannon.Muzzle.forward * .6f, Quaternion.identity);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(dropped.gameObject, gameObject.scene);
                dropped.SetAmmoItem(cannon.LoadedAmmo);
                dropped.Place(NetworkObject, dropped.transform.position, dropped.transform.rotation);
                ServerManager.Spawn(dropped.NetworkObject);
                cannon.ResetSupply();
            }
            if (!player.AddItem(InventoryItem.Cannon)) return false;
            var placement = placements[index]; placement.Removed = true; placements[index] = placement;
            cannon.gameObject.SetActive(false);
            return true;
        }
        public bool StoreBall(NetworkWeapon player)
        {
            if (!IsServerInitialized || ball == null || ball.Loaded || (holder != -1 && holder != player.Owner.ClientId) || !player.CanReach(ball.transform.position, ball.transform)) return false;
            if (!player.AddSupplyBalls(InventoryItem.Cannonball, 3)) return false;
            holder = -1; Crate.ResetSupply(); ResetStoredBallObserversRpc();
            return true;
        }
        [ObserversRpc]
        void ResetStoredBallObserversRpc()
        {
            if (!IsServerInitialized && Crate != null) Crate.ResetSupply();
        }
        public bool LoadInventoryBall(NetworkWeapon player, int index, InventoryItem ammo = InventoryItem.Cannonball)
        {
            var cannon = Cannon(index);
            if (!IsServerInitialized || cannon == null || HasBoarding(index) || cannon.IsLoaded || !CannonAmmo.IsBall(ammo) || !player.CanReachCannon(cannon)) return false;
            if (!cannon.LoadAmmo(ammo)) return false;
            var placement = placements[index]; placement.Loaded = true; placement.Ammo = ammo; placement.Fuse = -1f; placements[index] = placement;
            return true;
        }
        public void MoveCarriage(int index,Vector3 position,Quaternion rotation)
        {
            if(!IsServerInitialized || index<0 || index>=placements.Count)return;
            var placement=placements[index];placement.Position=position;placement.Rotation=rotation;placements[index]=placement;
        }
        void ApplyState()
        {
            if (Crate == null) return;
            Crate.Kit.SetActive(!kitTaken.Value);
            while (Crate.Cannons.Count < placements.Count)
            {
                var placement = placements[Crate.Cannons.Count];
                Crate.AddCannon(placement.Position, placement.Rotation, placement.Mortar);
            }
            for (int i = 0; i < placements.Count; i++)
            {
                var cannon=Crate.Cannons[i];
                cannon.gameObject.SetActive(!placements[i].Removed);
                if (placements[i].Removed) continue;
                if (cannon.Operator == null || IsServerInitialized)
                    cannon.SetAim(placements[i].Elevation, placements[i].Traverse);
                if (!IsServerInitialized)
                {
                    if (placements[i].Loaded && !cannon.IsLoaded) cannon.LoadAmmo(placements[i].Ammo);
                    else if (!placements[i].Loaded && cannon.IsLoaded) cannon.ResetSupply();
                    cannon.ShowFuse(placements[i].Fuse);
                    cannon.ShowFireQueued(placements[i].FireQueued);
                }
                if(!IsServerInitialized)
                {
                    float blend=1f-Mathf.Exp(-20f*Time.deltaTime);
                    cannon.transform.localPosition=Vector3.Lerp(cannon.transform.localPosition,placements[i].Position,blend);
                    cannon.transform.localRotation=Quaternion.Slerp(cannon.transform.localRotation,placements[i].Rotation,blend);
                }
            }
        }
        void Update()
        {
            UpdateBoarding();
            if (IsServerInitialized)
            {
                for (int i = 0; i < placements.Count; i++)
                {
                    var cannon = Cannon(i);
                    if (cannon == null) continue;
                    var placement = placements[i];
                    if (!cannon.IsIgnited && placement.FireQueued == cannon.IsFireQueued) continue;
                    placement.FireQueued = cannon.IsFireQueued;
                    placement.Fuse = cannon.IsIgnited ? cannon.FuseProgress : -1f;
                    placements[i] = placement;
                }
            }
            if (IsClientInitialized || IsServerInitialized) ApplyState();
        }
        public void NotifyIgnited(int index) { var placement = placements[index]; placement.Fuse = 0f; placements[index] = placement; }
        SimpleCannon Cannon(int index) => Crate != null && index >= 0 && index < Crate.Cannons.Count && index < placements.Count && !placements[index].Removed ? Crate.Cannons[index] : null;
        bool CanUse(NetworkConnection sender, Vector3 point)
        {
            var player = sender != null ? SessionController.Instance.GetPlayer(sender.ClientId) : null;
            return player != null && !player.Motor.IsDead && !player.Motor.LocomotionLocked && float.IsFinite(point.sqrMagnitude) && Vector3.Distance(player.transform.position, point) <= 6f;
        }
        public void NotifyFired(int index, Vector3 position, Vector3 velocity, InventoryItem ammo)
        {
            var placement = placements[index]; placement.Loaded = false; placement.FireQueued = false; placement.Fuse = -1f; placements[index] = placement;
            ShotObserversRpc(index, position, velocity, ammo);
        }
        [ObserversRpc]
        void ShotObserversRpc(int index, Vector3 position, Vector3 velocity, InventoryItem ammo)
        {
            if (IsServerInitialized) return;
            ApplyState();
            var cannon = Cannon(index);
            if (cannon != null) { cannon.SpawnShot(position, velocity, false, ammo); cannon.ResetSupply(); }
        }
        public void RequestBall(bool holding, Vector3 position) => BallServerRpc(holding, transform.InverseTransformPoint(position));
        [ServerRpc(RequireOwnership = false)]
        void BallServerRpc(bool holding, Vector3 position, NetworkConnection sender = null)
        {
            if (ball == null || sender == null) return;
            Vector3 world = transform.TransformPoint(position);
            if (ball.Loaded || (holder != -1 && holder != sender.ClientId) || (holding && (!CanUse(sender, world) || (holder == -1 && Vector3.Distance(world, ball.transform.position) > 1f)))) { RejectHoldTargetRpc(sender); return; }
            if (!holding && holder != sender.ClientId) return;
            if (!holding && !CanUse(sender, world)) world = ball.transform.position;
            holder = holding ? sender.ClientId : -1;
            ball.Held = holding; ball.AttachToPlatform(null); ball.transform.SetParent(null, true);
            ball.Body.isKinematic = true; ball.Body.position = world;
            ball.GetComponent<Collider>().enabled = !holding;
            if (!holding) { ball.Release(); }
        }
        [TargetRpc]
        void RejectHoldTargetRpc(NetworkConnection connection)
        { if (ball != null) { ball.Held = false; ball.GetComponent<Collider>().enabled = !ball.Loaded; } }
        public void RequestFire(int index) => FireServerRpc(index);
        public void PublishBotAim(SimpleCannon cannon)
        {
            if (!IsServerInitialized || cannon == null || Cannon(cannon.Index) != cannon) return;
            var placement = placements[cannon.Index];
            placement.Elevation = cannon.Elevation; placement.Traverse = cannon.Traverse;
            placements[cannon.Index] = placement;
        }
        public void RequestControl(int index, bool active) => ControlServerRpc(index, active);
        public void RequestAim(int index, float elevation, float traverse) => AimServerRpc(index, elevation, traverse);
        public void NotifyControlEnded(AdvancedPlayerController motor)
        {
            var player = motor.GetComponent<NetworkPlayer>();
            if (player != null && player.Owner != null && player.Owner.IsActive) ControlTargetRpc(player.Owner, player.NetworkObject, -1);
        }
        [ServerRpc(RequireOwnership = false)]
        void ControlServerRpc(int index, bool active, NetworkConnection sender = null)
        {
            var player = sender != null ? SessionController.Instance.GetPlayer(sender.ClientId) : null;
            var cannon = Cannon(index);
            if (player == null) return;
            if (!active)
            {
                if (cannon != null && cannon.Operator == player.Motor) cannon.ReleaseControl();
                return;
            }
            if (cannon == null || !CanUse(sender, cannon.transform.position) || !cannon.TakeControl(player.Motor)) return;
            ControlTargetRpc(sender, player.NetworkObject, index);
        }
        [TargetRpc]
        void ControlTargetRpc(NetworkConnection connection, NetworkObject player, int index)
        {
            if (player != null) player.GetComponent<CannonHands>().SetCannonView(Cannon(index));
        }
        [ServerRpc(RequireOwnership = false)]
        void AimServerRpc(int index, float elevation, float traverse, NetworkConnection sender = null)
        {
            var cannon = Cannon(index);
            var player = sender != null ? SessionController.Instance.GetPlayer(sender.ClientId) : null;
            if (cannon == null || player == null || !cannon.Aim(player.Motor, elevation, traverse)) return;
            var placement = placements[index];
            placement.Elevation = cannon.Elevation; placement.Traverse = cannon.Traverse;
            placements[index] = placement;
        }
        public void DragBreech(int index, float degrees, bool holding) => DragBreechServerRpc(index, degrees, holding);
        [ServerRpc(RequireOwnership = false)]
        void DragBreechServerRpc(int index, float degrees, bool holding, NetworkConnection sender = null)
        {
            var cannon = Cannon(index);
            var player = sender != null ? SessionController.Instance.GetPlayer(sender.ClientId) : null;
            if (cannon == null || player == null || !cannon.DragBreech(player.Motor, degrees, holding)) return;
            var placement = placements[index]; placement.Elevation = cannon.Elevation; placements[index] = placement;
        }
        [ServerRpc(RequireOwnership = false)]
        void FireServerRpc(int index, NetworkConnection sender = null)
        {
            var cannon = Cannon(index);
            var player = sender != null ? SessionController.Instance.GetPlayer(sender.ClientId) : null;
            if (cannon != null && player != null && cannon.Operator == player.Motor && cannon.InBreechRange(player.Motor))
                cannon.Fire(player.gameObject);
        }
        public void RequestLoad(int index, Vector3 localPosition) => LoadServerRpc(index, localPosition);
        [ServerRpc(RequireOwnership = false)]
        void LoadServerRpc(int index, Vector3 localPosition, NetworkConnection sender = null)
        {
            var cannon = Cannon(index);
            if (ball == null || cannon == null || cannon.IsLoaded || ball.Loaded || sender == null || holder != sender.ClientId || !CanUse(sender, cannon.Muzzle.position)) return;
            Vector3 position = transform.TransformPoint(localPosition);
            if (!float.IsFinite(position.sqrMagnitude) || !cannon.CanLoadFrom(position)) return;
            ball.transform.position = position;
            var player = SessionController.Instance.GetPlayer(sender.ClientId);
            if (player != null && LoadInventoryBall(player.GetComponent<NetworkWeapon>(), index, ball.Ammo))
            { holder = -1; Crate.ResetSupply(); ResetStoredBallObserversRpc(); }
        }
        void FixedUpdate()
        {
            if (!IsServerInitialized || ball == null || ball.Loaded || Time.time < nextSync) return;
            var player = holder >= 0 ? SessionController.Instance.GetPlayer(holder) : null;
            if (holder >= 0 && (player == null || player.Motor.IsDead))
            { holder = -1; ball.Held = false; ball.GetComponent<Collider>().enabled = true; ball.Release(); }
            nextSync = Time.time + .05f;
            SyncBallObserversRpc(shipBody.transform.InverseTransformPoint(ball.transform.position), Quaternion.Inverse(shipBody.rotation) * ball.transform.rotation);
        }
        [ObserversRpc]
        void SyncBallObserversRpc(Vector3 localPosition, Quaternion localRotation)
        {
            if (IsServerInitialized || ball == null || ball.Loaded || ball.Held) return;
            ball.transform.SetParent(shipBody.transform, false);
            ball.transform.localPosition = localPosition;
            ball.transform.localRotation = localRotation;
            ball.Body.isKinematic = true;
            ball.AttachToPlatform(shipBody);
        }
    }
}
