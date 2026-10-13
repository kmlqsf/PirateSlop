using UnityEngine;
using PirateSlop.Networking;

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
        bool previewReady;
        bool previewMarker, markerWater;
        Vector3 markerTarget, markerPosition;
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
            float metersPerPixel = 2f * distance * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f) / Mathf.Max(1, camera.pixelHeight);
            arc.startWidth = arc.endWidth = Mathf.Max(.14f, metersPerPixel * 2.5f);
            circle.startWidth = circle.endWidth = Mathf.Max(.18f, metersPerPixel * 3f);
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
            if (!visible) { circle.enabled = false; cameraReady = previewReady = previewMarker = false; return; }
            if (Time.unscaledTime < nextPreview) { DrawPreview(); return; }
            nextPreview = Time.unscaledTime + .05f;
            Vector3 position = cannon.ShotPosition, velocity = cannon.ShotVelocity;
            var source = cannon.GetComponentInParent<ShipController>().transform;
            int count = 1;
            points[0] = position;
            previewMarker = markerWater = false;
            bool impact = false, waterEntered = ProjectileWaterFlight.IsSubmerged(position, cannon.ProjectileRadius);
            float waterAge = 0f, dt = Time.fixedDeltaTime;
            for (float t = 0f; t < 20f && count < points.Length - 2; t += dt)
            {
                if (waterEntered && (waterAge += dt) >= ProjectileWaterFlight.CannonLifetime) break;
                float remaining = dt;
                for (int part = 0; part < 3 && remaining > .0001f; part++)
                {
                    Vector3 next = velocity;
                    Vector3 delta;
                    if (ProjectileWaterFlight.IsSubmerged(position, cannon.ProjectileRadius))
                        delta = ProjectileWaterFlight.Step(ref next, remaining, ProjectileWaterFlight.CannonDrag);
                    else
                    {
                        next = CannonShotDamage.StepVelocity(velocity, remaining);
                        delta = (velocity + next) * (.5f * remaining);
                    }
                    bool found = Trace(position, delta, cannon.ProjectileRadius, source, out var hit, out _, out _, false);
                    if (WaterImpactPhysics.Cross(OceanSurface.Instance, position, position + delta, cannon.ProjectileRadius, out _, out float fraction) &&
                        (!found || delta.magnitude * fraction < Vector3.Distance(position, hit)))
                    {
                        position += delta * fraction - Vector3.up * .001f;
                        if (!previewMarker)
                        {
                            markerTarget = position + Vector3.up * .15f;
                            previewMarker = markerWater = true;
                        }
                        velocity = Vector3.Lerp(velocity, next, fraction);
                        waterEntered = true;
                        points[count++] = position;
                        remaining *= 1f - fraction;
                        continue;
                    }
                    if (found)
                    {
                        position = hit;
                        if (!previewMarker)
                        {
                            markerTarget = position + Vector3.up * .15f;
                            previewMarker = true;
                        }
                        points[count++] = position;
                        impact = true;
                        break;
                    }
                    position += delta;
                    velocity = next;
                    points[count++] = position;
                    break;
                }
                if (impact) break;
            }
            previewCount = count;
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
            markerPosition = Vector3.Lerp(markerPosition, markerTarget, blend);
            previewReady = true;
            circle.enabled = previewMarker;
            Vector3 center = markerPosition;
            for (int i = 0; i < 64; i++)
            {
                float angle = i * Mathf.PI * 2f / 64f;
                circle.SetPosition(i, center + new Vector3(Mathf.Cos(angle) * BlastRadius, 0f, Mathf.Sin(angle) * BlastRadius));
            }
        }

        void OnGUI()
        {
            var player = cannon != null ? cannon.Operator : null;
            if (Event.current.type != EventType.Repaint || !previewMarker || player == null || !player.InputActive || player.ActiveCannon != cannon || SessionController.MenuOpen) return;
            var camera = player.PlayerCamera;
            if (camera == null || !camera.enabled) return;
            Vector3 point = camera.WorldToScreenPoint(markerPosition);
            if (point.z <= 0f) return;
            float x = point.x, y = Screen.height - point.y;
            var color = new Color(1f, .86f, .2f);
            Stroke(new Rect(x - 22f, y - 1f, 14f, 2f));
            Stroke(new Rect(x + 8f, y - 1f, 14f, 2f));
            Stroke(new Rect(x - 1f, y - 22f, 2f, 14f));
            Stroke(new Rect(x - 1f, y + 8f, 2f, 14f));
            PirateHudStyle.Diamond(new Vector2(x, y), 4f, color);
            float range = Vector3.ProjectOnPlane(markerPosition - cannon.ShotPosition, Vector3.up).magnitude;
            PirateHudStyle.Brush(new Rect(x - 105f, y + 28f, 210f, 45f), PirateHudStyle.Ink);
            PirateHudStyle.Label(new Rect(x - 100f, y + 29f, 200f, 22f), markerWater ? "ВХОД В ВОДУ" : "ТОЧКА ПОПАДАНИЯ", color);
            PirateHudStyle.Label(new Rect(x - 100f, y + 49f, 200f, 22f), $"{range:0} м", PirateHudStyle.Paper);
            void Stroke(Rect rect)
            {
                PirateHudStyle.Fill(new Rect(rect.x - 2f, rect.y - 2f, rect.width + 4f, rect.height + 4f), PirateHudStyle.Ink);
                PirateHudStyle.Fill(rect, color);
            }
        }

        void OnDisable()
        {
            if (arc != null) arc.enabled = false;
            if (circle != null) circle.enabled = false;
        }
        static readonly RaycastHit[] hitBuffer = new RaycastHit[64];
        public static RaycastHit[] CastHits(Vector3 position, Vector3 delta, float radius, out int count)
        {
            if (delta.sqrMagnitude < .000001f) { count = 0; return hitBuffer; }
            count = Physics.SphereCastNonAlloc(position, radius, delta.normalized, hitBuffer, delta.magnitude, ~0, QueryTriggerInteraction.Collide);
            if (count < hitBuffer.Length) return hitBuffer;
            var hits = Physics.SphereCastAll(position, radius, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Collide);
            count = hits.Length;
            return hits;
        }
        public static bool Trace(Vector3 position, Vector3 delta, float radius, Transform source, out Vector3 point, out Vector3 normal, out Collider collider, bool includeWater = true)
        {
            point = position + delta;
            normal = Vector3.up;
            collider = null;
            float distance = delta.magnitude;
            bool found = false;
            var hits = CastHits(position, delta, radius, out int count);
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (!PlayerHitbox.IsTarget(hit.collider) || hit.collider.gameObject.layer == LayerMask.NameToLayer("Water")) continue;
                if (hit.collider.gameObject.layer == LayerMask.NameToLayer("ShipDebris")) continue;
                if (hit.collider.GetComponentInParent<BoardingWalkSurface>() != null) continue;
                if ((source != null && hit.transform.IsChildOf(source)) || hit.collider.GetComponentInParent<CannonShotDamage>() != null || hit.distance > distance) continue;
                point = hit.point; normal = hit.normal; collider = hit.collider; distance = hit.distance; found = true;
            }
            var ocean = OceanSurface.Instance;
            if (includeWater && WaterImpactPhysics.Cross(ocean, position, position + delta, radius, out var waterPoint, out float high))
            {
                if (!found || delta.magnitude * high < distance)
                {
                    point = waterPoint;
                    normal = Vector3.up; collider = null; found = true;
                }
            }
            return found;
        }
    }
}
