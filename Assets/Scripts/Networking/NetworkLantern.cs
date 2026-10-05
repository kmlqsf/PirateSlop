using FishNet.Object;
using FishNet.Object.Synchronizing;

namespace PirateSlop.Networking
{
    [UnityEngine.DefaultExecutionOrder(90)]
    public sealed class NetworkLantern : NetworkBehaviour
    {
        readonly SyncVar<bool> lit = new();
        HandLanternVisual visual;
        public bool Lit => lit.Value;

        public void SetLit(bool value)
        {
            if (IsSpawned && !IsServerInitialized) return;
            lit.Value = value;
        }

        void LateUpdate()
        {
            if (visual == null) visual = GetComponentInChildren<HandLanternVisual>(true);
            if (visual != null) visual.SetLit(lit.Value, IsSpawned);
        }
    }
}
