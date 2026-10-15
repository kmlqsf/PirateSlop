using System;
using UnityEngine;

namespace PirateSlop.World
{
    public sealed class EnvironmentTestGallery : MonoBehaviour
    {
        public const string ResourcePath = "EnvironmentTest/Gallery";
        public const string Marker = "environment_test";
        public float Radius = 4000f;
        public Vector3 Spawn;
        public string Revision;
        public Transform[] Labels;
        GUIStyle labelStyle;

        public static bool IsTest(WorldLayout layout) => layout != null && layout.Points.Exists(p => p.Tag == Marker);

        public static Vector3 WhirlpoolCenter(WorldLayout layout)
        {
            foreach(var point in layout.Points)
                if(point.Id=="test_ship_0") return new Vector3(point.Position.x,layout.SeaLevel,point.Position.z-550f);
            throw new InvalidOperationException("Environment test ship spawn is missing.");
        }

        public static WorldLayout CreateLayout(WorldProfile profile, float seaLevel)
        {
            var gallery = Resources.Load<EnvironmentTestGallery>(ResourcePath);
            if (gallery == null) throw new InvalidOperationException("Rebuild the environment gallery from PirateSlop > Prepare Environment Test.");
            var layout = new WorldLayout { Seed = 1, Resolution = 32, Radius = Mathf.Max(gallery.Radius, 4000f), Depth = 200f, SeaLevel = seaLevel, CatalogHash = WorldGenerator.CatalogHash(profile) };
            layout.Points.Add(new WorldPoint { Id = Marker + "/" + gallery.Revision, Tag = Marker, Position = Vector3.up * seaLevel, Rule = -1 });
            for (int i = 0; i < 10; i++)
                layout.Points.Add(new WorldPoint { Id = "test_ship_" + i, Tag = "ship_spawn", Position = gallery.Spawn + new Vector3(-i * 65f, seaLevel, 0f), Yaw = 180f, Rule = -1 });
            return layout;
        }

        public static void SpawnInto(WorldLayout layout, Transform parent)
        {
            var gallery = Resources.Load<EnvironmentTestGallery>(ResourcePath);
            if (gallery == null || !layout.Points.Exists(p => p.Id == Marker + "/" + gallery.Revision))
                throw new InvalidOperationException("Environment gallery differs from the host. Update the game assets.");
            var instance = Instantiate(gallery, parent);
            foreach (var terrain in instance.GetComponentsInChildren<Terrain>(true))
                if (terrain.terrainData == null)
                {
                    Destroy(instance.gameObject);
                    throw new InvalidOperationException("Данные островов повреждены или отсутствуют. Обновите файлы игры.");
                }
            var ocean = Resources.Load<GameObject>("EnvironmentTest/Ocean");
            if (ocean == null) throw new InvalidOperationException("The environment test ocean is missing.");
            Instantiate(ocean, parent);
            var sky = Resources.Load<GameObject>("EnvironmentTest/SkyDayNight");
            if (sky != null) Instantiate(sky, parent);
            instance.transform.localPosition = Vector3.up * layout.SeaLevel; var wp = instance.gameObject.AddComponent<WhirlpoolTest>(); wp.Center = WhirlpoolCenter(layout); wp.Depth = OceanSurface.CentralWhirlpoolDepth;
            instance.gameObject.AddComponent<EnvironmentEventTestRow>().SeaLevel = layout.SeaLevel;
            var tentacles = new GameObject("WhirlpoolTentacles");
            tentacles.transform.SetParent(instance.transform,false);
            tentacles.AddComponent<WhirlpoolTentacleRing>().Initialize(wp.Center,wp.Radius,wp.Depth,layout.SeaLevel,layout.Seed);
        }

        void OnGUI()
        {
            if (PirateSlop.Performance.PerformanceBenchmarkController.HideTestLabels) return;
            if (RoguelikeUpgradeUI.WindowOpen) return;
            var camera = Camera.main;
            if (camera == null) return;
            labelStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 20, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, .9f, .62f) } };
            foreach (var label in Labels)
            {
                if (label == null) continue;
                var point = camera.WorldToScreenPoint(label.position);
                if (point.z <= 0 || point.x < 0 || point.x > Screen.width || point.y < 0 || point.y > Screen.height) continue;
                var content = new GUIContent(label.name);
                var size = labelStyle.CalcSize(content);
                var rect = new Rect(point.x - size.x * .5f - 10, Screen.height - point.y - 18, size.x + 20, 36);
                GUI.Box(rect, GUIContent.none);
                GUI.Label(rect, content, labelStyle);
            }
        }
    }
}


