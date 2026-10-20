using System;
using System.Collections.Generic;
using UnityEngine;

namespace PirateSlop.World
{
    public sealed class MainOceanScenery : MonoBehaviour
    {
        [Serializable]
        public sealed class Asset
        {
            public string Key;
            public GameObject Prefab;
            public Mesh HorizonMesh;
            public float Radius, MinScale = 1f, MaxScale = 1f;
        }

        public const string ResourcePath = "MainOcean/Scenery";
        public const string Tag = "main_ocean_scenery";
        public string Revision;
        public Asset[] Assets = Array.Empty<Asset>();
        public static string CatalogVersion => Resources.Load<MainOceanScenery>(ResourcePath)?.Revision ?? "missing";

        public Asset Find(string key)
        {
            foreach (var asset in Assets) if (asset.Key == key) return asset;
            throw new InvalidOperationException("Missing main ocean scenery: " + key);
        }

        public static float Scale(WorldPoint point) => point.Rule * .001f;

        public void Spawn(WorldPoint point, Transform parent)
        {
            var asset = Find(point.TypeId);
            float scale = Scale(point);
            if (asset.Prefab == null || scale < asset.MinScale - .001f || scale > asset.MaxScale + .001f)
                throw new InvalidOperationException("Invalid main ocean scenery: " + point.Id);
            var instance = Instantiate(asset.Prefab, parent);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one * scale;
            DistantSceneryRenderer horizon = null;
            if (asset.HorizonMesh != null)
            {
                var root = parent.parent != null ? parent.parent : parent;
                horizon = root.GetComponent<DistantSceneryRenderer>() ?? root.gameObject.AddComponent<DistantSceneryRenderer>();
                horizon.Add(asset.HorizonMesh, instance.transform.localToWorldMatrix);
            }
            foreach (var visual in instance.GetComponentsInChildren<CoastalRockVisual>(true))
            {
                var group = visual.GetComponent<LODGroup>();
                if (group == null || visual.Contours == null || visual.Contours.Length == 0) continue;
                var levels = group.GetLODs();
                if (levels.Length == 0) continue;
                levels[levels.Length - 1].screenRelativeTransitionHeight = 0f;
                group.SetLODs(levels);
                if (horizon != null) foreach (var level in levels) foreach (var renderer in level.renderers) horizon.UseNearFade(renderer);
            }
        }

        public static void AddPoints(WorldLayout layout, float offset)
        {
            var catalog = Resources.Load<MainOceanScenery>(ResourcePath);
            if (catalog == null || catalog.Assets.Length != 9 || string.IsNullOrEmpty(catalog.Revision))
                throw new InvalidOperationException("The main ocean scenery catalog is missing.");
            new Builder(layout, catalog, offset).Build();
        }

        public static List<WorldRoute> SailingLanes(WorldLayout layout)
        {
            var result = new List<WorldRoute>();
            foreach (var from in layout.Points)
            {
                string ring = from.Tag == "ship_spawn" ? "A" : from.Tag == MainOceanEvents.Tag && from.Id.StartsWith("main_event/A", StringComparison.Ordinal) ? "B" :
                    from.Tag == MainOceanEvents.Tag && from.Id.StartsWith("main_event/B", StringComparison.Ordinal) ? "C" :
                    from.Tag == MainOceanEvents.Tag && from.Id.StartsWith("main_event/C", StringComparison.Ordinal) ? "D" : null;
                if (ring == null) continue;
                WorldPoint first = null, second = null;
                float best = float.PositiveInfinity, next = float.PositiveInfinity;
                foreach (var to in layout.Points)
                {
                    if (to.Tag != MainOceanEvents.Tag || !to.Id.StartsWith("main_event/" + ring, StringComparison.Ordinal)) continue;
                    float distance = (to.Position - from.Position).sqrMagnitude;
                    if (distance < best) { second = first; next = best; first = to; best = distance; }
                    else if (distance < next) { second = to; next = distance; }
                }
                AddLane(result, from, first);
                if (from.Tag != "ship_spawn") AddLane(result, from, second);
            }
            return result;
        }

        static void AddLane(List<WorldRoute> result, WorldPoint from, WorldPoint to)
        {
            if (to != null) result.Add(new WorldRoute { Id = from.Id + "/to/" + to.Id, FromId = from.Id, ToId = to.Id,
                Waypoints = new List<Vector3> { from.Position, to.Position } });
        }

        public static float LaneClearance(WorldRoute route) => route.FromId.StartsWith("ship_", StringComparison.Ordinal) ? 80f :
            route.FromId.StartsWith("main_event/A", StringComparison.Ordinal) ? 85f : route.FromId.StartsWith("main_event/B", StringComparison.Ordinal) ? 95f : 110f;

        public static float EventClearance(WorldPoint point)
        {
            switch ((MainOceanEventKind)point.Rule)
            {
                case MainOceanEventKind.Whale: return 380f;
                case MainOceanEventKind.Kamikaze: return 310f;
                case MainOceanEventKind.CannonBreach: return 270f;
                case MainOceanEventKind.Platforming: case MainOceanEventKind.Rigging: return 260f;
                case MainOceanEventKind.Skull: return 240f;
                case MainOceanEventKind.Capture: case MainOceanEventKind.SeagullKey: return 230f;
                case MainOceanEventKind.Balance: case MainOceanEventKind.SternCargo: return 220f;
                case MainOceanEventKind.Clam: return 200f;
                default: return 140f;
            }
        }

        public static float SegmentDistance(Vector3 point, Vector3 a, Vector3 b)
        {
            point.y = a.y = b.y = 0f;
            var direction = b - a;
            float along = Mathf.Clamp01(Vector3.Dot(point - a, direction) / Mathf.Max(.001f, direction.sqrMagnitude));
            return Vector3.Distance(point, a + direction * along);
        }

        sealed class Builder
        {
            readonly WorldLayout layout;
            readonly MainOceanScenery catalog;
            readonly List<WorldRoute> lanes;
            readonly List<WorldPoint> placed = new();
            readonly List<float> radii = new();
            readonly float offset;
            MapRandom random;
            int sequence;

            public Builder(WorldLayout layout, MainOceanScenery catalog, float offset)
            {
                this.layout = layout; this.catalog = catalog; this.offset = offset;
                lanes = SailingLanes(layout);
                random = new MapRandom(unchecked((uint)layout.Seed ^ 0x974c3a21u));
            }

            Vector3 Polar(float angle, float radius)
            {
                float radians = (angle + offset) * Mathf.Deg2Rad;
                return new Vector3(Mathf.Sin(radians) * radius * layout.Radius, layout.SeaLevel, Mathf.Cos(radians) * radius * layout.Radius);
            }

            public void Build()
            {
                Landmark("lagoon_west", "Sea_Lagoon_Cave", 260f, .48f, 43f);
                Landmark("lagoon_east", "Sea_Lagoon_Cave", 90f, .46f, 211f);
                Landmark("arch_north", "SeaArch_Huge_A", 0f, .48f, 61f);
                Landmark("arch_east", "SeaArch_Huge_A", 145f, .40f, 134f);
                Landmark("arch_south", "SeaArch_Huge_A", 310f, .42f, 35f);
                Landmark("island_palms", "SmallSandyIsland", 46f, .65f, 24f);
                Landmark("island_south", "SmallSandyIsland", 178f, .75f, 112f);
                Landmark("island_west", "SmallSandyIsland", 257f, .58f, 267f);
                Landmark("island_north", "SmallSandyIsland", 105f, .85f, 180f);
                Landmark("island_cove", "TwoLandingIsland", 197f, .51f, 75f);
                Ridge("north_teeth", 8f, .77f, 31f, 9, false);
                Ridge("north_shoal", 31f, .58f, 43f, 7, true);
                Ridge("east_teeth", 77f, .57f, 27f, 6, false);
                Ridge("east_shoal", 94f, .71f, 59f, 8, true);
                Ridge("outer_east", 121f, .83f, 38f, 9, false);
                Ridge("south_east", 151f, .73f, 24f, 9, false);
                Ridge("southern_shoal", 170f, .55f, 39f, 5, true);
                Ridge("outer_south", 207f, .83f, 28f, 9, false);
                Ridge("west_teeth", 226f, .66f, 53f, 6, false);
                Ridge("outer_west", 273f, .79f, 42f, 9, false);
                Ridge("western_shrine", 290f, .64f, 34f, 6, false, true);
                Ridge("north_west", 342f, .67f, 19f, 9, false);
                Ridge("inner_north", 20f, .40f, 44f, 4, false);
                Ridge("inner_east", 101f, .43f, 29f, 4, true);
                Ridge("inner_south", 157f, .38f, 53f, 4, false);
                Ridge("inner_west", 235f, .40f, 23f, 4, false, true);
                Ridge("inner_passage", 310f, .37f, 41f, 4, false);
                Ridge("final_east", 57f, .28f, 18f, 3, false);
                Ridge("final_west", 190f, .27f, 48f, 3, false);
                for (int i = 0; i < 18; i++)
                    Ridge("edge_" + i, i * 20f + random.Range(-5f, 5f), random.Range(.86f, .925f), random.Range(15f, 75f), 6 + (int)(random.Next() % 4), i % 4 == 0);
            }

            void Landmark(string group, string key, float angle, float radius, float yaw)
            {
                var origin = Polar(angle, radius);
                for (int attempt = 0; attempt < 100; attempt++)
                {
                    var point = origin + Quaternion.Euler(0, offset + attempt * 137.5f, 0) * Vector3.forward * (attempt == 0 ? 0f : 45f + attempt * 4.5f);
                    if (TryPlace(group, key, point, yaw + offset + random.Range(-8f, 8f))) return;
                }
                throw new InvalidOperationException("No safe position for main ocean landmark: " + group);
            }

            void Ridge(string group, float angle, float radius, float yaw, int count, bool shoal, bool shrine = false)
            {
                var origin = Polar(angle, radius);
                var turn = Quaternion.Euler(0, yaw + offset + random.Range(-9f, 9f), 0);
                bool small = radius >= .60f;
                float spacing = random.Range(shoal || small ? 74f : 96f, shoal || small ? 108f : 138f);
                for (int i = 0; i < count; i++)
                {
                    string key = shrine && i < 2 ? "Reef_Moai_A" : shoal && i % 2 == 0 ? "Reef_ShallowField_A" :
                        !small && i == count / 2 ? "Reef_Spires_C" : i % 4 == 0 ? "Reef_Spires_B" : "Reef_Spires_A";
                    for (int attempt = 0; attempt < 60; attempt++)
                    {
                        float along = (i - (count - 1) * .5f) * spacing + (i % 2 == 0 ? 18f : -18f) + random.Range(-32f, 32f);
                        var local = new Vector3(random.Range(-48f, 48f) + Mathf.Sin(i * 1.4f) * 35f, 0, along);
                        if (attempt > 10) local += new Vector3(random.Range(-160f, 160f), 0, random.Range(-80f, 80f));
                        if (TryPlace(group, key, origin + turn * local, yaw + offset + random.Range(-32f, 32f))) break;
                    }
                }
            }

            bool TryPlace(string group, string key, Vector3 position, float yaw)
            {
                var asset = catalog.Find(key);
                float scale = Mathf.Round(random.Range(asset.MinScale, asset.MaxScale) * 1000f) * .001f;
                float radius = asset.Radius * scale;
                float distance = new Vector2(position.x, position.z).magnitude;
                if (distance - radius < 720f || distance + radius > layout.Radius - 220f) return false;
                if ((key == "Sea_Lagoon_Cave" || key == "SeaArch_Huge_A" || key == "TwoLandingIsland" || key == "Reef_Spires_C") && distance + radius > layout.Radius * .61f) return false;
                foreach (var point in layout.Points)
                {
                    float clearance = point.Tag == "ship_spawn" ? 180f : point.Tag == MainOceanEvents.Tag ? EventClearance(point) : 0f;
                    if (clearance > 0f && Vector3.Distance(position, point.Position) < radius + clearance) return false;
                }
                foreach (var lane in lanes)
                    if (SegmentDistance(position, lane.Waypoints[0], lane.Waypoints[1]) < radius + LaneClearance(lane)) return false;
                string prefix = "scenery/" + group + "/";
                for (int i = 0; i < placed.Count; i++)
                {
                    float gap = placed[i].Id.StartsWith(prefix, StringComparison.Ordinal) ? 18f : 100f;
                    if (Vector3.Distance(position, placed[i].Position) < radius + radii[i] + gap) return false;
                }
                var result = new WorldPoint { Id = prefix + sequence++, Tag = Tag, TypeId = key, Position = position, Yaw = yaw, Rule = Mathf.RoundToInt(scale * 1000f) };
                layout.Points.Add(result); placed.Add(result); radii.Add(radius);
                return true;
            }
        }
    }
}
