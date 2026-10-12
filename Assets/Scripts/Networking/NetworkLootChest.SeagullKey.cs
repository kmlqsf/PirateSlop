using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkLootChest
    {
        readonly SyncVar<NetworkObject> seagullSupport = new();
        AudioSource seagullLockSound;
        float nextLockRattle;
        public bool InSeagullWreck => seagullSupport.Value != null;
        public NetworkSeagullEvent SeagullWreck => seagullSupport.Value != null ? seagullSupport.Value.GetComponent<NetworkSeagullEvent>() : null;
        public bool SeagullChestAvailable => SeagullWreck != null && SeagullWreck.ChestAvailable;
        public void PlaceInSeagullWreck(NetworkSeagullEvent owner)
        {
            kind.Value = SeaLootKind.SeagullKey; phase.Value = SeaLootState.Ready;
            seagullSupport.Value = owner.NetworkObject; eventPoint.Value = owner.transform.position;
        }
        public bool TryUnlockSeagullKey(NetworkWeapon actor, out bool justUnlocked)
        {
            justUnlocked = false;
            if (!InSeagullWreck) return true;
            var owner = SeagullWreck;
            if (owner == null) return false;
            if (owner.Unlocked) return true;
            if (owner.Unlock(actor)) { justUnlocked = true; SeagullUnlockRpc(); return true; }
            if (Time.time >= nextLockRattle) { nextLockRattle = Time.time + .6f; SeagullRattleRpc(); }
            return false;
        }
        [ObserversRpc(RunLocally = true)]
        void SeagullRattleRpc()
        {
            SeagullWreck?.RattleLock();
            if (IsClientInitialized) GameAudio.Attached(ref seagullLockSound, SoundCue.LockpickJam, transform);
        }
        [ObserversRpc(RunLocally = true)]
        void SeagullUnlockRpc()
        {
            if (IsClientInitialized) GameAudio.Attached(ref seagullLockSound, SoundCue.LockpickSuccess, transform);
        }
        bool UpdateSeagullChest()
        {
            if (!InSeagullWreck) return false;
            var owner = SeagullWreck;
            if (owner == null) return true;
            if (owner.ChestSocket != null) transform.SetPositionAndRotation(owner.ChestSocket.position, owner.ChestSocket.rotation);
            return true;
        }
        string SeagullChestHint(PlayerInventory inventory)
        {
            var owner = SeagullWreck;
            if (SeagullChestAvailable) return "E — открыть · F — нести сундук";
            if (owner != null && owner.Unlocked) return "Замок расстёгивается";
            if (owner != null && owner.KeyHolder == inventory.GetComponent<NetworkObject>()) return "E — отпереть сундук ключом";
            return "E — открыть · Сундук заперт: ключ у одной из чаек";
        }
    }
}
