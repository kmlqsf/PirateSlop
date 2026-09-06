using FishNet.Object;
using FishNet.Object.Synchronizing;
namespace PirateSlop.Networking
{
    public sealed class NetworkHealth : NetworkBehaviour
    {
        readonly SyncVar<float> health = new(-1f);
        CombatHealth target;
        void Awake() => target = GetComponent<CombatHealth>();
        public override void OnStartServer() { base.OnStartServer(); Publish(target.Current); }
        public void Publish(float value) { if (IsServerInitialized) health.Value = value; }
        void Update() { if (IsClientInitialized && !IsServerInitialized && health.Value >= 0) target.ApplySnapshot(health.Value); }
    }
}
