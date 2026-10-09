using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkWeapon
    {
        public bool ThrowVortexBottle(Vector3 direction, Vector3 eyeOffset)
        {
            if (!IsServerInitialized || !CanHandleBall() || !float.IsFinite(direction.sqrMagnitude) || direction.sqrMagnitude < .5f) return false;
            int index = (int)InventoryItem.VortexBottle;
            if (DropPrefabs == null || index >= DropPrefabs.Length || DropPrefabs[index] == null || DropPrefabs[index].GetComponent<NetworkVortexBottle>() == null) return false;
            if (!GetBottleLaunch(direction, eyeOffset, NetworkVortexBottle.ThrowSpeed, out var point, out var speed)) return false;
            if (!ConsumeEquipment(inventory.SelectedSlot, InventoryItem.VortexBottle)) return false;
            var bottle = Instantiate(DropPrefabs[index], point, Quaternion.identity);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(bottle.gameObject, gameObject.scene);
            bottle.GetComponent<NetworkVortexBottle>().Launch(GetComponent<NetworkPlayer>(), speed);
            ServerManager.Spawn(bottle.NetworkObject);
            return true;
        }
    }
}
