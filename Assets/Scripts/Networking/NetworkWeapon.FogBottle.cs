using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkWeapon
    {
        public bool ThrowFogBottle(Vector3 direction, Vector3 eyeOffset)
        {
            if (!IsServerInitialized || !CanHandleBall() || !float.IsFinite(direction.sqrMagnitude) || direction.sqrMagnitude < .5f) return false;
            int index = (int)InventoryItem.FogBottle;
            if (DropPrefabs == null || index >= DropPrefabs.Length || DropPrefabs[index] == null || (DropPrefabs[index].GetComponent<NetworkFogBottle>() == null || DropPrefabs[index].GetComponent<NetworkFogBottle>().CloudPrefab == null)) return false;
            var motor = GetComponent<AdvancedPlayerController>();
            Vector3 eye = transform.position + Vector3.up * (motor.IsCrouched ? .8f : 1.5f);
            if (float.IsFinite(eyeOffset.sqrMagnitude) && eyeOffset.sqrMagnitude < 4f && !FirearmTrace.Cast(gameObject, eye, transform.position + eyeOffset, out _)) eye = transform.position + eyeOffset;
            direction.Normalize();
            Vector3 point = eye + direction * .55f;
            if (FirearmTrace.Cast(gameObject, eye, point, out _)) return false;
            if (!ConsumeEquipment(inventory.SelectedSlot, InventoryItem.FogBottle)) return false;
            var bottle = Instantiate(DropPrefabs[index], point, Quaternion.identity);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(bottle.gameObject, gameObject.scene);
            Vector3 speed = direction * NetworkFogBottle.ThrowSpeed;
            var ship = GetComponent<ShipDeckPassenger>()?.Ship;
            if (ship != null) speed += ship.GetComponent<ShipController>().CannonPointVelocity(point);
            bottle.GetComponent<NetworkFogBottle>().Launch(GetComponent<NetworkPlayer>(), speed);
            ServerManager.Spawn(bottle.NetworkObject);
            return true;
        }
    }
}

