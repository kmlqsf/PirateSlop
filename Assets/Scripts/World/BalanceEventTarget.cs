using PirateSlop.Networking;
using UnityEngine;

namespace PirateSlop.World
{
    public sealed class BalanceEventTarget : MonoBehaviour
    {
        public NetworkBalanceEvent Owner;
        public int StoneIndex = -1;

        public static BalanceEventTarget FromCollider(Collider collider)
        {
            var target = collider.GetComponentInParent<BalanceEventTarget>();
            if (target != null) return target;
            var balance = collider.GetComponentInParent<NetworkBalanceEvent>();
            return balance != null && balance.WeightSocket != null && collider.transform.IsChildOf(balance.WeightSocket.parent) ? balance.DepositTarget : null;
        }
    }
}
