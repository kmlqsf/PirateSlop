using FishNet.Object;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed class NetworkCannon : NetworkBehaviour
    {
        SimpleCannon cannon;
        Cannonball ball;
        Rigidbody shipBody;
        void Awake() { cannon = GetComponentInChildren<SimpleCannon>(true); ball = GetComponentInChildren<Cannonball>(true); shipBody = GetComponent<Rigidbody>(); if (cannon != null) cannon.Network = this; }
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
            ball.transform.position = shipBody.transform.TransformPoint(localPosition);
            cannon.TryLoad(ball);
            SyncLoadedObserversRpc(localPosition);
        }
        [ObserversRpc(BufferLast = true)] void SyncLoadedObserversRpc(Vector3 localPosition)
        {
            if (IsServerInitialized || ball == null || cannon == null || cannon.IsLoaded) return;
            ball.transform.position = shipBody.transform.TransformPoint(localPosition);
            cannon.TryLoad(ball);
        }
        float nextSync;
        void FixedUpdate()
        {
            if (!IsServerInitialized || ball == null || ball.Loaded || Time.time < nextSync) return;
            nextSync = Time.time + .05f;
            SyncBallObserversRpc(shipBody.transform.InverseTransformPoint(ball.transform.position), shipBody.transform.InverseTransformDirection(ball.Body.linearVelocity));
        }
        [ObserversRpc] void SyncBallObserversRpc(Vector3 localPosition, Vector3 localVelocity)
        {
            if (IsServerInitialized || ball == null || ball.Loaded) return;
            ball.transform.position = shipBody.transform.TransformPoint(localPosition);
            ball.Body.isKinematic = true;
        }
    }
}
