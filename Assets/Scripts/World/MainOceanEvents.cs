using System;
using PirateSlop.Networking;
using UnityEngine;

namespace PirateSlop.World
{
    public enum MainOceanEventKind { Capture, Raft, Sunken, Shark, Skull, Whale, Clam, Balance, Rigging, SternCargo, CannonBreach, SeagullKey, Platforming, Kamikaze }

    public static class MainOceanEvents
    {
        public const string Tag = "main_ocean_event";
        public const int EventCount = 20;
        static readonly MainOceanEventKind[] outer = { MainOceanEventKind.Raft, MainOceanEventKind.Raft, MainOceanEventKind.Sunken, MainOceanEventKind.Sunken, MainOceanEventKind.Skull, MainOceanEventKind.Skull, MainOceanEventKind.Shark, MainOceanEventKind.Shark, MainOceanEventKind.Raft, MainOceanEventKind.Sunken };
        static readonly MainOceanEventKind[] middle = { MainOceanEventKind.Capture, MainOceanEventKind.Clam, MainOceanEventKind.Balance, MainOceanEventKind.SeagullKey, MainOceanEventKind.Kamikaze };
        static readonly MainOceanEventKind[] inner = { MainOceanEventKind.Whale, MainOceanEventKind.Rigging, MainOceanEventKind.SternCargo };
        static readonly MainOceanEventKind[] final = { MainOceanEventKind.CannonBreach, MainOceanEventKind.Platforming };

        public static void AddPoints(WorldLayout layout, float offset)
        {
            var random = new MapRandom(unchecked((uint)layout.Seed ^ 0xc3e91a7bu));
            int shift = (int)(random.Next() % 5) * 2;
            for (int i = 0; i < outer.Length; i++) Add(layout, "A", i, outer[(i + shift) % outer.Length], .70f, offset + i * 36f);
            for (int i = 0; i < middle.Length; i++) Add(layout, "B", i, middle[i], .50f, offset + 18f + i * 72f);
            for (int i = 0; i < inner.Length; i++) Add(layout, "C", i, inner[i], .33f, offset + 30f + i * 120f);
            for (int i = 0; i < final.Length; i++) Add(layout, "D", i, final[i], .22f, offset + 40f + i * 180f);
        }

        static void Add(WorldLayout layout, string ring, int index, MainOceanEventKind kind, float radius, float angle)
        {
            float radians = angle * Mathf.Deg2Rad;
            layout.Points.Add(new WorldPoint {
                Id = "main_event/" + ring + (index + 1), Tag = Tag, TypeId = kind.ToString(), Rule = (int)kind,
                Position = new Vector3(Mathf.Sin(radians) * layout.Radius * radius, layout.SeaLevel, Mathf.Cos(radians) * layout.Radius * radius),
                Yaw = angle + 180f
            });
        }

        public static void ValidateCatalog(LootCatalog catalog)
        {
            if (catalog == null || catalog.ChestPrefab == null || catalog.SkullEventPrefab == null || catalog.WhaleEventPrefab == null ||
                catalog.ClamEventPrefab == null || catalog.BalanceEventPrefab == null || catalog.RiggingEventPrefab == null ||
                catalog.SternCargoEventPrefab == null || catalog.CannonBreachEventPrefab == null || catalog.SeagullEventPrefab == null ||
                catalog.PlatformingEventPrefab == null || catalog.KamikazeEventPrefab == null || catalog.KamikazeIslandPrefab == null)
                throw new InvalidOperationException("Для основной карты нужны все 14 ивентов, наградной сундук и остров камикадзе в LootCatalog.");
        }

        public static void SpawnStatic(WorldLayout layout, Transform parent)
        {
            var catalog = SessionController.Instance != null ? SessionController.Instance.Config.Loot : null;
            ValidateCatalog(catalog);
            foreach (var point in layout.Points)
                if (point.Tag == Tag && point.Rule == (int)MainOceanEventKind.Kamikaze)
                {
                    var island = UnityEngine.Object.Instantiate(catalog.KamikazeIslandPrefab, point.Position, Quaternion.Euler(0, point.Yaw, 0), parent);
                    island.name = "MainKamikazeIsland";
                }
        }

        public static Vector2Int RewardItemTypes(Vector3 position)
        {
            var layout = ProceduralWorld.Instance != null ? ProceduralWorld.Instance.Layout : null;
            return RewardItemTypes(layout, position);
        }

        public static Vector2Int RewardItemTypes(WorldLayout layout, Vector3 position)
        {
            if (!MainOceanMap.IsMain(layout)) return default;
            WorldPoint nearest = null;
            float distance = 400f * 400f;
            foreach (var point in layout.Points)
            {
                if (point.Tag != Tag) continue;
                float dx = point.Position.x - position.x, dz = point.Position.z - position.z;
                float squared = dx * dx + dz * dz;
                if (squared >= distance) continue;
                nearest = point; distance = squared;
            }
            if (nearest == null) return default;
            var kind = (MainOceanEventKind)nearest.Rule;
            if (nearest.Id.StartsWith("main_event/D", StringComparison.Ordinal)) return new Vector2Int(8, 8);
            return kind == MainOceanEventKind.Capture || kind == MainOceanEventKind.Raft || kind == MainOceanEventKind.Sunken || kind == MainOceanEventKind.Shark || kind == MainOceanEventKind.Skull
                ? new Vector2Int(3, 4) : new Vector2Int(6, 7);
        }
    }
}
