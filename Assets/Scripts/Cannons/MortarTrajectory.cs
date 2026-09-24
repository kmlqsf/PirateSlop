using UnityEngine;

namespace PirateSlop
{
    public sealed class MortarTrajectory : MonoBehaviour
    {
        float BlastRadius => CannonAmmo.MortarBlastRadius(cannon.LoadedAmmo);
        public Material PreviewMaterial;
        SimpleCannon cannon;
        LineRenderer arc, circle;
        readonly Vector3[] points = new Vector3[2048];
        float nextPreview;
        int previewCount, visualFrame = -1;
        readonly Vector3[] displayed = new Vector3[128];
        bool previewReady, previewImpact;
        Vector3 cameraPosition;
        Quaternion cameraRotation;
        bool cameraReady;
        public void PositionCamera(Camera camera)
        {
            UpdatePreview();
            Vector3 forward = Vector3.ProjectOnPlane(cannon.Muzzle.forward, Vector3.up).normalized;
            var bounds = new Bounds(cannon.ShotPosition, Vector3.one * BlastRadius * 2f);
            for (int i = 0; i < previewCount; i++) bounds.Encapsulate(points[i]);
            if (previewCount > 0)
            {
                bounds.Encapsulate(points[previewCount - 1] + Vector3.one * BlastRadius);
                bounds.Encapsulate(points[previewCount - 1] - Vector3.one * BlastRadius);
            }
            float halfFov = camera.fieldOfView * Mathf.Deg2Rad * .5f;
            halfFov = Mathf.Min(halfFov, Mathf.Atan(Mathf.Tan(halfFov) * camera.aspect));
            float distance = Mathf.Max(20f, bounds.extents.magnitude / Mathf.Sin(halfFov) + 4f);
            Vector3 direction = (forward + Vector3.down * .8f).normalized;
            Vector3 target = bounds.center - direction * distance;
            Quaternion rotation = Quaternion.LookRotation(direction);
            float blend = cameraReady ? 1f - Mathf.Exp(-8f * Time.deltaTime) : 1f;
            cameraPosition = Vector3.Lerp(cameraPosition, target, blend);
            cameraRotation = Quaternion.Slerp(cameraRotation, rotation, blend);
            cameraReady = true;
            camera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
        }
        void Awake()
        {
            cannon = GetComponent<SimpleCannon>();
            arc = MakeLine("MortarArc", .065f);
            circle = MakeLine("MortarBlastArea", .1f);
            circle.loop = true;
            circle.positionCount = 64;
        }
        LineRenderer MakeLine(string name, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = PreviewMaterial;
            line.useWorldSpace = true;
            line.startWidth = line.endWidth = width;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            return line;
        }
        void LateUpdate() => UpdatePreview();
        void UpdatePreview()
        {
            bool visible = cannon.Operator != null && cannon.Operator.InputActive && cannon.Operator.ActiveCannon == cannon;
            arc.enabled = visible;
            if (!visible) { circle.enabled = false; cameraReady = previewReady = false; return; }
            if (Time.unscaledTime < nextPreview) { DrawPreview(); return; }
            nextPreview = Time.unscaledTime + .05f;
            Vector3 position = cannon.ShotPosition, velocity = cannon.ShotVelocity;
            var source = cannon.GetComponentInParent<ShipController>().transform;
            int count = 1;
            points[0] = position;
            bool impact = false;
            float dt = Time.fixedDeltaTime;
            for (float t = 0f; t < 19f && count < points.Length; t += dt)
            {
                Vector3 next = CannonShotDamage.StepVelocity(velocity, dt);
                Vector3 delta = (velocity + next) * (.5f * dt);
                if (Trace(position, delta, cannon.ProjectileRadius, source, out var hit, out _, out _))
                {
                    position = hit;
                    points[count++] = position;
                    impact = true;
                    break;
                }
                position += delta;
                velocity = next;
                points[count++] = position;
            }
            previewCount = count;
            previewImpact = impact;
            DrawPreview();
        }
        void DrawPreview()
        {
            if (visualFrame == Time.frameCount || previewCount == 0) return;
            visualFrame = Time.frameCount;
            float blend = previewReady ? 1f - Mathf.Exp(-30f * Time.deltaTime) : 1f;
            arc.positionCount = displayed.Length;
            for (int i = 0; i < displayed.Length; i++)
            {
                float sample = i * (previewCount - 1f) / (displayed.Length - 1f);
                int index = Mathf.FloorToInt(sample);
                Vector3 target = Vector3.Lerp(points[index], points[Mathf.Min(index + 1, previewCount - 1)], sample - index);
                displayed[i] = Vector3.Lerp(displayed[i], target, blend);
                arc.SetPosition(i, displayed[i]);
            }
            previewReady = true;
            circle.enabled = previewImpact;
            Vector3 center = displayed[displayed.Length - 1];
            for (int i = 0; i < 64; i++)
            {
                float angle = i * Mathf.PI * 2f / 64f;
                circle.SetPosition(i, center + new Vector3(Mathf.Cos(angle) * BlastRadius, .15f, Mathf.Sin(angle) * BlastRadius));
            }
        }

        void OnDisable()
        {
            if (arc != null) arc.enabled = false;
            if (circle != null) circle.enabled = false;
        }
        static readonly RaycastHit[] hitBuffer = new RaycastHit[32];
        public static bool Trace(Vector3 position, Vector3 delta, float radius, Transform source, out Vector3 point, out Vector3 normal, out Collider collider)
        {
            point = position + delta;
            normal = Vector3.up;
            collider = null;
            float distance = delta.magnitude;
            bool found = false;
            int count = Physics.SphereCastNonAlloc(position, radius, delta.normalized, hitBuffer, distance, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                var hit = hitBuffer[i];
                if (!PlayerHitbox.IsTarget(hit.collider)) continue;
                if (hit.collider.gameObject.layer == LayerMask.NameToLayer("ShipDebris")) continue;
                if ((source != null && hit.transform.IsChildOf(source)) || hit.collider.GetComponentInParent<CannonShotDamage>() != null || hit.distance > distance) continue;
                point = hit.point; normal = hit.normal; collider = hit.collider; distance = hit.distance; found = true;
            }
            var ocean = OceanSurface.Instance;
            if (ocean != null && (position + delta).y - radius <= ocean.Height(position + delta))
            {
                float low = 0f, high = 1f;
                for (int i = 0; i < 8; i++)
                {
                    float t = (low + high) * .5f;
                    Vector3 sample = position + delta * t;
                    if (sample.y - radius > ocean.Height(sample)) low = t; else high = t;
                }
                if (!found || delta.magnitude * high < distance)
                {
                    point = position + delta * high;
                    point.y = ocean.Height(point);
                    normal = Vector3.up; collider = null; found = true;
                }
            }
            return found;
        }
    }
}
