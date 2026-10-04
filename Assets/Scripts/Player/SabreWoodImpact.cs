using UnityEngine;
using UnityEngine.Rendering;
using PirateSlop.Networking;
using PirateSlop.Ships;
using System.Collections.Generic;

namespace PirateSlop
{
    public sealed class SabreWoodImpact : MonoBehaviour
    {
        public const float HoldSeconds = 30, FadeSeconds = 3;
        const int Capacity = 96;
        static SabreWoodImpact instance;
        readonly Transform[] marks = new Transform[Capacity];
        readonly Renderer[] renderers = new Renderer[Capacity];
        readonly Mesh[] meshes = new Mesh[Capacity];
        readonly ShipDamageSection[] sections = new ShipDamageSection[Capacity];
        readonly ulong[] fragments = new ulong[Capacity];
        readonly ShipSectionState[] states = new ShipSectionState[Capacity];
        readonly float[] created = new float[Capacity];
        readonly ParticleSystem[] bursts = new ParticleSystem[24];
        Material cutMaterial, chipMaterial;
        MaterialPropertyBlock block;
        int nextMark, nextBurst;

        static SabreWoodImpact Get()
        {
            if (instance != null) return instance;
            instance = new GameObject("SabreWoodImpactPool").AddComponent<SabreWoodImpact>();
            instance.cutMaterial = Resources.Load<Material>("SabreCut");
            instance.chipMaterial = Resources.Load<Material>("SabreWoodChip");
            instance.block = new MaterialPropertyBlock();
            return instance;
        }
        public static void Present(SabreWoodHit impact)
        {
            if (!Application.isPlaying || Application.isBatchMode) return;
            if (impact.Anchor == null && impact.ShipId > 0)
                foreach (var ship in NetworkShip.ActiveShips)
                    if (ship != null && ship.ParticipantId.Value == impact.ShipId) { impact.Anchor = ship.NetworkObject; break; }
            if (impact.Anchor == null) return;
            var destruction = impact.Anchor.GetComponent<ShipDestruction>();
            if (destruction == null) return;
            ShipDamageSection section = null;
            foreach (var candidate in destruction.Sections)
                if (candidate != null && candidate.SectionId == impact.SectionId) { section = candidate; break; }
            if (section == null || !section.gameObject.activeInHierarchy) return;
            Vector3 point = impact.Anchor.transform.TransformPoint(impact.Point);
            Vector3 normal = impact.Anchor.transform.TransformDirection(impact.Normal).normalized;
            Vector3 tangent = Vector3.ProjectOnPlane(impact.Anchor.transform.TransformDirection(impact.Tangent), normal).normalized;
            RaycastHit? contact = null;
            foreach (var hit in Physics.RaycastAll(point + normal * .07f, -normal, .15f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (ShipV3CollisionBatch.ResolveSection(hit.collider, hit.point) != section || Vector3.Dot(hit.normal, normal) < .8f) continue;
                if (!contact.HasValue || hit.distance < contact.Value.distance) contact = hit;
            }
            if (!contact.HasValue) return;
            var pool = Get();
            pool.Mark(contact.Value, section, tangent);
            pool.Chips(contact.Value.point, contact.Value.normal);
            GameAudio.Play(SoundCue.SabreWood, contact.Value.point);
        }
        void Mark(RaycastHit hit, ShipDamageSection section, Vector3 tangent)
        {
            if (cutMaterial == null) return;
            if (tangent.sqrMagnitude < .001f) tangent = Vector3.Cross(hit.normal, Vector3.up).normalized;
            if (tangent.sqrMagnitude < .001f) tangent = Vector3.Cross(hit.normal, Vector3.right).normalized;
            float length = Random.Range(.26f, .34f), width = Random.Range(.035f, .05f);
            Transform anchor = hit.collider.GetComponent<ShipV3CollisionBatch>() != null ? section.Intact != null ? section.Intact.transform : section.transform : hit.collider.transform;
            int index = nextMark++ % Capacity;
            if (marks[index] == null)
            {
                if (meshes[index] != null) Destroy(meshes[index]);
                var go = new GameObject();
                go.name = "SabreWoodCut";
                meshes[index] = new Mesh { name = "SabreCutSurface" };
                go.AddComponent<MeshFilter>().sharedMesh = meshes[index];
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = cutMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                marks[index] = go.transform; renderers[index] = renderer;
            }
            var mark = marks[index];
            mark.SetParent(anchor, false);
            mark.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity); mark.localScale = Vector3.one;
            bool projected = Project(meshes[index], hit, section, anchor, tangent, length, width);
            mark.gameObject.SetActive(projected);
            if (!projected) return;
            sections[index] = section; fragments[index] = section.RemovedFragments; states[index] = section.State;
            created[index] = Time.unscaledTime;
            block.SetColor("_BaseColor", new Color(.12f, .055f, .02f, 1));
            block.SetFloat("_Seed", Random.Range(0f, 100f));
            renderers[index].SetPropertyBlock(block);
        }
        static bool Project(Mesh mesh, RaycastHit center, ShipDamageSection section, Transform anchor, Vector3 tangent, float length, float width)
        {
            const int Columns = 12, Rows = 4;
            var vertices = new Vector3[(Columns + 1) * (Rows + 1)];
            var world = new Vector3[vertices.Length];
            var uv = new Vector2[vertices.Length];
            var valid = new bool[vertices.Length];
            var triangles = new List<int>();
            Vector3 across = Vector3.Cross(center.normal, tangent).normalized;
            for (int y = 0; y <= Rows; y++)
                for (int x = 0; x <= Columns; x++)
                {
                    int index = y * (Columns + 1) + x;
                    uv[index] = new Vector2((float)x / Columns, (float)y / Rows);
                    Vector3 point = center.point + tangent * ((uv[index].x - .5f) * length) + across * ((uv[index].y - .5f) * width);
                    if (!center.collider.Raycast(new Ray(point + center.normal * .08f, -center.normal), out var hit, .20f) || Vector3.Dot(hit.normal, center.normal) < .35f || ShipV3CollisionBatch.ResolveSection(hit.collider, hit.point) != section) continue;
                    world[index] = hit.point + hit.normal * .002f;
                    vertices[index] = anchor.InverseTransformPoint(world[index]); valid[index] = true;
                }
            float gap = Mathf.Max(length / Columns, width / Rows) * 2.5f;
            for (int y = 0; y < Rows; y++)
                for (int x = 0; x < Columns; x++)
                {
                    int a = y * (Columns + 1) + x, b = a + 1, c = a + Columns + 1, d = c + 1;
                    if (!valid[a] || !valid[b] || !valid[c] || !valid[d]) continue;
                    if (Vector3.Distance(world[a], world[b]) > gap || Vector3.Distance(world[a], world[c]) > gap || Vector3.Distance(world[d], world[b]) > gap || Vector3.Distance(world[d], world[c]) > gap) continue;
                    triangles.Add(a); triangles.Add(c); triangles.Add(b);
                    triangles.Add(b); triangles.Add(c); triangles.Add(d);
                }
            mesh.Clear();
            if (triangles.Count == 0) return false;
            mesh.vertices = vertices; mesh.uv = uv; mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            return true;
        }
        void Chips(Vector3 point, Vector3 normal)
        {
            if (chipMaterial == null) return;
            int index = nextBurst++ % bursts.Length;
            var particles = bursts[index];
            if (particles == null) particles = bursts[index] = new GameObject("SabreWoodChipBurst").AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.gameObject.SetActive(true);
            particles.transform.SetPositionAndRotation(point + normal * .004f, Quaternion.LookRotation(normal));
            var main = particles.main;
            main.loop = false; main.playOnAwake = false; main.duration = .1f; main.maxParticles = 12;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.35f, .65f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.8f, 1.7f);
            main.startSize = new ParticleSystem.MinMaxCurve(.008f, .024f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(.35f, .18f, .07f), new Color(.68f, .45f, .22f));
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.gravityModifier = 1; main.stopAction = ParticleSystemStopAction.Disable;
            var shape = particles.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 58; shape.radius = .006f;
            var emission = particles.emission; emission.rateOverTime = 0; emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)Random.Range(8, 13)) });
            var spin = particles.rotationOverLifetime; spin.enabled = true; spin.z = new ParticleSystem.MinMaxCurve(-12, 12);
            var fade = particles.colorOverLifetime; fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, .65f), new GradientAlphaKey(0, 1) });
            fade.color = gradient;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = chipMaterial; renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            particles.Play();
        }
        void Update()
        {
            for (int i = 0; i < Capacity; i++)
            {
                if (marks[i] == null || !marks[i].gameObject.activeSelf) continue;
                var section = sections[i];
                float age = Time.unscaledTime - created[i];
                if (!marks[i].gameObject.activeInHierarchy || section == null || section.RemovedFragments != fragments[i] || section.State != states[i] || age >= HoldSeconds + FadeSeconds)
                { marks[i].gameObject.SetActive(false); continue; }
                if (age <= HoldSeconds) continue;
                renderers[i].GetPropertyBlock(block);
                block.SetColor("_BaseColor", new Color(.12f, .055f, .02f, 1 - Mathf.SmoothStep(0, 1, (age - HoldSeconds) / FadeSeconds)));
                renderers[i].SetPropertyBlock(block);
            }
        }
        void OnDestroy()
        {
            foreach (var mark in marks) if (mark != null) Destroy(mark.gameObject);
            foreach (var mesh in meshes) if (mesh != null) Destroy(mesh);
            foreach (var burst in bursts) if (burst != null) Destroy(burst.gameObject);
            if (instance == this) instance = null;
        }
    }
}
