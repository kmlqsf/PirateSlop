using System.Collections.Generic;
using UnityEngine;

namespace PirateSlop
{
    public struct ShipBreach
    {
        public int SectionId;
        public Vector3 LocalPoint;
        public float Area;
    }
    public sealed class ShipFlooding : MonoBehaviour
    {
        public Transform WaterVisual;
        public float EmptyHeight = -1f, FullHeight = 3.5f;
        public float Level { get; private set; }
        readonly Dictionary<int, ShipBreach> breaches = new();
        public IEnumerable<ShipBreach> Breaches => breaches.Values;
        public void SetBreach(int id, Vector3 localPoint, float area)
        {
            if (area <= 0f) breaches.Remove(id);
            else breaches[id] = new ShipBreach { SectionId = id, LocalPoint = localPoint, Area = area };
        }
        public void Clear() { breaches.Clear(); SetLevel(0f); }
        public float Simulate(float dt, ShipDestructionProfile profile)
        {
            var ocean = OceanSurface.Instance;
            if (ocean == null) return Level;
            float flow = 0f;
            foreach (var breach in breaches.Values)
            {
                var point = transform.TransformPoint(breach.LocalPoint);
                float depth = Mathf.Clamp(ocean.Height(point) - point.y, 0f, profile.MaximumDepth);
                flow += breach.Area * Mathf.Sqrt(2f * 9.81f * depth);
            }
            SetLevel(Level + flow * profile.FloodCoefficient * dt);
            return Level;
        }
        public void SetLevel(float value)
        {
            Level = Mathf.Clamp01(value);
            if (WaterVisual == null) return;
            WaterVisual.gameObject.SetActive(Level > .001f);
            var position = WaterVisual.localPosition;
            position.y = Mathf.Lerp(EmptyHeight, FullHeight, Level);
            WaterVisual.localPosition = position;
        }
    }
}
