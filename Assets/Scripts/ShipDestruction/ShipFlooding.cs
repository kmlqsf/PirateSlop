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
        public float FullWaterline = 4.1f;
        public float FullBowPitch = 8f;
        public float Level { get; private set; }
        readonly Dictionary<int, ShipBreach> breaches = new();
        public IEnumerable<ShipBreach> Breaches => breaches.Values;
        public void SetBreach(int id, Vector3 localPoint, float area)
        {
            if (area <= 0f) breaches.Remove(id);
            else breaches[id] = new ShipBreach { SectionId = id, LocalPoint = localPoint, Area = area };
        }
        public void Clear() { breaches.Clear(); SetLevel(0f); }
        public void SetSectionBreaches(ShipDamageSection section, ShipSectionSnapshot entry, ShipSectionDefinition definition, bool enabled)
        {
            if (section.Fragments.Length == 0)
            {
                float scale = entry.State == ShipSectionState.Destroyed ? 1f : entry.State == ShipSectionState.Critical ? definition.CriticalLeak : definition.DamagedLeak;
                SetBreach(entry.SectionId, entry.BreachPoint, enabled && entry.Breach ? definition.BreachArea * scale : 0f);
                return;
            }
            for (int i = 0; i < section.Fragments.Length; i++)
            {
                int key = entry.SectionId * 64 + i;
                bool removed = entry.State == ShipSectionState.Destroyed || (entry.RemovedFragments & (1UL << i)) != 0;
                if (!enabled || !removed) { breaches.Remove(key); continue; }
                var fragment = section.Fragments[i];
                var filter = fragment.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                var bounds = filter.sharedMesh.bounds;
                var local = bounds.center;
                local.y = bounds.min.y;
                var point = transform.InverseTransformPoint(fragment.transform.TransformPoint(local));
                var size = Vector3.Scale(bounds.size, fragment.transform.lossyScale);
                float area = Mathf.Clamp(Mathf.Abs(size.y) * Mathf.Max(Mathf.Abs(size.x), Mathf.Abs(size.z)), .02f, 4f);
                breaches[key] = new ShipBreach { SectionId = entry.SectionId, LocalPoint = point, Area = area };
            }
        }
        public float Simulate(float dt, ShipDestructionProfile profile)
        {
            var ocean = OceanSurface.Instance;
            if (ocean == null || dt <= 0f || Level >= 1f) return Level;
            float flow = 0f;
            foreach (var breach in breaches.Values)
            {
                var point = transform.TransformPoint(breach.LocalPoint);
                float depth = ocean.Height(point) - point.y;
                if (depth < 0f) continue;
                depth = Mathf.Clamp(depth, .025f, Mathf.Max(.025f, profile.MaximumDepth));
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
