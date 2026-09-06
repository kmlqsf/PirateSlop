using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using UnityEngine;

namespace PirateSlop.Networking
{
    public struct CannonPlacement
    {
        public Vector3 Position;
        public float Yaw;
    }

    public sealed class NetworkCannon : NetworkBehaviour
    {
        readonly SyncVar<bool> kitTaken = new(false);
        readonly SyncList<CannonPlacement> placements = new();
        readonly SyncVar<int> loadedIndex = new(-1);
        public CannonballCrate Crate { get; private set; }
        Cannonball ball;
        Rigidbody shipBody;
        int holder = -1, appliedLoaded = -1;
        float nextSync;

        void Awake()
        {
            Crate = GetComponentInChildren<CannonballCrate>(true);
            shipBody = GetComponent<Rigidbody>();
            if (Crate != null) { ball = Crate.Supply; ball.Network = this; }
        }
        public override void OnStartServer() { base.OnStartServer(); if (Crate != null) Crate.ResetSupply(); }
        public override void OnStartClient() { base.OnStartClient(); ApplyState(); }
        public bool TakeKit()
        {
            if (!IsServerInitialized || Crate == null || kitTaken.Value || !Crate.KitAvailable) return false;
            kitTaken.Value = true; Crate.Kit.SetActive(false); return true;
        }
        public void Place(Vector3 position, float yaw)
        {
            if (!IsServerInitialized) return;
            placements.Add(new CannonPlacement { Position = position, Yaw = yaw });
            ApplyState();
        }
        void ApplyState()
        {
            if (Crate == null) return;
            Crate.Kit.SetActive(!kitTaken.Value);
            while (Crate.Cannons.Count < placements.Count)
            {
                var placement = placements[Crate.Cannons.Count];
                Crate.AddCannon(placement.Position, placement.Yaw);
            }
            if (IsServerInitialized || appliedLoaded == loadedIndex.Value) return;
            if (appliedLoaded >= 0 && appliedLoaded < Crate.Cannons.Count) Crate.Cannons[appliedLoaded].ResetSupply();
            appliedLoaded = loadedIndex.Value;
            if (appliedLoaded >= 0 && appliedLoaded < Crate.Cannons.Count)
            {
                var cannon = Crate.Cannons[appliedLoaded];
                ball.transform.position = cannon.Muzzle.position;
                cannon.TryLoad(ball);
            }
        }
        void Update() { if (IsClientInitialized || IsServerInitialized) ApplyState(); }
        SimpleCannon Cannon(int index) => Crate != null && index >= 0 && index < Crate.Cannons.Count ? Crate.Cannons[index] : null;
        bool CanUse(NetworkConnection sender, Vector3 point)
        {
            var player = sender != null ? SessionController.Instance.GetPlayer(sender.ClientId) : null;
            return player != null && !player.Motor.IsDead && !player.Motor.LocomotionLocked && float.IsFinite(point.sqrMagnitude) && Vector3.Distance(player.transform.position, point) <= 6f;
        }
        public void NotifyFired(int index, Vector3 position, Vector3 velocity)
        {
            holder = -1; loadedIndex.Value = -1;
            ShotObserversRpc(index, position, velocity);
        }
        [ObserversRpc]
        void ShotObserversRpc(int index, Vector3 position, Vector3 velocity)
        {
            if (IsServerInitialized) return;
            ApplyState();
            var cannon = Cannon(index);
            if (cannon != null) { cannon.SpawnShot(position, velocity, false); cannon.ResetSupply(); appliedLoaded = -1; }
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
            if (!holding) { ball.Release(); ball.Body.linearVelocity = shipBody.GetPointVelocity(world); ball.Body.angularVelocity = Vector3.zero; }
        }
        [TargetRpc]
        void RejectHoldTargetRpc(NetworkConnection connection)
        { if (ball != null) { ball.Held = false; ball.GetComponent<Collider>().enabled = !ball.Loaded; } }
        public void RequestFire(int index) => FireServerRpc(index);
        [ServerRpc(RequireOwnership = false)]
        void FireServerRpc(int index, NetworkConnection sender = null)
        {
            var cannon = Cannon(index);
            if (cannon != null && CanUse(sender, cannon.transform.position)) cannon.Fire();
        }
        public void RequestLoad(int index, Vector3 localPosition) => LoadServerRpc(index, localPosition);
        [ServerRpc(RequireOwnership = false)]
        void LoadServerRpc(int index, Vector3 localPosition, NetworkConnection sender = null)
        {
            var cannon = Cannon(index);
            if (ball == null || cannon == null || cannon.IsLoaded || ball.Loaded || sender == null || holder != sender.ClientId || !CanUse(sender, cannon.Muzzle.position)) return;
            Vector3 position = transform.TransformPoint(localPosition);
            if (!float.IsFinite(position.sqrMagnitude) || Vector3.Distance(position, cannon.Muzzle.position) > .4f) return;
            ball.transform.position = position;
            if (cannon.TryLoad(ball)) { holder = -1; loadedIndex.Value = index; }
        }
        void FixedUpdate()
        {
            if (!IsServerInitialized || ball == null || ball.Loaded || Time.time < nextSync) return;
            var player = holder >= 0 ? SessionController.Instance.GetPlayer(holder) : null;
            if (holder >= 0 && (player == null || player.Motor.IsDead))
            { holder = -1; ball.Held = false; ball.GetComponent<Collider>().enabled = true; ball.Release(); }
            nextSync = Time.time + .05f;
            SyncBallObserversRpc(shipBody.transform.InverseTransformPoint(ball.transform.position));
        }
        [ObserversRpc]
        void SyncBallObserversRpc(Vector3 localPosition)
        {
            if (IsServerInitialized || ball == null || ball.Loaded || ball.Held) return;
            ball.transform.SetParent(shipBody.transform, false);
            ball.transform.localPosition = localPosition;
            ball.Body.isKinematic = true;
            ball.AttachToPlatform(shipBody);
        }
    }
}
