using UnityEngine;
namespace PirateSlop
{
    [System.Serializable]
    public struct PlayerCommand
    {
        public Vector2 Move;
        public float Yaw, Pitch;
        public bool Sprint, Crouch, Slide, Jump, Use, Release, Rise;
        public bool IsValid => float.IsFinite(Move.x) && float.IsFinite(Move.y) && float.IsFinite(Yaw)
            && Move.sqrMagnitude <= 1.01f && Mathf.Abs(Yaw) <= 36000f && float.IsFinite(Pitch) && Mathf.Abs(Pitch) <= 85f;
    }
    [System.Serializable]
    public struct PlayerState
    {
        public Vector3 Position, SlideDirection;
        public float Yaw, VerticalVelocity, SlideTimer, Cooldown;
        public bool Crouched, Locked, Grounded;
        public float PlanarSpeed;
        public bool Swimming;
        public Vector3 SwimVelocity;
        public float Breath;
        public bool Climbing;
        public float LadderCooldown;
    }
    [System.Serializable]
    public struct ShipState
    {
        public Vector3 Position;
        public float Yaw, Speed, Bank, Sail, Rudder, Pitch, WaveRoll, WaveTime;
        public bool Controlling;
    }
}
