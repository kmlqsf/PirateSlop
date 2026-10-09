using UnityEngine;
using UnityEngine.Rendering;

namespace PirateSlop
{
    public sealed partial class StormRainController
    {
        const int WetCapacity = 2048;
        public const float WetWoodLifetime = 30;
        struct WetSpot
        {
            public Contact Contact;
            public Vector3 Tangent;
            public float Radius, Stretch, Seed;
        }
        readonly WetSpot[] wetSpots = new WetSpot[WetCapacity];
        readonly Vector3[] wetVertices = new Vector3[WetCapacity * 4], wetNormals = new Vector3[WetCapacity * 4];
        readonly Vector2[] wetUV = new Vector2[WetCapacity * 4];
        readonly Color[] wetColors = new Color[WetCapacity * 4];
        Mesh wetMesh;
        GameObject wetObject;
        MeshRenderer wetRenderer;
        int wetCursor;
        int wetWarmFrames;
        public int ActiveWetSpots { get; private set; }

        void InitializeWetWood()
        {
            var material = Resources.Load<Material>("Storm/RainWetWood");
            if (material == null) return;
            wetObject = new GameObject("Rain persistent wet wood") { hideFlags = HideFlags.HideAndDontSave };
            wetObject.transform.SetParent(transform, false);
            wetObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            wetObject.transform.localScale = Vector3.one;
            wetMesh = new Mesh { name = "Rain wet wood pool", hideFlags = HideFlags.HideAndDontSave };
            wetMesh.MarkDynamic();
            var triangles = new int[WetCapacity * 6];
            for (int i = 0; i < WetCapacity; i++)
            {
                int v = i * 4, t = i * 6;
                wetUV[v] = Vector2.zero; wetUV[v + 1] = Vector2.right;
                wetUV[v + 2] = Vector2.one; wetUV[v + 3] = Vector2.up;
                triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
                triangles[t + 3] = v; triangles[t + 4] = v + 3; triangles[t + 5] = v + 2;
            }
            wetMesh.vertices = wetVertices; wetMesh.normals = wetNormals; wetMesh.colors = wetColors;
            wetMesh.uv = wetUV; wetMesh.triangles = triangles;
            wetObject.AddComponent<MeshFilter>().sharedMesh = wetMesh;
            wetRenderer = wetObject.AddComponent<MeshRenderer>();
            wetRenderer.sharedMaterial = material;
            wetRenderer.shadowCastingMode = ShadowCastingMode.Off;
            wetRenderer.receiveShadows = false;
            wetRenderer.lightProbeUsage = LightProbeUsage.Off;
            wetRenderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            wetRenderer.enabled = false;
        }

        void AddWetWood(Contact contact)
        {
            if (wetMesh == null) return;
            var normal = contact.Normal;
            var tangent = Vector3.Cross(normal, Vector3.forward).normalized;
            if (tangent.sqrMagnitude < .1f) tangent = Vector3.right;
            var rotation = Quaternion.AngleAxis(Next01() * 360, normal);
            var spot = new WetSpot { Contact = contact, Tangent = rotation * tangent,
                Radius = .055f + Next01() * .055f, Stretch = 1.1f + Next01() * .6f, Seed = Next01() };
            for (int i = 0; i < wetSpots.Length; i++)
            {
                ref var old = ref wetSpots[i];
                if (!old.Contact.Active || old.Contact.Collider != contact.Collider ||
                    old.Contact.Section != contact.Section || old.Contact.Fragments != contact.Fragments ||
                    old.Contact.SectionState != contact.SectionState ||
                    (old.Contact.Point - contact.Point).sqrMagnitude > .0036f || Vector3.Dot(old.Contact.Normal, normal) < .9f) continue;
                old.Contact.Started = contact.Started;
                old.Radius = Mathf.Min(.16f, old.Radius + .015f);
                return;
            }
            wetSpots[wetCursor++ % WetCapacity] = spot;
        }

        void UpdateWetWood()
        {
            if (wetMesh == null) return;
            if (!downpour) { wetRenderer.enabled = false; return; }
            if (ActiveWetSpots == 0 && wetCursor == 0 && wetWarmFrames >= 8) { wetRenderer.enabled = false; return; }
            System.Array.Clear(wetColors, 0, wetColors.Length);
            ActiveWetSpots = 0;
            var bounds = new Bounds(viewer.transform.position, Vector3.one);
            for (int i = 0; i < wetSpots.Length; i++)
            {
                ref var spot = ref wetSpots[i];
                ref var contact = ref spot.Contact;
                if (!contact.Active) continue;
                float age = Time.time - contact.Started;
                if (age >= WetWoodLifetime || contact.Collider == null || !contact.Collider.enabled ||
                    !contact.Collider.gameObject.activeInHierarchy || contact.Section != null &&
                    (contact.Section.RemovedFragments != contact.Fragments || contact.Section.State != contact.SectionState))
                { contact.Active = false; continue; }
                ActiveWetSpots++;
                var pose = contact.Collider.transform;
                var normal = pose.localToWorldMatrix.inverse.transpose.MultiplyVector(contact.Normal).normalized;
                var point = pose.TransformPoint(contact.Point) + normal * .006f;
                var tangent = pose.TransformDirection(spot.Tangent).normalized;
                var across = Vector3.Cross(normal, tangent).normalized;
                float radius = spot.Radius * Mathf.Lerp(.65f, 1, Mathf.Clamp01(age / .2f));
                var right = tangent * radius * spot.Stretch;
                var up = across * radius;
                int first = i * 4;
                wetVertices[first] = point - right - up; wetVertices[first + 1] = point + right - up;
                wetVertices[first + 2] = point + right + up; wetVertices[first + 3] = point - right + up;
                float fade = 1 - Mathf.SmoothStep(0, 1, age / WetWoodLifetime);
                var color = new Color(spot.Seed, 0, 0, fade * .48f);
                for (int v = 0; v < 4; v++) { wetNormals[first + v] = normal; wetColors[first + v] = color; }
                bounds.Encapsulate(point - Vector3.one * .3f); bounds.Encapsulate(point + Vector3.one * .3f);
            }
            bool warming = wetWarmFrames++ < 8;
            if (warming && ActiveWetSpots == 0)
            {
                var point = viewer.transform.position + viewer.transform.forward * 2 + viewer.transform.up * .3f;
                var right = viewer.transform.right * .01f; var up = viewer.transform.up * .01f;
                wetVertices[0] = point - right - up; wetVertices[1] = point + right - up;
                wetVertices[2] = point + right + up; wetVertices[3] = point - right + up;
                bounds.Encapsulate(point);
            }
            wetRenderer.enabled = ActiveWetSpots > 0 || warming;
            wetMesh.vertices = wetVertices; wetMesh.normals = wetNormals; wetMesh.colors = wetColors;
            wetMesh.bounds = bounds;
        }

        void DisposeWetWood()
        {
            if (wetObject != null) Destroy(wetObject);
            if (wetMesh != null) Destroy(wetMesh);
        }
    }
}
