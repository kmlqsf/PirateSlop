using UnityEngine;
using PirateSlop.Networking;

namespace PirateSlop.Ships
{
    public sealed class ShipV3ClothMotion : MonoBehaviour
    {
        public bool PinTop;
        public float Strength = .15f;
        Mesh mesh;
        Vector3[] rest, work, normals;
        NetworkShip ship;
        Bounds bounds;
        SkinnedMeshRenderer skin;
        Vector3 pinAxis;
        float pinMin, pinMax, unitsScale;
        float[] weights;
        Renderer visual;
        uint windTick;
        float tickTime;
        void Awake()
        {
            var filter = GetComponent<MeshFilter>();
            skin = GetComponent<SkinnedMeshRenderer>();
            visual = GetComponent<Renderer>();
            ship = GetComponentInParent<NetworkShip>();
            var source = skin != null ? skin.sharedMesh : filter != null ? filter.sharedMesh : null;
            if (source == null || !source.isReadable) return;
            mesh = Instantiate(source);
            if (skin != null) skin.sharedMesh = mesh; else filter.sharedMesh = mesh;
            mesh.MarkDynamic(); rest = mesh.vertices; work = new Vector3[rest.Length]; normals = mesh.normals; bounds = mesh.bounds;
            ship = GetComponentInParent<NetworkShip>();
            unitsScale = Mathf.Max(.01f, transform.lossyScale.magnitude / Mathf.Sqrt(3f));
            Strength /= unitsScale;
            pinAxis = PinTop ? transform.InverseTransformDirection(Vector3.up) : Vector3.right;
            pinMin = float.MaxValue; pinMax = float.MinValue;
            foreach (var vertex in rest)
            {
                float coordinate = Vector3.Dot(vertex, pinAxis);
                pinMin = Mathf.Min(pinMin, coordinate); pinMax = Mathf.Max(pinMax, coordinate);
            }
            weights = new float[rest.Length];
            for (int i = 0; i < weights.Length; i++)
            {
                float pin = Mathf.InverseLerp(pinMin, pinMax, Vector3.Dot(rest[i], pinAxis));
                weights[i] = 1f - pin;
            }
            if (skin != null)
            {
                mesh.ClearBlendShapes();
                var delta = new Vector3[rest.Length];
                var zero = new Vector3[rest.Length];
                for (int shape = 0; shape < 3; shape++)
                {
                    for (int i = 0; i < rest.Length; i++)
                    {
                        Vector3 direction = Vector3.ProjectOnPlane(transform.TransformDirection(normals[i]), ship.transform.up).normalized;
                        direction = transform.InverseTransformDirection(direction);
                        float phase = rest[i].x * unitsScale * 2.2f + rest[i].z * unitsScale * .7f + shape * 2.094f;
                        delta[i] = direction * (Mathf.Sin(phase) * Strength * weights[i]);
                    }
                    mesh.AddBlendShapeFrame("PinnedWind_" + shape, 100f, delta, zero, zero);
                }
                skin.localBounds = new Bounds(bounds.center, bounds.size + Vector3.one * (Strength * 3f));
            }
        }
        void LateUpdate()
        {
            if (ship == null || !ship.IsSpawned) return;
            if (visual != null && !visual.isVisible) return;
            uint tick = ship.TimeManager.Tick;
            if (tick != windTick) { windTick = tick; tickTime = Time.unscaledTime; }
            float time = (float)tick * (float)ship.TimeManager.TickDelta + Mathf.Min(Time.unscaledTime - tickTime, (float)ship.TimeManager.TickDelta);
            if (skin != null)
            {
                for (int i = 0; i < skin.sharedMesh.blendShapeCount; i++)
                    skin.SetBlendShapeWeight(i, Mathf.Sin(time * 2.1f - i * 2.094f) * 40f);
                return;
            }
            if (mesh == null) return;
            Vector3 motion = transform.InverseTransformDirection(ship.Motor.MotionAngularVelocity);
            Vector3 sway = transform.InverseTransformVector(ship.transform.right * ((Mathf.Sin(time * 1.8f) * .65f + Mathf.Sin(time * 2.6f) * .15f) * Strength * unitsScale
                + Mathf.Clamp(ship.Motor.MotionAngularVelocity.z * .025f, -.06f, .06f)));
            for (int i = 0; i < rest.Length; i++)
            {
                if (PinTop) { work[i] = rest[i] + sway * weights[i]; continue; }
                float weight = weights[i];
                float wave = Mathf.Sin(time * 2.3f + rest[i].x * 2.2f + rest[i].y * 1.7f) * .6f + Mathf.Sin(time * 4.4f + rest[i].x * 3.1f) * .25f;
                work[i] = rest[i] + normals[i] * (wave * Strength * weight) + Vector3.right * (Mathf.Clamp(motion.z * .015f, -.12f, .12f) / unitsScale) * weight;
            }
            mesh.vertices = work;
        }
        void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }
}
