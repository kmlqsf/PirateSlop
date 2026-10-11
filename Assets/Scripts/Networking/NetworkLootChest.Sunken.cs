using FishNet.Object.Synchronizing;
using PirateSlop.World;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkLootChest
    {
        readonly SyncVar<Vector3> sunkenFloor = new();
        readonly SyncVar<Vector3> sunkenRopeEnd = new();
        readonly SyncVar<float> sunkenDirection = new();
        float sunkenPreviousAngle;
        Vector3 sunkenPreviousPosition, shownRopeEnd;
        GameObject sunkenGrip;
        SeaRopeVisual sunkenRope;
        readonly Vector3[] sunkenRopePoints = new Vector3[544];
        readonly Transform[] sunkenShackles = new Transform[4];
        readonly bool[] sunkenShackleReleased = new bool[4];
        readonly float[] sunkenShackleReleasedAt = new float[4];
        readonly Vector3[] sunkenShackleOrigins = new Vector3[4], sunkenShackleVelocities = new Vector3[4];
        readonly Quaternion[] sunkenShackleRotations = new Quaternion[4];
        BoxCollider sunkenBox;
        float nextSunkenRopeVisual;
        Vector3 SunkenFreeEnd
        {
            get
            {
                float direction = sunkenDirection.Value == 0f ? 1f : sunkenDirection.Value;
                float angle = -direction * (1f - progress.Value) * Mathf.Clamp(Catalog.SunkenUnwrapTurns, 1, 5) * Mathf.PI * 2f;
                var local = SunkenSurfacePoint(angle, 1f - progress.Value);
                return transform.TransformPoint(local + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * .62f + Vector3.up * .04f);
            }
        }

        void InitializeSunkenRope()
        {
            var bottom = eventPoint.Value;
            bottom.y = ProceduralWorld.Instance.GroundHeight(bottom) + .08f;
            sunkenFloor.Value = bottom;
            sunkenRopeEnd.Value = SunkenFreeEnd;
        }

        bool ValidSunkenPosition(NetworkWeapon player)
        {
            var motor = player.GetComponent<AdvancedPlayerController>();
            var offset = player.transform.position + Vector3.up - transform.position;
            float radius = new Vector2(offset.x, offset.z).magnitude;
            return motor.IsSwimming && radius >= .9f && radius <= 3.3f && Mathf.Abs(offset.y - .4f) <= 1.4f && player.transform.position.y + 1.65f < eventPoint.Value.y;
        }

        float SunkenAngle(Vector3 position)
        {
            var offset = position - transform.position;
            return Mathf.Atan2(offset.z, offset.x) * Mathf.Rad2Deg;
        }

        void BeginSunkenUnwrap()
        {
            sunkenPreviousPosition = worker.transform.position;
            sunkenPreviousAngle = SunkenAngle(sunkenPreviousPosition);
            sunkenRopeEnd.Value = sunkenPreviousPosition + Vector3.up * 1.1f;
        }

        void TickSunkenUnwrap()
        {
            var position = worker.transform.position;
            float angle = SunkenAngle(position);
            float delta = Mathf.DeltaAngle(sunkenPreviousAngle, angle);
            float limit = Time.deltaTime * 8f + .2f;
            if (Vector3.Distance(position, sunkenPreviousPosition) > limit)
            { StopWork(); return; }
            sunkenPreviousPosition = position;
            sunkenPreviousAngle = angle;
            sunkenRopeEnd.Value = position + Vector3.up * 1.1f + worker.transform.forward * .2f;
            if (sunkenDirection.Value == 0f && Mathf.Abs(delta) > .1f) sunkenDirection.Value = Mathf.Sign(delta);
            progress.Value = Mathf.Clamp01(progress.Value + delta * sunkenDirection.Value / (360f * Mathf.Clamp(Catalog.SunkenUnwrapTurns, 1, 5)));
            if (progress.Value >= 1f) Unlock();
        }

        internal PlayerCommand SunkenOrbitCommand(Vector3 position)
        {
            var offset = Vector3.ProjectOnPlane(position - transform.position, Vector3.up);
            float radius = offset.magnitude;
            var radial = radius > .01f ? offset / radius : Vector3.right;
            float direction = sunkenDirection.Value == 0f ? 1f : sunkenDirection.Value;
            var velocity = new Vector3(-radial.z, 0, radial.x) * direction + radial * Mathf.Clamp((1.8f - radius) * 1.5f, -1f, 1f);
            float height = transform.position.y - .6f - position.y;
            return new PlayerCommand { Yaw = Mathf.Atan2(velocity.x, velocity.z) * Mathf.Rad2Deg, Move = Vector2.up, Rise = height > .2f, Crouch = height < -.2f };
        }

        void CreateSunkenRope()
        {
            var geometry = new GameObject("ContinuousHempRope");
            geometry.transform.SetParent(eventVisual.transform, false);
            sunkenRope = geometry.AddComponent<SeaRopeVisual>();
            geometry.GetComponent<MeshRenderer>().sharedMaterial = seaArt.Hemp;
            sunkenGrip = Instantiate(seaArt.RopeGrip, transform, false);
            sunkenGrip.name = "SunkenRopeEnd";
            shownRopeEnd = occupied.Value ? sunkenRopeEnd.Value : SunkenFreeEnd;
            for (int i = 0; i < sunkenShackles.Length; i++)
            {
                sunkenShackles[i] = Instantiate(seaArt.RopeShackle, eventVisual.transform, false).transform;
                sunkenShackles[i].name = "RopeClevis" + i;
                sunkenShackleReleased[i] = phase.Value != SeaLootState.Locked || progress.Value >= 1f - ShackleWrapFraction(i);
                sunkenShackleReleasedAt[i] = -10f;
            }
            UpdateSunkenRope();
        }

        static float ShackleWrapFraction(int index) => (3 - index) * .26f;

        Vector3 SunkenSurfacePoint(float angle, float wrapFraction)
        {
            if (seaArt == null) seaArt = Resources.Load<SeaEventArt>("SeaEvents/SeaEventArt");
            if (seaArt != null && seaArt.ChestRopeSurface != null && seaArt.ChestRopeSurface.Length == 17 * 256)
            {
                float column = Mathf.Repeat(angle / (Mathf.PI * 2f), 1f) * 256f;
                float row = Mathf.Clamp01(wrapFraction) * 16f;
                int columnIndex = Mathf.FloorToInt(column), y = Mathf.Min(15, Mathf.FloorToInt(row));
                int a = y * 256 + columnIndex, b = y * 256 + (columnIndex + 1) % 256;
                float u = column - columnIndex, v = row - y;
                var point = Vector3.Lerp(Vector3.Lerp(seaArt.ChestRopeSurface[a], seaArt.ChestRopeSurface[b], u), Vector3.Lerp(seaArt.ChestRopeSurface[a + 256], seaArt.ChestRopeSurface[b + 256], u), v);
                var normal = Vector3.Lerp(Vector3.Lerp(seaArt.ChestRopeNormals[a], seaArt.ChestRopeNormals[b], u), Vector3.Lerp(seaArt.ChestRopeNormals[a + 256], seaArt.ChestRopeNormals[b + 256], u), v);
                normal = Vector3.ProjectOnPlane(normal, Vector3.up).normalized;
                float fastening = 0;
                for (int i = 0; i < sunkenShackles.Length; i++)
                {
                    float proximity = Mathf.Clamp01(1f - Mathf.Abs(wrapFraction - ShackleWrapFraction(i)) / .018f);
                    fastening = Mathf.Max(fastening, proximity * proximity * (3f - 2f * proximity));
                }
                return point + normal * (.021f + .035f * fastening);
            }
            if (sunkenBox == null) sunkenBox = GetComponent<BoxCollider>();
            var center = sunkenBox != null ? sunkenBox.center : new Vector3(0, .2f, 0);
            var size = sunkenBox != null ? sunkenBox.size : new Vector3(1.2f, .4f, .62f);
            float halfX = size.x * .5f + .054f, halfZ = size.z * .5f + .054f;
            const float corner = .05f;
            float x = Mathf.Cos(angle), z = Mathf.Sin(angle);
            float lower = 0, upper = Mathf.Sqrt(halfX * halfX + halfZ * halfZ);
            for (int i = 0; i < 12; i++)
            {
                float radius = (lower + upper) * .5f;
                var q = new Vector2(Mathf.Abs(x * radius) - (halfX - corner), Mathf.Abs(z * radius) - (halfZ - corner));
                float distance = new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0) - corner;
                if (distance > 0) upper = radius; else lower = radius;
            }
            float r = (lower + upper) * .5f;
            return new Vector3(center.x + x * r, center.y - size.y * .5f + .065f + Mathf.Max(.08f, size.y - .11f) * wrapFraction, center.z + z * r);
        }

        void UpdateSunkenShackles(bool locked)
        {
            float direction = sunkenDirection.Value == 0f ? 1f : sunkenDirection.Value;
            float turns = Mathf.Clamp(Catalog.SunkenUnwrapTurns, 1, 5);
            for (int i = 0; i < sunkenShackles.Length; i++)
            {
                var shackle = sunkenShackles[i];
                bool released = !locked || progress.Value >= 1f - ShackleWrapFraction(i);
                if (released != sunkenShackleReleased[i])
                {
                    sunkenShackleReleased[i] = released;
                    if (released)
                    {
                        sunkenShackleReleasedAt[i] = Time.time;
                        sunkenShackleOrigins[i] = shackle.position;
                        sunkenShackleRotations[i] = shackle.rotation;
                        sunkenShackleVelocities[i] = shackle.forward * .9f + Vector3.up * .55f + shackle.right * ((i % 2 == 0 ? 1 : -1) * .25f);
                    }
                }
                if (released)
                {
                    float age = Time.time - sunkenShackleReleasedAt[i];
                    shackle.gameObject.SetActive(age < 1.8f);
                    if (age >= 1.8f) continue;
                    shackle.position = sunkenShackleOrigins[i] + sunkenShackleVelocities[i] * age + Vector3.down * (age * age * .8f);
                    shackle.rotation = sunkenShackleRotations[i] * Quaternion.Euler(age * 130f, age * 75f, age * 190f);
                    shackle.localScale = Vector3.one * Mathf.Clamp01((1.8f - age) / .35f);
                    continue;
                }
                shackle.gameObject.SetActive(true); shackle.localScale = Vector3.one;
                float s = ShackleWrapFraction(i), angle = -direction * turns * Mathf.PI * 2f * s;
                var local = SunkenSurfacePoint(angle, s);
                var before = SunkenSurfacePoint(angle - .005f, s);
                var after = SunkenSurfacePoint(angle + .005f, s);
                var tangent = (after - before).normalized;
                var outward = Vector3.Cross(Vector3.up, tangent).normalized;
                shackle.SetPositionAndRotation(transform.TransformPoint(local - outward * .056f), transform.rotation * Quaternion.LookRotation(outward, Vector3.up));
            }
        }

        void UpdateSunkenRope()
        {
            if (sunkenRope == null) return;
            bool locked = phase.Value == SeaLootState.Locked;
            sunkenRope.gameObject.SetActive(locked);
            sunkenGrip.SetActive(locked && !occupied.Value);
            UpdateSunkenShackles(locked);
            if (!locked) return;
            var target = occupied.Value ? sunkenRopeEnd.Value : SunkenFreeEnd;
            shownRopeEnd = Vector3.Lerp(shownRopeEnd, target, 1f - Mathf.Exp(-15f * Time.deltaTime));
            sunkenGrip.transform.position = SunkenFreeEnd;
            if (Time.time < nextSunkenRopeVisual) return;
            float distance = Camera.main != null ? Vector3.Distance(Camera.main.transform.position, transform.position) : 0;
            nextSunkenRopeVisual = Time.time + (distance < 60f ? .04f : distance < 200f ? .12f : .35f);
            float turns = Mathf.Clamp(Catalog.SunkenUnwrapTurns, 1, 5);
            float fraction = 1f - progress.Value, remaining = fraction * turns * Mathf.PI * 2f;
            int segments = Mathf.RoundToInt(turns) * 96;
            float direction = sunkenDirection.Value == 0f ? 1f : sunkenDirection.Value;
            int count = 0;
            var entry = transform.TransformPoint(SunkenSurfacePoint(0, 0));
            var entryTangent = transform.TransformDirection(new Vector3(0, 0, -direction));
            var start = sunkenFloor.Value;
            var control = start + Vector3.up * Mathf.Min(2f, Vector3.Distance(start, entry) * .35f);
            for (int i = 0; i < 16; i++)
                AddSunkenRopePoint(CubicRopePoint(start, control, entry - entryTangent * .22f, entry, i / 16f), ref count);
            for (int i = 0; i <= segments; i++)
            {
                float bearing = -direction * remaining * i / segments;
                AddSunkenRopePoint(transform.TransformPoint(SunkenSurfacePoint(bearing, fraction * i / segments)), ref count);
            }
            var exit = transform.TransformPoint(SunkenSurfacePoint(-direction * remaining, fraction));
            var previous = transform.TransformPoint(SunkenSurfacePoint(-direction * (remaining - .012f), fraction));
            var exitTangent = (exit - previous).normalized;
            var endControl = Vector3.Lerp(exit, shownRopeEnd, .7f) + Vector3.down * .07f;
            for (int i = 1; i <= 16; i++)
                AddSunkenRopePoint(CubicRopePoint(exit, exit + exitTangent * .18f, endControl, shownRopeEnd, i / 16f), ref count);
            sunkenRope.SetPath(sunkenRopePoints, count, .022f);
        }

        void AddSunkenRopePoint(Vector3 world, ref int count) => sunkenRopePoints[count++] = eventVisual.transform.InverseTransformPoint(world);

        static Vector3 CubicRopePoint(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            float u = 1f - t;
            return a * (u * u * u) + b * (3f * u * u * t) + c * (3f * u * t * t) + d * (t * t * t);
        }
    }
}
