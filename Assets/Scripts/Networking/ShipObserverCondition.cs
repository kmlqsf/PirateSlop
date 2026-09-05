using FishNet.Connection;
using FishNet.Object;
using FishNet.Observing;
using UnityEngine;
namespace PirateSlop.Networking
{
    [CreateAssetMenu(menuName = "PirateSlop/Ship Observer")]
    public sealed class ShipObserverCondition : ObserverCondition
    {
        public override ObserverConditionType GetConditionType() => ObserverConditionType.Timed;
        public override bool ConditionMet(NetworkConnection connection, bool currentlyAdded, out bool notProcessed)
        {
            notProcessed = false;
            var session = SessionController.Instance;
            if (session == null) return false;
            var viewer = session.GetPlayer(connection.ClientId);
            if (viewer == null) return NetworkObject.Owner == connection;
            var targetPlayer = NetworkObject.GetComponent<NetworkPlayer>();
            var target = targetPlayer != null ? targetPlayer.Ship : NetworkObject.GetComponent<NetworkShip>();
            if (target == null) return false;
            if (target.Owner == connection || viewer.Passenger.Ship == target.Body) return true;
            float radius = session.Config.ObserverRadius * (currentlyAdded ? 1 + session.Config.ObserverHysteresis : 1);
            return (viewer.transform.position - target.transform.position).sqrMagnitude <= radius * radius;
        }
    }
}
