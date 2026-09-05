using UnityEngine;
namespace PirateSlop
{
    [System.Serializable]
    public struct PlayerCommand
    {
        public Vector2 Move;
        public float Yaw;
        public bool Sprint, Crouch, Slide, Jump, Use, Release;
        public bool IsValid => float.IsFinite(Move.x) && float.IsFinite(Move.y) && float.IsFinite(Yaw)
            && Move.sqrMagnitude <= 1.01f && Mathf.Abs(Yaw) <= 36000f;
    }
    [System.Serializable]
    public struct PlayerState
    {
        public Vector3 Position, SlideDirection;
        public float Yaw, VerticalVelocity, SlideTimer, Cooldown;
        public bool Crouched, Locked;
    }
    [System.Serializable]
    public struct ShipState
    {
        public Vector3 Position;
        public float Yaw, Speed, Bank, Sail, Rudder;
        public bool Controlling;
    }
}
