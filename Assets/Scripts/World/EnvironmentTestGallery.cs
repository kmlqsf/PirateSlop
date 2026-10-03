using System;
using UnityEngine;

namespace PirateSlop.World
{
    public sealed class EnvironmentTestGallery : MonoBehaviour
    {
        public const string ResourcePath = "EnvironmentTest/Gallery";
        public const string Marker = "environment_test";
        public float Radius = 2000f;
        public Vector3 Spawn;
        public string Revision;
        public Transform[] Labels;
        GUIStyle labelStyle;

        public static bool IsTest(WorldLayout layout) => layout != null && layout.Points.Exists(p => p.Tag == Marker);

        public static WorldLayout CreateLayout(WorldProfile profile, float seaLevel)
        {
            var gallery = Resources.Load<EnvironmentTestGallery>(ResourcePath);
            if (gallery == null) throw new InvalidOperationException("Rebuild the environment gallery from PirateSlop > Prepare Environment Test.");
            var layout = new WorldLayout { Seed = 1, Resolution = 32, Radius = gallery.Radius, Depth = 80f, SeaLevel = seaLevel, CatalogHash = WorldGenerator.CatalogHash(profile) };
            layout.Points.Add(new WorldPoint { Id = Marker + "/" + gallery.Revision, Tag = Marker, Position = Vector3.up * seaLevel, Rule = -1 });
            for (int i = 0; i < 10; i++)
                layout.Points.Add(new WorldPoint { Id = "test_ship_" + i, Tag = "ship_spawn", Position = gallery.Spawn + new Vector3(i * 65f, seaLevel, 0), Yaw = 90f, Rule = -1 });
            return layout;
        }

        public static void SpawnInto(WorldLayout layout, Transform parent)
        {
            var gallery = Resources.Load<EnvironmentTestGallery>(ResourcePath);
            if (gallery == null || !layout.Points.Exists(p => p.Id == Marker + "/" + gallery.Revision))
                throw new InvalidOperationException("Environment gallery differs from the host. Update the game assets.");
            var instance = Instantiate(gallery, parent);
            instance.transform.localPosition = Vector3.up * layout.SeaLevel; var wp = instance.gameObject.AddComponent<WhirlpoolTest>(); wp.Center = gallery.Spawn + new Vector3(200f, 0, -400f);
            var whalePrefab = Resources.Load<GameObject>("Whale/WhaleLootPOI");
            if (whalePrefab != null)
            {
                var whale = Instantiate(whalePrefab, parent);
                whale.transform.position = gallery.Spawn + new Vector3(140f, layout.SeaLevel - 4.5f, 160f);
                whale.transform.rotation = Quaternion.Euler(0f, -30f, 0f);
            }
        }

        void OnGUI()
        {
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


