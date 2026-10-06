using System.Collections.Generic;
using PirateSlop.Ships;
using UnityEngine;
using UnityEngine.Rendering;

namespace PirateSlop
{
    public sealed class WaterSplashContacts : MonoBehaviour
    {
        const int ProbeCapacity = 256, MarkCapacity = 192, RayBudget = 128, SurfaceRayBudget = 128, Grid = 4;
        readonly Dictionary<uint, int> probes = new(ProbeCapacity);
        readonly uint[] seeds = new uint[ProbeCapacity];
        readonly Vector3[] previous = new Vector3[ProbeCapacity];
        readonly float[] deadlines = new float[ProbeCapacity], sizes = new float[ProbeCapacity];
        readonly Transform[] marks = new Transform[MarkCapacity];
        readonly Mesh[] meshes = new Mesh[MarkCapacity];
        readonly Renderer[] renderers = new Renderer[MarkCapacity];
        readonly Collider[] surfaces = new Collider[MarkCapacity];
        readonly ShipDamageSection[] sections = new ShipDamageSection[MarkCapacity];
        readonly ulong[] fragments = new ulong[MarkCapacity];
        readonly ShipSectionState[] states = new ShipSectionState[MarkCapacity];
        readonly float[] created = new float[MarkCapacity];
        readonly Vector3[] vertices = new Vector3[(Grid + 1) * (Grid + 1)];
        readonly Vector2[] uv = new Vector2[(Grid + 1) * (Grid + 1)];
        readonly bool[] valid = new bool[(Grid + 1) * (Grid + 1)];
        readonly List<int> triangles = new(Grid * Grid * 6);
        readonly Vector2[] lens = new Vector2[12];
        readonly float[] lensAt = new float[12], lensSize = new float[12];
        Material material;
        MaterialPropertyBlock block;
        Texture2D lensTexture;
        Camera focus;
        int nextProbe, nextMark, nextLens, sampleCursor, surfaceRays;
        float sampleAt, markAt, markTokens = 16f, lensCooldown;

        void Awake()
        {
            var shader = Resources.Load<Shader>("EnvironmentTest/WaterWetMark");
            if (shader != null) material = new Material(shader) { name = "Water wet marks", hideFlags = HideFlags.HideAndDontSave };
            block = new MaterialPropertyBlock();
            for (int i = 0; i < lensAt.Length; i++) lensAt[i] = -10f;
        }

        public void Track(Vector3 position, Vector3 velocity, float life, float size, uint seed)
        {
            if (focus == null || !focus.isActiveAndEnabled) focus = Camera.main;
            if (focus != null && (position - focus.transform.position).sqrMagnitude > 8100f) return;
            int index = nextProbe++ % ProbeCapacity;
            probes.Remove(seeds[index]);
            seeds[index] = seed;
            previous[index] = position;
            sizes[index] = size;
            deadlines[index] = Time.time + life + .2f;
            probes[seed] = index;
        }

        public bool Sample(ParticleSystem.Particle[] particles, int count)
        {
            if (Time.time < sampleAt || !isActiveAndEnabled || count == 0) return false;
            sampleAt = Time.time + .033f;
            surfaceRays = 0;
            if (focus == null || !focus.isActiveAndEnabled) focus = Camera.main;
            bool changed = false;
            int rays = 0, visited = 0;
            while (visited < count && rays < RayBudget)
            {
                int index = (sampleCursor + visited++) % count;
                var particle = particles[index];
                if (!probes.TryGetValue(particle.randomSeed, out int probe)) continue;
                if (particle.remainingLifetime <= 0f || Time.time >= deadlines[probe]) { probes.Remove(particle.randomSeed); continue; }
                Vector3 start = previous[probe], delta = particle.position - start;
                previous[probe] = particle.position;
                float distance = delta.magnitude;
                if (distance < .001f) continue;
                rays++;
                bool contact = Physics.Raycast(start, delta / distance, out var hit, distance, ~0, QueryTriggerInteraction.Ignore);
                Vector3 end = contact ? hit.point : particle.position;
                if (focus != null && Time.time >= lensCooldown)
                {
                    Vector3 segment = end - start;
                    float fraction = segment.sqrMagnitude > .000001f ? Mathf.Clamp01(Vector3.Dot(focus.transform.position - start, segment) / segment.sqrMagnitude) : 0f;
                    if ((start + segment * fraction - focus.transform.position).sqrMagnitude < .09f) WetLens();
                    else if (contact && hit.collider.GetComponentInParent<AdvancedPlayerController>() is { IsLocal: true }) WetLens();
                }
                if (!contact) continue;
                if (hit.collider.GetComponentInParent<ShipController>() != null) Mark(hit, sizes[probe]);
                probes.Remove(particle.randomSeed);
                particle.remainingLifetime = 0f;
                particles[index] = particle;
                changed = true;
            }
            sampleCursor = (sampleCursor + visited) % count;
            return changed;
        }

        void Mark(RaycastHit hit, float size)
        {
            if (material == null) return;
            if (surfaceRays + vertices.Length > SurfaceRayBudget) return;
            markTokens = Mathf.Min(16f, markTokens + Mathf.Max(0f, Time.time - markAt) * 120f);
            markAt = Time.time;
            if (markTokens < 1f) return;
            markTokens -= 1f;
            var section = ShipV3CollisionBatch.ResolveSection(hit.collider, hit.point);
            if (section != null && section.State == ShipSectionState.Destroyed) return;
            Transform anchor = hit.collider.transform;
            for (int i = 0; i < MarkCapacity; i++)
                if (marks[i] != null && marks[i].gameObject.activeInHierarchy && surfaces[i] == hit.collider &&
                    (anchor.TransformPoint(meshes[i].bounds.center) - hit.point).sqrMagnitude < .004f)
                { created[i] = Time.time; return; }
            int index = nextMark++ % MarkCapacity;
            if (marks[index] == null)
            {
                if (meshes[index] != null) Destroy(meshes[index]);
                var go = new GameObject("WaterWetMark");
                meshes[index] = new Mesh { name = "Wet surface patch" };
                go.AddComponent<MeshFilter>().sharedMesh = meshes[index];
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = true;
                marks[index] = go.transform;
                renderers[index] = renderer;
            }
            var mark = marks[index];
            mark.SetParent(anchor, false);
            mark.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            mark.localScale = Vector3.one;
            mark.gameObject.layer = hit.collider.gameObject.layer;
            float diameter = hit.normal.y > .65f ? Mathf.Clamp(size * 5f, .10f, .30f) : Mathf.Clamp(size * 2f, .045f, .12f);
            mark.gameObject.SetActive(Project(meshes[index], hit, anchor, section, diameter));
            surfaces[index] = hit.collider;
            sections[index] = section;
            fragments[index] = section != null ? section.RemovedFragments : 0;
            states[index] = section != null ? section.State : default;
            created[index] = Time.time;
            block.Clear();
            block.SetFloat("_Opacity", 0f);
            renderers[index].SetPropertyBlock(block);
        }

        bool Project(Mesh mesh, RaycastHit center, Transform anchor, ShipDamageSection section, float diameter)
        {
            Vector3 tangent = Vector3.Cross(center.normal, Vector3.up).normalized;
            if (tangent.sqrMagnitude < .001f) tangent = Vector3.Cross(center.normal, Vector3.right).normalized;
            Vector3 across = Vector3.Cross(center.normal, tangent).normalized;
            triangles.Clear();
            for (int y = 0; y <= Grid; y++)
                for (int x = 0; x <= Grid; x++)
                {
                    int index = y * (Grid + 1) + x;
                    uv[index] = new Vector2((float)x / Grid, (float)y / Grid);
                    vertices[index] = anchor.InverseTransformPoint(center.point + center.normal * .003f);
                    Vector3 point = center.point + (tangent * (uv[index].x - .5f) + across * (uv[index].y - .5f)) * diameter;
                    valid[index] = false;
                    surfaceRays++;
                    if (!center.collider.Raycast(new Ray(point + center.normal * .06f, -center.normal), out var hit, .12f) ||
                        Vector3.Dot(hit.normal, center.normal) < .25f || section != null && ShipV3CollisionBatch.ResolveSection(hit.collider, hit.point) != section) continue;
                    vertices[index] = anchor.InverseTransformPoint(hit.point + hit.normal * .003f);
                    valid[index] = true;
                }
            for (int y = 0; y < Grid; y++)
                for (int x = 0; x < Grid; x++)
                {
                    int a = y * (Grid + 1) + x, b = a + 1, c = a + Grid + 1, d = c + 1;
                    if (valid[a] && valid[b] && valid[c]) { triangles.Add(a); triangles.Add(b); triangles.Add(c); }
                    if (valid[b] && valid[c] && valid[d]) { triangles.Add(b); triangles.Add(d); triangles.Add(c); }
                }
            mesh.Clear();
            if (triangles.Count == 0) return false;
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return true;
        }

        void Update()
        {
            for (int i = 0; i < MarkCapacity; i++)
            {
                if (marks[i] == null || !marks[i].gameObject.activeSelf) continue;
                float age = Time.time - created[i];
                var section = sections[i];
                if (age >= 3.5f || surfaces[i] == null || !surfaces[i].enabled || !surfaces[i].gameObject.activeInHierarchy ||
                    section != null && (section.RemovedFragments != fragments[i] || section.State != states[i]))
                { marks[i].gameObject.SetActive(false); continue; }
                block.Clear();
                block.SetFloat("_Opacity", Mathf.SmoothStep(0f, 1f, age / .06f) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f, 3.5f, age))));
                renderers[i].SetPropertyBlock(block);
            }
        }

        void WetLens()
        {
            lensCooldown = Time.time + .12f;
            for (int i = 0; i < 4; i++)
            {
                int index = nextLens++ % lens.Length;
                lens[index] = new Vector2(Random.Range(.08f, .92f), Random.Range(.08f, .88f));
                lensAt[index] = Time.time;
                lensSize[index] = Random.Range(.012f, .027f);
            }
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || focus == null || !focus.isActiveAndEnabled) return;
            bool visible = false;
            for (int i = 0; i < lens.Length; i++) visible |= Time.time - lensAt[i] < 1.2f;
            if (!visible) return;
            if (lensTexture == null)
            {
                lensTexture = new Texture2D(64, 64, TextureFormat.RGBA32, false) { name = "Water lens droplet", hideFlags = HideFlags.HideAndDontSave };
                var pixels = new Color[4096];
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 64; x++)
                    {
                        float radius = new Vector2((x + .5f) / 32f - 1f, (y + .5f) / 32f - 1f).magnitude;
                        float edge = Mathf.Exp(-Mathf.Pow((radius - .78f) * 14f, 2f));
                        float glint = Mathf.Exp(-new Vector2((x - 21f) / 8f, (y - 19f) / 5f).sqrMagnitude);
                        pixels[y * 64 + x] = new Color(.55f + glint * .4f, .66f + glint * .3f, .69f + glint * .3f,
                            (1f - Mathf.SmoothStep(.8f, 1f, radius)) * (.07f + edge * .18f + glint * .2f));
                    }
                lensTexture.SetPixels(pixels);
                lensTexture.Apply(false, true);
            }
            Color saved = GUI.color;
            for (int i = 0; i < lens.Length; i++)
            {
                float age = Time.time - lensAt[i];
                if (age >= 1.2f) continue;
                float size = Screen.height * lensSize[i];
                GUI.color = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, age / .06f) * (1f - Mathf.SmoothStep(.3f, 1.2f, age)));
                GUI.DrawTexture(new Rect(lens[i].x * Screen.width, (lens[i].y + age * .018f) * Screen.height, size, size * 1.35f), lensTexture);
            }
            GUI.color = saved;
        }

        void OnDisable()
        {
            probes.Clear();
            for (int i = 0; i < MarkCapacity; i++) if (marks[i] != null) marks[i].gameObject.SetActive(false);
            for (int i = 0; i < lensAt.Length; i++) lensAt[i] = -10f;
        }

        void OnDestroy()
        {
            for (int i = 0; i < MarkCapacity; i++)
            {
                if (marks[i] != null) Destroy(marks[i].gameObject);
                if (meshes[i] != null) Destroy(meshes[i]);
            }
            if (material != null) Destroy(material);
            if (lensTexture != null) Destroy(lensTexture);
        }
    }
}
