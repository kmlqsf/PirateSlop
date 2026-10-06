using PirateSlop.Networking;
namespace PirateSlop
{
    public sealed partial class PlayerInventory
    {
        public const int NormalSlotStorage = 8, ExtraPocketSlot = 7, ExtraAmmoSlot = 8;
        NetworkPlayer slotUpgradePlayer;
        bool SlotUpgrade(UpgradeEffect effect)
        {
            if (slotUpgradePlayer == null) slotUpgradePlayer = GetComponent<NetworkPlayer>();
            return slotUpgradePlayer != null && slotUpgradePlayer.HasUpgrade(effect);
        }
        public bool IsNormalSlot(int slot) => slot >= 0 && (slot < 6 || slot == ExtraPocketSlot && SlotUpgrade(UpgradeEffect.ExtraPocket));
        public bool IsAmmoSlot(int slot) => slot == AmmoSlot || slot == ExtraAmmoSlot && SlotUpgrade(UpgradeEffect.ExtraAmmoSlot);
        public bool SlotAvailable(int slot) => IsNormalSlot(slot) || IsAmmoSlot(slot);
        public int ReadyAmmoSlot => BallCount(AmmoSlot) > 0 ? AmmoSlot : BallCount(ExtraAmmoSlot) > 0 ? ExtraAmmoSlot : AmmoSlot;
        public int FindAmmoSlot(InventoryItem item)
        {
            for (int i = 0; i < 2; i++)
            {
                int slot = i == 0 ? AmmoSlot : ExtraAmmoSlot;
                if (IsAmmoSlot(slot) && BallCount(slot) < AmmoCapacity && (BallCount(slot) == 0 || BallItem(slot) == item)) return slot;
            }
            return -1;
        }
    }
}
