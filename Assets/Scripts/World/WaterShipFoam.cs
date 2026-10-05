using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace PirateSlop
{
    [DefaultExecutionOrder(1050), DisallowMultipleComponent]
    public sealed class WaterShipFoam : MonoBehaviour
    {
        public static WaterShipFoam Instance { get; private set; }
        [Min(.05f)] public float ContactWidth = .9f;
        [Range(0, 1)] public float ContactStrength = .55f;
        [Min(.1f)] public float ContactHeightRange = .85f;
        [Range(0, 1)] public float WakeStrength = .9f;
        [Range(0, 2)] public float BowWaveStrength = 1f;
        [Range(0, 2)] public float SprayStrength = 1f;
        [Range(0, .22f)] public float BowWaveHeight = .22f;
        [Min(.1f)] public float HullWidthScale = 1f;
        [Min(.1f)] public float HullLengthScale = 1f;
        [Min(1f)] public float WaterlineBow = 18.216f;
        [Min(1f)] public float WaterlineStern = 15.809f;
        public float[] WaterlineHalfWidths = { 4.74f, 5.84f, 6.12f, 6.26f, 6.51f, 6.40f, 6.63f, 6.42f, 6.56f, 6.33f, 6.23f, 6.26f, 5.84f, 5.67f, 5.06f, 4.48f, 3.57f, 0f };
        const int MaximumShips = 32, WakeSlots = 24, ContactSlots = 256;
        const float AtlasExtent = 512f, ContactLife = 2.8f, WakeLife = 12f;
        sealed class HullState
        {
            public ShipController Ship;
            public bool Ready;
            public Vector3 Position, Source;
            public readonly Vector3[] Probes = new Vector3[38];
            public readonly float[] Gaps = new float[38], LastHit = new float[38], SampleTimes = new float[38];
            public readonly Packet[] Wake = new Packet[WakeSlots];
            public float WakeTime, BowTime, BowSpeed, SprayTime;
            public float Impact;
            public Vector3 ImpactPosition, ImpactWaterVelocity, ImpactHullVelocity, ImpactNormal;
            public int Head;
        }
        struct Packet
        {
            public bool Active, Bow;
            public HullState Owner;
            public Vector2 Position, Direction, Parameter, WaveOffset;
            public float Born, Strength, Width, Length, Seed;
        }
        struct Hit
        {
            public HullState Hull;
            public int Probe;
            public Vector2 Position, Normal, Parameter, WaveOffset;
            public float Energy, Priority;
        }
        struct SurfaceImpact
        {
            public Vector2 Position, Direction, Parameter, Offset;
            public float Born, Strength, Radius, Life, Stretch;
        }
        readonly SurfaceImpact[] surfaceImpacts = new SurfaceImpact[64];
        int surfaceImpactHead;
        public int SurfaceImpactCount { get; private set; }

        public void EmitSurfaceImpact(WaterImpactEvent impact)
        {
            if (ocean == null || impact.Strength <= 0f) return;
            var position = impact.Position;
            ocean.SampleFoamSurface(position, out _, out _, out var parameter);
            Vector2 direction = new Vector2(impact.Tangent.x, impact.Tangent.z).normalized;
            if (direction.sqrMagnitude < .001f) direction = Vector2.right;
            surfaceImpacts[surfaceImpactHead++ % surfaceImpacts.Length] = new SurfaceImpact {
                Position = new Vector2(position.x, position.z), Direction = direction, Parameter = parameter,
                Offset = ocean.FoamHorizontalOffset(parameter), Born = Time.time,
                Strength = Mathf.Clamp(impact.Strength * .8f, .1f, 1.2f), Radius = Mathf.Clamp(impact.Radius, .2f, 1.5f),
                Life = Mathf.Lerp(1.2f, 2.8f, Mathf.Clamp01(impact.Strength)),
                Stretch = Mathf.Lerp(1f, 1.8f, Mathf.Clamp01(impact.Drift / 7f))
            };
        }

        readonly RaycastHit[] sprayHits = new RaycastHit[64];
        readonly HullState[] hulls = new HullState[MaximumShips];
        readonly Packet[] contacts = new Packet[ContactSlots];
        readonly List<Hit> hits = new(64);
        readonly List<Vector3> vertices = new(11000);
        readonly List<Vector2> uvs = new(11000);
        readonly List<Vector4> ages = new(11000);
        readonly List<int> triangles = new(16500);
        BoatAttackOcean ocean;
        WaterBowSpray spray;
        RenderTexture atlas;
        Material stampMaterial;
        Mesh stampMesh;
        CommandBuffer commands;
        Vector2 atlasOrigin;
        float sampledAt;
        int frame = -1, contactHead, sequence;
        public int ActiveContactCount { get; private set; }
        public int ActiveWakeCount { get; private set; }
        public int ContactBursts { get; private set; }
        public int BowPackets { get; private set; }
        public float MaximumBowSpeed { get; private set; }
        static readonly int CountId = Shader.PropertyToID("_WaterShipCount");
        static readonly int AtlasId = Shader.PropertyToID("_WaterShipFoamAtlas");
        static readonly int MappingId = Shader.PropertyToID("_WaterShipFoamMapping");
        public static WaterShipFoam Ensure(GameObject owner)
        {
            if (Instance != null) return Instance;
            var component = owner.GetComponent<WaterShipFoam>();
            return component != null ? component : owner.AddComponent<WaterShipFoam>();
        }
        void OnEnable()
        {
            if (!Application.isPlaying) return;
            Instance = this;
            ocean = GetComponent<BoatAttackOcean>();
            spray = WaterBowSpray.Ensure(gameObject);
            frame = -1;
            sampledAt = Time.time;
            var shader = Resources.Load<Shader>("EnvironmentTest/ShipFoamAtlas");
            if (shader == null) { Debug.LogError("Ship foam atlas shader is missing.", this); return; }
            stampMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            stampMesh = new Mesh { name = "Dynamic ship foam stamps", hideFlags = HideFlags.HideAndDontSave };
            stampMesh.MarkDynamic();
            atlas = new RenderTexture(2048, 2048, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear) { name = "Ship foam history", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            atlas.Create();
            commands = new CommandBuffer { name = "Ship foam history" };
        }
        void OnDisable()
        {
            if (Instance == this) Instance = null;
            System.Array.Clear(hulls, 0, hulls.Length);
            System.Array.Clear(contacts, 0, contacts.Length);
            System.Array.Clear(surfaceImpacts, 0, surfaceImpacts.Length);
            surfaceImpactHead = SurfaceImpactCount = 0;
            ActiveContactCount = ActiveWakeCount = ContactBursts = contactHead = sequence = 0;
            frame = -1;
            commands?.Release();
            commands = null;
            if (atlas != null) { atlas.Release(); Destroy(atlas); }
            if (stampMesh != null) Destroy(stampMesh);
            if (stampMaterial != null) Destroy(stampMaterial);
            atlas = null; stampMesh = null; stampMaterial = null;
        }
        void LateUpdate()
        {
            Collect();
            if (ocean != null && ocean.Water != null)
            {
                Apply(ocean.Water.RuntimeMaterial);
                Apply(ocean.Water.RuntimeInfiniteMaterial);
            }
        }
        void Collect()
        {
            if (frame == Time.frameCount || atlas == null || ocean == null || ocean.Surface == null) return;
            frame = Time.frameCount;
            float now = Time.time, dt = now - sampledAt;
            if (dt >= .1f)
            {
                hits.Clear();
                SampleShips(now, dt);
                for (int emitted = 0; emitted < 4 && hits.Count > 0; emitted++)
                {
                    int best = 0;
                    for (int i = 1; i < hits.Count; i++) if (hits[i].Priority > hits[best].Priority) best = i;
                    var hit = hits[best];
                    hit.Hull.LastHit[hit.Probe] = now;
                    contacts[contactHead] = new Packet { Active = true, Owner = hit.Hull, Position = hit.Position, Direction = hit.Normal, Parameter = hit.Parameter, WaveOffset = hit.WaveOffset, Born = now, Strength = ContactStrength * hit.Energy, Width = ContactWidth * .65f, Length = 1.6f, Seed = ++sequence * .731f };
                    contactHead = (contactHead + 1) % ContactSlots;
                    ContactBursts++;
                    hits.RemoveAt(best);
                }
                sampledAt = now;
            }
            RenderAtlas(now);
        }
        void SampleShips(float now, float dt)
        {
            int bowBudget = 8, sprayBudget = 2;
            MaximumBowSpeed = 0;
            for (int i = 0; i < hulls.Length; i++)
                if (hulls[i] != null && (hulls[i].Ship == null || !hulls[i].Ship.gameObject.activeInHierarchy)) hulls[i] = null;
            var camera = Camera.main;
            var focus = camera != null ? camera.transform.position : ocean.transform.position;
            foreach (var ship in ShipController.ActiveControllers)
            {
                if (ship == null || !ship.gameObject.activeInHierarchy) continue;
                HullState state = null;
                for (int i = 0; i < hulls.Length; i++) if (hulls[i]?.Ship == ship) { state = hulls[i]; break; }
                if (state == null)
                    for (int i = 0; i < hulls.Length; i++) if (hulls[i] == null) { state = hulls[i] = new HullState { Ship = ship }; break; }
                if (state == null) continue;
                var body = ship.transform;
                var position = body.position;
                var movement = position - state.Position;
                bool continuous = state.Ready && movement.sqrMagnitude < 400f;
                if (!continuous)
                {
                    System.Array.Clear(state.Wake, 0, state.Wake.Length);
                    state.BowSpeed = state.Impact = 0;
                    state.BowTime = state.SprayTime = now;
                    System.Array.Clear(state.SampleTimes, 0, state.SampleTimes.Length);
                    System.Array.Clear(state.LastHit, 0, state.LastHit.Length);
                    for (int index = 0; index < contacts.Length; index++) if (contacts[index].Owner == state) contacts[index].Active = false;
                }
                var velocity = continuous ? movement / dt : Vector3.zero;
                float speed = new Vector2(velocity.x, velocity.z).magnitude;
                float widthScale = Mathf.Max(.1f, ship.HullFootprint.x / 13f) * HullWidthScale;
                float lengthScale = Mathf.Max(.1f, ship.HullFootprint.y / 46f) * HullLengthScale;
                float distance = Vector2.Distance(new Vector2(focus.x, focus.z), new Vector2(position.x, position.z));
                bool near = distance < 280f;
                Hit first = default, second = default;
                state.Impact = 0;
                float forwardSpeed = 0;
                for (int probe = 0; probe < 38; probe++)
                {
                    int section = probe / 2;
                    float sign = (probe & 1) == 0 ? -1f : 1f;
                    float z = Mathf.Lerp(-WaterlineStern, WaterlineBow, section / 17f) * lengthScale;
                    float width = Width(Mathf.Min(section, 17)) * widthScale;
                    float derivative = (Width(Mathf.Min(section + 1, 17)) - Width(Mathf.Max(section - 1, 0))) * widthScale / Mathf.Max(.1f, (section == 0 || section == 17 ? 1f : 2f) * (WaterlineStern + WaterlineBow) * lengthScale / 17f);
                    var local = probe < 36 ? new Vector3(sign * width, 0, z) : new Vector3(0, 0, probe == 36 ? -WaterlineStern * lengthScale : WaterlineBow * lengthScale);
                    var normal = body.TransformDirection(probe < 36 ? new Vector3(sign, 0, -derivative).normalized : (probe == 36 ? Vector3.back : Vector3.forward));
                    var point = body.TransformPoint(local);
                    if (!near || (distance > 150f && probe < 36 && section % 3 != 0)) continue;
                    ocean.SampleFoamSurface(point, out float height, out var waterVelocity, out var parameter);
                    float gap = height + ocean.Surface.SeaLevel + ocean.Surface.GetWhirlpoolHeight(point) - point.y;
                    if (probe == 37) forwardSpeed = continuous ? Mathf.Max(0, Vector3.Dot(velocity - waterVelocity, new Vector3(body.forward.x, 0, body.forward.z).normalized)) : 0;
                    if (continuous && now - state.SampleTimes[probe] < dt * 1.5f)
                    {
                        var relative = waterVelocity - (point - state.Probes[probe]) / dt;
                        float inward = Mathf.Max(0, -Vector2.Dot(new Vector2(relative.x, relative.z), new Vector2(normal.x, normal.z).normalized));
                        float rise = Mathf.Max(0, (gap - state.Gaps[probe]) / dt);
                        float wet = 1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(ContactHeightRange * .4f, ContactHeightRange, Mathf.Abs(gap)));
                        float energy = wet * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.18f, 1.6f, Mathf.Max(rise, inward * .28f)));
                        float impact = wet * rise;
                        if ((probe >= 28 && probe < 36 || probe == 37) && impact > state.Impact)
                        {
                            state.Impact = impact;
                            state.ImpactPosition = new Vector3(point.x, point.y + gap + .05f, point.z);
                            state.ImpactWaterVelocity = waterVelocity;
                            state.ImpactHullVelocity = (point - state.Probes[probe]) / dt - waterVelocity;
                            state.ImpactNormal = normal;
                        }
                        if (energy > .08f && now - state.LastHit[probe] > .4f)
                        {
                            var hit = new Hit { Hull = state, Probe = probe, Position = new Vector2(point.x, point.z), Normal = new Vector2(normal.x, normal.z).normalized, Parameter = parameter, WaveOffset = ocean.FoamHorizontalOffset(parameter), Energy = energy, Priority = energy / (1 + distance / 80f) };
                            if (hit.Priority > first.Priority) { second = first; first = hit; }
                            else if (hit.Priority > second.Priority) second = hit;
                        }
                    }
                    state.Probes[probe] = point;
                    state.Gaps[probe] = gap;
                    state.SampleTimes[probe] = now;
                }
                state.BowSpeed = Mathf.Lerp(state.BowSpeed, forwardSpeed, 1f - Mathf.Exp(-dt / .25f));
                MaximumBowSpeed = Mathf.Max(MaximumBowSpeed, state.BowSpeed);
                if (near && continuous && now - state.BowTime >= .25f && state.BowSpeed > .5f && BowWaveStrength > 0 && bowBudget >= 7)
                {
                    EmitBow(state, body, widthScale, lengthScale, now);
                    state.BowTime = now;
                    bowBudget -= 7;
                }
                if (near && continuous && sprayBudget > 0 && state.Impact > 1.25f && now - state.SprayTime > .6f && SprayStrength > 0 && spray != null)
                {
                    var origin = ResolveSprayOrigin(state, out var outward);
                    spray.EmitImpact(origin, state.ImpactWaterVelocity, state.ImpactHullVelocity, outward, Mathf.InverseLerp(1.25f, 3.5f, state.Impact), SprayStrength);
                    state.SprayTime = now;
                    sprayBudget--;
                }
                if (first.Priority > 0) hits.Add(first);
                if (second.Priority > 0) hits.Add(second);
                bool forwardTravel = Vector3.Dot(velocity, body.forward) >= 0;
                var source = body.TransformPoint(new Vector3(0, 0, forwardTravel ? -WaterlineStern * lengthScale : WaterlineBow * lengthScale));
                if (!continuous || !near || speed < .35f)
                {
                    state.Source = source;
                    state.WakeTime = now;
                }
                else if (now - state.WakeTime >= .5f)
                {
                    var from = new Vector2(state.Source.x, state.Source.z);
                    var to = new Vector2(source.x, source.z);
                    var delta = to - from;
                    if (delta.sqrMagnitude > .04f && delta.sqrMagnitude < 400f)
                    {
                        var center = (from + to) * .5f;
                        ocean.SampleFoamSurface(new Vector3(center.x, 0, center.y), out _, out _, out var parameter);
                        state.Wake[state.Head] = new Packet { Active = true, Position = center, Direction = delta.normalized, Parameter = parameter, WaveOffset = ocean.FoamHorizontalOffset(parameter), Born = now, Strength = WakeStrength * Mathf.Clamp01(speed / 8f), Width = Mathf.Max(.5f, forwardTravel ? Width(0) : Width(17)) * widthScale, Length = delta.magnitude * .5f + .8f, Seed = ++sequence * .731f };
                        state.Head = (state.Head + 1) % WakeSlots;
                    }
                    state.Source = source;
                    state.WakeTime = now;
                }
                state.Position = position;
                state.Ready = true;
            }
        }
        Vector3 ResolveSprayOrigin(HullState state, out Vector3 outward)
        {
            Vector3 point = state.ImpactPosition;
            outward = Vector3.ProjectOnPlane(state.ImpactNormal, Vector3.up).normalized;
            if (outward.sqrMagnitude < .001f) outward = Vector3.ProjectOnPlane(state.Ship.transform.forward, Vector3.up).normalized;
            for (int iteration = 0; iteration < 2; iteration++)
            {
                point.y = ocean.Surface.Height(point) + .055f;
                int count = Physics.RaycastNonAlloc(point + outward * 6f, -outward, sprayHits, 12f, ~0, QueryTriggerInteraction.Ignore);
                var found = count < sprayHits.Length ? sprayHits : Physics.RaycastAll(point + outward * 6f, -outward, 12f, ~0, QueryTriggerInteraction.Ignore);
                if (found != sprayHits) count = found.Length;
                float nearest = 12f;
                RaycastHit contact = default;
                for (int i = 0; i < count; i++)
                {
                    var hit = found[i];
                    if (hit.distance >= nearest || hit.collider.GetComponentInParent<ShipController>() != state.Ship || Vector3.Dot(hit.normal, outward) < .1f) continue;
                    nearest = hit.distance; contact = hit;
                }
                if (contact.collider == null) break;
                point = contact.point + outward * .10f;
            }
            point.y = ocean.Surface.Height(point) + .055f;
            return point;
        }

        void EmitBow(HullState state, Transform body, float widthScale, float lengthScale, float now)
        {
            float strength = BowWaveStrength * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.5f, 8f, state.BowSpeed));
            strength *= 1f + .25f * Mathf.Clamp01(state.Impact / 3.5f);
            for (int section = 14; section <= 16; section++)
            {
                float z = Mathf.Lerp(-WaterlineStern, WaterlineBow, section / 17f) * lengthScale;
                float derivative = (Width(section + 1) - Width(section - 1)) * widthScale / (2f * (WaterlineStern + WaterlineBow) * lengthScale / 17f);
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    var normal = body.TransformDirection(new Vector3(sign, 0, -derivative).normalized);
                    var point = body.TransformPoint(new Vector3(sign * Width(section) * widthScale, 0, z)) + normal * .55f;
                    AddBow(state, point, normal, .65f, 1.7f * lengthScale, strength * 1.05f, now);
                }
            }
            AddBow(state, body.TransformPoint(new Vector3(0, 0, WaterlineBow * lengthScale + .7f)), body.forward, .85f, .9f, strength * .7f, now);
        }
        void AddBow(HullState state, Vector3 point, Vector3 normal, float width, float length, float strength, float now)
        {
            ocean.SampleFoamSurface(point, out _, out _, out var parameter);
            contacts[contactHead] = new Packet { Active = true, Bow = true, Owner = state, Position = new Vector2(point.x, point.z), Direction = new Vector2(normal.x, normal.z).normalized, Parameter = parameter, WaveOffset = ocean.FoamHorizontalOffset(parameter), Born = now, Strength = strength, Width = width, Length = length, Seed = ++sequence * .731f };
            contactHead = (contactHead + 1) % ContactSlots;
        }
        float Width(int section) => WaterlineHalfWidths != null && section < WaterlineHalfWidths.Length ? Mathf.Max(0, WaterlineHalfWidths[section]) : 0;
        void RenderAtlas(float now)
        {
            vertices.Clear(); uvs.Clear(); ages.Clear(); triangles.Clear();
            ActiveContactCount = ActiveWakeCount = BowPackets = 0;
            var camera = Camera.main;
            var focus = camera != null ? camera.transform.position : ocean.transform.position;
            atlasOrigin = new Vector2(Mathf.Floor(focus.x / 8f) * 8f, Mathf.Floor(focus.z / 8f) * 8f) - Vector2.one * (AtlasExtent * .5f);
            foreach (var hull in hulls)
            {
                if (hull?.Ship == null || !hull.Ship.gameObject.activeInHierarchy) continue;
                StampHull(hull.Ship);
                StampBowHeight(hull);
            }
            foreach (var packet in contacts)
            {
                if (!packet.Active || packet.Owner?.Ship == null || !packet.Owner.Ship.gameObject.activeInHierarchy || now - packet.Born >= (packet.Bow ? 1.4f : ContactLife)) continue;
                if (packet.Bow) BowPackets++; else ActiveContactCount++;
                float age = now - packet.Born;
                var position = packet.Position + ocean.FoamHorizontalOffset(packet.Parameter) - packet.WaveOffset + packet.Direction * ((packet.Bow ? 1f : .18f) * age);
                Stamp(position, packet.Direction, packet.Width + age * .3f, packet.Length + age * .28f, packet.Strength, age / (packet.Bow ? 1.4f : ContactLife), packet.Seed);
            }
            foreach (var hull in hulls)
            {
                if (hull == null) continue;
                foreach (var packet in hull.Wake)
                {
                    if (!packet.Active || now - packet.Born >= WakeLife) continue;
                    ActiveWakeCount++;
                    float age = now - packet.Born;
                    var position = packet.Position + ocean.FoamHorizontalOffset(packet.Parameter) - packet.WaveOffset;
                    var side = new Vector2(packet.Direction.y, -packet.Direction.x);
                    float spread = packet.Width + age * .7f;
                    float strength = packet.Strength * Mathf.Exp(-age / 5f);
                    Stamp(position + side * spread, side, .9f + age * .14f, packet.Length + age * .12f, strength * .65f, age / WakeLife, packet.Seed, true);
                    Stamp(position - side * spread, -side, .9f + age * .14f, packet.Length + age * .12f, strength * .65f, age / WakeLife, packet.Seed + 7f, true);
                    Stamp(position, side, packet.Width + age * .18f, packet.Length + age * .12f, strength * .28f, age / WakeLife, packet.Seed + 13f, true);
                }
            }
            SurfaceImpactCount = 0;
            foreach (var impact in surfaceImpacts)
            {
                float age = now - impact.Born;
                if (impact.Life <= 0f || age >= impact.Life) continue;
                SurfaceImpactCount++;
                Vector2 position = impact.Position + ocean.FoamHorizontalOffset(impact.Parameter) - impact.Offset
                    + impact.Direction * (age * (impact.Stretch - 1f) * .5f);
                float radius = impact.Radius + age * Mathf.Lerp(.5f, 1.2f, Mathf.Clamp01(impact.Strength));
                Stamp(position, impact.Direction, radius * impact.Stretch, radius, impact.Strength, age / impact.Life, impact.Born * .731f, false, 4);
            }
            stampMesh.Clear();
            if (vertices.Count > 0)
            {
                stampMesh.SetVertices(vertices); stampMesh.SetUVs(0, uvs); stampMesh.SetUVs(1, ages); stampMesh.SetTriangles(triangles, 0, false);
                stampMesh.bounds = new Bounds(new Vector3(.5f, .5f, 0), Vector3.one * 4f);
            }
            stampMaterial.SetVector("_AtlasWorld", new Vector4(atlasOrigin.x, atlasOrigin.y, AtlasExtent, now));
            commands.Clear();
            commands.SetRenderTarget(atlas);
            commands.ClearRenderTarget(false, true, Color.clear);
            if (vertices.Count > 0) commands.DrawMesh(stampMesh, Matrix4x4.identity, stampMaterial, 0, 0);
            Graphics.ExecuteCommandBuffer(commands);
        }

        public float BowHeight(Vector3 point, ShipController exclude = null)
        {
            float height = 0;
            foreach (var hull in hulls)
            {
                if (hull?.Ship == null || hull.Ship == exclude || !hull.Ready || !hull.Ship.gameObject.activeInHierarchy) continue;
                float amplitude = BowWaveHeight * Mathf.Clamp01(BowWaveStrength * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.5f, 8f, hull.BowSpeed)));
                if (amplitude <= 0) continue;
                var delta = point - hull.Ship.transform.position;
                var forward = new Vector3(hull.Ship.transform.forward.x, 0, hull.Ship.transform.forward.z).normalized;
                var right = new Vector3(forward.z, 0, -forward.x);
                float scale = Mathf.Max(.1f, hull.Ship.HullFootprint.y / 46f) * HullLengthScale;
                float s = WaterlineBow * scale + .8f - Vector3.Dot(delta, forward);
                float x = Vector3.Dot(delta, right);
                float shoulder = .6f + Mathf.Max(0, s) * 1.4f;
                float width = 1.5f + Mathf.Max(0, s) * .03f;
                float cross = (Mathf.Abs(x) - shoulder) / width;
                float crest = Mathf.Exp(-cross * cross) * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-.6f, .4f, s)) * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(7f, 10f, s)));
                height = Mathf.Max(height, amplitude * crest);
            }
            return height;
        }
        void StampBowHeight(HullState hull)
        {
            float amount = Mathf.Clamp01(BowWaveStrength * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.5f, 8f, hull.BowSpeed)));
            if (amount <= .001f || BowWaveHeight <= 0) return;
            var body = hull.Ship.transform;
            var forward = new Vector2(body.forward.x, body.forward.z).normalized;
            var right = new Vector2(forward.y, -forward.x);
            float scale = Mathf.Max(.1f, hull.Ship.HullFootprint.y / 46f) * HullLengthScale;
            var origin = new Vector2(body.position.x, body.position.z) + forward * (WaterlineBow * scale + .8f);
            int start = vertices.Count;
            for (int i = 0; i < 4; i++)
            {
                var local = new Vector2(i == 0 || i == 3 ? -18 : 18, i < 2 ? -2 : 12);
                var point = (origin + right * local.x - forward * local.y - atlasOrigin) / AtlasExtent;
                vertices.Add(new Vector3(point.x, point.y, 0));
                uvs.Add(local);
                ages.Add(new Vector4(BowWaveHeight * amount, 0, amount, 3));
            }
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
        }
        void StampHull(ShipController ship)
        {
            var body = ship.transform;
            var center = new Vector2(body.position.x, body.position.z);
            if (center.x < atlasOrigin.x - 30 || center.y < atlasOrigin.y - 30 || center.x > atlasOrigin.x + AtlasExtent + 30 || center.y > atlasOrigin.y + AtlasExtent + 30) return;
            float widthScale = Mathf.Max(.1f, ship.HullFootprint.x / 13f) * HullWidthScale;
            float lengthScale = Mathf.Max(.1f, ship.HullFootprint.y / 46f) * HullLengthScale;
            int start = vertices.Count;
            var origin = (center - atlasOrigin) / AtlasExtent;
            vertices.Add(new Vector3(origin.x, origin.y, 0)); uvs.Add(Vector2.zero); ages.Add(new Vector4(0, 0, 0, 1));
            for (int i = 0; i < 36; i++)
            {
                int section = i < 18 ? i : 35 - i;
                float side = i < 18 ? -1f : 1f;
                float z = Mathf.Lerp(-WaterlineStern, WaterlineBow, section / 17f) * lengthScale;
                if (section == 0) z -= 2f;
                if (section == 17) z += 2f;
                var point = body.TransformPoint(new Vector3(side * (Width(section) * widthScale + 2f), 0, z));
                var uv = (new Vector2(point.x, point.z) - atlasOrigin) / AtlasExtent;
                vertices.Add(new Vector3(uv.x, uv.y, 0)); uvs.Add(Vector2.zero); ages.Add(new Vector4(0, 0, 0, 1));
            }
            for (int i = 0; i < 36; i++)
            {
                triangles.Add(start); triangles.Add(start + i + 1); triangles.Add(start + (i + 1) % 36 + 1);
            }
        }

        void Stamp(Vector2 position, Vector2 axis, float width, float length, float strength, float age, float seed, bool wake = false, int kind = -1)
        {
            if (position.x < atlasOrigin.x - 24 || position.y < atlasOrigin.y - 24 || position.x > atlasOrigin.x + AtlasExtent + 24 || position.y > atlasOrigin.y + AtlasExtent + 24) return;
            var along = new Vector2(-axis.y, axis.x) * length;
            var across = axis * width;
            int start = vertices.Count;
            for (int i = 0; i < 4; i++)
            {
                var uv = new Vector2(i == 0 || i == 3 ? -1 : 1, i < 2 ? -1 : 1);
                var point = (position + across * uv.x + along * uv.y - atlasOrigin) / AtlasExtent;
                vertices.Add(new Vector3(point.x, point.y, 0)); uvs.Add(uv); ages.Add(new Vector4(age, seed, strength, kind >= 0 ? kind : wake ? 2 : 0));
            }
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
        }
        public static void Apply(Material material)
        {
            if (material == null) return;
            var source = Instance;
            if (source == null || source.atlas == null || source.frame < 0) { material.SetInteger(CountId, 0); return; }
            material.SetInteger(CountId, 1);
            material.SetTexture(AtlasId, source.atlas);
            material.SetVector(MappingId, new Vector4(source.atlasOrigin.x, source.atlasOrigin.y, 1f / AtlasExtent, 1f / AtlasExtent));
        }
        public static void Apply(MaterialPropertyBlock properties)
        {
            if (properties == null) return;
            var source = Instance;
            if (source == null || source.atlas == null || source.frame < 0) { properties.SetInteger(CountId, 0); return; }
            properties.SetInteger(CountId, 1);
            properties.SetTexture(AtlasId, source.atlas);
            properties.SetVector(MappingId, new Vector4(source.atlasOrigin.x, source.atlasOrigin.y, 1f / AtlasExtent, 1f / AtlasExtent));
        }
    }
}
