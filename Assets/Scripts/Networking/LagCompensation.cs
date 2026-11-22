using System.Collections.Generic;
using FishNet.Managing.Timing;
using UnityEngine;

namespace PirateSlop.Networking
{
    public static class LagCompensation
    {
        public const float MaxRewind = .35f;
        public const float ObserverSmoothing = .06f;
        const int HistoryLength = 40;

        struct Pose
        {
            public double Time;
            public Vector3 Position;
            public Quaternion Rotation;
            public float Height;
            public Vector3 Center;
        }

        sealed class Track
        {
            public NetworkPlayer Player;
            public CharacterController Body;
            public CombatHealth Health;
            public PlayerHitbox Hitbox;
            public readonly Pose[] Ring = new Pose[HistoryLength];
            public int Count, Head;
            public Pose Saved;
        }

        static readonly Dictionary<NetworkPlayer, Track> tracks = new();
        static readonly List<Track> rewound = new();
        static readonly List<NetworkPlayer> stale = new();

        public static void Record(NetworkPlayer player, double time)
        {
            if (player == null) return;
            if (!tracks.TryGetValue(player, out var track))
            {
                var body = player.GetComponent<CharacterController>();
                if (body == null) return;
                track = new Track { Player = player, Body = body, Health = player.GetComponent<CombatHealth>(), Hitbox = player.GetComponentInChildren<PlayerHitbox>(true) };
                tracks[player] = track;
            }
            var root = track.Body.transform;
            track.Ring[track.Head] = new Pose { Time = time, Position = root.position, Rotation = root.rotation, Height = track.Body.height, Center = track.Body.center };
            track.Head = (track.Head + 1) % HistoryLength;
            if (track.Count < HistoryLength) track.Count++;
        }

        public static void Forget(NetworkPlayer player)
        {
            if (player != null) tracks.Remove(player);
        }

        public static float Delay(TimeManager timeManager, uint viewTick)
        {
            if (timeManager == null || viewTick == 0 || viewTick == TimeManager.UNSET_TICK || viewTick > timeManager.Tick) return 0f;
            float seconds = (float)((timeManager.Tick - viewTick) * timeManager.TickDelta) + ObserverSmoothing;
            return Mathf.Clamp(seconds, 0f, MaxRewind);
        }

        public static uint ClientViewTick(TimeManager timeManager, bool server)
        {
            if (server || timeManager == null) return 0;
            uint tick = timeManager.LastPacketTick.LastRemoteTick;
            return tick == TimeManager.UNSET_TICK ? 0 : tick;
        }

        public static void Rewind(NetworkPlayer shooter, float delay)
        {
            Restore();
            if (delay <= 0f || tracks.Count == 0) return;
            double target = Time.timeAsDouble - delay;
            stale.Clear();
            foreach (var pair in tracks)
            {
                var track = pair.Value;
                if (pair.Key == null || track.Body == null) { stale.Add(pair.Key); continue; }
                if (pair.Key == shooter || !track.Body.enabled || track.Count < 2 || (track.Health != null && track.Health.IsDead)) continue;
                if (!Sample(track, target, out var pose)) continue;
                track.Saved = Current(track);
                Apply(track, pose, true);
                rewound.Add(track);
            }
            for (int i = 0; i < stale.Count; i++) tracks.Remove(stale[i]);
            if (rewound.Count > 0) Physics.SyncTransforms();
        }

        public static void Restore()
        {
            if (rewound.Count == 0) return;
            for (int i = 0; i < rewound.Count; i++)
            {
                var track = rewound[i];
                if (track.Body == null) continue;
                Apply(track, track.Saved, track.Body.enabled);
            }
            rewound.Clear();
            Physics.SyncTransforms();
        }

        static Pose Current(Track track)
        {
            var root = track.Body.transform;
            return new Pose { Time = Time.timeAsDouble, Position = root.position, Rotation = root.rotation, Height = track.Body.height, Center = track.Body.center };
        }

        static bool Sample(Track track, double target, out Pose pose)
        {
            pose = default;
            int newest = (track.Head - 1 + HistoryLength) % HistoryLength;
            if (target >= track.Ring[newest].Time) return false;
            Pose later = track.Ring[newest];
            for (int i = 1; i < track.Count; i++)
            {
                var earlier = track.Ring[(newest - i + HistoryLength) % HistoryLength];
                if (earlier.Time <= target)
                {
                    float t = later.Time > earlier.Time ? (float)((target - earlier.Time) / (later.Time - earlier.Time)) : 0f;
                    pose = new Pose
                    {
                        Time = target,
                        Position = Vector3.Lerp(earlier.Position, later.Position, t),
                        Rotation = Quaternion.Slerp(earlier.Rotation, later.Rotation, t),
                        Height = t < .5f ? earlier.Height : later.Height,
                        Center = t < .5f ? earlier.Center : later.Center
                    };
                    return true;
                }
                later = earlier;
            }
            pose = later;
            return true;
        }

        static void Apply(Track track, Pose pose, bool enabled)
        {
            var body = track.Body;
            body.enabled = false;
            body.transform.SetPositionAndRotation(pose.Position, pose.Rotation);
            body.height = pose.Height;
            body.center = pose.Center;
            body.enabled = enabled;
            if (track.Hitbox != null) track.Hitbox.Sync();
        }
    }
}
