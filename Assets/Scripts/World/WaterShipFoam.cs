using System.Collections.Generic;
using UnityEngine;
namespace PirateSlop
{
    [DefaultExecutionOrder(100), DisallowMultipleComponent]
    public sealed class WaterShipFoam : MonoBehaviour
    {
        public static WaterShipFoam Instance { get; private set; }
        [Min(.05f)] public float ContactWidth = .9f;
        [Range(0, 1)] public float ContactStrength = .55f;
        [Min(.1f)] public float ContactHeightRange = 2.5f;
        [Range(0, 1)] public float WakeStrength = .9f;
        [Min(.1f)] public float HullWidthScale = 1f;
        [Min(.1f)] public float HullLengthScale = 1f;
        [Min(1f)] public float WaterlineBow = 18.216f;
        [Min(1f)] public float WaterlineStern = 15.809f;
        public float[] WaterlineHalfWidths = { 4.74f, 5.84f, 6.12f, 6.26f, 6.51f, 6.40f, 6.63f, 6.42f, 6.56f, 6.33f, 6.23f, 6.26f, 5.84f, 5.67f, 5.06f, 4.48f, 3.57f, 0f };
        readonly Vector4[] positions = new Vector4[32];
        readonly Vector4[] shapes = new Vector4[32];
        readonly Vector4[] tilts = new Vector4[32];
        readonly Vector4[] contour = new Vector4[5];
        readonly Dictionary<ShipController, float> strengths = new();
        int count, frame = -1;
        static readonly int CountId = Shader.PropertyToID("_WaterShipCount");
        static readonly int PositionId = Shader.PropertyToID("_WaterShipPositions");
        static readonly int ShapeId = Shader.PropertyToID("_WaterShipShapes");
        static readonly int TiltId = Shader.PropertyToID("_WaterShipTilts");
        static readonly int ContourId = Shader.PropertyToID("_WaterShipContour");
        static readonly int ParamsId = Shader.PropertyToID("_WaterShipFoamParams");
        public static WaterShipFoam Ensure(GameObject owner)
        {
            if (Instance != null) return Instance;
            var component = owner.GetComponent<WaterShipFoam>();
            return component != null ? component : owner.AddComponent<WaterShipFoam>();
        }
        void OnEnable() { Instance = this; frame = -1; }
        void OnDisable() { if (Instance == this) Instance = null; strengths.Clear(); count = 0; frame = -1; }
        void LateUpdate() => Collect();
        void Collect()
        {
            if (frame == Time.frameCount) return;
            frame = Time.frameCount;
            count = 0;
            foreach (var ship in ShipController.ActiveControllers)
            {
                if (ship == null || !ship.gameObject.activeInHierarchy || count >= positions.Length) continue;
                strengths.TryGetValue(ship, out float strength);
                strength = Mathf.MoveTowards(strength, Mathf.Clamp01(Mathf.Abs(ship.Speed) / Mathf.Max(.1f, ship.MaxSpeed)), Time.deltaTime * .65f);
                strengths[ship] = strength;
                var transform = ship.transform;
                var position = transform.position;
                float widthScale = Mathf.Max(.1f, ship.HullFootprint.x / 13f) * HullWidthScale;
                float lengthScale = Mathf.Max(.1f, ship.HullFootprint.y / 46f) * HullLengthScale;
                var right = transform.right;
                var forward = transform.forward;
                positions[count] = new Vector4(position.x, position.z, transform.eulerAngles.y * Mathf.Deg2Rad, strength);
                shapes[count] = new Vector4(widthScale, WaterlineStern * lengthScale, WaterlineBow * lengthScale, position.y);
                tilts[count] = new Vector4(right.y / Mathf.Max(.1f, new Vector2(right.x, right.z).magnitude), forward.y / Mathf.Max(.1f, new Vector2(forward.x, forward.z).magnitude), 0, 0);
                count++;
            }
            for (int i = 0; i < contour.Length; i++) contour[i] = Vector4.zero;
            for (int i = 0; i < 18; i++)
            {
                float width = WaterlineHalfWidths != null && i < WaterlineHalfWidths.Length ? Mathf.Max(0, WaterlineHalfWidths[i]) : 0;
                var value = contour[i / 4];
                value[i % 4] = width;
                contour[i / 4] = value;
            }
        }
        Vector4 Parameters => new Vector4(ContactWidth, ContactStrength, ContactHeightRange, WakeStrength);
        public static void Apply(Material material)
        {
            if (material == null) return;
            var source = Instance;
            if (source == null) { material.SetInteger(CountId, 0); return; }
            source.Collect();
            material.SetInteger(CountId, source.count);
            material.SetVectorArray(PositionId, source.positions);
            material.SetVectorArray(ShapeId, source.shapes);
            material.SetVectorArray(TiltId, source.tilts);
            material.SetVectorArray(ContourId, source.contour);
            material.SetVector(ParamsId, source.Parameters);
        }
        public static void Apply(MaterialPropertyBlock properties)
        {
            if (properties == null) return;
            var source = Instance;
            if (source == null) { properties.SetInteger(CountId, 0); return; }
            source.Collect();
            properties.SetInteger(CountId, source.count);
            properties.SetVectorArray(PositionId, source.positions);
            properties.SetVectorArray(ShapeId, source.shapes);
            properties.SetVectorArray(TiltId, source.tilts);
            properties.SetVectorArray(ContourId, source.contour);
            properties.SetVector(ParamsId, source.Parameters);
        }
    }
}
