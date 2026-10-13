using System.Collections.Generic;
using PirateSlop.Customization;
using UnityEngine;
using UnityEngine.Rendering;

namespace PirateSlop
{
    public sealed class SeaCaptureFlagText : MonoBehaviour
    {
        ShipNameGlyphLibrary library;
        Mesh mesh;
        Vector3[] rest, waved;
        readonly List<string> displayedNames = new();
        readonly Dictionary<int, FlatGlyph> flatGlyphs = new();
        bool namesKnown;
        sealed class FlatGlyph
        {
            public Vector3[] Vertices;
            public int[] Triangles;
        }
        public Vector2 Size { get; private set; } = new(2.8f, 1.65f);

        public void Initialize(Material ink)
        {
            library = Resources.Load<ShipNameGlyphLibrary>("Customization/ShipNameGlyphs");
            mesh = new Mesh { name = "CaptureFlagNames", indexFormat = IndexFormat.UInt32 };
            mesh.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = ink;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        public bool SetNames(IReadOnlyList<string> names)
        {
            bool changed = !namesKnown || names.Count != displayedNames.Count;
            if (!changed)
                for (int i = 0; i < names.Count; i++) if (names[i] != displayedNames[i]) { changed = true; break; }
            if (!changed) return false;
            namesKnown = true;
            displayedNames.Clear();
            foreach (string name in names) displayedNames.Add(name);
            string text = string.Join("\nVS\n", names);
            mesh.Clear();
            if (text.Length == 0 || library == null) { rest = waved = null; Size = new Vector2(2.8f, 1.65f); return true; }
            var lines = text.Split('\n');
            const float scale = .3f, lineHeight = .48f, margin = .25f;
            var widths = new float[lines.Length];
            float width = 2.8f;
            for (int row = 0; row < lines.Length; row++)
            {
                float cursor = 0f;
                int previous = 0;
                foreach (char character in lines[row])
                {
                    if (character == ' ') { cursor += library.SpaceAdvance; previous = 0; continue; }
                    if (!library.TryGetGlyph(character, out var glyph) || glyph.Mesh == null) continue;
                    cursor += library.PairOffset(previous, character) + glyph.Advance + .02f;
                    previous = character;
                }
                widths[row] = cursor * scale;
                width = Mathf.Max(width, widths[row] + margin * 2f);
            }
            float height = Mathf.Max(1.65f, lines.Length * lineHeight + margin * 2f);
            Size = new Vector2(width, height);
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int row = 0; row < lines.Length; row++)
            {
                float cursor = (width - widths[row]) * .5f;
                float baseline = height * .5f + lines.Length * lineHeight * .5f - (row + 1f) * lineHeight + .07f;
                int previous = 0;
                foreach (char character in lines[row])
                {
                    if (character == ' ') { cursor += library.SpaceAdvance * scale; previous = 0; continue; }
                    if (!library.TryGetGlyph(character, out var glyph) || glyph.Mesh == null) continue;
                    cursor += library.PairOffset(previous, character) * scale;
                    var flat = Flatten(character, glyph.Mesh);
                    var source = flat.Vertices;
                    var indices = flat.Triangles;
                    for (int side = 0; side < 2; side++)
                    {
                        int first = vertices.Count;
                        foreach (var point in source)
                        {
                            float x = cursor + point.x * scale;
                            vertices.Add(new Vector3(side == 0 ? x : width - x, baseline + point.y * scale, side == 0 ? -.025f : .025f));
                        }
                        foreach (int index in indices) triangles.Add(first + index);
                    }
                    cursor += (glyph.Advance + .02f) * scale;
                    previous = character;
                }
            }
            rest = vertices.ToArray();
            waved = new Vector3[rest.Length];
            mesh.vertices = rest;
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateBounds();
            return true;
        }

        FlatGlyph Flatten(int codepoint, Mesh source)
        {
            if (flatGlyphs.TryGetValue(codepoint, out var saved)) return saved;
            var points = source.vertices;
            var indices = source.triangles;
            float front = float.MinValue;
            foreach (var point in points) front = Mathf.Max(front, point.z);
            var remap = new Dictionary<int, int>();
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i < indices.Length; i += 3)
            {
                if (Mathf.Abs(points[indices[i]].z - front) > .00001f || Mathf.Abs(points[indices[i + 1]].z - front) > .00001f || Mathf.Abs(points[indices[i + 2]].z - front) > .00001f) continue;
                for (int corner = 0; corner < 3; corner++)
                {
                    int original = indices[i + corner];
                    if (!remap.TryGetValue(original, out int index))
                    {
                        index = vertices.Count;
                        vertices.Add(points[original]);
                        remap.Add(original, index);
                    }
                    triangles.Add(index);
                }
            }
            var glyph = new FlatGlyph { Vertices = vertices.ToArray(), Triangles = triangles.ToArray() };
            flatGlyphs.Add(codepoint, glyph);
            return glyph;
        }

        public void Wave(float time, float phase)
        {
            if (rest == null) return;
            for (int i = 0; i < rest.Length; i++)
            {
                var point = rest[i];
                float edge = point.x / Mathf.Max(.1f, Size.x);
                point.z += edge * (.12f * Mathf.Sin(time * 3.7f - edge * 7f + phase) + .045f * Mathf.Sin(time * 6f - edge * 13f + point.y * 2f));
                point.y += edge * (.045f * Mathf.Sin(time * 2.4f - edge * 5f + phase) - .09f * edge);
                waved[i] = point;
            }
            mesh.vertices = waved;
            mesh.RecalculateBounds();
        }

        void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }
    }
}
