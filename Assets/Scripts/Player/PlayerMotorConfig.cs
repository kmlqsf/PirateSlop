using UnityEngine;

namespace PirateSlop
{
    [CreateAssetMenu(menuName = "PirateSlop/Player Motor", fileName = "PlayerMotorConfig")]
    public class PlayerMotorConfig : ScriptableObject
    {
        [Header("Move")]
        public float walkSpeed = 3.6f;
        public float sprintMultiplier = 1.45f;
        public float gravity = -20f;
        public float jumpSpeed = 6.5f;
        public float lookSensitivity = 1f;
        public float minPitch = -80f;
        public float maxPitch = 80f;

        [Header("Controller")]
        public float height = 1.8f;
        public float radius = 0.32f;
        public float stepOffset = 0.55f;
        public float skinWidth = 0.08f;

        [Header("Fall")]
        public float killY = -8f;
    }
}
