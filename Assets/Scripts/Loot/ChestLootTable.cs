using System;
using System.Collections.Generic;
using System.IO;
using PirateSlop.Networking;
using PirateSlop.World;
using UnityEngine;

namespace PirateSlop
{
    public struct ChestLootStack
    {
        public InventoryItem Item;
        public int Count;
    }

    public static class ChestLootTable
    {
        [Serializable]
        sealed class Table
        {
            public int schemaVersion;
            public ChestSettings chest;
            public Entry[] entries;
        }

        [Serializable]
        sealed class ChestSettings
        {
            public int minItemTypes;
            public int maxItemTypes;
        }

        [Serializable]
        sealed class Entry
        {
            public string item;
            public string name;
            public float chancePercent;
            public int minCount;
            public int maxCount;
            [NonSerialized] public InventoryItem Item;
        }

        static Table current;
        static string loadedJson, rejectedJson, lastWarning;
        public static string Path => System.IO.Path.Combine(Application.streamingAssetsPath, "Loot", "ChestLoot.json");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            current = null;
            loadedJson = rejectedJson = lastWarning = null;
        }

        public static ChestLootStack[] RollContents(ref MapRandom random)
        {
            var table = Load();
            int count = table.chest.minItemTypes + (int)(random.Next() % (uint)(table.chest.maxItemTypes - table.chest.minItemTypes + 1));
            var candidates = new List<Entry>();
            foreach (var entry in table.entries)
                if (entry.chancePercent > 0) candidates.Add(entry);
            var result = new ChestLootStack[count];
            for (int slot = 0; slot < count; slot++)
            {
                double total = 0;
                foreach (var entry in candidates) total += entry.chancePercent;
                double value = random.Value() * total;
                int chosenIndex = candidates.Count - 1;
                for (int i = 0; i < candidates.Count; i++)
                {
                    value -= candidates[i].chancePercent;
                    if (value < 0) { chosenIndex = i; break; }
                }
                var chosen = candidates[chosenIndex];
                int amount = chosen.minCount + (int)(random.Next() % (uint)(chosen.maxCount - chosen.minCount + 1));
                result[slot] = new ChestLootStack { Item = chosen.Item, Count = amount };
                candidates.RemoveAt(chosenIndex);
            }
            return result;
        }

        public static bool TryRollItem(System.Random random, Predicate<InventoryItem> eligible, out InventoryItem item)
        {
            item = InventoryItem.None;
            var table = Load();
            double total = 0;
            foreach (var entry in table.entries)
                if (entry.chancePercent > 0 && eligible(entry.Item)) total += entry.chancePercent;
            if (total <= 0) return false;
            double value = random.NextDouble() * total;
            foreach (var entry in table.entries)
            {
                if (entry.chancePercent <= 0 || !eligible(entry.Item)) continue;
                item = entry.Item;
                value -= entry.chancePercent;
                if (value < 0) return true;
            }
            return item != InventoryItem.None;
        }

        static Table Load()
        {
            string json = null;
            try
            {
                json = File.ReadAllText(Path);
                if (current != null && json == loadedJson) return current;
                if (current != null && json == rejectedJson) return current;
                if (json.Length > 1000000) throw new InvalidDataException("Chest loot table is too large.");
                var table = JsonUtility.FromJson<Table>(json);
                if (table == null || table.schemaVersion != 1 || table.entries == null || table.entries.Length == 0)
                    throw new InvalidDataException("Expected schemaVersion 1 and a non-empty entries array.");
                if (table.chest == null || table.chest.minItemTypes < 1 || table.chest.maxItemTypes < table.chest.minItemTypes || table.chest.maxItemTypes > 10)
                    throw new InvalidDataException("chest must specify 1 <= minItemTypes <= maxItemTypes <= 10.");
                var ids = new HashSet<InventoryItem>();
                int enabled = 0;
                foreach (var entry in table.entries)
                {
                    if (entry == null || !Enum.TryParse(entry.item, false, out InventoryItem item) ||
                        !Enum.IsDefined(typeof(InventoryItem), item) || entry.item != item.ToString() ||
                        item == InventoryItem.None || item == InventoryItem.Plank || !ids.Add(item))
                        throw new InvalidDataException("Unknown, unsupported or duplicate chest item: " + entry?.item);
                    if (!float.IsFinite(entry.chancePercent) || entry.chancePercent < 0 || entry.chancePercent > 100 ||
                        entry.minCount < 1 || entry.maxCount < entry.minCount || entry.maxCount > 1000)
                        throw new InvalidDataException("Invalid chance or stack range for " + entry.item);
                    entry.Item = item;
                    if (entry.chancePercent > 0) enabled++;
                }
                if (enabled < table.chest.maxItemTypes)
                    throw new InvalidDataException("At least chest.maxItemTypes distinct items must have a positive chance to fill every chest slot.");
                current = table;
                loadedJson = json;
                rejectedJson = lastWarning = null;
                return current;
            }
            catch (Exception exception) when (exception is IOException || exception is InvalidDataException || exception is UnauthorizedAccessException || exception is ArgumentException)
            {
                if (current == null) throw new InvalidOperationException("Cannot load chest loot table at " + Path, exception);
                string failure = exception.Message;
                if (lastWarning != failure)
                {
                    Debug.LogWarning("Chest loot table rejected; keeping the last valid table. " + failure);
                    lastWarning = failure;
                }
                rejectedJson = json;
                return current;
            }
        }
    }
}
