using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkEquipment
    {
        public HandMortarSettings ExplosiveBallSettings;
        public NetworkHandMortarBall ExplosiveBallPrefab;
    }

    public sealed partial class NetworkWeapon
    {
        public bool ThrowExplosiveBall(Vector3 direction, Vector3 eyeOffset)
        {
            var equipment = GetComponent<NetworkEquipment>();
            if (!IsServerInitialized || !CanHandleBall() || equipment == null || equipment.ExplosiveBallSettings == null || equipment.ExplosiveBallPrefab == null) return false;
            if (!GetBottleLaunch(direction, eyeOffset, equipment.ExplosiveBallSettings.LaunchSpeed, out var point, out var speed)) return false;
            if (!ConsumeEquipment(inventory.SelectedSlot, InventoryItem.ExplosiveBall)) return false;
            var ball = Instantiate(equipment.ExplosiveBallPrefab, point, Quaternion.LookRotation(direction));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(ball.gameObject, gameObject.scene);
            ball.Launch(GetComponent<NetworkPlayer>(), speed, equipment.ExplosiveBallSettings);
            ServerManager.Spawn(ball.NetworkObject);
            FishThrowSoundObserversRpc(SoundCue.PufferThrow, point);
            return true;
        }
    }
}
