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
        void Awake()
        {
            var filter = GetComponent<MeshFilter>();
            skin = GetComponent<SkinnedMeshRenderer>();
            ship = GetComponentInParent<NetworkShip>();
            if (skin != null) return;
            if (filter == null || filter.sharedMesh == null || !filter.sharedMesh.isReadable) return;
            mesh = Instantiate(filter.sharedMesh); filter.sharedMesh = mesh;
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
        }
        void LateUpdate()
        {
            if (ship == null || !ship.IsSpawned) return;
            float time = (float)ship.TimeManager.Tick * (float)ship.TimeManager.TickDelta;
            if (skin != null)
            {
                for (int i = 0; i < skin.sharedMesh.blendShapeCount; i++)
                    skin.SetBlendShapeWeight(i, (Mathf.Sin(time * (1.7f + i * .8f) + i * 2f) + 1f) * 18f);
                return;
            }
            if (mesh == null) return;
            Vector3 motion = transform.InverseTransformDirection(ship.Motor.MotionAngularVelocity);
            for (int i = 0; i < rest.Length; i++)
            {
                float pin = Mathf.InverseLerp(pinMin, pinMax, Vector3.Dot(rest[i], pinAxis));
                float weight = PinTop ? 1f - pin : pin;
                float wave = Mathf.Sin(time * 2.3f + rest[i].x * 2.2f + rest[i].y * 1.7f) * .6f + Mathf.Sin(time * 4.4f + rest[i].x * 3.1f) * .25f;
                work[i] = rest[i] + normals[i] * (wave * Strength * weight) + Vector3.right * (Mathf.Clamp(motion.z * .015f, -.12f, .12f) / unitsScale) * weight;
            }
            mesh.vertices = work;
        }
        void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }
}
