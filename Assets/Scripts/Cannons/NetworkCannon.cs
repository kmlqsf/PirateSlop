using FishNet.Object;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed class NetworkCannon : NetworkBehaviour
    {
        SimpleCannon cannon;
        Cannonball ball;
        Rigidbody shipBody;
        void Awake() { cannon = GetComponentInChildren<SimpleCannon>(true); ball = GetComponentInChildren<Cannonball>(true); shipBody = GetComponentInParent<Rigidbody>(); if (cannon != null) { cannon.Network = this; cannon.InitializeSupply(ball); } if (ball != null) ball.Network = this; }
        public void NotifyFired(Vector3 position, Vector3 velocity)
        {
            holder = -1;
            SyncLoadedObserversRpc(false, shipBody.transform.InverseTransformPoint(ball.transform.position));
            ShotObserversRpc(position, velocity);
        }
        [ObserversRpc] void ShotObserversRpc(Vector3 position, Vector3 velocity)
        { if (!IsServerInitialized && cannon != null) cannon.SpawnShot(position, velocity, false); }
        int holder = -1;
        public void RequestBall(bool holding, Vector3 position) => BallServerRpc(holding, transform.InverseTransformPoint(position));
        [ServerRpc(RequireOwnership = false)]
        void BallServerRpc(bool holding, Vector3 position, FishNet.Connection.NetworkConnection sender = null)
        {
            if (ball == null || sender == null) return;
            if (ball.Loaded || (holder != -1 && holder != sender.ClientId)) { RejectHoldTargetRpc(sender); return; }
            var player = SessionController.Instance.GetPlayer(sender.ClientId);
            Vector3 world = transform.TransformPoint(position);
            if (player == null || !float.IsFinite(world.x) || !float.IsFinite(world.y) || !float.IsFinite(world.z) || Vector3.Distance(player.transform.position, world) > 6f) { RejectHoldTargetRpc(sender); return; }
            holder = holding ? sender.ClientId : -1;
            ball.Held = holding; ball.AttachToPlatform(null); ball.transform.SetParent(null, true);
            ball.Body.isKinematic = true; ball.Body.position = world;
            ball.GetComponent<Collider>().enabled = !holding;
            if (!holding) { ball.Release(); ball.Body.linearVelocity = shipBody.GetPointVelocity(world); ball.Body.angularVelocity = Vector3.zero; }
        }
        [TargetRpc] void RejectHoldTargetRpc(FishNet.Connection.NetworkConnection connection)
        { if (ball != null) { ball.Held = false; ball.GetComponent<Collider>().enabled = true; } }
        public void RequestFire() { if (IsServerInitialized) cannon.Fire(); else FireServerRpc(); }
        [ServerRpc(RequireOwnership = false)] void FireServerRpc() { cannon.Fire(); }
        public void RequestLoad(Vector3 localPosition)
        {
            if (IsServerInitialized) Load(localPosition); else LoadServerRpc(localPosition);
        }
        [ServerRpc(RequireOwnership = false)] void LoadServerRpc(Vector3 localPosition) => Load(localPosition);
        void Load(Vector3 localPosition)
        {
            if (ball == null || cannon == null || cannon.IsLoaded) return;
            holder = -1; ball.Held = false; ball.GetComponent<Collider>().enabled = true;
            ball.transform.position = shipBody.transform.TransformPoint(localPosition);
            cannon.TryLoad(ball);
            if (cannon.IsLoaded) SyncLoadedObserversRpc(true, localPosition);
        }
        [ObserversRpc(BufferLast = true)] void SyncLoadedObserversRpc(bool isLoaded, Vector3 localPosition)
        {
            if (IsServerInitialized || ball == null || cannon == null) return;
            if (!isLoaded) { cannon.ResetSupply(); return; }
            if (cannon.IsLoaded) return;
            ball.transform.position = shipBody.transform.TransformPoint(localPosition);
            cannon.TryLoad(ball);
        }
        float nextSync;
        void FixedUpdate()
        {
            if (!IsServerInitialized || ball == null || ball.Loaded || Time.time < nextSync) return;
            if (holder != -1 && SessionController.Instance.GetPlayer(holder) == null)
            { holder = -1; ball.Held = false; ball.GetComponent<Collider>().enabled = true; ball.Release(); }
            nextSync = Time.time + .05f;
            SyncBallObserversRpc(shipBody.transform.InverseTransformPoint(ball.transform.position), shipBody.transform.InverseTransformDirection(ball.Body.linearVelocity));
        }
        [ObserversRpc] void SyncBallObserversRpc(Vector3 localPosition, Vector3 localVelocity)
        {
            if (IsServerInitialized || ball == null || ball.Loaded || ball.Held) return;
            ball.AttachToPlatform(null);
            ball.transform.position = shipBody.transform.TransformPoint(localPosition);
            ball.Body.isKinematic = true;
        }
    }
}
