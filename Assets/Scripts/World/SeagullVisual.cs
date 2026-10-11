using UnityEngine;
using UnityEngine.Rendering;

namespace PirateSlop.World
{
    public struct SeagullFall
    {
        public Vector3 Position, Velocity;
        public Quaternion Rotation;
        public float Seconds, Age, AnimationTime, Power, Fold, Feet;
        public bool Landed, OnWater;
        public Vector3 ContactPosition, ContactNormal, LocalContactPosition;
        public Quaternion ContactRotation, LocalContactRotation;
        public FishNet.Object.NetworkObject Support;
    }

    public sealed class SeagullVisual : MonoBehaviour
    {
        public const float FootHeight = .225f;
        public const float SinkSeconds = 3f;
        static Mesh sphere, beak, tail, feetMesh;
        static readonly Mesh[] inner = new Mesh[2], outer = new Mesh[2];
        static Material white, dark, feetMaterial;
        readonly Transform[] shoulders = new Transform[2], tips = new Transform[2];
        Transform body, head, feet;
        float phase, frequency;
        float animationTime, animationPower, animationFold, animationFeet;
        readonly System.Collections.Generic.List<Collider> hitboxes = new();
        readonly Quaternion[] fallShoulders = new Quaternion[2], fallTips = new Quaternion[2];
        SeagullFall fall;
        float fallStarted, checkedFallAge;
        bool splashed, contactConfirmed;
        Transform contactSupport;
        Vector3 localContactPosition;
        Quaternion localContactRotation;
        public bool Shot { get; private set; }
        public System.Action<Vector3> FirearmHit;
        public System.Action<SeagullFall> SurfaceContact;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetMeshes()
        {
            foreach (var mesh in new[] { sphere, beak, tail, feetMesh, inner[0], inner[1], outer[0], outer[1] })
                if (mesh != null) Destroy(mesh);
            sphere = beak = tail = feetMesh = null;
            inner[0] = inner[1] = outer[0] = outer[1] = null;
            white = dark = feetMaterial = null;
        }

        public static SeagullVisual Create(Transform parent, int seed)
        {
            var bird = new GameObject("Seagull").AddComponent<SeagullVisual>();
            bird.transform.SetParent(parent, false);
            bird.Initialize(seed);
            return bird;
        }

        public void Initialize(int seed)
        {
            if (body != null) return;
            EnsureMeshes();
            phase = (seed & 1023) * .037f;
            frequency = 1.75f + (seed & 15) * .012f;
            body = Pivot(transform, "BodyRig");
            Part(body, "Body", sphere, Vector3.zero, new Vector3(.23f, .2f, .65f), white);
            Part(body, "Tail", tail, new Vector3(0, 0, -.32f), Vector3.one, dark);
            head = Pivot(body, "HeadRig");
            head.localPosition = new Vector3(0, .09f, .25f);
            Part(head, "Head", sphere, new Vector3(0, 0, .05f), Vector3.one * .19f, white);
            Part(head, "Beak", beak, new Vector3(0, -.03f, .13f), Vector3.one, dark);
            for (int side = 0; side < 2; side++)
            {
                float sign = side == 0 ? -1 : 1;
                shoulders[side] = Pivot(body, side == 0 ? "LeftWing" : "RightWing");
                shoulders[side].localPosition = new Vector3(sign * .07f, .02f, 0);
                Part(shoulders[side], "InnerWing", inner[side], Vector3.zero, Vector3.one, white);
                tips[side] = Pivot(shoulders[side], "Wingtip");
                tips[side].localPosition = new Vector3(sign * .45f, 0, 0);
                Part(tips[side], "OuterWing", outer[side], Vector3.zero, Vector3.one, dark);
            }
            feet = Part(transform, "Feet", feetMesh, Vector3.zero, Vector3.one, feetMaterial);
            Hitbox(body, Vector3.zero, new Vector3(.25f, .22f, .67f));
            Hitbox(head, new Vector3(0, 0, .08f), new Vector3(.19f, .19f, .27f));
            for (int side = 0; side < 2; side++)
            {
                float sign = side == 0 ? -1 : 1;
                Hitbox(shoulders[side], new Vector3(sign * .23f, 0, -.03f), new Vector3(.46f, .065f, .34f));
                Hitbox(tips[side], new Vector3(sign * .27f, 0, -.03f), new Vector3(.54f, .055f, .28f));
            }
            Animate(0, .6f, 0, 0);
        }

        public void Animate(float time, float power, float fold, float feetBlend, float flare = 0, bool resting = false)
        {
            animationTime = time; animationPower = power; animationFold = fold; animationFeet = feetBlend;
            float beat = time * frequency * Mathf.PI * 2 + phase + Mathf.Sin(time * .43f + phase) * .12f;
            fold = Mathf.Clamp01(fold);
            power = Mathf.Clamp01(power);
            for (int side = 0; side < 2; side++)
            {
                float sign = side == 0 ? -1 : 1;
                float wingBeat = beat + sign * .035f;
                float stroke = (Mathf.Sin(wingBeat) + Mathf.Sin(wingBeat * 2f) * .14f) * .88f;
                float shoulderAngle = 5f + stroke * 25f * power + Mathf.Sin(time * .7f + phase) * 1.3f * (1f - power);
                float tipAngle = Mathf.Sin(wingBeat - 1.05f) * 11f * power;
                shoulders[side].localRotation = Quaternion.Euler(0, sign * 78 * fold, sign * Mathf.Lerp(shoulderAngle, -6, fold));
                tips[side].localRotation = Quaternion.Euler(0, sign * 30 * fold, sign * tipAngle * (1 - fold));
                tips[side].localScale = new Vector3(Mathf.Lerp(1, .6f, fold), 1, Mathf.Lerp(1, .85f, fold));
            }
            body.localRotation = Quaternion.Euler(-flare * 13, 0, 0);
            body.localPosition = Vector3.up * Mathf.Lerp(Mathf.Sin(beat - .45f) * .006f * power, Mathf.Sin(time * 1.9f + phase) * .003f, fold);
            head.localRotation = Quaternion.Euler(Mathf.Sin(time * .37f + phase) * 5 * fold,
                Mathf.Sin(time * .31f + phase) * 23 * fold, 0);
            feet.gameObject.SetActive(feetBlend > .01f);
            feet.localScale = new Vector3(1, Mathf.Clamp01(feetBlend), 1);
        }

        void Hitbox(Transform parent, Vector3 center, Vector3 size)
        {
            var shape = parent.gameObject.AddComponent<BoxCollider>();
            shape.isTrigger = true;
            shape.center = center;
            shape.size = size;
            hitboxes.Add(shape);
        }

        public void ReceiveFirearmHit(Vector3 direction)
        {
            if (!Shot) FirearmHit?.Invoke(direction);
        }

        public SeagullFall CaptureFall(Vector3 velocity, Vector3 direction)
        {
            velocity = Vector3.ClampMagnitude(velocity, 16f) * .7f + direction.normalized * 2.5f;
            velocity.y = Mathf.Min(velocity.y, 1f);
            var state = new SeagullFall {
                Position = transform.position, Rotation = transform.rotation, Velocity = velocity,
                AnimationTime = animationTime, Power = animationPower, Fold = animationFold, Feet = animationFeet
            };
            var point = state.Position;
            for (int i = 0; i < 3; i++)
            {
                float surface = OceanSurface.Instance != null ? OceanSurface.Instance.Height(point) : 0f;
                float height = Mathf.Max(0, state.Position.y - surface - .04f);
                state.Seconds = Mathf.Max(.18f, (velocity.y + Mathf.Sqrt(velocity.y * velocity.y + 20f * height)) / 10f);
                point = FallPoint(state, state.Seconds);
            }
            return state;
        }

        static Vector3 FallPoint(SeagullFall state, float age)
        {
            float drift = (1f - Mathf.Exp(-.7f * age)) / .7f;
            return state.Position + Vector3.ProjectOnPlane(state.Velocity, Vector3.up) * drift
                + Vector3.up * (state.Velocity.y * age - 5f * age * age);
        }

        public void BeginFall(SeagullFall state)
        {
            if (Shot)
            {
                if (!state.Landed || contactConfirmed) return;
                SetContactSupport(state, contactSupport);
                fall = state;
                fallStarted = Time.time - state.Age;
                contactConfirmed = true;
                splashed |= state.Age - state.Seconds > .3f;
                UpdateFall();
                return;
            }
            Shot = true;
            fall = state;
            fallStarted = Time.time - state.Age;
            checkedFallAge = 0;
            contactConfirmed = state.Landed;
            splashed = state.Landed && state.Age - state.Seconds > .3f;
            SetContactSupport(state, null);
            foreach (var shape in hitboxes) shape.enabled = false;
            Animate(state.AnimationTime, state.Power, state.Fold, state.Feet);
            for (int side = 0; side < 2; side++)
            {
                fallShoulders[side] = shoulders[side].localRotation;
                fallTips[side] = tips[side].localRotation;
            }
            gameObject.SetActive(true);
            UpdateFall();
        }

        void LateUpdate()
        {
            if (Shot) UpdateFall();
        }

        void UpdateFall()
        {
            float age = Mathf.Max(0, Time.time - fallStarted);
            if (!fall.Landed) CheckSurfaceContact(age);
            float sinking = Mathf.Max(0, age - fall.Seconds);
            if (fall.Landed && sinking >= SinkSeconds) { gameObject.SetActive(false); return; }
            float falling = fall.Landed ? fall.Seconds : age;
            var point = FallPoint(fall, falling);
            float sideSign = Mathf.Sin(phase) >= 0 ? 1f : -1f;
            float turn = Mathf.SmoothStep(0, 1, Mathf.Clamp01(age / .65f));
            var rotation = fall.Rotation * Quaternion.Euler(55f * turn, 12f * sideSign * turn,
                sideSign * (95f * turn + Mathf.Sin(falling * 4.5f) * 12f * turn));
            if (fall.Landed && fall.OnWater)
            {
                point = fall.ContactPosition;
                float surface = OceanSurface.Instance != null ? OceanSurface.Instance.Height(point) : point.y - .04f;
                float progress = Mathf.Clamp01(sinking / SinkSeconds);
                point.y = surface + .04f - 1.35f * Mathf.SmoothStep(0, 1, progress)
                    + Mathf.Sin(sinking * 3.5f + phase) * .025f * (1 - progress);
                var floating = Quaternion.Euler(12f + Mathf.Sin(sinking * 2f + phase) * 5f,
                    fall.Rotation.eulerAngles.y, sideSign * 82f);
                rotation = Quaternion.Slerp(rotation, floating, Mathf.SmoothStep(0, 1, Mathf.Clamp01(sinking / .5f)));
                if (!splashed)
                {
                    splashed = true;
                    var impact = point; impact.y = surface;
                    WaterImpactPhysics.Report(impact, Vector3.ProjectOnPlane(fall.Velocity, Vector3.up) * Mathf.Exp(-.7f * falling)
                        + Vector3.up * (fall.Velocity.y - 10f * falling), .8f, .2f);
                }
            }
            else if (fall.Landed)
            {
                point = contactSupport != null ? contactSupport.TransformPoint(localContactPosition) : fall.ContactPosition;
                var resting = contactSupport != null ? contactSupport.rotation * localContactRotation : fall.ContactRotation;
                rotation = Quaternion.Slerp(rotation, resting, Mathf.SmoothStep(0, 1, Mathf.Clamp01(sinking / .3f)));
            }
            transform.SetPositionAndRotation(point, rotation);
            float settle = Mathf.SmoothStep(0, 1, Mathf.Clamp01(age / .45f));
            float fold = fall.Landed ? Mathf.SmoothStep(0, 1, Mathf.Clamp01(sinking / .9f)) : 0f;
            for (int side = 0; side < 2; side++)
            {
                float sign = side == 0 ? -1 : 1;
                float flutter = Mathf.Sin(age * 13f + side * 1.4f) * 12f * Mathf.Exp(-age * 3f);
                var shoulder = Quaternion.Euler(0, sign * Mathf.Lerp(22f, 58f, fold), sign * (34f + side * 14f + flutter));
                var tip = Quaternion.Euler(0, sign * 18f, sign * 28f);
                if (fall.Landed && !fall.OnWater)
                {
                    float groundBlend = Mathf.SmoothStep(0, 1, Mathf.Clamp01(sinking / .35f));
                    shoulder = Quaternion.Slerp(shoulder, Quaternion.Euler(0, sign * 88f, 0), groundBlend);
                    tip = Quaternion.Slerp(tip, Quaternion.identity, groundBlend);
                }
                shoulders[side].localRotation = Quaternion.Slerp(fallShoulders[side], shoulder, settle);
                tips[side].localRotation = Quaternion.Slerp(fallTips[side], tip, settle);
                tips[side].localScale = Vector3.Lerp(tips[side].localScale, Vector3.one, settle);
            }
            body.localPosition = Vector3.zero;
            head.localRotation = Quaternion.Euler(22f * settle, 0, 0);
            feet.gameObject.SetActive(true);
            feet.localScale = new Vector3(1, Mathf.Lerp(fall.Feet, .7f, settle), 1);
        }

        void SetContactSupport(SeagullFall state, Transform fallback)
        {
            contactSupport = state.Support != null ? state.Support.transform : !state.OnWater ? fallback : null;
            if (contactSupport == null) return;
            localContactPosition = state.Support != null ? state.LocalContactPosition : contactSupport.InverseTransformPoint(state.ContactPosition);
            localContactRotation = state.Support != null ? state.LocalContactRotation : Quaternion.Inverse(contactSupport.rotation) * state.ContactRotation;
        }

        void CheckSurfaceContact(float age)
        {
            float fromAge = checkedFallAge;
            checkedFallAge = age;
            int steps = Mathf.Clamp(Mathf.CeilToInt((age - fromAge) / .06f), 1, 128);
            var ocean = OceanSurface.Instance;
            int waterLayer = LayerMask.NameToLayer("Water");
            for (int step = 0; step < steps; step++)
            {
                float a = Mathf.Lerp(fromAge, age, step / (float)steps);
                float b = Mathf.Lerp(fromAge, age, (step + 1f) / steps);
                var start = FallPoint(fall, a);
                var end = FallPoint(fall, b);
                var delta = end - start;
                float distance = delta.magnitude;
                if (distance < .00001f) continue;
                RaycastHit nearest = default;
                foreach (var hit in Physics.SphereCastAll(start, .14f, delta / distance, distance, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.gameObject.layer == waterLayer || hit.collider.GetComponentInParent<CombatHealth>() != null) continue;
                    if (hit.distance <= distance) { nearest = hit; distance = hit.distance; }
                }
                bool water = WaterImpactPhysics.Cross(ocean, start, end, .04f, out var waterPoint, out float waterFraction);
                if (!water && end.y <= (ocean != null ? ocean.Height(end) : 0f) + .04f)
                {
                    water = true;
                    waterPoint = end; waterPoint.y = ocean != null ? ocean.Height(end) : 0f;
                    waterFraction = 1f;
                }
                if (water && (nearest.collider == null || delta.magnitude * waterFraction < nearest.distance))
                {
                    Land(a + (b - a) * waterFraction, age, waterPoint + Vector3.up * .04f, Vector3.up, null, true);
                    return;
                }
                if (nearest.collider != null)
                {
                    float fraction = Mathf.Clamp01(nearest.distance / delta.magnitude);
                    Land(a + (b - a) * fraction, age, nearest.point + nearest.normal * .14f, nearest.normal, nearest.collider, false);
                    return;
                }
            }
        }

        void Land(float contactAge, float age, Vector3 position, Vector3 normal, Collider surface, bool onWater)
        {
            normal = normal.sqrMagnitude > .001f ? normal.normalized : Vector3.up;
            var forward = Vector3.ProjectOnPlane(fall.Rotation * Vector3.forward, normal).normalized;
            if (forward.sqrMagnitude < .5f) forward = Vector3.Cross(normal, Vector3.right).normalized;
            if (forward.sqrMagnitude < .5f) forward = Vector3.Cross(normal, Vector3.forward).normalized;
            float side = Mathf.Sin(phase) >= 0 ? 1f : -1f;
            fall.Seconds = contactAge;
            fall.Age = age;
            fall.Landed = true;
            fall.OnWater = onWater;
            fall.ContactPosition = position;
            fall.ContactNormal = normal;
            fall.ContactRotation = Quaternion.LookRotation(forward, normal) * Quaternion.Euler(12f, 0, side * 82f);
            fall.Support = surface != null ? surface.GetComponentInParent<FishNet.Object.NetworkObject>() : null;
            if (surface != null && fall.Support == null)
            {
                var raft = surface.GetComponentInParent<PirateSlop.Networking.RaftPlatform>();
                if (raft != null && raft.Chest != null) fall.Support = raft.Chest.NetworkObject;
            }
            if (fall.Support != null)
            {
                fall.LocalContactPosition = fall.Support.transform.InverseTransformPoint(position);
                fall.LocalContactRotation = Quaternion.Inverse(fall.Support.transform.rotation) * fall.ContactRotation;
            }
            SetContactSupport(fall, surface != null ? surface.transform : null);
            SurfaceContact?.Invoke(fall);
        }

        public static float FlightPower(float time, int seed)
        {
            float cycle = Mathf.Repeat(time + (seed & 31) * .27f, 9.4f);
            return .06f + .74f * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(2.1f, 3.2f, cycle)))
                + .74f * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(7.9f, 9.4f, cycle));
        }

        static Transform Pivot(Transform parent, string name)
        {
            var value = new GameObject(name).transform;
            value.SetParent(parent, false);
            return value;
        }

        static Transform Part(Transform parent, string name, Mesh mesh, Vector3 position, Vector3 scale, Material material)
        {
            var value = Pivot(parent, name);
            value.localPosition = position;
            value.localScale = scale;
            value.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = value.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return value;
        }

        static Mesh Surface(string name, Vector3[] vertices, int[] triangles)
        {
            var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static Mesh Wing(string name, Vector3[] polygon, float sign)
        {
            var vertices = new Vector3[polygon.Length * 2];
            var triangles = new System.Collections.Generic.List<int>();
            for (int i = 0; i < polygon.Length; i++)
            {
                vertices[i] = new Vector3(polygon[i].x * sign, .008f, polygon[i].z);
                vertices[i + polygon.Length] = new Vector3(polygon[i].x * sign, -.008f, polygon[i].z);
            }
            for (int i = 1; i < polygon.Length - 1; i++)
            {
                triangles.AddRange(new[] { 0, i, i + 1, polygon.Length, polygon.Length + i + 1, polygon.Length + i });
            }
            for (int i = 0; i < polygon.Length; i++)
            {
                int j = (i + 1) % polygon.Length;
                triangles.AddRange(new[] { i, j, polygon.Length + i, j, polygon.Length + j, polygon.Length + i });
            }
            if (sign < 0)
                for (int i = 0; i < triangles.Count; i += 3) (triangles[i], triangles[i + 2]) = (triangles[i + 2], triangles[i]);
            return Surface(name, vertices, triangles.ToArray());
        }

        static void EnsureMeshes()
        {
            if (sphere != null) return;
            white = Resources.Load<Material>("World/SeagullWhite");
            dark = Resources.Load<Material>("World/SeagullDark");
            feetMaterial = Resources.Load<Material>("World/SeagullFeet");
            if (white == null || dark == null || feetMaterial == null) throw new System.InvalidOperationException("Prepare the seagull assets first.");
            var vertices = new System.Collections.Generic.List<Vector3>();
            var indices = new System.Collections.Generic.List<int>();
            for (int y = 0; y <= 8; y++)
                for (int x = 0; x <= 12; x++)
                {
                    float latitude = y * Mathf.PI / 8;
                    float longitude = x * Mathf.PI * 2 / 12;
                    vertices.Add(new Vector3(Mathf.Sin(latitude) * Mathf.Cos(longitude), Mathf.Cos(latitude), Mathf.Sin(latitude) * Mathf.Sin(longitude)) * .5f);
                }
            for (int y = 0; y < 8; y++) for (int x = 0; x < 12; x++)
            {
                int i = y * 13 + x;
                indices.AddRange(new[] { i, i + 1, i + 13, i + 1, i + 14, i + 13 });
            }
            sphere = Surface("SeagullBody", vertices.ToArray(), indices.ToArray());
            var normals = sphere.vertices;
            for (int i = 0; i < normals.Length; i++) normals[i] = normals[i].normalized;
            sphere.normals = normals;
            beak = Surface("SeagullBeak", new[] { new Vector3(-.025f, -.02f, 0), new Vector3(.025f, -.02f, 0), new Vector3(0, .025f, 0), new Vector3(0, -.005f, .18f) }, new[] { 0, 2, 1, 0, 1, 3, 1, 2, 3, 2, 0, 3 });
            tail = Wing("SeagullTail", new[] { new Vector3(-.11f, 0, 0), new Vector3(.11f, 0, 0), new Vector3(.14f, 0, -.22f), new Vector3(-.14f, 0, -.22f) }, 1);
            for (int side = 0; side < 2; side++)
            {
                float sign = side == 0 ? -1 : 1;
                inner[side] = Wing("SeagullInnerWing", new[] { Vector3.zero, new Vector3(.45f, 0, .16f), new Vector3(.45f, 0, -.2f), new Vector3(.25f, 0, -.23f) }, sign);
                outer[side] = Wing("SeagullWingtip", new[] { new Vector3(0, 0, .16f), new Vector3(.6f, 0, -.02f), new Vector3(.45f, 0, -.2f), new Vector3(0, 0, -.2f) }, sign);
            }
            vertices.Clear(); indices.Clear();
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? -.065f : .065f;
                int i = vertices.Count;
                vertices.AddRange(new[] { new Vector3(x - .012f, -.07f, .08f), new Vector3(x + .012f, -.07f, .08f),
                    new Vector3(x + .012f, -.212f, .08f), new Vector3(x - .012f, -.212f, .08f),
                    new Vector3(x, -.225f, .08f), new Vector3(x - .045f, -.225f, .19f), new Vector3(x + .045f, -.225f, .19f) });
                indices.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3, i + 2, i + 1, i, i + 3, i + 2, i, i + 4, i + 5, i + 6, i + 6, i + 5, i + 4 });
            }
            var footVertices = new Vector3[indices.Count];
            var footIndices = new int[indices.Count];
            for (int i = 0; i < indices.Count; i++) { footVertices[i] = vertices[indices[i]]; footIndices[i] = i; }
            feetMesh = Surface("SeagullFeet", footVertices, footIndices);
        }
    }
}
