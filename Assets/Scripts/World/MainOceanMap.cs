using System;
using UnityEngine;

namespace PirateSlop.World
{
    public static class MainOceanMap
    {
        public const string Marker = "main_ocean";
        public const string Revision = "ocean-base-1";

        public static bool IsMain(WorldLayout layout) => layout != null && layout.Points.Exists(p => p.Tag == Marker);
        public static bool UsesModernOcean(WorldLayout layout) => IsMain(layout) || EnvironmentTestGallery.IsTest(layout);

        public static WorldLayout CreateLayout(WorldProfile profile, int seed, int shipCount, float seaLevel)
        {
            var gallery = Resources.Load<EnvironmentTestGallery>(EnvironmentTestGallery.ResourcePath);
            if (gallery == null) throw new InvalidOperationException("The ocean environment is missing.");
            var layout = new WorldLayout { Seed = seed, Resolution = 32, Radius = Mathf.Max(gallery.Radius, 4000f), Depth = 200f, SeaLevel = seaLevel, CatalogHash = WorldGenerator.CatalogHash(profile) };
            layout.Points.Add(new WorldPoint { Id = Marker + "/" + Revision, Tag = Marker, Position = Vector3.up * seaLevel, Rule = -1 });
            float radius = Mathf.Max(layout.Radius * .25f, shipCount * 65f / (2f * Mathf.PI));
            var random = new MapRandom(unchecked((uint)seed));
            float offset = random.Range(0f, 2f * Mathf.PI);
            for (int i = 0; i < shipCount; i++)
            {
                float angle = offset + i * 2f * Mathf.PI / shipCount;
                var position = new Vector3(Mathf.Sin(angle) * radius, seaLevel, Mathf.Cos(angle) * radius);
                layout.Points.Add(new WorldPoint { Id = "ship_" + i, Tag = "ship_spawn", Position = position, Yaw = angle * Mathf.Rad2Deg + 180f, Rule = -1 });
            }
            return layout;
        }

        public static void SpawnEnvironment(Transform parent)
        {
            var ocean = Resources.Load<GameObject>("EnvironmentTest/Ocean");
            var sky = Resources.Load<GameObject>("EnvironmentTest/SkyDayNight");
            if (ocean == null || sky == null) throw new InvalidOperationException("The ocean or sky environment is missing.");
            UnityEngine.Object.Instantiate(ocean, parent);
            UnityEngine.Object.Instantiate(sky, parent);
        }

        public static void SpawnInto(WorldLayout layout, Transform parent)
        {
            if (!layout.Points.Exists(p => p.Id == Marker + "/" + Revision)) throw new InvalidOperationException("The ocean map differs from the host. Update the game assets.");
            SpawnEnvironment(parent);
            var root = new GameObject("CentralWhirlpool");
            root.transform.SetParent(parent, false);
            var whirlpool = root.AddComponent<WhirlpoolTest>();
            whirlpool.Center = Vector3.up * layout.SeaLevel;
            whirlpool.Depth = OceanSurface.CentralWhirlpoolDepth;
            whirlpool.ShowLabel = false;
            var tentacles = new GameObject("WhirlpoolTentacles");
            tentacles.transform.SetParent(root.transform, false);
            tentacles.AddComponent<WhirlpoolTentacleRing>().Initialize(whirlpool.Center, whirlpool.Radius, whirlpool.Depth, layout.SeaLevel, layout.Seed);
        }
    }
}
