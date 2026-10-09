using UnityEngine;
using UnityEngine.Rendering;

namespace PirateSlop.World
{
    public sealed class SeagullVisual : MonoBehaviour
    {
        public const float FootHeight = .225f;
        static Mesh sphere, beak, tail, feetMesh;
        static readonly Mesh[] inner = new Mesh[2], outer = new Mesh[2];
        static Material white, dark, feetMaterial;
        readonly Transform[] shoulders = new Transform[2], tips = new Transform[2];
        Transform body, head, feet;
        float phase, frequency;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetMeshes()
        {
            foreach (var mesh in new[] { sphere, beak, tail, feetMesh, inner[0], inner[1], outer[0], outer[1] })
                if (mesh != null) Destroy(mesh);
            sphere = beak = tail = feetMesh = null;
            inner[0] = inner[1] = outer[0] = outer[1] = null;
            white = dark = feetMaterial = null;
        }

        public static SeagullVisual Create(Transform parent, int seed)
        {
            var bird = new GameObject("Seagull").AddComponent<SeagullVisual>();
            bird.transform.SetParent(parent, false);
            bird.Initialize(seed);
            return bird;
        }

        public void Initialize(int seed)
        {
            if (body != null) return;
            EnsureMeshes();
            phase = (seed & 1023) * .037f;
            frequency = 1.75f + (seed & 15) * .012f;
            body = Pivot(transform, "BodyRig");
            Part(body, "Body", sphere, Vector3.zero, new Vector3(.23f, .2f, .65f), white);
            Part(body, "Tail", tail, new Vector3(0, 0, -.32f), Vector3.one, dark);
            head = Pivot(body, "HeadRig");
            head.localPosition = new Vector3(0, .09f, .25f);
            Part(head, "Head", sphere, new Vector3(0, 0, .05f), Vector3.one * .19f, white);
            Part(head, "Beak", beak, new Vector3(0, -.03f, .13f), Vector3.one, dark);
            for (int side = 0; side < 2; side++)
            {
                float sign = side == 0 ? -1 : 1;
                shoulders[side] = Pivot(body, side == 0 ? "LeftWing" : "RightWing");
                shoulders[side].localPosition = new Vector3(sign * .07f, .02f, 0);
                Part(shoulders[side], "InnerWing", inner[side], Vector3.zero, Vector3.one, white);
                tips[side] = Pivot(shoulders[side], "Wingtip");
                tips[side].localPosition = new Vector3(sign * .45f, 0, 0);
                Part(tips[side], "OuterWing", outer[side], Vector3.zero, Vector3.one, dark);
            }
            feet = Part(transform, "Feet", feetMesh, Vector3.zero, Vector3.one, feetMaterial);
            Animate(0, .6f, 0, 0);
        }

        public void Animate(float time, float power, float fold, float feetBlend, float flare = 0, bool resting = false)
        {
            float beat = time * frequency * Mathf.PI * 2 + phase + Mathf.Sin(time * .43f + phase) * .12f;
            fold = Mathf.Clamp01(fold);
            power = Mathf.Clamp01(power);
            for (int side = 0; side < 2; side++)
            {
                float sign = side == 0 ? -1 : 1;
                float wingBeat = beat + sign * .035f;
                float stroke = (Mathf.Sin(wingBeat) + Mathf.Sin(wingBeat * 2f) * .14f) * .88f;
                float shoulderAngle = 5f + stroke * 25f * power + Mathf.Sin(time * .7f + phase) * 1.3f * (1f - power);
                float tipAngle = Mathf.Sin(wingBeat - 1.05f) * 11f * power;
                shoulders[side].localRotation = Quaternion.Euler(0, sign * 78 * fold, sign * Mathf.Lerp(shoulderAngle, -6, fold));
                tips[side].localRotation = Quaternion.Euler(0, sign * 30 * fold, sign * tipAngle * (1 - fold));
                tips[side].localScale = new Vector3(Mathf.Lerp(1, .6f, fold), 1, Mathf.Lerp(1, .85f, fold));
            }
            body.localRotation = Quaternion.Euler(-flare * 13, 0, 0);
            body.localPosition = Vector3.up * Mathf.Lerp(Mathf.Sin(beat - .45f) * .006f * power, Mathf.Sin(time * 1.9f + phase) * .003f, fold);
            head.localRotation = Quaternion.Euler(Mathf.Sin(time * .37f + phase) * 5 * fold,
                Mathf.Sin(time * .31f + phase) * 23 * fold, 0);
            feet.gameObject.SetActive(feetBlend > .01f);
            feet.localScale = new Vector3(1, Mathf.Clamp01(feetBlend), 1);
        }

        public static float FlightPower(float time, int seed)
        {
            float cycle = Mathf.Repeat(time + (seed & 31) * .27f, 9.4f);
            return .06f + .74f * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(2.1f, 3.2f, cycle)))
                + .74f * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(7.9f, 9.4f, cycle));
        }

        static Transform Pivot(Transform parent, string name)
        {
            var value = new GameObject(name).transform;
            value.SetParent(parent, false);
            return value;
        }

        static Transform Part(Transform parent, string name, Mesh mesh, Vector3 position, Vector3 scale, Material material)
        {
            var value = Pivot(parent, name);
            value.localPosition = position;
            value.localScale = scale;
            value.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = value.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return value;
        }

        static Mesh Surface(string name, Vector3[] vertices, int[] triangles)
        {
            var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static Mesh Wing(string name, Vector3[] polygon, float sign)
        {
            var vertices = new Vector3[polygon.Length * 2];
            var triangles = new System.Collections.Generic.List<int>();
            for (int i = 0; i < polygon.Length; i++)
            {
                vertices[i] = new Vector3(polygon[i].x * sign, .008f, polygon[i].z);
                vertices[i + polygon.Length] = new Vector3(polygon[i].x * sign, -.008f, polygon[i].z);
            }
            for (int i = 1; i < polygon.Length - 1; i++)
            {
                triangles.AddRange(new[] { 0, i, i + 1, polygon.Length, polygon.Length + i + 1, polygon.Length + i });
            }
            for (int i = 0; i < polygon.Length; i++)
            {
                int j = (i + 1) % polygon.Length;
                triangles.AddRange(new[] { i, j, polygon.Length + i, j, polygon.Length + j, polygon.Length + i });
            }
            if (sign < 0)
                for (int i = 0; i < triangles.Count; i += 3) (triangles[i], triangles[i + 2]) = (triangles[i + 2], triangles[i]);
            return Surface(name, vertices, triangles.ToArray());
        }

        static void EnsureMeshes()
        {
            if (sphere != null) return;
            white = Resources.Load<Material>("World/SeagullWhite");
            dark = Resources.Load<Material>("World/SeagullDark");
            feetMaterial = Resources.Load<Material>("World/SeagullFeet");
            if (white == null || dark == null || feetMaterial == null) throw new System.InvalidOperationException("Prepare the seagull assets first.");
            var vertices = new System.Collections.Generic.List<Vector3>();
            var indices = new System.Collections.Generic.List<int>();
            for (int y = 0; y <= 8; y++)
                for (int x = 0; x <= 12; x++)
                {
                    float latitude = y * Mathf.PI / 8;
                    float longitude = x * Mathf.PI * 2 / 12;
                    vertices.Add(new Vector3(Mathf.Sin(latitude) * Mathf.Cos(longitude), Mathf.Cos(latitude), Mathf.Sin(latitude) * Mathf.Sin(longitude)) * .5f);
                }
            for (int y = 0; y < 8; y++) for (int x = 0; x < 12; x++)
            {
                int i = y * 13 + x;
                indices.AddRange(new[] { i, i + 1, i + 13, i + 1, i + 14, i + 13 });
            }
            sphere = Surface("SeagullBody", vertices.ToArray(), indices.ToArray());
            var normals = sphere.vertices;
            for (int i = 0; i < normals.Length; i++) normals[i] = normals[i].normalized;
            sphere.normals = normals;
            beak = Surface("SeagullBeak", new[] { new Vector3(-.025f, -.02f, 0), new Vector3(.025f, -.02f, 0), new Vector3(0, .025f, 0), new Vector3(0, -.005f, .18f) }, new[] { 0, 2, 1, 0, 1, 3, 1, 2, 3, 2, 0, 3 });
            tail = Wing("SeagullTail", new[] { new Vector3(-.11f, 0, 0), new Vector3(.11f, 0, 0), new Vector3(.14f, 0, -.22f), new Vector3(-.14f, 0, -.22f) }, 1);
            for (int side = 0; side < 2; side++)
            {
                float sign = side == 0 ? -1 : 1;
                inner[side] = Wing("SeagullInnerWing", new[] { Vector3.zero, new Vector3(.45f, 0, .16f), new Vector3(.45f, 0, -.2f), new Vector3(.25f, 0, -.23f) }, sign);
                outer[side] = Wing("SeagullWingtip", new[] { new Vector3(0, 0, .16f), new Vector3(.6f, 0, -.02f), new Vector3(.45f, 0, -.2f), new Vector3(0, 0, -.2f) }, sign);
            }
            vertices.Clear(); indices.Clear();
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? -.065f : .065f;
                int i = vertices.Count;
                vertices.AddRange(new[] { new Vector3(x - .012f, -.07f, .08f), new Vector3(x + .012f, -.07f, .08f),
                    new Vector3(x + .012f, -.212f, .08f), new Vector3(x - .012f, -.212f, .08f),
                    new Vector3(x, -.225f, .08f), new Vector3(x - .045f, -.225f, .19f), new Vector3(x + .045f, -.225f, .19f) });
                indices.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3, i + 2, i + 1, i, i + 3, i + 2, i, i + 4, i + 5, i + 6, i + 6, i + 5, i + 4 });
            }
            var footVertices = new Vector3[indices.Count];
            var footIndices = new int[indices.Count];
            for (int i = 0; i < indices.Count; i++) { footVertices[i] = vertices[indices[i]]; footIndices[i] = i; }
            feetMesh = Surface("SeagullFeet", footVertices, footIndices);
        }
    }
}
