using FishNet.Object;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkWeapon
    {
        public bool ThrowFish(InventoryItem item, Vector3 direction, bool carriedCatch = false)
        {
            if (!IsServerInitialized || (item != InventoryItem.Pufferfish && item != InventoryItem.Swordfish) || !float.IsFinite(direction.sqrMagnitude) || direction.sqrMagnitude < .5f) return false;
            if (!carriedCatch && !CanHandleBall()) return false;
            int index = (int)item;
            if (DropPrefabs == null || index >= DropPrefabs.Length || DropPrefabs[index] == null || DropPrefabs[index].GetComponent<NetworkFishProjectile>() == null) return false;
            Vector3 origin = transform.position + Vector3.up * 1.5f;
            Vector3 point = origin + direction.normalized * .6f;
            if (FirearmTrace.Cast(gameObject, origin, point, out _)) return false;
            if (!carriedCatch && !ConsumeEquipment(inventory.SelectedSlot, item)) return false;
            var fish = Instantiate(DropPrefabs[index], point, Quaternion.LookRotation(direction));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(fish.gameObject, gameObject.scene);
            fish.GetComponent<NetworkFishProjectile>().Launch(GetComponent<NetworkPlayer>(), direction);
            ServerManager.Spawn(fish.NetworkObject);
            FishThrowSoundObserversRpc(item == InventoryItem.Pufferfish ? SoundCue.PufferThrow : SoundCue.SwordfishThrow, point);
            return true;
        }
        [ObserversRpc(RunLocally = true)]
        void FishThrowSoundObserversRpc(SoundCue cue, Vector3 point) => GameAudio.Play(cue, point);
    }
}
