using UnityEngine;

namespace PirateSlop.World
{
    public static class BoatAttackWaterTest
    {
        public const string SceneName = "BoatAttackWaterTest";
        public const string Marker = "boat_attack_water_test";
        public const float EdgeDistance = 50f;
        public static Vector3 WhirlpoolCenter => Vector3.forward * (OceanSurface.CentralWhirlpoolRadius + EdgeDistance);
        public static bool IsTest(WorldLayout layout) => layout != null && layout.Points.Exists(p => p.Tag == Marker);

        public static WorldLayout CreateLayout(WorldProfile profile, float seaLevel)
        {
            var layout = new WorldLayout
            {
                Seed = 2, Resolution = 32, Radius = 2500f, Depth = 200f,
                SeaLevel = seaLevel, CatalogHash = WorldGenerator.CatalogHash(profile)
            };
            layout.Points.Add(new WorldPoint { Id = Marker + "/1", Tag = Marker, Rule = -1 });
            for (int i = 0; i < 10; i++)
                layout.Points.Add(new WorldPoint
                {
                    Id = "water_test_ship_" + i, Tag = "ship_spawn", Rule = -1,
                    Position = new Vector3(i * 70f, seaLevel, 0f), Yaw = 0f
                });
            return layout;
        }
    }
}
