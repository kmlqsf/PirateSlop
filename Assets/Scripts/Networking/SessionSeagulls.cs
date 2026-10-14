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
        public int LootBird;
        public InventoryItem LootItem;
        public float Age;
        public float EndAge;
        public byte ShotMask;
        public SeagullFall[] Falls;
        public Vector3 Start, Control, End;
        public Vector3[] Perches, Normals;
    }

    public struct SeagullHitMessage : IBroadcast
    {
        public int Id, Index;
        public float EndAge;
        public SeagullFall Fall;
    }

    public sealed partial class SessionController
    {
        sealed class SeagullEvent
        {
            public SeagullFlockMessage Message;
            public float Started;
            public readonly float[] ShotAt = new float[8];
        }
        readonly List<SeagullEvent> seagullEvents = new();
        readonly Dictionary<int, AmbientSeagullFlock> seagullVisuals = new();
        readonly Dictionary<NetworkPlayer, float> seagullVisits = new();
        readonly Dictionary<NetworkPlayer, float> seagullRetryAt = new();
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
            nextSeagullAt = Time.time + seagullRandom.Next(30, 46);
            var humans = players.Values.Where(p => p != null && !p.IsBot.Value && p.Motor != null && !p.Motor.IsDead).ToArray();
            if (humans.Length == 0) return;
            foreach (var departed in seagullVisits.Keys.Where(p => p == null || !players.Values.Contains(p)).ToArray()) seagullVisits.Remove(departed);
            foreach (var departed in seagullRetryAt.Keys.Where(p => p == null || !players.Values.Contains(p)).ToArray()) seagullRetryAt.Remove(departed);
            if (seagullEvents.Count >= 3) return;
            var ordered = humans.Where(p => !seagullRetryAt.TryGetValue(p, out var retry) || Time.time >= retry)
                .OrderBy(p => seagullVisits.TryGetValue(p, out var visited) ? visited : float.NegativeInfinity)
                .ThenBy(p => seagullRandom.Next()).ToArray();
            bool landing = seagullRandom.Next(3) == 0;
            foreach (var player in ordered.Take(3))
            {
                int previous = nextSeagullId;
                SpawnSeagulls(player.transform.position, player.transform.forward, landing, false);
                if (previous == nextSeagullId) { seagullRetryAt[player] = Time.time + 90f; continue; }
                seagullRetryAt.Remove(player);
                seagullVisits[player] = Time.time;
                break;
            }
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
                LootBird = -1, LootItem = InventoryItem.None,
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
            message.Falls = new SeagullFall[message.Count];
            if (seagullRandom.NextDouble() < .5 && ChestLootTable.TryRollItem(seagullRandom, item => SeagullLootPrefab(item) != null, out var loot))
            {
                message.LootBird = seagullRandom.Next(message.Count);
                message.LootItem = loot;
            }
            nextSeagullId = message.Id;
            seagullEvents.Add(new SeagullEvent { Message = message, Started = Time.time });
            manager.ServerManager.Broadcast(message);
            ReceiveSeagulls(message, Channel.Reliable);
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
                message.Falls = (SeagullFall[])entry.Message.Falls.Clone();
                for (int i = 0; i < message.Count; i++)
                    if ((message.ShotMask & (1 << i)) != 0) message.Falls[i].Age = Time.time - entry.ShotAt[i];
                if (message.Age < AmbientSeagullFlock.Duration(message)) manager.ServerManager.Broadcast(connection, message);
            }
        }

        public void ShootSeagull(int id, int index, Vector3 direction)
        {
            if (manager == null || !manager.ServerManager.Started) return;
            var entry = seagullEvents.Find(e => e.Message.Id == id);
            if (entry == null || index < 0 || index >= entry.Message.Count || (entry.Message.ShotMask & (1 << index)) != 0) return;
            if (!seagullVisuals.TryGetValue(id, out var flock) || flock == null) return;
            var bird = flock.Bird(index);
            if (bird == null || bird.Shot || !bird.gameObject.activeSelf) return;
            float age = Time.time - entry.Started;
            var current = AmbientSeagullFlock.Evaluate(entry.Message, index, age);
            var next = AmbientSeagullFlock.Evaluate(entry.Message, index, age + .04f);
            var fall = bird.CaptureFall((next.Position - current.Position) / .04f, direction);
            entry.Message.ShotMask |= (byte)(1 << index);
            entry.Message.Falls[index] = fall;
            entry.ShotAt[index] = Time.time;
            if (entry.Message.LootBird == index) DropSeagullLoot(entry.Message.LootItem, bird, (next.Position - current.Position) / .04f, current.Resting);
            entry.Message.EndAge = Mathf.Max(AmbientSeagullFlock.Duration(entry.Message), age + fall.Seconds + SeagullVisual.SinkSeconds);
            var message = new SeagullHitMessage { Id = id, Index = index, Fall = fall, EndAge = entry.Message.EndAge };
            manager.ServerManager.Broadcast(message);
            ReceiveSeagullHit(message, Channel.Reliable);
        }

        void ReceiveSeagullHit(SeagullHitMessage message, Channel channel)
        {
            if (seagullVisuals.TryGetValue(message.Id, out var flock) && flock != null)
                flock.ShootBird(message.Index, message.Fall, message.EndAge);
        }

        public void LandSeagull(int id, int index, SeagullFall fall)
        {
            if (manager == null || !manager.ServerManager.Started || !fall.Landed) return;
            var entry = seagullEvents.Find(e => e.Message.Id == id);
            if (entry == null || index < 0 || index >= entry.Message.Count || (entry.Message.ShotMask & (1 << index)) == 0) return;
            fall.Age = Time.time - entry.ShotAt[index];
            entry.Message.Falls[index] = fall;
            float endAge = entry.ShotAt[index] - entry.Started + fall.Seconds + SeagullVisual.SinkSeconds;
            entry.Message.EndAge = Mathf.Max(AmbientSeagullFlock.Duration(entry.Message), endAge);
            var message = new SeagullHitMessage { Id = id, Index = index, Fall = fall, EndAge = entry.Message.EndAge };
            manager.ServerManager.Broadcast(message);
            ReceiveSeagullHit(message, Channel.Reliable);
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
            seagullVisuals.Clear(); seagullEvents.Clear(); seagullVisits.Clear(); seagullRetryAt.Clear();
            seagullPerches = null; seagullRandom = null; nextSeagullAt = 0; nextSeagullId = 0;
        }

        public NetworkFish SeagullLootPrefab(InventoryItem item)
        {
            var weapon = PlayerPrefab != null ? PlayerPrefab.GetComponent<NetworkWeapon>() : null;
            int index = CannonAmmo.IsBall(item) ? (int)InventoryItem.Cannonball : (int)item;
            return weapon != null && weapon.DropPrefabs != null && index >= 0 && index < weapon.DropPrefabs.Length ? weapon.DropPrefabs[index] : null;
        }

        void DropSeagullLoot(InventoryItem item, SeagullVisual bird, Vector3 velocity, bool resting)
        {
            var prefab = SeagullLootPrefab(item);
            if (prefab == null) return;
            var point = bird.transform.TransformPoint(new Vector3(0, -.35f, 0));
            if (resting)
            {
                var bounds = LootPlacement.VisualBounds(prefab);
                float bottom = (bounds.center.y - bounds.extents.y) * prefab.transform.localScale.y;
                point.y = bird.transform.position.y - SeagullVisual.FootHeight - bottom + .04f;
            }
            var dropped = Instantiate(prefab, point, Quaternion.Euler(0, bird.transform.eulerAngles.y, 0));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(dropped.gameObject, bird.gameObject.scene);
            if (CannonAmmo.IsBall(item)) dropped.SetAmmoItem(item);
            dropped.Place(null, point, dropped.transform.rotation);
            manager.ServerManager.Spawn(dropped.NetworkObject);
            dropped.BeginSeagullLoot(Vector3.ProjectOnPlane(velocity, Vector3.up) * .25f);
        }
    }
}
