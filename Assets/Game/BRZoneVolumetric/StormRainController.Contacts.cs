using PirateSlop.Ships;
using UnityEngine;
using UnityEngine.Rendering;

namespace PirateSlop
{
    public sealed partial class StormRainController
    {
        const int ImpactCapacity = 128, QuadsPerImpact = 7;
        struct Contact
        {
            public Vector3 Point, Normal;
            public Collider Collider;
            public ShipDamageSection Section;
            public ulong Fragments;
            public ShipSectionState SectionState;
            public float Started, Height, HeightTime, VerticalVelocity;
            public bool Active, Water;
        }
        readonly Contact[] impacts = new Contact[ImpactCapacity];
        readonly Vector3[] impactVertices = new Vector3[ImpactCapacity * QuadsPerImpact * 4];
        readonly Vector3[] impactNormals = new Vector3[ImpactCapacity * QuadsPerImpact * 4];
        readonly Vector2[] impactUV = new Vector2[ImpactCapacity * QuadsPerImpact * 4];
        readonly Color[] impactColors = new Color[ImpactCapacity * QuadsPerImpact * 4];
        Mesh impactMesh;
        GameObject impactObject;
        int nextImpact, waterCursor;
        public int ActiveContacts { get; private set; }

        void InitializeImpacts()
        {
            impactObject = new GameObject("Rain water and deck contacts") { hideFlags = HideFlags.HideAndDontSave };
            impactObject.transform.SetParent(transform, false);
            impactObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            impactObject.transform.localScale = Vector3.one;
            impactMesh = new Mesh { name = "Rain contact pool", hideFlags = HideFlags.HideAndDontSave };
            impactMesh.MarkDynamic();
            var indices = new int[impactVertices.Length / 4 * 6];
            for (int q = 0; q < impactVertices.Length / 4; q++)
            {
                int v = q * 4, t = q * 6;
                impactUV[v] = Vector2.zero; impactUV[v + 1] = Vector2.right;
                impactUV[v + 2] = Vector2.one; impactUV[v + 3] = Vector2.up;
                indices[t] = v; indices[t + 1] = v + 2; indices[t + 2] = v + 1;
                indices[t + 3] = v; indices[t + 4] = v + 3; indices[t + 5] = v + 2;
            }
            impactMesh.vertices = impactVertices; impactMesh.uv = impactUV;
            impactMesh.colors = impactColors; impactMesh.normals = impactNormals; impactMesh.triangles = indices;
            impactObject.AddComponent<MeshFilter>().sharedMesh = impactMesh;
            var renderer = impactObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = contactRain;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        void EmitContact(Vector3 eye, OceanSurface sea, StormVolumeController storm)
        {
            float angle = Next01() * Mathf.PI * 2;
            float radius = 3 + Mathf.Sqrt(Next01()) * 15;
            var point = eye + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
            if (downpour && Next01() < .7f)
            {
                var forward = new Vector3(viewer.transform.forward.x, 0, viewer.transform.forward.z).normalized;
                radius = 1 + Mathf.Sqrt(Next01()) * 4;
                point = eye + forward * 2 + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
            }
            if (AmountAt(point, storm) < Next01()) return;
            point.y = WaterHeight(point, sea);
            float fallTime = (Mathf.Max(eye.y + 22, point.y + 20) - point.y) / -velocity.y;
            var start = point - velocity * fallTime;
            bool blocked = Cast(start, velocity.normalized, velocity.magnitude * fallTime + .1f, out var hit) && hit.point.y > point.y + .02f;
            var contact = new Contact { Point = point, Normal = Vector3.up, Height = point.y, HeightTime = Time.time, Water = !blocked, Active = true, Started = Time.time };
            if (blocked)
            {
                if (hit.collider.GetComponentInParent<ShipController>() == null) return;
                contact.Section = ShipV3CollisionBatch.ResolveSection(hit.collider, hit.point);
                if (contact.Section != null && contact.Section.State == ShipSectionState.Destroyed) return;
                contact.Collider = hit.collider;
                contact.Point = hit.collider.transform.InverseTransformPoint(hit.point);
                contact.Normal = hit.collider.transform.localToWorldMatrix.transpose.MultiplyVector(hit.normal).normalized;
                contact.Fragments = contact.Section != null ? contact.Section.RemovedFragments : 0;
                contact.SectionState = contact.Section != null ? contact.Section.State : default;
                if (downpour) AddWetWood(contact);
            }
            impacts[nextImpact++ % ImpactCapacity] = contact;
        }

        void UpdateImpacts(OceanSurface sea)
        {
            if (impactMesh == null) return;
            impactObject.SetActive(true);
            for (int visited = 0; visited < ImpactCapacity && HeightQueries < (downpour ? 6 : 4); visited++)
            {
                int index = waterCursor++ % ImpactCapacity;
                var contact = impacts[index];
                if (!contact.Active || !contact.Water || Time.time - contact.Started > .5f) continue;
                if (downpour && sea.HeightSource is BoatAttackOcean boat)
                {
                    HeightQueries++;
                    boat.SampleFoamSurface(contact.Point, out float waveHeight, out var speed, out _);
                    contact.Height = sea.SeaLevel + waveHeight + sea.GetWhirlpoolHeight(contact.Point);
                    contact.VerticalVelocity = speed.y;
                }
                else contact.Height = WaterHeight(contact.Point, sea);
                contact.HeightTime = Time.time;
                impacts[index] = contact;
            }
            System.Array.Clear(impactColors, 0, impactColors.Length);
            ActiveContacts = 0;
            for (int index = 0; index < impacts.Length; index++)
            {
                var contact = impacts[index];
                if (!contact.Active) continue;
                float age = Time.time - contact.Started;
                if (age > (contact.Water ? .5f : 2.4f) || !contact.Water &&
                    (contact.Collider == null || !contact.Collider.enabled || !contact.Collider.gameObject.activeInHierarchy ||
                    contact.Section != null && (contact.Section.RemovedFragments != contact.Fragments || contact.Section.State != contact.SectionState)))
                { contact.Active = false; impacts[index] = contact; continue; }
                ActiveContacts++;
                Vector3 point, normal;
                if (contact.Water) { point = contact.Point; point.y = contact.Height + contact.VerticalVelocity * Mathf.Min(.15f, Time.time-contact.HeightTime); normal = Vector3.up; }
                else
                {
                    point = contact.Collider.transform.TransformPoint(contact.Point);
                    normal = contact.Collider.transform.localToWorldMatrix.inverse.transpose.MultiplyVector(contact.Normal).normalized;
                }
                point += normal * (downpour ? .022f : .012f);
                var tangent = Vector3.Cross(normal, Vector3.forward).normalized;
                if (tangent.sqrMagnitude < .1f) tangent = Vector3.right;
                var across = Vector3.Cross(normal, tangent).normalized;
                int first = index * QuadsPerImpact;
                float t = Mathf.Clamp01(age / .5f);
                float fade = (1 - t) * Mathf.SmoothStep(0, 1, age / .025f);
                float strength = downpour ? 1.5f : 1;
                Quad(first, point, tangent * Mathf.Lerp(.055f, .24f, t), across * Mathf.Lerp(.055f, .24f, t), normal, new Color(1, 1, 1, fade * .52f * strength));
                Quad(first + 1, point + normal * .002f, tangent * Mathf.Lerp(.035f, .15f, t), across * Mathf.Lerp(.035f, .15f, t), normal, new Color(1, 1, 1, fade * .3f));
                float splashAge = Mathf.Min(age, .32f);
                for (int drop = 0; drop < 4; drop++)
                {
                    float angle = drop * Mathf.PI * .5f + index;
                    Vector3 outward = tangent * Mathf.Cos(angle) + across * Mathf.Sin(angle);
                    Vector3 position = point + outward * (splashAge * .38f) + normal * Mathf.Max(0, splashAge * 1.2f - 4.905f * splashAge * splashAge);
                    Quad(first + 2 + drop, position, viewer.transform.right * .012f, viewer.transform.up * .019f, -viewer.transform.forward,
                        new Color(0, 1, 1, (1 - Mathf.Clamp01(age / .28f)) * .55f * strength));
                }
                if (!contact.Water && !downpour)
                    Quad(first + 6, point - normal * .009f, tangent * .075f, across * .075f, normal,
                        new Color(2, 1, 1, (1 - Mathf.SmoothStep(.5f, 2.4f, age)) * (downpour ? .38f : .22f)));
            }
            impactMesh.vertices = impactVertices; impactMesh.colors = impactColors; impactMesh.normals = impactNormals;
            impactMesh.bounds = new Bounds(viewer.transform.position, Vector3.one * 180);
        }

        void Quad(int index, Vector3 center, Vector3 right, Vector3 up, Vector3 normal, Color color)
        {
            int first = index * 4;
            impactVertices[first] = center - right - up; impactVertices[first + 1] = center + right - up;
            impactVertices[first + 2] = center + right + up; impactVertices[first + 3] = center - right + up;
            for (int i = 0; i < 4; i++) { impactNormals[first + i] = normal; impactColors[first + i] = color; }
        }

        void ClearImpacts()
        {
            for (int i = 0; i < impacts.Length; i++) impacts[i].Active = false;
            ActiveContacts = 0;
            if (impactObject != null) impactObject.SetActive(false);
        }

        void DisposeImpacts()
        {
            if (impactObject != null) Destroy(impactObject);
            if (impactMesh != null) Destroy(impactMesh);
        }
    }
}
