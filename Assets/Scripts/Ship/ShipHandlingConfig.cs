using UnityEngine;

namespace PirateSlop
{
    [CreateAssetMenu(menuName = "PirateSlop/Ship Handling", fileName = "ShipHandlingConfig")]
    public class ShipHandlingConfig : ScriptableObject
    {
        [Header("Speed (m/s)")]
        public float maxSpeed = 8f;
        public float acceleration = 2.5f;
        public float deceleration = 1.6f;

        [Header("Turning (deg/s at full speed)")]
        public float maxTurnSpeed = 18f;
        [Range(0f, 1f)] public float minTurnSpeedFactor = 0.15f;
        public float rudderLerp = 4f;
        public float rudderReturn = 2.5f;

        [Header("Throttle")]
        [Range(0f, 1f)] public float throttleLerp = 3f;
    }
}
