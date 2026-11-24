using UnityEngine;

namespace PirateSlop.World
{
    public static class TutorialBay
    {
        const string Marker = "tutorial_bay";
        public static bool IsTutorial(WorldLayout layout) => layout != null && layout.Points.Count > 0 && layout.Points[0].Tag == Marker;
        public static Vector3 StartPoint(float seaLevel) => new Vector3(200f, seaLevel, 200f);
        static readonly Vector3 gateOffset = new(110f, 0f, 225f);
        public const float GateHalfWidth = 20f;
        public static Vector3 GateForward => gateOffset.normalized;
        public static Vector3 GateRight => Vector3.Cross(Vector3.up, GateForward);
        public static Vector3 Gate(float seaLevel) => StartPoint(seaLevel) + gateOffset;

        public static Vector3 ShoreBattery(float seaLevel) => StartPoint(seaLevel) + new Vector3(150f, 6f, 110f);

        internal static void SpawnEnvironment(Transform parent)
        {
            MainOceanMap.SpawnEnvironment(parent);
            var surface = OceanSurface.Instance;
            surface.DisableWhirlpool();
            if (surface.HeightSource is BoatAttackOcean waves) waves.UpdateSurface();
        }

        public static WorldLayout Create(WorldProfile profile, float seaLevel)
        {
            var layout = new WorldLayout { Seed = 751, Resolution = 64, Radius = 750, Depth = 65,
                SeaLevel = seaLevel, CatalogHash = WorldGenerator.CatalogHash(profile) };
            var start = StartPoint(seaLevel);
            layout.Points.Add(new WorldPoint { Id = Marker, Tag = Marker, Rule = -1, Position = start });
            layout.Points.Add(new WorldPoint { Id = "training_ship", Tag = "ship_spawn", Position = start, Rule = -1 });
            var rocks = new LocationSettings
            {
                Id = "training_rocks",
                Shape = Landform.SeaStack,
                Roughness = .42f,
                Rock = new Color(.23f, .26f, .28f),
                Sand = new Color(.38f, .37f, .33f)
            };
            var battery = ShoreBattery(seaLevel);
            battery.y = seaLevel;
            layout.Locations.Add(new LocationRecord { Id = "training_headland", Type = rocks, Position = battery,
                Radius = 30f, Height = 9f, Seed = 972 });
            layout.Locations.Add(new LocationRecord { Id = "training_rock", Type = rocks, Position = start + new Vector3(-141f, 0f, 93f),
                Radius = 22f, Height = 7f, Seed = 974 });
            return layout;
        }
    }
}
