using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkEquipment
    {
        readonly SyncVar<int> lanternLitSlots = new();
        HandLanternVisual viewLantern, worldLantern;
        float nextLanternToggle;

        public bool LanternLit(int slot) => slot >= 0 && slot < PlayerInventory.AmmoSlot && (lanternLitSlots.Value & (1 << slot)) != 0;

        public void SetLanternLit(int slot, bool lit)
        {
            if (!IsServerInitialized || slot < 0 || slot >= PlayerInventory.AmmoSlot || inventory.EquipmentAt(slot) != InventoryItem.Lantern) return;
            int bit = 1 << slot;
            lanternLitSlots.Value = lit ? lanternLitSlots.Value | bit : lanternLitSlots.Value & ~bit;
        }

        void ReadLanternInput(Keyboard keyboard)
        {
            if (keyboard == null || !keyboard.eKey.wasPressedThisFrame || inventory.InteractionUsed ||
                (inventory.Fishing != null && inventory.Fishing.PickupFocused) ||
                (GetComponent<PirateSlop.Ships.ShipV3PlayerInteraction>()?.ConsumedInput ?? false)) return;
            ToggleLanternServerRpc(inventory.SelectedSlot);
        }

        [ServerRpc]
        void ToggleLanternServerRpc(int slot)
        {
            if (!CanUse || Item != InventoryItem.Lantern || inventory.SelectedSlot != slot || Time.unscaledTime < nextLanternToggle) return;
            nextLanternToggle = Time.unscaledTime + .2f;
            bool lit = !LanternLit(slot);
            SetLanternLit(slot, lit);
            LanternSoundObserversRpc(lit);
        }

        [ObserversRpc(RunLocally = true)]
        void LanternSoundObserversRpc(bool lit) => GameAudio.Play(lit ? SoundCue.FlameLight : SoundCue.FlameExtinguish, transform.position);

        void PresentLantern()
        {
            if (Item != InventoryItem.Lantern) return;
            bool lit = LanternLit(inventory.SelectedSlot);
            bool first = IsOwner && !motor.IsThirdPerson;
            if (viewLantern != null) viewLantern.SetLit(lit, Active && first);
            if (worldLantern != null) worldLantern.SetLit(lit, Active && !first);
        }
    }
}
