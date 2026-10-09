using PirateSlop.Networking;
using UnityEngine;

namespace PirateSlop.World
{
    public sealed class AmbientSeagullFlock : MonoBehaviour
    {
        public const float Lifetime = 66;
        public static float Duration(SeagullFlockMessage state) => state.Perches != null && state.Perches.Length > 0 ? Lifetime : 32;
        public struct BirdPose
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public float Power, Fold, Feet, Flare;
            public bool Resting, Visible;
        }
        SeagullFlockMessage message;
        SeagullVisual[] birds;
        float receivedAt;
        float nextCallAge;
        int calls;

        public void Initialize(SeagullFlockMessage state)
        {
            message = state;
            receivedAt = Time.time;
            nextCallAge = state.Age + 8 + (state.Seed & 3);
            birds = new SeagullVisual[state.Count];
            for (int i = 0; i < birds.Length; i++) birds[i] = SeagullVisual.Create(transform, state.Seed + i * 113);
            Apply(state.Age);
        }

        void LateUpdate()
        {
            if (birds == null) return;
            float age = message.Age + Time.time - receivedAt;
            if (age >= Duration(message) || ProceduralWorld.Instance == null || !ProceduralWorld.Instance.Ready) { Destroy(gameObject); return; }
            Apply(age);
            if (calls >= 2 || age < nextCallAge) return;
            nextCallAge = age + (message.Perches.Length > 0 ? 22 : 11) + (message.Seed & 7);
            int start = (message.Seed % birds.Length + calls * 3) % birds.Length;
            calls++;
            for (int i = 0; i < birds.Length; i++)
            {
                var bird = birds[(start + i) % birds.Length];
                if (!bird.gameObject.activeSelf) continue;
                GameAudio.SeagullCall(bird.transform.position);
                break;
            }
        }

        void Apply(float age)
        {
            for (int i = 0; i < birds.Length; i++)
            {
                var pose = Evaluate(message, i, age);
                var bird = birds[i];
                bird.gameObject.SetActive(pose.Visible);
                if (!pose.Visible) continue;
                bird.transform.SetPositionAndRotation(pose.Position, pose.Rotation);
                bird.Animate(age, pose.Power, pose.Fold, pose.Feet, pose.Flare, pose.Resting);
            }
        }

        public static BirdPose Evaluate(SeagullFlockMessage state, int index, float age)
        {
            int seed = state.Seed + index * 113;
            var pose = new BirdPose { Visible = age >= 0, Power = SeagullVisual.FlightPower(age, seed) };
            var heading = Vector3.ProjectOnPlane(state.End - state.Start, Vector3.up).normalized;
            if (heading.sqrMagnitude < .5f) heading = Vector3.forward;
            var right = Vector3.Cross(Vector3.up, heading);
            var offset = right * ((index % 2 == 0 ? -1 : 1) * (2 + index * 1.3f)) - heading * (index * 1.8f);
            if (state.Perches == null || index >= state.Perches.Length)
            {
                float t = age / 32f;
                Flight(ref pose, state.Start + offset, state.Control + offset, state.End + offset, t, seed);
                pose.Visible &= t <= 1;
                return pose;
            }
            float elapsed = age - index * .85f;
            Vector3 normal = state.Normals[index].normalized;
            Vector3 contact = state.Perches[index] + normal * SeagullVisual.FootHeight;
            Vector3 approach = contact + Vector3.up * 15 - heading * 18;
            Vector3 cruiseControl = Vector3.Lerp(state.Start, approach, .55f) + Vector3.up * 12;
            var landingRotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(heading, normal), normal);
            if (elapsed < 16)
            {
                Flight(ref pose, state.Start + offset, cruiseControl, approach, Mathf.Clamp01(age / (16 + index * .85f)), seed);
            }
            else if (elapsed < 21.5f)
            {
                float t = (elapsed - 16) / 5.5f;
                float eased = 1 - (1 - t) * (1 - t);
                var middle = contact - heading * 5 + Vector3.up * 3;
                Flight(ref pose, approach, middle, contact, eased, seed, false);
                var cruiseEnd = new BirdPose();
                Flight(ref cruiseEnd, state.Start + offset, cruiseControl, approach, 1, seed);
                float approachBlend = Mathf.SmoothStep(0, 1, Mathf.Clamp01((elapsed - 16) / .85f));
                pose.Rotation = Quaternion.Slerp(cruiseEnd.Rotation, pose.Rotation, approachBlend);
                float flare = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.72f, 1, t));
                pose.Rotation = Quaternion.Slerp(pose.Rotation, landingRotation, flare);
                pose.Feet = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.45f, .8f, t));
                pose.Power = Mathf.Lerp(SeagullVisual.FlightPower(age, seed), .85f, approachBlend) * (1 - flare);
                pose.Flare = Mathf.Sin(flare * Mathf.PI) * .9f;
            }
            else
            {
                float restLength = 14 + (seed & 7);
                float restAge = elapsed - 21.5f;
                if (restAge < restLength)
                {
                    pose.Position = contact;
                    pose.Rotation = landingRotation;
                    pose.Power = 0;
                    pose.Feet = 1;
                    pose.Fold = Mathf.SmoothStep(0, 1, Mathf.Clamp01(restAge / .8f));
                    pose.Resting = true;
                }
                else
                {
                    float departure = restAge - restLength;
                    if (departure < 1.6f)
                    {
                        float launch = Mathf.Max(0, departure - .45f);
                        pose.Position = contact + heading * (launch * 8) + Vector3.up * (launch * 5 + Mathf.Sin(Mathf.Clamp01(launch) * Mathf.PI) * .35f);
                        pose.Rotation = Quaternion.Slerp(landingRotation, Quaternion.LookRotation(heading + Vector3.up * .4f), Mathf.Clamp01(launch * 3));
                        pose.Fold = 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01(departure / .55f));
                        pose.Feet = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.65f, 1.6f, departure));
                        pose.Power = Mathf.SmoothStep(0, 1, Mathf.Clamp01(departure / .35f));
                    }
                    else
                    {
                        var from = contact + heading * 9.2f + Vector3.up * 5.75f;
                        float t = (departure - 1.6f) / 18;
                        var end = state.End + offset;
                        Flight(ref pose, from, Vector3.Lerp(from, end, .5f) + Vector3.up * 20, end, t, seed);
                        pose.Rotation = Quaternion.Slerp(Quaternion.LookRotation(heading + Vector3.up * .4f), pose.Rotation,
                            Mathf.SmoothStep(0, 1, Mathf.Clamp01((departure - 1.6f) / .85f)));
                        pose.Power = Mathf.Max(pose.Power, 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.6f, 3.6f, departure)));
                        pose.Visible &= t <= 1;
                    }
                }
            }
            return pose;
        }

        static Vector3 Curve(Vector3 a, Vector3 b, Vector3 c, float t) => (1 - t) * (1 - t) * a + 2 * (1 - t) * t * b + t * t * c;

        static void Flight(ref BirdPose pose, Vector3 a, Vector3 b, Vector3 c, float t, int seed, bool wander = true)
        {
            t = Mathf.Clamp01(t);
            Vector3 direction = 2 * ((1 - t) * (b - a) + t * (c - b));
            if (direction.sqrMagnitude < .001f) direction = c - a;
            var before = Vector3.ProjectOnPlane(Vector3.Lerp(b - a, c - b, Mathf.Max(0, t - .03f)), Vector3.up).normalized;
            var after = Vector3.ProjectOnPlane(Vector3.Lerp(b - a, c - b, Mathf.Min(1, t + .03f)), Vector3.up).normalized;
            float bank = Mathf.Clamp(-Vector3.SignedAngle(before, after, Vector3.up) * 5, -28, 28);
            pose.Position = Curve(a, b, c, t);
            if (wander)
            {
                float envelope = Mathf.Sin(t * Mathf.PI);
                pose.Position += Vector3.up * (Mathf.Sin(t * 22 + (seed & 31)) * .8f * envelope);
            }
            pose.Rotation = Quaternion.LookRotation(direction.normalized, Vector3.up) * Quaternion.Euler(0, 0, bank);
        }
    }
}
