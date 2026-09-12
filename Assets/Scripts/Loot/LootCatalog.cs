using System;
using PirateSlop.Networking;
using PirateSlop.World;
using UnityEngine;

namespace PirateSlop
{
    [CreateAssetMenu(menuName = "PirateSlop/Loot Catalog")]
    public sealed class LootCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public InventoryItem Item = InventoryItem.Cannon;
            public string Name = "Корабельная пушка";
            [Min(0)] public float Weight = 1;
        }
        public NetworkLootChest ChestPrefab;
        public NetworkFish[] LoosePrefabs;
        public Vector2Int LooseItemsPerIsland = new(2, 4);
        public Vector2Int ChestsPerIsland = new(1, 3);
        public Vector2Int ItemsPerChest = new(1, 2);
        [Min(1)] public int SeaEventsPerType = 3;
        [Min(10)] public float CaptureRadius = 65;
        [Min(1)] public float CaptureSeconds = 45;
        [Min(1)] public int LockpickSteps = 5;
        [Min(1)] public float UntieSeconds = 4;
        [Min(3)] public float SunkenDepth = 8;
        [Min(.1f)] public float LootRiseSpeed = 1.5f;
        public Entry[] Items = { new Entry() };
        public bool Roll(ref MapRandom random, out InventoryItem item)
        {
            item = default;
            float total = 0;
            foreach (var entry in Items)
                if (entry != null && float.IsFinite(entry.Weight) && entry.Weight > 0) total += entry.Weight;
            if (!float.IsFinite(total) || total <= 0) return false;
            float value = random.Value() * total;
            foreach (var entry in Items)
            {
                if (entry == null || !float.IsFinite(entry.Weight) || entry.Weight <= 0) continue;
                item = entry.Item;
                value -= entry.Weight;
                if (value < 0) return true;
            }
            return true;
        }
        public string ItemName(InventoryItem item)
        {
            foreach (var entry in Items) if (entry != null && entry.Item == item) return entry.Name;
            return item.ToString();
        }
        public static int Count(Vector2Int range, ref MapRandom random)
        {
            int min = Mathf.Clamp(range.x, 1, 16), max = Mathf.Clamp(range.y, min, 16);
            return min + (int)(random.Next() % (uint)(max - min + 1));
        }
    }
}
