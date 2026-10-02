using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed class NetworkFogCloud : NetworkBehaviour
    {
        readonly SyncVar<float> age = new();
        FogCloudVisual visual;
        float started, receivedAge = -1f, receivedAt;
        void Awake() => visual = GetComponent<FogCloudVisual>();
        public void Initialize() => started = Time.time;
        void Update()
        {
            if (!IsSpawned) return;
            float elapsed;
            if (IsServerInitialized)
            {
                elapsed = Time.time - started;
                if (elapsed >= FogCloudVisual.Duration)
                {
                    ServerManager.Despawn(NetworkObject);
                    return;
                }
                age.Value = Mathf.Floor(elapsed * 5f) / 5f;
            }
            else
            {
                if (receivedAge != age.Value)
                {
                    receivedAge = age.Value;
                    receivedAt = Time.time;
                }
                elapsed = receivedAge + Time.time - receivedAt;
            }
            if (visual != null) visual.SetAge(elapsed);
        }
        public override void OnStopNetwork()
        {
            if (visual != null) visual.SetAge(FogCloudVisual.Duration);
            base.OnStopNetwork();
        }
    }
}
