using FishNet.Object;
using FishNet.Object.Synchronizing;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkLootChest
    {
        readonly SyncVar<NetworkObject> clamSupport = new();
        readonly SyncVar<bool> inClam = new();
        UnityEngine.GameObject clamPadlock;
        public NetworkClamEvent Clam => clamSupport.Value != null ? clamSupport.Value.GetComponent<NetworkClamEvent>() : null;
        public bool InClam => inClam.Value;
        public void PlaceInClam(NetworkClamEvent clam)
        {
            kind.Value = SeaLootKind.Clam; phase.Value = SeaLootState.Locked;
            clamSupport.Value = clam.NetworkObject; inClam.Value = true;
            eventPoint.Value = clam.transform.position;
        }
        bool UpdateClamChest()
        {
            if (!InClam)
            {
                if (clamPadlock != null) clamPadlock.SetActive(false);
                return false;
            }
            var clam = Clam;
            if (IsServerInitialized && clam != null && clam.ChestAvailable && phase.Value == SeaLootState.Locked) phase.Value = SeaLootState.Ready;
            if (clam != null && clam.ChestSocket != null) transform.SetPositionAndRotation(clam.ChestSocket.position, clam.ChestSocket.rotation);
            if (IsClientInitialized && !UnityEngine.Application.isBatchMode && clam != null)
            {
                if (clamPadlock == null && !clam.ChestAvailable && clam.PadlockPrefab != null)
                {
                    clamPadlock = UnityEngine.Object.Instantiate(clam.PadlockPrefab, transform);
                    clamPadlock.transform.localPosition = new UnityEngine.Vector3(0, .38f, .32f);
                    clamPadlock.transform.localRotation = UnityEngine.Quaternion.identity;
                }
                if (clamPadlock != null) clamPadlock.SetActive(!clam.ChestAvailable);
            }
            return true;
        }
        string ClamHint => Clam == null ? "Раковина с сундуком" : Clam.ChestAvailable ? "Ctrl / C — подлезть · E — открыть · F — нести сундук"
            : $"Сундук под замком · подъёмы {Clam.SuccessfulPairs}/{Clam.RequiredPairs} · нажмите оба рычага за {Clam.PairWindow:F1} с";
    }
}
