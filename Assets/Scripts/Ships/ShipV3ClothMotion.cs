using UnityEngine;
using PirateSlop.Networking;

namespace PirateSlop.Ships
{
    [DefaultExecutionOrder(80)]
    public sealed class ShipV3ClothMotion : MonoBehaviour
    {
        public bool PinTop;
        public float Strength = .15f;
        public int SailIndex = -1;
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
        ShipV3RenderBudget budget;
        float nextVisualUpdate;
        SailSystem sails;
        readonly int[] windShapes = new int[3];
        float amplitude;
        bool initialized;
        int foldedShape = -1;
        void Awake()
        {
            skin = GetComponent<SkinnedMeshRenderer>();
            visual = GetComponent<Renderer>();
            ship = GetComponentInParent<NetworkShip>();
            budget = GetComponentInParent<ShipV3RenderBudget>();
            sails = GetComponentInParent<SailSystem>();
        }
        void Initialize()
        {
            initialized = true;
            var filter = GetComponent<MeshFilter>();
            var source = skin != null ? skin.sharedMesh : filter != null ? filter.sharedMesh : null;
            if (source == null || !source.isReadable) return;
            mesh = Instantiate(source);
            if (skin != null) skin.sharedMesh = mesh; else filter.sharedMesh = mesh;
            mesh.MarkDynamic(); rest = mesh.vertices; work = new Vector3[rest.Length]; normals = mesh.normals; bounds = mesh.bounds;
            ship = GetComponentInParent<NetworkShip>();
            unitsScale = Mathf.Max(.01f, transform.lossyScale.magnitude / Mathf.Sqrt(3f));
            amplitude = Strength / unitsScale;
            if (SailIndex < 0 && !PinTop) amplitude *= 1.8f;
            pinAxis = PinTop ? transform.InverseTransformDirection(ship != null ? ship.transform.up : Vector3.up).normalized : Vector3.right;
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
                foldedShape = mesh.GetBlendShapeIndex("Furled");
                if (foldedShape < 0) foldedShape = mesh.GetBlendShapeIndex("Deployed.Furled");
                var delta = new Vector3[rest.Length];
                var deltaNormals = new Vector3[rest.Length];
                var zero = new Vector3[rest.Length];
                var size = bounds.size;
                Vector3 direction = size.x < size.y && size.x < size.z ? Vector3.right : size.z < size.y ? Vector3.forward : Vector3.up;
                for (int shape = 0; shape < 3; shape++)
                {
                    for (int i = 0; i < rest.Length; i++)
                    {
                        float wavelength = SailIndex >= 0 ? .7f : 2.2f;
                        float phase = (rest[i].x + rest[i].z * .45f) * unitsScale * wavelength
                            + Vector3.Dot(rest[i], pinAxis) * unitsScale * .6f + shape * 2.094f;
                        delta[i] = direction * (Mathf.Sin(phase) * amplitude * weights[i]);
                        work[i] = rest[i] + delta[i];
                    }
                    string name = "ShipWind_" + shape;
                    windShapes[shape] = mesh.GetBlendShapeIndex(name);
                    if (windShapes[shape] < 0)
                    {
                        mesh.vertices = work;
                        mesh.RecalculateNormals();
                        var deformedNormals = mesh.normals;
                        for (int i = 0; i < rest.Length; i++) deltaNormals[i] = normals.Length == rest.Length ? deformedNormals[i] - normals[i] : Vector3.zero;
                        mesh.vertices = rest;
                        if (normals.Length == rest.Length) mesh.normals = normals;
                        windShapes[shape] = mesh.blendShapeCount;
                        mesh.AddBlendShapeFrame(name, 100f, delta, deltaNormals, zero);
                    }
                }
                skin.localBounds = new Bounds(skin.localBounds.center, skin.localBounds.size + Vector3.one * (amplitude * 4f));
            }
            else mesh.bounds = new Bounds(bounds.center, bounds.size + Vector3.one * (amplitude * 4f));
        }
        void LateUpdate()
        {
            if (ship == null || !ship.IsSpawned) return;
            if (!initialized) Initialize();
            if (mesh == null) return;
            if (visual != null && !visual.isVisible) return;
            if (Time.unscaledTime < nextVisualUpdate) return;
            nextVisualUpdate = Time.unscaledTime + (budget != null ? budget.VisualInterval : 0f);
            uint tick = ship.TimeManager.Tick;
            if (tick != windTick) { windTick = tick; tickTime = Time.unscaledTime; }
            float time = (float)tick * (float)ship.TimeManager.TickDelta + Mathf.Min(Time.unscaledTime - tickTime, (float)ship.TimeManager.TickDelta);
            if (skin != null)
            {
                float deployed = SailIndex >= 0 && sails != null ? sails.Tension(SailIndex) : 1f;
                if (SailIndex >= 0 && foldedShape >= 0) deployed = 1f - Mathf.Clamp01(skin.GetBlendShapeWeight(foldedShape) / 100f);
                float speed = ship.Motor != null ? ship.Motor.CannonPointVelocity(ship.transform.position).magnitude : 0f;
                float gust = .8f + .2f * Mathf.Sin(time * .73f + transform.position.x * .013f);
                float flutter = (SailIndex >= 0 ? 72f : 82f) * Mathf.SmoothStep(0f, 1f, deployed) * gust * Mathf.Lerp(.75f, 1f, Mathf.Clamp01(speed / 12f));
                float frequency = SailIndex >= 0 ? 2.2f : 3.7f;
                for (int i = 0; i < windShapes.Length; i++)
                    skin.SetBlendShapeWeight(windShapes[i], (Mathf.Sin(time * frequency - i * 2.094f)
                        + .18f * Mathf.Sin(time * frequency * 1.8f - i * 1.3f)) * flutter);
                return;
            }
            Vector3 motion = transform.InverseTransformDirection(ship.Motor.MotionAngularVelocity);
            Vector3 sway = transform.InverseTransformVector(ship.transform.right * ((Mathf.Sin(time * 1.8f) * .65f + Mathf.Sin(time * 2.6f) * .15f) * amplitude * unitsScale
                + Mathf.Clamp(ship.Motor.MotionAngularVelocity.z * .025f, -.06f, .06f)));
            for (int i = 0; i < rest.Length; i++)
            {
                if (PinTop) { work[i] = rest[i] + sway * weights[i]; continue; }
                float weight = weights[i];
                float wave = Mathf.Sin(time * 2.3f + rest[i].x * 2.2f + rest[i].y * 1.7f) * .6f + Mathf.Sin(time * 4.4f + rest[i].x * 3.1f) * .25f;
                work[i] = rest[i] + normals[i] * (wave * amplitude * weight) + Vector3.right * (Mathf.Clamp(motion.z * .015f, -.12f, .12f) / unitsScale) * weight;
            }
            mesh.vertices = work;
        }
        void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }
}
