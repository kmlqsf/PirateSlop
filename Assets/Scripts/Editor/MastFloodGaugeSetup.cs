using System;
using System.Collections.Generic;
using PirateSlop.Ships;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace PirateSlop.EditorTools
{
    public static class MastFloodGaugeSetup
    {
        const string MeshPath = "Assets/Models/Ships/ShipV3/RuntimeMeshes/Fixtures/MastFloodGauge.asset";
        const string MaterialPath = "Assets/Models/Ships/ShipV3/Materials/MastFloodGauge.mat";
        const string PrefabPath = "Assets/Resources/Ships/MastFloodGauge.prefab";
        const float Height = 5.6f;
        static readonly Color32 Back = new(68, 54, 38, 255);
        static readonly Color32 Patina = new(97, 82, 52, 255);
        static readonly Color32 Brass = new(151, 112, 64, 255);
        static readonly Color32 Lip = new(200, 157, 94, 255);
        static readonly Color32 Seal = new(42, 38, 32, 255);

        [MenuItem("PirateSlop/Ships/Rebuild Mast Flood Gauge")]
        public static void Bake()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Bake the gauge in Edit Mode.");
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/MastFloodGauge.shader");
            if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Mast flood gauge shader is unavailable.");
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (mesh == null) mesh = new Mesh { name = "MastFloodGauge" };
            new Fitting().Write(mesh);
            if (!AssetDatabase.Contains(mesh)) AssetDatabase.CreateAsset(mesh, MeshPath);
            else { EditorUtility.SetDirty(mesh); AssetDatabase.SaveAssetIfDirty(mesh); }
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "MastFloodGauge", enableInstancing = true };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var unit = EditorUtility.CreateGameObjectWithHideFlags("MastFloodGauge", HideFlags.HideAndDontSave);
                SceneManager.MoveGameObjectToScene(unit, preview);
                unit.hideFlags = HideFlags.None;
                unit.transform.hideFlags = HideFlags.None;
                unit.transform.localPosition = new Vector3(0f, 7.54f, -2.035f);
                unit.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = unit.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                var gauge = unit.AddComponent<MastFloodGauge>();
                gauge.WaterLoop = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/MastFloodGauge/MastWaterFill.wav");
                gauge.BloodLoop = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/MastFloodGauge/MastBloodFill.wav");
                if (gauge.WaterLoop == null || gauge.BloodLoop == null) throw new InvalidOperationException("Missing mast liquid audio.");
                if (PrefabUtility.SaveAsPrefabAsset(unit, PrefabPath) == null)
                    throw new InvalidOperationException("Could not save " + PrefabPath);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        sealed class Fitting
        {
            readonly List<Vector3> positions = new();
            readonly List<Color32> colors = new();
            readonly List<Vector2> uv = new();
            readonly List<int> indices = new();

            public void Write(Mesh mesh)
            {
                int[] outer = Rim(.356f, 0f, .138f, -.095f, Back);
                outer = Join(outer, Rim(.356f, 0f, .138f, .006f, Back));
                outer = Join(outer, Rim(.342f, .018f, .125f, .044f, Brass));
                outer = Join(outer, Rim(.325f, .035f, .118f, .052f, Lip));
                outer = Join(outer, Rim(.286f, .123f, .096f, .050f, Brass));
                outer = Join(outer, Rim(.274f, .145f, .088f, .065f, Lip));
                outer = Join(outer, Rim(.260f, .164f, .083f, .034f, Patina));
                Join(outer, Rim(.251f, .173f, .076f, .026f, Seal));
                Glass();
                Band(.21f);
                Band(Height - .21f);
                for (int i = 0; i < 5; i++)
                {
                    float h = Mathf.Lerp(.16f, Height - .16f, i / 4f);
                    Rivet(-.306f, h);
                    Rivet(.306f, h);
                }
                Rivet(0f, .085f);
                Rivet(0f, Height - .085f);
                mesh.Clear();
                mesh.SetVertices(positions);
                mesh.SetColors(colors);
                mesh.SetUVs(0, uv);
                mesh.SetTriangles(indices, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
            }

            static Vector3 OnStem(float arc, float h, float depth)
            {
                float angle = arc / .30f + .075f;
                float radius = Mathf.Lerp(.323f, .288f, h / Height) + depth;
                return new Vector3(Mathf.Sin(angle) * radius, h, -h * .025f - Mathf.Cos(angle) * radius);
            }

            int Vertex(Vector3 point, Color32 color, Vector2 tex = default)
            {
                positions.Add(point);
                colors.Add(color);
                uv.Add(tex);
                return positions.Count - 1;
            }

            int[] Rim(float w, float inset, float corner, float depth, Color32 color)
            {
                var loop = new List<int>(40);
                float top = Height - inset;
                void Corner(float x, float y, float from)
                {
                    for (int i = 0; i <= 4; i++)
                    {
                        float a = (from + 90f * i / 4f) * Mathf.Deg2Rad;
                        loop.Add(Vertex(OnStem(x + corner * Mathf.Cos(a), y + corner * Mathf.Sin(a), depth), color));
                    }
                }
                void Across(float from, float to, float h)
                {
                    for (int i = 1; i < 9; i++)
                        loop.Add(Vertex(OnStem(Mathf.Lerp(from, to, i / 9f), h, depth), color));
                }
                Corner(w - corner, inset + corner, -90f);
                Corner(w - corner, top - corner, 0f);
                Across(w - corner, corner - w, top);
                Corner(corner - w, top - corner, 90f);
                Corner(corner - w, inset + corner, 180f);
                Across(corner - w, w - corner, inset);
                return loop.ToArray();
            }

            int[] Join(int[] outer, int[] inner)
            {
                for (int i = 0; i < outer.Length; i++)
                {
                    int j = (i + 1) % outer.Length;
                    Quad(outer[i], inner[i], inner[j], outer[j]);
                }
                return inner;
            }

            void Quad(int a, int b, int c, int d)
            {
                indices.Add(a); indices.Add(b); indices.Add(c);
                indices.Add(a); indices.Add(c); indices.Add(d);
            }

            void Glass()
            {
                const int columns = 32;
                const float width = .258f, inset = .166f, corner = .08f;
                int start = positions.Count;
                for (int i = 0; i <= columns; i++)
                {
                    float u = i / (float)columns;
                    float arc = (2f * u - 1f) * width;
                    float end = Mathf.Max(0f, Mathf.Abs(arc) - width + corner);
                    float offset = corner - Mathf.Sqrt(Mathf.Max(0f, corner * corner - end * end));
                    float depth = .027f + .009f * (1f - Mathf.Pow(2f * u - 1f, 2f));
                    float v = offset / (Height - 2f * inset);
                    Vertex(OnStem(arc, inset + offset, depth), new Color32(255, 255, 255, 0), new Vector2(u, v));
                    Vertex(OnStem(arc, Height - inset - offset, depth), new Color32(255, 255, 255, 0), new Vector2(u, 1f - v));
                    if (i == 0) continue;
                    int previous = start + (i - 1) * 2;
                    Quad(previous, previous + 1, previous + 3, previous + 2);
                }
            }

            void Band(float h)
            {
                const int segments = 36;
                int[] Row(float offset, float depth, Color32 color)
                {
                    var row = new int[segments];
                    for (int i = 0; i < segments; i++)
                        row[i] = Vertex(OnStem(i * 2f * Mathf.PI * .30f / segments, h + offset, depth), color);
                    return row;
                }
                var ring = Row(-.072f, -.066f, Back);
                ring = Join(ring, Row(-.064f, -.005f, Back));
                ring = Join(ring, Row(-.046f, .013f, Brass));
                ring = Join(ring, Row(.046f, .013f, Patina));
                ring = Join(ring, Row(.064f, -.005f, Brass));
                Join(ring, Row(.072f, -.066f, Back));
            }

            void Rivet(float arc, float h)
            {
                const int sides = 10;
                float angle = arc / .30f + .075f;
                var axis = new Vector3(Mathf.Sin(angle), 0f, -Mathf.Cos(angle));
                var tangent = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var center = OnStem(arc, h, .050f);
                int[] Circle(float radius, float outward, Color32 color)
                {
                    var row = new int[sides];
                    for (int i = 0; i < sides; i++)
                    {
                        float a = i * 2f * Mathf.PI / sides;
                        row[i] = Vertex(center + axis * outward + radius * (Mathf.Cos(a) * tangent + Mathf.Sin(a) * Vector3.up), color);
                    }
                    return row;
                }
                var rim = Circle(.032f, 0f, Back);
                rim = Join(rim, Circle(.029f, .010f, Patina));
                rim = Join(rim, Circle(.020f, .023f, Lip));
                int tip = Vertex(center + axis * .028f, Lip);
                for (int i = 0; i < sides; i++)
                {
                    indices.Add(rim[i]); indices.Add(tip); indices.Add(rim[(i + 1) % sides]);
                }
            }
        }
    }
}
