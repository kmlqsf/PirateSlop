using System.Collections.Generic;
using UnityEngine;

namespace PirateSlop
{
    public struct ShipBreach
    {
        public int SectionId;
        public Vector3 LocalPoint;
        public float Area;
        public int FragmentIndex;
        public Mesh SurfaceMesh;
        public Matrix4x4 SurfaceTransform;
        public Vector3 LowerPoint;
        public float MinimumHeight, MaximumHeight;
    }
    public sealed class ShipFlooding : MonoBehaviour
    {
        public Transform WaterVisual;
        public Mesh[] WaterSlices;
        public int ReportedOpenImpacts { get; set; }
        float visualLevel;
        MeshFilter waterFilter;
        public float EmptyHeight = -1f, FullHeight = 3.5f;
        public float FullWaterline = 4.1f;
        public float FullBowPitch = 8f;
        public float DrainDuration = 30f, WaterlineAllowance = 3.5f;
        public Vector4 FloodSecondsByHits = new(60f, 40f, 20f, 10f);
        public float Level { get; private set; }
        public bool UseBilgeFlooding;
        public float StrongHoleFillSeconds = 540f;
        public float ReferenceBreachArea = .35f, ReferencePressureHead = 1f;
        public float UpperLeakPeriod = 3.2f, UpperLeakBurstDuration = .8f, UpperLeakAverage = .3f;
        public int BreachRevision { get; private set; }
        public float StrongHoleRate => 1f / Mathf.Max(1f, StrongHoleFillSeconds);
        public float BilgeSurfaceHeight => Mathf.Lerp(EmptyHeight, FullHeight, Level);
        public float LeakClock
        {
            get
            {
                var ship = GetComponent<PirateSlop.Networking.NetworkShip>();
                return ship != null && ship.TimeManager != null ? (float)ship.TimeManager.Tick * (float)ship.TimeManager.TickDelta : Time.time;
            }
        }
        readonly Dictionary<int, ShipBreach> breaches = new();
        readonly PirateSlop.Ships.ShipBilgeLeakGeometry.Builder pressureBuilder = new();
        List<PirateSlop.Ships.ShipBilgeLeakGeometry.Aperture> apertures = new();
        int pressureRevision = -1;
        PirateSlop.Ships.ShipBilgeWater bilgeWater;
        readonly Vector3[] clipped = new Vector3[4];
        readonly Dictionary<ulong, Dictionary<int, ulong>> impacts = new();
        ulong impactId;
        public int OpenImpactCount
        {
            get
            {
                if (UseBilgeFlooding) { CountBilgeLeaks(out int lower, out int upper); return lower + upper; }
                int count = 0;
                foreach (var impact in impacts.Values)
                    foreach (var mask in impact.Values)
                        if (mask != 0) { count++; break; }
                return count;
            }
        }
        public void BeginImpact() => impactId++;
        public ulong BeginFireImpact() => ++impactId;
        public void RegisterImpact(ulong eventId, int sectionId, ulong fragments)
        {
            if (fragments == 0) return;
            if (!impacts.TryGetValue(eventId, out var impact)) impacts[eventId] = impact = new Dictionary<int, ulong>();
            impact.TryGetValue(sectionId, out ulong previous);
            impact[sectionId] = previous | fragments;
        }
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
            BreachRevision++;
        }
        public void Clear()
        {
            breaches.Clear(); impacts.Clear(); impactId = 0; BreachRevision++; SetLevel(0f);
        }
        public bool CanOpenBreach(Vector3 point)
        {
            if (UseBilgeFlooding)
            {
                float height = transform.InverseTransformPoint(point).y;
                return height >= EmptyHeight && height < FullHeight;
            }
            return OceanSurface.Instance != null && point.y <= OceanSurface.Instance.Height(point) + WaterlineAllowance;
        }
        public bool CanOpenBreach(MeshFilter filter)
        {
            if (filter == null || filter.sharedMesh == null || filter.sharedMesh.vertexCount == 0) return false;
            if (!UseBilgeFlooding) return CanOpenBreach(filter.transform.TransformPoint(filter.sharedMesh.bounds.center));
            var bounds = filter.sharedMesh.bounds;
            var matrix = transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            var point = matrix.MultiplyPoint3x4(bounds.center);
            float extent = Mathf.Abs(matrix.m10) * bounds.extents.x + Mathf.Abs(matrix.m11) * bounds.extents.y + Mathf.Abs(matrix.m12) * bounds.extents.z;
            return point.y + extent > EmptyHeight + .015f && point.y - extent < FullHeight - .015f;
        }
        public void SetSectionBreaches(ShipDamageSection section, ShipSectionSnapshot entry, ShipSectionDefinition definition, bool enabled)
        {
            if (UseBilgeFlooding) enabled &= definition.Type == ShipSectionType.Hull && definition.CanFlood;
            BreachRevision++;
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
                var point = transform.InverseTransformPoint(fragment.transform.TransformPoint(bounds.center));
                var matrix = transform.worldToLocalMatrix * fragment.transform.localToWorldMatrix;
                float extent = Mathf.Abs(matrix.m10) * bounds.extents.x + Mathf.Abs(matrix.m11) * bounds.extents.y + Mathf.Abs(matrix.m12) * bounds.extents.z;
                if (UseBilgeFlooding && (point.y + extent <= EmptyHeight + .015f || point.y - extent >= FullHeight - .015f)) { breaches.Remove(key); continue; }
                float lowerHeight = Mathf.Max(EmptyHeight, point.y - extent);
                if (UseBilgeFlooding) point.y = (lowerHeight + Mathf.Min(FullHeight, point.y + extent)) * .5f;
                var size = Vector3.Scale(bounds.size, fragment.transform.lossyScale);
                float area = Mathf.Clamp(Mathf.Abs(size.y) * Mathf.Max(Mathf.Abs(size.x), Mathf.Abs(size.z)), .02f, 4f);
                breaches[key] = new ShipBreach { SectionId = entry.SectionId, FragmentIndex = i, LocalPoint = point, Area = area, SurfaceMesh = filter.sharedMesh, SurfaceTransform = matrix, LowerPoint = new Vector3(point.x, lowerHeight, point.z), MinimumHeight = EmptyHeight, MaximumHeight = FullHeight };
            }
        }
        public float Simulate(float dt, ShipDestructionProfile profile)
        {
            if (UseBilgeFlooding)
            {
                if (dt <= 0f) return Level;
                PrepareApertures();
                float inflow = 0f;
                foreach (var aperture in apertures)
                {
                    var plane = OceanPlane(aperture.Center);
                    for (int i = 0; i < aperture.Triangles.Length; i += 3)
                    {
                        int wetCount = ClipWetTriangle(aperture.Triangles[i], aperture.Triangles[i + 1], aperture.Triangles[i + 2], plane);
                        for (int j = 2; j < wetCount; j++)
                        {
                            var a = clipped[0]; var b = clipped[j - 1]; var c = clipped[j];
                            float area = Vector3.Cross(transform.TransformVector(b - a), transform.TransformVector(c - a)).magnitude * .5f;
                            float speed = (PressureSpeed((a * 4 + b + c) / 6f, plane) + PressureSpeed((a + b * 4 + c) / 6f, plane) + PressureSpeed((a + b + c * 4) / 6f, plane)) / 3f;
                            inflow += area * speed;
                        }
                    }
                }
                inflow *= StrongHoleRate / Mathf.Max(.01f, ReferenceBreachArea);
                SetLevel(Level + inflow * dt);
                return Level;
            }
            var ocean = OceanSurface.Instance;
            if (ocean == null || dt <= 0f) return Level;
            int count = OpenImpactCount;
            float seconds = count == 1 ? FloodSecondsByHits.x : count == 2 ? FloodSecondsByHits.y : count == 3 ? FloodSecondsByHits.z : FloodSecondsByHits.w;
            float rate = count > 0 ? 1f / Mathf.Max(10f, seconds) : -1f / Mathf.Max(1f, DrainDuration);
            SetLevel(Level + rate * dt);
            return Level;
        }
        public float UpperIntensity(ulong seed, float time)
        {
            float period = Mathf.Max(.1f, UpperLeakPeriod);
            float burst = Mathf.Clamp(UpperLeakBurstDuration, .05f, period);
            float phase = (seed % 997) / 997f * period;
            return Mathf.Repeat(time + phase, period) < burst ? UpperLeakAverage * period / burst : 0f;
        }
        public bool StrongBreach(ShipBreach breach)
        {
            PrepareApertures();
            var plane = OceanPlane(breach.LocalPoint);
            return pressureBuilder.FullyCovered(breach, plane);
        }
        void PrepareApertures()
        {
            if (bilgeWater == null) bilgeWater = GetComponent<PirateSlop.Ships.ShipBilgeWater>();
            if (pressureRevision == BreachRevision) return;
            pressureRevision = BreachRevision;
            apertures = pressureBuilder.Apertures(breaches.Values);
        }
        public Vector4 OceanPlane(Vector3 localPoint)
        {
            var ocean = OceanSurface.Instance;
            if (ocean == null) return new Vector4(0f, -1f, 0f, -100000f);
            var world = transform.TransformPoint(localPoint);
            float height = ocean.Height(world);
            const float step = .5f;
            float x = (ocean.Height(world + Vector3.right * step) - ocean.Height(world - Vector3.right * step)) / (2f * step);
            float z = (ocean.Height(world + Vector3.forward * step) - ocean.Height(world - Vector3.forward * step)) / (2f * step);
            var gradient = new Vector3(x, -1f, z);
            var local = new Vector3(Vector3.Dot(gradient, transform.TransformVector(Vector3.right)), Vector3.Dot(gradient, transform.TransformVector(Vector3.up)), Vector3.Dot(gradient, transform.TransformVector(Vector3.forward)));
            return new Vector4(local.x, local.y, local.z, height - world.y - Vector3.Dot(local, localPoint));
        }
        public static float OceanHead(Vector4 plane, Vector3 point) => plane.x * point.x + plane.y * point.y + plane.z * point.z + plane.w;
        float PressureSpeed(Vector3 point, Vector4 plane)
        {
            float inside = Level > .0001f ? Mathf.Max(0f, ((bilgeWater != null && bilgeWater.isActiveAndEnabled ? bilgeWater.SurfaceHeight(point) : BilgeSurfaceHeight) - point.y) * transform.up.y) : 0f;
            return Mathf.Sqrt(Mathf.Max(0f, OceanHead(plane, point) - inside) / Mathf.Max(.01f, ReferencePressureHead));
        }
        int ClipWetTriangle(Vector3 a, Vector3 b, Vector3 c, Vector4 plane)
        {
            int count = 0;
            var previous = c;
            float before = OceanHead(plane, previous);
            for (int i = 0; i < 3; i++)
            {
                var current = i == 0 ? a : i == 1 ? b : c;
                float after = OceanHead(plane, current);
                if ((before > 0f) != (after > 0f)) clipped[count++] = Vector3.LerpUnclamped(previous, current, before / (before - after));
                if (after > 0f) clipped[count++] = current;
                previous = current; before = after;
            }
            return count;
        }
        public void OpeningHeads(MeshFilter filter, out float lower, out float upper)
        {
            var bounds = filter.sharedMesh.bounds;
            var matrix = transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            var plane = OceanPlane(matrix.MultiplyPoint3x4(bounds.center));
            lower = float.MinValue; upper = float.MaxValue;
            for (int i = 0; i < 8; i++)
            {
                var point = bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                float head = OceanHead(plane, matrix.MultiplyPoint3x4(point));
                lower = Mathf.Max(lower, head); upper = Mathf.Min(upper, head);
            }
        }
        public float WaveHead(Vector3 localPoint)
        {
            var ocean = OceanSurface.Instance;
            if (ocean == null) return 0f;
            var world = transform.TransformPoint(localPoint);
            return Mathf.Max(0f, ocean.Height(world) - world.y);
        }
        public float UpperIntensity(Vector3 localPoint) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(WaveHead(localPoint) / .12f)) * UpperLeakAverage;
        bool TryImpactPoint(Dictionary<int, ulong> impact, out Vector3 point, out bool strong)
        {
            point = Vector3.zero;
            strong = false;
            float weight = 0f;
            float lowerHeight = float.MaxValue;
            foreach (var mask in impact)
                for (int i = 0; i < 64; i++)
                {
                    if ((mask.Value & (1UL << i)) == 0) continue;
                    if (!breaches.TryGetValue(mask.Key * 64 + i, out var breach) && !(i == 0 && breaches.TryGetValue(mask.Key, out breach))) continue;
                    if (breach.LocalPoint.y < EmptyHeight || breach.LocalPoint.y >= FullHeight) continue;
                    float area = Mathf.Max(.01f, breach.Area);
                    point += breach.LowerPoint * area;
                    lowerHeight = Mathf.Min(lowerHeight, breach.LowerPoint.y);
                    weight += area;
                    strong |= StrongBreach(breach);
                }
            if (weight <= 0f) return false;
            point /= weight;
            point.y = lowerHeight;
            return WaveHead(point) > 0f;
        }
        public void CountBilgeLeaks(out int strong, out int upper)
        {
            strong = upper = 0;
            foreach (var impact in impacts.Values)
                if (TryImpactPoint(impact, out _, out bool lower))
                {
                    if (lower) strong++; else upper++;
                }
        }
        public void Pump(float seconds)
        {
            if (UseBilgeFlooding && seconds > 0f) SetLevel(Level - StrongHoleRate * 3f * seconds);
        }
        public void SetLevel(float value)
        {
            Level = Mathf.Clamp01(value);
        }
        void LateUpdate()
        {
            if (UseBilgeFlooding) return;
            if (WaterVisual == null) return;
            visualLevel = Mathf.Lerp(visualLevel, Level, 1f - Mathf.Exp(-8f * Time.deltaTime));
            WaterVisual.gameObject.SetActive(visualLevel > .001f);
            if (WaterSlices != null && WaterSlices.Length > 0)
            {
                if (waterFilter == null) waterFilter = WaterVisual.GetComponent<MeshFilter>();
                waterFilter.sharedMesh = WaterSlices[Mathf.Clamp(Mathf.FloorToInt(visualLevel * (WaterSlices.Length - 1)), 0, WaterSlices.Length - 1)];
            }
            var position = WaterVisual.localPosition;
            position.y = Mathf.Lerp(EmptyHeight, FullHeight, visualLevel);
            WaterVisual.localPosition = position;
        }
    }
}

