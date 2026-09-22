using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed class BotPersonalWeapons
    {
        readonly PlayerInventory inventory;
        readonly PirateWeapon pistol;
        readonly NetworkWeapon network;
        readonly NetworkEquipment equipment;
        readonly FirearmHandling handling;
        public BotPersonalWeapons(NetworkPlayer player)
        {
            inventory = player.GetComponent<PlayerInventory>(); pistol = player.GetComponent<PirateWeapon>();
            network = player.GetComponent<NetworkWeapon>(); equipment = player.GetComponent<NetworkEquipment>();
            handling = player.GetComponent<FirearmHandling>();
        }
        public FirearmDefinition Definition(int slot) => inventory.PistolAt(slot) ? handling.Pistol :
            inventory.EquipmentAt(slot) == InventoryItem.Musket ? handling.Musket :
            inventory.EquipmentAt(slot) == InventoryItem.DoubleBarrel ? handling.Shotgun : null;
        public FirearmDefinition Selected => Definition(inventory.SelectedSlot);
        public string Name => inventory.SabreSelected ? "сабля" : inventory.PistolSelected ? "пистолет" :
            inventory.EquipmentAt(inventory.SelectedSlot) == InventoryItem.Musket ? "мушкет" : "двустволка";
        public bool Loaded => inventory.PistolSelected ? pistol.Loaded : Selected != null && equipment.ServerRounds(inventory.SelectedSlot) >= Selected.Capacity;
        public bool Reloading => inventory.PistolSelected ? pistol.Reloading : equipment.IsReloading;
        public int RangedSlot(float distance)
        {
            if (Reloading && Selected != null) return inventory.SelectedSlot;
            int best = -1; float score = float.NegativeInfinity;
            for (int i = 0; i < 6; i++)
            {
                var definition = Definition(i);
                if (definition == null) continue;
                var settings = definition.Ballistics;
                float falloff = Mathf.InverseLerp(settings.FalloffStart, settings.FalloffEnd, distance);
                float damage = Mathf.Min(definition.DamageCap, Mathf.Lerp(settings.NearDamage, settings.FarDamage, falloff) * definition.Pellets);
                float spreadRadius = Mathf.Tan(definition.AimSpread * Mathf.Deg2Rad) * distance;
                float value = damage / Mathf.Max(1f, spreadRadius * spreadRadius / .16f);
                if (distance > settings.Range) value -= 1000f;
                if (i == inventory.SelectedSlot) value += 8f;
                if (value <= score) continue;
                score = value; best = i;
            }
            return best;
        }
        public bool Act(bool reload, bool melee, Vector3 direction, Vector3 eye)
        {
            if (melee || inventory.PistolSelected) return network.TryAct(melee ? (byte)2 : reload ? (byte)1 : (byte)0, direction, eye, true);
            return equipment.TryFirearm(reload ? (byte)1 : (byte)0, direction, eye, true);
        }
    }
}
