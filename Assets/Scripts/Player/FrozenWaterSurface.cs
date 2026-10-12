using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PirateSlop
{
    public static class FrozenWaterSurface
    {
        struct Thaw { public Vector3 Point; public Scene Scene; public float End; }
        static readonly HashSet<FrozenWaterTile> tiles = new();
        static readonly List<Thaw> thawing = new();
        static Texture2D atlas;
        static Color[] pixels;
        static float uploadAt;
        const int Resolution = 256;
        const float Size = 128f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState()
        {
            tiles.Clear(); thawing.Clear(); uploadAt = 0;
            Shader.SetGlobalVector("_FrozenWaterMapping", Vector4.zero);
        }

        public static void Add(FrozenWaterTile tile) { tiles.Add(tile); uploadAt = 0; }
        public static void Remove(FrozenWaterTile tile)
        {
            if (!tiles.Remove(tile)) return;
            if (Application.isPlaying) thawing.Add(new Thaw { Point = tile.transform.position, Scene = tile.gameObject.scene, End = Time.time + .8f });
            uploadAt = 0;
        }

        static float Weight(Vector2 delta)
        {
            float distance = Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y));
            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((distance - 1.15f) / 3.5f));
        }

        public static float Height(Vector3 position, float height, Scene scene)
        {
            float best = 0, target = height;
            foreach (var tile in tiles)
            {
                if (tile == null || tile.gameObject.scene != scene) continue;
                var p = tile.transform.position;
                float weight = Weight(new Vector2(position.x - p.x, position.z - p.z));
                if (weight > best) { best = weight; target = p.y - .1f; }
            }
            for (int i = thawing.Count - 1; i >= 0; i--)
            {
                var thaw = thawing[i];
                if (thaw.End <= Time.time) { thawing.RemoveAt(i); continue; }
                if (thaw.Scene != scene) continue;
                float weight = Weight(new Vector2(position.x - thaw.Point.x, position.z - thaw.Point.z)) * Mathf.Clamp01((thaw.End - Time.time) / .8f);
                if (weight > best) { best = weight; target = thaw.Point.y - .1f; }
            }
            return Mathf.Lerp(height, target, best);
        }

        public static void Upload(OceanSurface ocean, Vector3? focusPosition = null)
        {
            if (Application.isBatchMode || focusPosition == null && Time.time < uploadAt) return;
            uploadAt = Time.time + .05f;
            if (atlas == null)
            {
                atlas = new Texture2D(Resolution, Resolution, TextureFormat.RGBAFloat, false, true) { name = "FrozenWaterSurfaceMask", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                pixels = new Color[Resolution * Resolution];
            }
            var camera = Camera.main;
            var focus = focusPosition ?? (camera != null ? camera.transform.position : ocean.transform.position);
            Vector2 origin = new Vector2(Mathf.Floor(focus.x / 8f) * 8f - Size * .5f, Mathf.Floor(focus.z / 8f) * 8f - Size * .5f);
            System.Array.Clear(pixels, 0, pixels.Length);
            foreach (var tile in tiles)
                if (tile != null && tile.gameObject.scene == ocean.gameObject.scene) Paint(tile.transform.position, origin, 1f, true, tile.Formation);
            for (int i = thawing.Count - 1; i >= 0; i--)
            {
                var thaw = thawing[i];
                if (thaw.End <= Time.time) { thawing.RemoveAt(i); continue; }
                if (thaw.Scene == ocean.gameObject.scene) Paint(thaw.Point, origin, Mathf.Clamp01((thaw.End - Time.time) / .8f), false);
            }
            atlas.SetPixels(pixels); atlas.Apply(false, false);
            Shader.SetGlobalTexture("_FrozenWaterMask", atlas);
            Shader.SetGlobalVector("_FrozenWaterMapping", new Vector4(origin.x, origin.y, 1f / Size, 1f / Size));
        }

        static void Paint(Vector3 point, Vector2 origin, float strength, bool solid, float formation = 1f)
        {
            const float step = Size / Resolution;
            int x0 = Mathf.Clamp(Mathf.FloorToInt((point.x - 4.7f - origin.x) / step), 0, Resolution - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((point.x + 4.7f - origin.x) / step), 0, Resolution - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((point.z - 4.7f - origin.y) / step), 0, Resolution - 1);
            int z1 = Mathf.Clamp(Mathf.CeilToInt((point.z + 4.7f - origin.y) / step), 0, Resolution - 1);
            for (int z = z0; z <= z1; z++) for (int x = x0; x <= x1; x++)
            {
                Vector2 delta = new Vector2(origin.x + (x + .5f) * step - point.x, origin.y + (z + .5f) * step - point.z);
                float weight = Weight(delta) * strength;
                int index = z * Resolution + x;
                float core = solid && delta.magnitude / 1.6f <= formation * 1.4f && Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y)) < 1.02f ? 1f : 0f;
                if (weight > pixels[index].g) pixels[index] = new Color(point.y - .1f, weight, Mathf.Max(core, pixels[index].b), 0);
                else if (core > pixels[index].b) pixels[index].b = core;
            }
        }
    }
}
