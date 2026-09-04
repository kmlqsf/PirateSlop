using UnityEngine;

namespace PirateSlop
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMotor : MonoBehaviour
    {
        [SerializeField] PlayerMotorConfig config;
        [SerializeField] Transform lookPivot;
        [SerializeField] Transform respawnPoint;

        CharacterController _cc;
        float _vertical;
        float _pitch;
        bool _locomotionLocked;
        Transform _platform;
        Vector3 _platformPos;
        Quaternion _platformRot;
        Vector3 _spawnPos;
        Quaternion _spawnRot;

        public PlayerMotorConfig Config => config;
        public Transform LookPivot => lookPivot;
        public bool IsGrounded => _cc != null && _cc.isGrounded;
        public bool LocomotionLocked => _locomotionLocked;

        public void SetConfig(PlayerMotorConfig value) => config = value;
        public void SetLookPivot(Transform pivot) => lookPivot = pivot;
        public void SetRespawn(Transform point) => respawnPoint = point;
        public void SetLocomotionLocked(bool locked) => _locomotionLocked = locked;

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            ApplyControllerShape();
            _spawnPos = transform.position;
            _spawnRot = transform.rotation;
        }

        public void ApplyControllerShape()
        {
            if (_cc == null || config == null) return;
            _cc.height = config.height;
            _cc.radius = config.radius;
            _cc.center = new Vector3(0f, config.height * 0.5f, 0f);
            _cc.stepOffset = Mathf.Min(config.stepOffset, config.height);
            _cc.skinWidth = config.skinWidth;
            _cc.minMoveDistance = 0f;
        }

        public void Tick(GameplayInput.Frame input, float dt)
        {
            if (config == null || _cc == null) return;

            RidePlatform();
            ApplyLook(input.Look);

            Vector3 planar = Vector3.zero;
            if (!_locomotionLocked)
            {
                Vector3 yawFwd = Vector3.ProjectOnPlane(lookPivot != null ? lookPivot.forward : transform.forward, Vector3.up).normalized;
                Vector3 yawRight = Vector3.ProjectOnPlane(lookPivot != null ? lookPivot.right : transform.right, Vector3.up).normalized;
                planar = (yawFwd * input.Move.y + yawRight * input.Move.x) * config.walkSpeed;
            }

            if (_cc.isGrounded && _vertical < 0f)
                _vertical = -2f;

            if (!_locomotionLocked && input.Jump && _cc.isGrounded)
                _vertical = config.jumpSpeed;

            _vertical += config.gravity * dt;
            _cc.Move((planar + Vector3.up * _vertical) * dt);

            if (transform.position.y < config.killY)
                Respawn();
        }

        void ApplyLook(Vector2 look)
        {
            if (lookPivot == null || config == null) return;
            transform.Rotate(0f, look.x * config.lookSensitivity, 0f, Space.World);
            _pitch = Mathf.Clamp(_pitch - look.y * config.lookSensitivity, config.minPitch, config.maxPitch);
            lookPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        void RidePlatform()
        {
            Transform next = ProbePlatform();
            if (next != null && next == _platform)
            {
                Vector3 deltaPos = next.position - _platformPos;
                Quaternion deltaRot = next.rotation * Quaternion.Inverse(_platformRot);
                if (deltaPos.sqrMagnitude > 0.0000001f || deltaRot != Quaternion.identity)
                {
                    _cc.enabled = false;
                    Vector3 offset = transform.position - _platformPos;
                    transform.position = next.position + deltaRot * offset;
                    transform.rotation = deltaRot * transform.rotation;
                    _cc.enabled = true;
                }
            }

            _platform = next;
            if (_platform != null)
            {
                _platformPos = _platform.position;
                _platformRot = _platform.rotation;
            }
        }

        Transform ProbePlatform()
        {
            float radius = _cc.radius * 0.9f;
            Vector3 origin = transform.position + Vector3.up * (radius + 0.05f);
            if (!Physics.SphereCast(origin, radius, Vector3.down, out RaycastHit hit, 0.45f, ~0, QueryTriggerInteraction.Ignore))
                return null;

            Rigidbody body = hit.rigidbody != null ? hit.rigidbody : hit.collider.attachedRigidbody;
            if (body != null) return body.transform;
            var ship = hit.collider.GetComponentInParent<ShipMotor>();
            return ship != null ? ship.transform : null;
        }

        public void Respawn()
        {
            Vector3 pos = respawnPoint != null ? respawnPoint.position : _spawnPos;
            Quaternion rot = respawnPoint != null ? respawnPoint.rotation : _spawnRot;
            _cc.enabled = false;
            transform.SetPositionAndRotation(pos, rot);
            _cc.enabled = true;
            _vertical = 0f;
            _platform = null;
        }
    }
}
