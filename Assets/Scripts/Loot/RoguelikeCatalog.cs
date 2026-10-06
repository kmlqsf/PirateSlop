using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PirateSlop
{
    public enum UpgradeRarity { Common, Rare, Epic, Legendary }

    [Serializable]
    public sealed class UpgradeCard
    {
        public string id, name, description, rarity, supersedes;
        [NonSerialized] public UpgradeRarity Rarity;
    }

    public sealed class RoguelikeCatalog
    {
        [Serializable]
        sealed class CardFile { public int schemaVersion; public UpgradeCard[] cards; }
        [Serializable]
        sealed class ChanceFile { public int schemaVersion; public Milestone[] milestones; }
        [Serializable]
        sealed class Milestone
        {
            public int chestNumber;
            public float common, rare, epic, legendary;
            public float[] Weights => new[] { common, rare, epic, legendary };
        }

        readonly Dictionary<string, UpgradeCard> cards = new();
        Milestone[] milestones;
        public IEnumerable<UpgradeCard> Cards => cards.Values;
        public UpgradeCard Find(string id) => id != null && cards.TryGetValue(id, out var card) ? card : null;

        public static RoguelikeCatalog Load()
        {
            try
            {
                string directory = Path.Combine(Application.streamingAssetsPath, "Roguelike");
                var cardFile = JsonUtility.FromJson<CardFile>(File.ReadAllText(Path.Combine(directory, "UpgradeCatalog.json")));
                var chanceFile = JsonUtility.FromJson<ChanceFile>(File.ReadAllText(Path.Combine(directory, "UpgradeChances.json")));
                if (cardFile == null || cardFile.schemaVersion != 1 || cardFile.cards == null || cardFile.cards.Length < 3 ||
                    chanceFile == null || chanceFile.schemaVersion != 1 || chanceFile.milestones == null || chanceFile.milestones.Length == 0)
                    throw new InvalidDataException("Expected schemaVersion 1, at least three cards and non-empty milestones.");
                var result = new RoguelikeCatalog();
                foreach (var card in cardFile.cards)
                {
                    if (card == null || string.IsNullOrWhiteSpace(card.id) || string.IsNullOrWhiteSpace(card.name) || string.IsNullOrWhiteSpace(card.description) ||
                        !Enum.TryParse(card.rarity, false, out card.Rarity) || !Enum.IsDefined(typeof(UpgradeRarity), card.Rarity) || !result.cards.TryAdd(card.id, card))
                        throw new InvalidDataException("Invalid or duplicate upgrade card.");
                }
                foreach (var card in result.cards.Values)
                {
                    var visited = new HashSet<string> { card.id };
                    var ancestor = card;
                    while (!string.IsNullOrEmpty(ancestor.supersedes))
                    {
                        ancestor = result.Find(ancestor.supersedes);
                        if (ancestor == null || !visited.Add(ancestor.id)) throw new InvalidDataException("Invalid supersedes chain.");
                    }
                }
                int previous = 0;
                foreach (var point in chanceFile.milestones)
                {
                    if (point == null || point.chestNumber <= previous) throw new InvalidDataException("Chest milestones must be positive and strictly increasing.");
                    double sum = 0;
                    foreach (float weight in point.Weights)
                    {
                        if (weight < 0 || float.IsNaN(weight) || float.IsInfinity(weight)) throw new InvalidDataException("Rarity weights must be finite and non-negative.");
                        sum += weight;
                    }
                    if (sum <= 0) throw new InvalidDataException("Each milestone needs positive total rarity weight.");
                    previous = point.chestNumber;
                }
                result.milestones = chanceFile.milestones;
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("ROGUELIKE_CONFIG: " + ex.Message + " Upgrade rewards are disabled for this match.");
                return null;
            }
        }

        public float[] WeightsAt(int chestNumber)
        {
            if (chestNumber <= milestones[0].chestNumber) return milestones[0].Weights;
            for (int i = 1; i < milestones.Length; i++)
            {
                if (chestNumber > milestones[i].chestNumber) continue;
                var lower = milestones[i - 1];
                var upper = milestones[i];
                float t = Mathf.InverseLerp(lower.chestNumber, upper.chestNumber, chestNumber);
                var a = lower.Weights;
                var b = upper.Weights;
                for (int j = 0; j < a.Length; j++) a[j] = Mathf.Lerp(a[j], b[j], t);
                return a;
            }
            return milestones[milestones.Length - 1].Weights;
        }

        public bool Eligible(UpgradeCard card, HashSet<string> taken)
        {
            if (taken.Contains(card.id)) return false;
            foreach (string id in taken)
            {
                var owned = Find(id);
                while (owned != null && !string.IsNullOrEmpty(owned.supersedes))
                {
                    if (owned.supersedes == card.id) return false;
                    owned = Find(owned.supersedes);
                }
            }
            return true;
        }

        public string[] Roll(HashSet<string> taken, float[] weights, System.Random random)
        {
            var groups = new List<UpgradeCard>[4];
            for (int i = 0; i < groups.Length; i++) groups[i] = new List<UpgradeCard>();
            foreach (var card in cards.Values) if (Eligible(card, taken)) groups[(int)card.Rarity].Add(card);
            var offers = new List<string>(3);
            for (int slot = 0; slot < 3; slot++)
            {
                double total = 0;
                int remaining = 0;
                for (int i = 0; i < groups.Length; i++)
                {
                    remaining += groups[i].Count;
                    if (groups[i].Count > 0) total += weights[i];
                }
                if (remaining == 0) break;
                bool fallback = total <= 0;
                double draw = random.NextDouble() * (fallback ? remaining : total);
                int rarity = -1;
                for (int i = 0; i < groups.Length; i++)
                {
                    if (groups[i].Count == 0) continue;
                    double weight = fallback ? groups[i].Count : weights[i];
                    if (weight <= 0) continue;
                    rarity = i;
                    draw -= weight;
                    if (draw < 0) break;
                }
                var group = groups[rarity];
                int index = random.Next(group.Count);
                offers.Add(group[index].id);
                group.RemoveAt(index);
            }
            return offers.ToArray();
        }
    }
}
