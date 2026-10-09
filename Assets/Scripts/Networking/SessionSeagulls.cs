using System;
using System.Collections.Generic;
using System.Linq;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Transporting;
using PirateSlop.World;
using UnityEngine;

namespace PirateSlop.Networking
{
    public struct SeagullFlockMessage : IBroadcast
    {
        public int Id, Seed, Count;
        public float Age;
        public Vector3 Start, Control, End;
        public Vector3[] Perches, Normals;
    }

    public sealed partial class SessionController
    {
        sealed class SeagullEvent
        {
            public SeagullFlockMessage Message;
            public float Started;
        }
        readonly List<SeagullEvent> seagullEvents = new();
        readonly Dictionary<int, AmbientSeagullFlock> seagullVisuals = new();
        CoastalSeagullPerches[] seagullPerches;
        System.Random seagullRandom;
        float nextSeagullAt;
        int nextSeagullId;

        void TickSeagulls()
        {
            if (manager == null) return;
            if (!manager.ServerManager.Started && !manager.ClientManager.Started) { ClearSeagulls(); return; }
            if (!manager.ServerManager.Started || ProceduralWorld.Instance == null || !ProceduralWorld.Instance.Ready) return;
            seagullEvents.RemoveAll(e => Time.time - e.Started >= AmbientSeagullFlock.Duration(e.Message));
            if (nextSeagullAt == 0) { nextSeagullAt = Time.time + 18; return; }
            if (Time.time < nextSeagullAt) return;
            seagullRandom ??= new System.Random(ProceduralWorld.Instance.Layout.Seed ^ 73891);
            nextSeagullAt = Time.time + seagullRandom.Next(45, 76);
            var humans = players.Values.Where(p => p != null && !p.IsBot.Value && p.Motor != null && !p.Motor.IsDead).ToArray();
            if (humans.Length == 0) return;
            var player = humans[seagullRandom.Next(humans.Length)];
            SpawnSeagulls(player.transform.position, player.transform.forward, seagullRandom.Next(3) == 0, false);
        }

        public string SpawnDeveloperSeagulls(Vector3 origin, Vector3 forward, bool landing)
        {
            if (!DeveloperMenu.Available || manager == null || !manager.ServerManager.Started) return "Команду выполняет хост.";
            return SpawnSeagulls(origin, forward, landing, true);
        }

        string SpawnSeagulls(Vector3 origin, Vector3 forward, bool landing, bool requiredLanding)
        {
            var world = ProceduralWorld.Instance;
            if (world == null || !world.Ready) return "Карта ещё загружается.";
            seagullEvents.RemoveAll(e => Time.time - e.Started >= AmbientSeagullFlock.Duration(e.Message));
            if (seagullEvents.Count >= 3) return "Уже летят три стаи. Дождитесь их пролёта.";
            seagullRandom ??= new System.Random(world.Layout.Seed ^ 73891);
            seagullPerches ??= world.GetComponentsInChildren<CoastalSeagullPerches>();
            forward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .5f) forward = Vector3.forward;
            var right = Vector3.Cross(Vector3.up, forward);
            var points = new List<Vector3>();
            var normals = new List<Vector3>();
            float altitude = Mathf.Max(world.Layout.SeaLevel + seagullRandom.Next(18, 29), origin.y + 10f);
            var candidates = new List<(Vector3 point, Vector3 normal, float distance)>();
            foreach (var perches in seagullPerches)
            {
                if (perches == null || !perches.isActiveAndEnabled) continue;
                for (int i = 0; i < perches.Points.Length && i < perches.Normals.Length; i++)
                {
                    var point = perches.transform.TransformPoint(perches.Points[i]);
                    float distance = Vector3.ProjectOnPlane(point - origin, Vector3.up).magnitude;
                    if (distance > 450) continue;
                    if (!landing || point.y < world.Layout.SeaLevel + 4) continue;
                    var normal = perches.transform.localToWorldMatrix.inverse.transpose.MultiplyVector(perches.Normals[i]).normalized;
                    if (normal.y < .75f) continue;
                    float score = distance + (Vector3.Dot((point - origin).normalized, forward) < .1f ? 140 : 0);
                    candidates.Add((point, normal, score));
                }
            }
            var message = new SeagullFlockMessage
            {
                Id = nextSeagullId + 1, Seed = seagullRandom.Next(1, int.MaxValue), Count = seagullRandom.Next(6, 9),
                Start = origin - forward * 100 - right * 35,
                Control = origin + right * 20,
                End = origin + forward * 170 + right * 35,
                Perches = Array.Empty<Vector3>(), Normals = Array.Empty<Vector3>()
            };
            bool clearFlight = false;
            float[] headings = { 0f, 75f, -75f, 180f };
            foreach (float angle in headings)
            {
                var flightForward = Quaternion.Euler(0f, angle, 0f) * forward;
                var flightRight = Vector3.Cross(Vector3.up, flightForward);
                message.Start = origin - flightForward * 100f - flightRight * 35f;
                message.Control = origin + flightRight * 20f;
                message.End = origin + flightForward * 170f + flightRight * 35f;
                message.Start.y = altitude; message.Control.y = altitude + 3f; message.End.y = altitude + 2f;
                clearFlight = true;
                for (int i = 0; i < message.Count; i++)
                    if (!ClearSeagullRoute(message, i, 0f, 32f, 1f)) { clearFlight = false; break; }
                if (clearFlight) break;
            }
            if (!clearFlight) return "Рядом нет свободного пути для низкого пролёта стаи.";
            int attempts = 0;
            foreach (var candidate in candidates.OrderBy(c => c.distance))
            {
                if (points.Count >= 3 || attempts >= 16) break;
                if (points.Any(p => Vector3.Distance(p, candidate.point) < 2)) continue;
                attempts++;
                message.Perches = points.Append(candidate.point).ToArray();
                message.Normals = normals.Append(candidate.normal).ToArray();
                int index = points.Count;
                float stagger = index * .85f;
                int seed = message.Seed + index * 113;
                float departure = stagger + 21.5f + 14 + (seed & 7);
                if (!ClearSeagullRoute(message, index, 0, stagger + 16, .5f)
                    || !ClearSeagullRoute(message, index, stagger + 16, stagger + 21.5f, .2f)
                    || !ClearSeagullRoute(message, index, departure, departure + 1.6f, .2f)
                    || !ClearSeagullRoute(message, index, departure + 1.6f, departure + 19.6f, .5f)) continue;
                points.Add(candidate.point); normals.Add(candidate.normal);
            }
            if (requiredLanding && landing && points.Count == 0) return "Рядом нет скалы со свободной площадкой для посадки.";
            message.Perches = points.ToArray(); message.Normals = normals.ToArray();
            nextSeagullId = message.Id;
            seagullEvents.Add(new SeagullEvent { Message = message, Started = Time.time });
            manager.ServerManager.Broadcast(message);
            if (manager.ClientManager.Started) ReceiveSeagulls(message, Channel.Reliable);
            return points.Count > 0 ? $"Стая вызвана: {points.Count} чайки направляются на скалы." : "Стая чаек пролетит над вами.";
        }

        static bool ClearSeagullRoute(SeagullFlockMessage message, int index, float from, float to, float step)
        {
            var previous = AmbientSeagullFlock.Evaluate(message, index, from).Position;
            int samples = Mathf.CeilToInt((to - from) / step);
            for (int sample = 1; sample <= samples; sample++)
            {
                var point = AmbientSeagullFlock.Evaluate(message, index, Mathf.Lerp(from, to, sample / (float)samples)).Position;
                if ((point - previous).sqrMagnitude > .000001f && Physics.Linecast(previous, point, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
                previous = point;
            }
            return true;
        }

        void SendSeagulls(NetworkConnection connection)
        {
            foreach (var entry in seagullEvents)
            {
                var message = entry.Message;
                message.Age = Time.time - entry.Started;
                if (message.Age < AmbientSeagullFlock.Duration(message)) manager.ServerManager.Broadcast(connection, message);
            }
        }

        void ReceiveSeagulls(SeagullFlockMessage message, Channel channel)
        {
            var world = ProceduralWorld.Instance;
            if (world == null || !world.Ready || message.Count < 1 || message.Count > 8 || message.Age < 0 || message.Age >= AmbientSeagullFlock.Duration(message) || seagullVisuals.ContainsKey(message.Id)) return;
            if (message.Perches == null || message.Normals == null || message.Perches.Length > 3 || message.Normals.Length != message.Perches.Length) return;
            foreach (var key in seagullVisuals.Where(p => p.Value == null).Select(p => p.Key).ToArray()) seagullVisuals.Remove(key);
            if (seagullVisuals.Count >= 3) return;
            var flock = new GameObject("AmbientSeagullFlock").AddComponent<AmbientSeagullFlock>();
            flock.transform.SetParent(world.transform, false);
            flock.Initialize(message);
            seagullVisuals.Add(message.Id, flock);
        }

        void ClearSeagulls()
        {
            foreach (var flock in seagullVisuals.Values) if (flock != null) Destroy(flock.gameObject);
            seagullVisuals.Clear(); seagullEvents.Clear();
            seagullPerches = null; seagullRandom = null; nextSeagullAt = 0; nextSeagullId = 0;
        }
    }
}
