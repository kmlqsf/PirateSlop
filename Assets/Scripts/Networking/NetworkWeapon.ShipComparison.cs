using FishNet.Object;
using PirateSlop.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkWeapon
    {
        float nextComparisonVisit;
        void UpdateShipComparison()
        {
            if (!IsOwner || SessionController.Instance == null || !SessionController.Instance.Config.ShipComparisonEnabled) return;
            var keyboard = Keyboard.current;
            if (keyboard == null || !GetComponent<AdvancedPlayerController>().InputActive) return;
            if (keyboard.f5Key.wasPressedThisFrame) VisitComparisonServerRpc(-1);
            else if (keyboard.f2Key.wasPressedThisFrame) VisitComparisonServerRpc(0);
            else if (keyboard.f3Key.wasPressedThisFrame) VisitComparisonServerRpc(1);
            else if (keyboard.f4Key.wasPressedThisFrame) VisitComparisonServerRpc(2);
        }

        [ServerRpc]
        void VisitComparisonServerRpc(int index)
        {
            var session = SessionController.Instance;
            var player = GetComponent<NetworkPlayer>();
            if (session == null || !session.Config.ShipComparisonEnabled || Time.time < nextComparisonVisit || player.Motor.IsDead || player.Motor.LocomotionLocked || LootHandsBusy || GetComponent<CannonHands>().HasHeldBall) return;
            Vector3 position;
            float yaw;
            if (index == -1)
            {
                if (player.Ship == null || player.Ship.IsSinking) return;
                position = player.Ship.transform.TransformPoint(session.Config.PlayerLocalSpawn);
                yaw = player.Ship.transform.eulerAngles.y;
            }
            else
            {
                var ship = ShipComparison.Find(index);
                if (ship == null) return;
                position = ship.transform.TransformPoint(ship.DeckSpawn);
                yaw = ship.transform.eulerAngles.y;
            }
            nextComparisonVisit = Time.time + .5f;
            player.Teleport(new PlayerState { Position = position, Yaw = yaw });
        }
    }
}
