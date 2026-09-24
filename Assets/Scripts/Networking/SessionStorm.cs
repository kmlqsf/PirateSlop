using FishNet.Broadcast;
using FishNet.Transporting;
using PirateSlop.World;
using UnityEngine;

namespace PirateSlop.Networking
{
    public struct StormMessage : IBroadcast
    {
        public float Elapsed, Duration, StartRadius;
        public bool Paused;
    }

    public sealed partial class SessionController
    {
        bool stormRunning;
        bool stormPaused;
        float stormPausedAt;
        public bool StormPaused => manager != null && manager.ServerManager.Started ? stormPaused : storm != null && storm.Paused;
        float StormElapsed => (stormPaused ? stormPausedAt : Time.time) - stormStarted;
                public float stormDuration = 600f;
        float stormStarted, stormTick, stormRadius;

        public void SetStormDuration(float newDuration)
        {
            if (!stormRunning) return;
            // Adjust stormStarted so that the current progress is preserved?
            // Or just change duration. If we just change duration, progress jumps.
            // Preserving progress:
            // current_progress = StormElapsed / old_duration
            // new_elapsed = current_progress * newDuration
            // stormStarted = Time.time - new_elapsed
            float progress = Mathf.Clamp01(StormElapsed / stormDuration);
            stormDuration = Mathf.Max(1f, newDuration);
            float newElapsed = progress * stormDuration;
            if (stormPaused) stormPausedAt = stormStarted + newElapsed;
            else stormStarted = Time.time - newElapsed;
            
            stormTick = Time.time;
            manager.ServerManager.Broadcast(CurrentStorm());
        }
        StormZone storm;
        public float SafeRadius(float ahead = 0f) => stormRunning
            ? Mathf.Lerp(stormRadius, StormZone.FinalRadius, Mathf.Clamp01((StormElapsed + (stormPaused ? 0 : ahead)) / stormDuration))
            : ProceduralWorld.Instance.Layout.Radius;

        void StartStorm()
        {
            stormRunning = true;
            stormPaused = false;
            stormStarted = Time.time;
            stormTick = Time.time;
            stormRadius = ProceduralWorld.Instance.Layout.Radius;
        }

        StormMessage CurrentStorm() => new StormMessage
        {
            Elapsed = StormElapsed,
            Paused = stormPaused,
            Duration = stormDuration,
            StartRadius = stormRadius
        };

        void ReceiveStorm(StormMessage message, Channel channel)
        {
            if (storm == null) storm = new GameObject("BattleStorm").AddComponent<StormZone>();
            storm.Synchronize(message.Elapsed, message.Duration, message.StartRadius, message.Paused);
        }

        public bool ToggleStormPause()
        {
            if (!DeveloperMenu.Available || manager == null || !manager.ServerManager.Started || !stormRunning) return false;
            if (stormPaused) stormStarted += Time.time - stormPausedAt;
            else stormPausedAt = Time.time;
            stormPaused = !stormPaused;
            var message = CurrentStorm();
            manager.ServerManager.Broadcast(message);
            if (manager.ClientManager.Started) ReceiveStorm(message, Channel.Reliable);
            return true;
        }

        void TickStorm()
        {
            if (manager == null) return;
            if (!manager.ServerManager.Started && !manager.ClientManager.Started)
            {
                stormRunning = false;
                stormPaused = false;
                if (storm != null) Destroy(storm.gameObject);
                return;
            }
            if (!manager.ServerManager.Started || !stormRunning || Time.time < stormTick) return;
            stormTick = Time.time + 1f;
            var message = CurrentStorm();
            manager.ServerManager.Broadcast(message);
            float progress = Mathf.Clamp01(message.Elapsed / message.Duration);
            float radius = Mathf.Lerp(stormRadius, StormZone.FinalRadius, progress);
            if (OceanSurface.Instance != null) OceanSurface.Instance.WaveScale = Mathf.Lerp(.06f, 2.5f, progress * progress);
            if (stormPaused) return;
            foreach (var player in players.Values)
            {
                if (player == null) continue;
                Vector3 point = player.transform.position;
                if (new Vector2(point.x, point.z).sqrMagnitude >= radius * radius)
                    player.GetComponent<CombatHealth>()?.Damage(Mathf.Lerp(5f, 15f, progress));
            }
        }
    }
}

