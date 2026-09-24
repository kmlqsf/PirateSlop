using UnityEngine;

namespace PirateSlop.Networking
{
    [System.Serializable]
    public sealed class BotMotionSettings
    {
        [Min(1)] public int PathNodesPerFrame = 48;
        [Range(.25f, 4f)] public float PathMillisecondsPerFrame = 1.5f;
        [Min(32)] public int MaxPathNodes = 1000;
        [Min(.3f)] public float CellSize = .6f;
        [Min(1)] public float SearchRadius = 35f;
        [Min(1)] public float RetryAfter = 1f;
        [Min(1)] public float RecoveryAfter = 3f;
        [Min(15)] public float TaskTimeout = 90f;
        public bool AllowEmergencyTeleport = true;
        [Min(5)] public float HumanStationGrace = 30f;
        [Min(2)] public float FailedTaskRetry = 15f;
        [Range(.1f, 1f)] public float CruiseSails = 1f;
        public Vector3[] CannonSites = {
            new(-3.8f, 4.5f, -1f), new(3.8f, 4.5f, -1f),
            new(-3.8f, 4.5f, 2f), new(3.8f, 4.5f, 2f),
            new(-3.8f, 4.5f, 5f), new(3.8f, 4.5f, 5f)
        };
        [Min(10)] public float TeleportCooldown = 60f;
        [Range(.5f, 3f)] public float RecoveryRadius = 2f;
    }
}
