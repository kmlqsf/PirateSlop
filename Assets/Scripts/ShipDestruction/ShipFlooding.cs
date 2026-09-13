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
        public float DrainDuration = 30f, WaterlineAllowance = 3.5f;
        public Vector4 FloodSecondsByHits = new(60f, 40f, 20f, 10f);
        public float Level { get; private set; }
        readonly Dictionary<int, ShipBreach> breaches = new();
        readonly Dictionary<ulong, Dictionary<int, ulong>> impacts = new();
        ulong impactId;
        public int OpenImpactCount
        {
            get
            {
                int count = 0;
                foreach (var impact in impacts.Values)
                    foreach (var mask in impact.Values)
                        if (mask != 0) { count++; break; }
                return count;
            }
        }
        public void BeginImpact() => impactId++;
        public void RegisterImpact(int sectionId, ulong fragments)
        {
            if (fragments == 0) return;
            if (!impacts.TryGetValue(impactId, out var impact)) impacts[impactId] = impact = new Dictionary<int, ulong>();
            impact.TryGetValue(sectionId, out ulong previous);
            impact[sectionId] = previous | fragments;
        }
        public IEnumerable<ShipBreach> Breaches => breaches.Values;
        public void SetBreach(int id, Vector3 localPoint, float area)
        {
            if (area <= 0f) breaches.Remove(id);
            else breaches[id] = new ShipBreach { SectionId = id, LocalPoint = localPoint, Area = area };
        }
        public void Clear()
        {
            breaches.Clear(); impacts.Clear(); impactId = 0; SetLevel(0f);
        }
        public bool CanOpenBreach(Vector3 point) => OceanSurface.Instance != null && point.y <= OceanSurface.Instance.Height(point) + WaterlineAllowance;
        public void SetSectionBreaches(ShipDamageSection section, ShipSectionSnapshot entry, ShipSectionDefinition definition, bool enabled)
        {
            foreach (var impact in impacts.Values)
                if (impact.TryGetValue(entry.SectionId, out ulong mask))
                    impact[entry.SectionId] = mask & (enabled ? section.Fragments.Length == 0 ? entry.Breach ? 1UL : 0UL : entry.LeakingFragments & entry.RemovedFragments : 0UL);
            if (section.Fragments.Length == 0)
            {
                float scale = entry.State == ShipSectionState.Destroyed ? 1f : entry.State == ShipSectionState.Critical ? definition.CriticalLeak : definition.DamagedLeak;
                SetBreach(entry.SectionId, entry.BreachPoint, enabled && entry.Breach ? definition.BreachArea * scale : 0f);
                return;
            }
            for (int i = 0; i < section.Fragments.Length; i++)
            {
                int key = entry.SectionId * 64 + i;
                bool removed = (entry.RemovedFragments & entry.LeakingFragments & (1UL << i)) != 0;
                if (!enabled || !removed) { breaches.Remove(key); continue; }
                var fragment = section.Fragments[i];
                var filter = fragment.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                var bounds = filter.sharedMesh.bounds;
                var point = entry.BreachPoint;
                var size = Vector3.Scale(bounds.size, fragment.transform.lossyScale);
                float area = Mathf.Clamp(Mathf.Abs(size.y) * Mathf.Max(Mathf.Abs(size.x), Mathf.Abs(size.z)), .02f, 4f);
                breaches[key] = new ShipBreach { SectionId = entry.SectionId, LocalPoint = point, Area = area };
            }
        }
        public float Simulate(float dt, ShipDestructionProfile profile)
        {
            var ocean = OceanSurface.Instance;
            if (ocean == null || dt <= 0f) return Level;
            int count = OpenImpactCount;
            float seconds = count == 1 ? FloodSecondsByHits.x : count == 2 ? FloodSecondsByHits.y : count == 3 ? FloodSecondsByHits.z : FloodSecondsByHits.w;
            float rate = count > 0 ? 1f / Mathf.Max(10f, seconds) : -1f / Mathf.Max(1f, DrainDuration);
            SetLevel(Level + rate * dt);
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

