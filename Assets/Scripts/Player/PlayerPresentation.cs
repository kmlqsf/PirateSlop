using UnityEngine;
using PirateSlop.Networking;

namespace PirateSlop
{
    [DefaultExecutionOrder(500)]
    public sealed class PlayerPresentation : MonoBehaviour
    {
        static int synchronizedFrame = -1;
        PlayerInventory inventory;
        CannonHands hands;
        NetworkWeapon weapon;

        void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
            hands = GetComponent<CannonHands>();
            weapon = GetComponent<NetworkWeapon>();
        }

        void LateUpdate()
        {
            if (synchronizedFrame != Time.frameCount)
            {
                Physics.SyncTransforms();
                synchronizedFrame = Time.frameCount;
            }
            if (inventory != null && inventory.isActiveAndEnabled) inventory.PresentPlacement();
            if (hands != null && hands.isActiveAndEnabled) hands.PresentHands();
            if (weapon != null && weapon.isActiveAndEnabled) weapon.PresentGrapple();
        }
    }
}
