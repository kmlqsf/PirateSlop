using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PirateSlop.Customization
{
    public sealed class ShipNameplate : MonoBehaviour
    {
        public Transform NameAnchor;
        public ShipNameGlyphLibrary GlyphLibrary;
        public float TextWidth = 4.2f;
        public float TextHeight = .48f;
        string displayed;
        MeshFilter geometry;
        MeshRenderer letterRenderer;
        Mesh letters;
        readonly List<Mesh> submeshes = new();

        void InitializeGeometry()
        {
            if (geometry != null) return;
            var root = new GameObject("CarvedName");
            root.transform.SetParent(NameAnchor, false);
            root.layer = NameAnchor.gameObject.layer;
            geometry = root.AddComponent<MeshFilter>();
            letterRenderer = root.AddComponent<MeshRenderer>();
            letterRenderer.sharedMaterials = GlyphLibrary.Materials;
            letterRenderer.shadowCastingMode = ShadowCastingMode.On;
            letterRenderer.receiveShadows = true;
            letters = new Mesh { name = "ShipNameLetters", indexFormat = IndexFormat.UInt32 };
            geometry.sharedMesh = letters;
            for (int i = 0; i < GlyphLibrary.Materials.Length; i++)
                submeshes.Add(new Mesh { name = "ShipNameMaterial_" + i, indexFormat = IndexFormat.UInt32 });
        }

        public void SetName(string value)
        {
            if (NameAnchor == null || GlyphLibrary == null || GlyphLibrary.Materials == null) return;
            string name = ShipCustomizationData.NormalizeName(value);
            if (displayed == name) return;
            InitializeGeometry();
            displayed = name;
            letters.Clear();
            letterRenderer.enabled = name.Length > 0;
            if (name.Length == 0) return;
            var layers = new List<CombineInstance>[submeshes.Count];
            for (int i = 0; i < layers.Length; i++) layers[i] = new List<CombineInstance>();
            float cursor = 0f;
            int previous = 0;
            for (int i = 0; i < name.Length; i++)
            {
                int codepoint = char.ConvertToUtf32(name, i);
                if (char.IsHighSurrogate(name[i])) i++;
                if (codepoint == ' ') { cursor += GlyphLibrary.SpaceAdvance; previous = 0; continue; }
                if (!GlyphLibrary.TryGetGlyph(codepoint, out var glyph) || glyph.Mesh == null) continue;
                cursor += GlyphLibrary.PairOffset(previous, codepoint);
                var matrix = Matrix4x4.Scale(new Vector3(.88f, 1f, 1f)) * Matrix4x4.Translate(Vector3.right * cursor);
                for (int layer = 0; layer < layers.Length && layer < glyph.Mesh.subMeshCount; layer++)
                    layers[layer].Add(new CombineInstance { mesh = glyph.Mesh, subMeshIndex = layer, transform = matrix });
                cursor += glyph.Advance + .02f;
                previous = codepoint;
            }
            var combined = new List<CombineInstance>();
            for (int i = 0; i < layers.Length; i++)
            {
                submeshes[i].Clear();
                if (layers[i].Count == 0) continue;
                submeshes[i].CombineMeshes(layers[i].ToArray(), true, true);
                combined.Add(new CombineInstance { mesh = submeshes[i], transform = Matrix4x4.identity });
            }
            if (combined.Count == 0) { letterRenderer.enabled = false; return; }
            letters.CombineMeshes(combined.ToArray(), false, false);
            letters.RecalculateBounds();
            var bounds = letters.bounds;
            float scale = Mathf.Min(TextHeight, TextWidth / Mathf.Max(.001f, bounds.size.x), TextHeight / Mathf.Max(.001f, bounds.size.y));
            geometry.transform.localScale = Vector3.one * scale;
            geometry.transform.localPosition = new Vector3(-bounds.center.x * scale, -bounds.center.y * scale, 0f);
        }

        void OnDestroy()
        {
            Release(letters);
            foreach (var mesh in submeshes) Release(mesh);
        }

        static void Release(Mesh mesh)
        {
            if (mesh == null) return;
            if (Application.isPlaying) Destroy(mesh);
            else DestroyImmediate(mesh);
        }
    }
}
