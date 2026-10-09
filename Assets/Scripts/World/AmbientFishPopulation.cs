using UnityEngine;

namespace PirateSlop.World
{
    public sealed class AmbientFishPopulation
    {
        sealed class School
        {
            public Vector3 Center;
            public float Heading, Speed, Turn, Depth, Height, EndsAt, StartsAt, Phase;
            public bool Swimming, Surface;
        }

        sealed class Fish
        {
            public AmbientFishVisual Visual;
            public School School;
            public Vector3 Offset, Position, LastChecked;
            public float Scale, Phase, Water, Floor, BlockedUntil, Heading, Speed, EscapeUntil, SwimPhase, Turn;
            public Vector3 EscapeDirection;
            public bool Checked, Clear;
        }

        const int Count = 20;
        readonly ProceduralWorld world;
        readonly Transform parent;
        readonly System.Random random;
        readonly Plane[] planes = new Plane[6];
        readonly School[] schools = new School[5];
        readonly Fish[] fish = new Fish[Count];
        bool loaded, available;
        int cursor;
        float waterAt, cameraWater;
        Vector3 lastEye;
        public int ActiveCount { get; private set; }

        public AmbientFishPopulation(ProceduralWorld source, Transform root)
        {
            world = source;
            parent = root;
            random = new System.Random(source.Layout.Seed ^ 471931);
        }

        float Range(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());

        void Load()
        {
            loaded = true;
            var commonPrefab = Resources.Load<GameObject>("Underwater/AmbientFish");
            var pufferPrefab = Resources.Load<GameObject>("Underwater/AmbientPufferfish");
            var swordPrefab = Resources.Load<GameObject>("Underwater/AmbientSwordfish");
            var common = commonPrefab != null ? commonPrefab.GetComponent<AmbientFishVisual>() : null;
            var puffer = pufferPrefab != null ? pufferPrefab.GetComponent<AmbientFishVisual>() : null;
            var sword = swordPrefab != null ? swordPrefab.GetComponent<AmbientFishVisual>() : null;
            if (common == null || puffer == null || sword == null)
            {
                Debug.LogWarning("Ambient fish prefabs are missing. Run Prepare Ambient Fish before building.");
                return;
            }
            available = true;
            for (int i = 0; i < schools.Length; i++) schools[i] = new School { StartsAt = Time.time + 2f + i * 4f, Phase = Range(0f, 6.28f) };
            for (int i = 0; i < Count; i++)
            {
                var prefab = i < 18 ? common : i == 18 ? puffer : sword;
                var visual = Object.Instantiate(prefab, parent);
                visual.gameObject.SetActive(false);
                fish[i] = new Fish
                {
                    Visual = visual,
                    School = schools[i < 18 ? i / 6 : i - 15],
                    Scale = i < 18 ? Range(.72f, 1.12f) : i == 18 ? Range(.62f, .82f) : Range(1.08f, 1.3f),
                    Phase = Range(0f, 6.28f),
                    Offset = i < 18 ? new Vector3((i % 3 - 1) * .75f + Range(-.2f, .2f), Range(-.18f, .18f), -(i % 6) * .62f) : Vector3.zero
                };
                visual.transform.localScale = Vector3.one * fish[i].Scale;
            }
        }

        public void Update(Camera view)
        {
            if (view == null || world == null || !world.Ready || OceanSurface.Instance == null)
            {
                Hide();
                return;
            }
            if (!loaded) Load();
            if (!available) return;
            var ocean = OceanSurface.Instance;
            var eye = view.transform.position;
            if (Time.time >= waterAt)
            {
                waterAt = Time.time + .25f;
                cameraWater = ocean.Height(eye);
            }
            if ((eye - lastEye).sqrMagnitude > 10000f)
            {
                Hide();
                for (int i = 0; i < schools.Length; i++) schools[i].StartsAt = Time.time + 1f + i * 3f;
            }
            lastEye = eye;
            bool underwater = eye.y < cameraWater - .2f;
            GeometryUtility.CalculateFrustumPlanes(view, planes);
            float dt = Mathf.Min(Time.deltaTime, .1f);
            bool began = false;
            for (int i = 0; i < schools.Length; i++)
            {
                var school = schools[i];
                if (school.Swimming && (Time.time >= school.EndsAt || (school.Center - eye).sqrMagnitude > 6400f)) End(school, Range(12f, 25f));
                if (!school.Swimming && Time.time >= school.StartsAt && !began)
                {
                    Begin(school, i, view, underwater, ocean);
                    began = true;
                }
                if (!school.Swimming) continue;
                school.Turn = Mathf.MoveTowards(school.Turn, Mathf.Sin(Time.time * .19f + school.Phase) * 3f, dt * 3f);
                school.Heading += school.Turn * dt;
                school.Center += Quaternion.Euler(0f, school.Heading, 0f) * Vector3.forward * (school.Speed * dt);
            }
            for (int i = 0; i < Count; i++)
            {
                var item = fish[i];
                var school = item.School;
                if (!school.Swimming) continue;
                var heading = Quaternion.Euler(0f, school.Heading, 0f);
                var formation = school.Center + heading * item.Offset;
                Vector3 threat = Vector3.zero;
                float nearest = 16f;
                bool startled = false;
                foreach (var player in Networking.NetworkPlayer.Active)
                {
                    if (player == null || player.Motor == null || player.Motor.IsDead) continue;
                    var away = item.Position - (player.transform.position + Vector3.up * .8f);
                    float distance = away.sqrMagnitude;
                    if (distance >= nearest) continue;
                    nearest = distance;
                    threat = Vector3.ProjectOnPlane(away, Vector3.up);
                    startled = true;
                }
                if (startled)
                {
                    if (threat.sqrMagnitude < .01f) threat = heading * Vector3.forward;
                    item.EscapeDirection = threat.normalized;
                    item.EscapeUntil = Time.time + 3f;
                }
                bool escaping = Time.time < item.EscapeUntil;
                float escapeSpeed = item.Visual.Species == 1 ? 1.8f : item.Visual.Species == 2 ? 4.8f : 3.6f;
                var cohesion = Vector3.ProjectOnPlane(formation - item.Position, Vector3.up);
                var direction = escaping ? item.EscapeDirection : heading * Vector3.forward + Vector3.ClampMagnitude(cohesion * .18f, .85f);
                float targetHeading = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                float oldHeading = item.Heading;
                item.Heading = Mathf.MoveTowardsAngle(item.Heading, targetHeading, dt * (escaping ? 180f : 65f));
                item.Turn = dt > .0001f ? Mathf.DeltaAngle(oldHeading, item.Heading) / dt : 0f;
                float targetSpeed = escaping ? escapeSpeed : school.Speed + Mathf.Min(.4f, cohesion.magnitude * .03f);
                item.Speed = Mathf.MoveTowards(item.Speed, targetSpeed, dt * (escaping ? 8f : 2f));
                var point = item.Position + Quaternion.Euler(0f, item.Heading, 0f) * Vector3.forward * (item.Speed * dt);
                float beatRate = item.Visual.Species == 1 ? 2.2f : item.Visual.Species == 2 ? 1.15f : 1.65f;
                item.SwimPhase += dt * beatRate * Mathf.Lerp(1f, 1.8f, Mathf.InverseLerp(school.Speed, escapeSpeed, item.Speed));
                float bob = Mathf.Sin(Time.time * .8f + item.Phase) * .12f;
                float desiredY = school.Surface ? item.Water - school.Depth + item.Offset.y + bob : school.Height + item.Offset.y + bob;
                point.y = item.Checked ? Mathf.MoveTowards(item.Position.y, desiredY, dt * 1.6f) : desiredY;
                if (item.Checked)
                {
                    float radius = item.Visual.BodyRadius * item.Scale;
                    point.y = Mathf.Min(point.y, item.Water - radius - .4f);
                    point.y = Mathf.Max(point.y, item.Floor + radius + .6f);
                }
                item.Position = point;
            }
            int samples = 4;
            for (int i = 0; i < Count && samples > 0; i++)
            {
                var item = fish[cursor];
                cursor = (cursor + 1) % Count;
                if (!item.School.Swimming) continue;
                SampleHabitat(item, ocean);
                samples--;
            }
            ActiveCount = 0;
            foreach (var item in fish)
            {
                bool visible = item.School.Swimming && item.Checked && item.Clear && Time.time >= item.BlockedUntil;
                if (item.Visual.gameObject.activeSelf != visible) item.Visual.gameObject.SetActive(visible);
                if (!visible) continue;
                ActiveCount++;
                var point = item.Position;
                float yaw = item.Heading;
                float pitch = Mathf.Cos(Time.time * .8f + item.Phase) * 2f;
                float roll = Mathf.Clamp(-item.Turn * .12f, -8f, 8f);
                item.Visual.transform.SetPositionAndRotation(point, Quaternion.Euler(pitch, yaw, roll));
                if (GeometryUtility.TestPlanesAABB(planes, new Bounds(point, Vector3.one * item.Visual.Length * item.Scale * 1.5f)))
                    item.Visual.Swim(item.SwimPhase, item.Speed, item.Turn * .1f);
            }
        }

        void Begin(School school, int index, Camera view, bool underwater, OceanSurface ocean)
        {
            var eye = view.transform.position;
            var forward = Vector3.ProjectOnPlane(view.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .1f) forward = Vector3.forward;
            var right = Vector3.Cross(Vector3.up, forward);
            float side = random.Next(2) == 0 ? -1f : 1f;
            float distance = underwater ? Range(8f, 16f) : Range(22f, 38f);
            float lateral = underwater ? Range(7f, 14f) : Range(12f, 24f);
            var center = eye + forward * distance + right * side * lateral;
            float water = ocean.Height(center);
            float floor = world.GroundHeight(center);
            school.Surface = !underwater || index == 0;
            school.Depth = index == 2 && !underwater ? Range(2f, 3.5f) : Range(.85f, 1.4f);
            school.Height = school.Surface ? water - school.Depth : Mathf.Clamp(eye.y + Range(-2f, 2f), floor + 2f, water - 1.2f);
            center.y = school.Height;
            if (floor > water - 2f || center.y <= floor + .8f) { school.StartsAt = Time.time + 3f; return; }
            var destination = eye + forward * Range(5f, 15f) - right * side * Range(8f, 18f);
            var direction = destination - center;
            school.Center = center;
            school.Heading = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            school.Speed = index == 3 ? .42f : index == 4 ? 1.7f : Range(.75f, 1.1f);
            school.EndsAt = Time.time + Range(48f, 68f);
            school.Swimming = true;
            school.Turn = 0f;
            foreach (var item in fish)
                if (item.School == school)
                {
                    item.Checked = item.Clear = false;
                    item.Water = water;
                    item.Floor = floor;
                    item.Position = center + Quaternion.Euler(0f, school.Heading, 0f) * item.Offset;
                    item.Heading = school.Heading;
                    item.Speed = school.Speed;
                    item.EscapeUntil = 0f;
                    item.SwimPhase = item.Phase;
                    item.BlockedUntil = 0f;
                }
        }

        void SampleHabitat(Fish item, OceanSurface ocean)
        {
            var point = item.Position;
            float radius = item.Visual.BodyRadius * item.Scale + .08f;
            item.Water = ocean.Height(point);
            item.Floor = world.GroundHeight(point);
            point.y = Mathf.Min(point.y, item.Water - radius - .35f);
            point.y = Mathf.Max(point.y, item.Floor + radius + .5f);
            var forward = Quaternion.Euler(0f, item.Heading, 0f) * Vector3.forward;
            float half = Mathf.Max(0f, item.Visual.Length * item.Scale * .5f - radius);
            int mask = ~(1 << 4);
            bool clear = point.y < item.Water - radius - .2f && point.y > item.Floor + radius;
            clear &= !Physics.CheckCapsule(point - forward * half, point + forward * half, radius, mask, QueryTriggerInteraction.Ignore);
            if (clear && item.Checked)
            {
                var travel = point - item.LastChecked;
                if (travel.sqrMagnitude > .0001f)
                    clear = !Physics.SphereCast(item.LastChecked, radius, travel.normalized, out _, travel.magnitude, mask, QueryTriggerInteraction.Ignore);
            }
            if (!clear)
            {
                item.BlockedUntil = Time.time + .7f;
                item.School.Turn = item.School.Turn <= 0f ? -35f : 35f;
            }
            item.Position = item.LastChecked = point;
            item.Checked = true;
            item.Clear = clear;
        }

        void End(School school, float delay)
        {
            school.Swimming = false;
            school.StartsAt = Time.time + delay;
        }

        void Hide()
        {
            ActiveCount = 0;
            if (!available) return;
            foreach (var item in fish) if (item.Visual != null && item.Visual.gameObject.activeSelf) item.Visual.gameObject.SetActive(false);
            foreach (var school in schools) End(school, Range(1f, 15f));
        }
    }
}
