using UnityEngine;
namespace PirateSlop.World
{
    public static class OceanaWaterTest
    {
        public const string SceneName = "OceanaWaterTest";
        public const string Marker = "oceana_water_test";
        public static bool IsTest(WorldLayout layout) => layout != null && layout.Points.Exists(p => p.Tag == Marker);
        public static WorldLayout CreateLayout(WorldProfile profile, float seaLevel)
        {
            var layout = BoatAttackWaterTest.CreateLayout(profile, seaLevel);
            layout.Seed = 3;
            var marker = layout.Points.Find(p => p.Tag == BoatAttackWaterTest.Marker);
            marker.Id = Marker + "/1";
            marker.Tag = Marker;
            return layout;
        }
    }
}
