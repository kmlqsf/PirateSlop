using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkShip
    {
        readonly SyncVar<bool> isAnchored = new(false);
        readonly SyncVar<float> anchorProgress = new(1f);
        readonly SyncVar<Vector3> anchorSeabedPoint = new();

        public bool AnchorDropped => isAnchored.Value;
        public float AnchorRaiseProgress => anchorProgress.Value;
        public Vector3 AnchorSeabedPoint => anchorSeabedPoint.Value;

        CapstanStation capstanStation;
        public CapstanStation Capstan => capstanStation != null ? capstanStation : (capstanStation = GetComponentInChildren<CapstanStation>());

        [ServerRpc(RequireOwnership = false)]
        public void DropAnchorServerRpc(FishNet.Connection.NetworkConnection sender = null)
        {
            if (IsSinking || isAnchored.Value || (Capstan != null && !Capstan.StructurallyAvailable)) return;
            var dropPt = transform.position + transform.forward * 5f;
            isAnchored.Value = true;
            anchorProgress.Value = 0f;
            anchorSeabedPoint.Value = dropPt;
            if (Motor != null) Motor.SetAnchored(true, dropPt);
            DropAnchorObserversRpc(dropPt);
        }

        [ObserversRpc(RunLocally = true)]
        void DropAnchorObserversRpc(Vector3 dropPt)
        {
            if (Motor != null) Motor.SetAnchored(true, dropPt);
            var capstan = GetComponentInChildren<CapstanStation>();
            if (capstan != null) capstan.OnAnchorDropped();
        }

        [ServerRpc(RequireOwnership = false)]
        public void RaiseAnchorDeltaServerRpc(float deltaDegrees, FishNet.Connection.NetworkConnection sender = null)
        {
            if (IsSinking || !float.IsFinite(deltaDegrees) || deltaDegrees <= 0f || (Capstan != null && !Capstan.StructurallyAvailable)) return;
            if (!isAnchored.Value && anchorProgress.Value >= 1f) return;

            float deltaProgress = deltaDegrees / (360f * 3f);
            float newProgress = Mathf.Clamp01(anchorProgress.Value + deltaProgress);
            anchorProgress.Value = newProgress;

            if (newProgress >= 1f && isAnchored.Value)
            {
                isAnchored.Value = false;
                if (Motor != null) Motor.SetAnchored(false);
                AnchorRaisedObserversRpc();
            }
        }

        [ObserversRpc(RunLocally = true)]
        void AnchorRaisedObserversRpc()
        {
            if (Motor != null) Motor.SetAnchored(false);
            var capstan = GetComponentInChildren<CapstanStation>();
            if (capstan != null) capstan.OnAnchorFullyRaised();
        }
    }
}
