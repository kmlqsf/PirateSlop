using UnityEngine;

namespace PirateSlop
{
    /// <summary>
    /// Kinematic ship locomotion. Stations write throttle/rudder; this class owns motion.
    /// Future systems (wind, damage, AI helm) should talk to this API, not Rigidbody directly.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ShipMotor : MonoBehaviour
    {
        [SerializeField] ShipHandlingConfig handling;
        [SerializeField] Transform helmWheel;
        [SerializeField] float helmWheelTilt = 120f;
        Quaternion _helmRest = Quaternion.identity;
        bool _helmRestCached;

        Rigidbody _rb;
        float _throttleTarget;
        float _throttle;
        float _rudderTarget;
        float _rudder;
        float _speed;
        bool _rudderHeld;

        public ShipHandlingConfig Handling => handling;
        public float Throttle => _throttle;
        public float ThrottleTarget => _throttleTarget;
        public float Speed => _speed;
        public float Rudder => _rudder;
        public Vector3 Forward => transform.forward;
        public Rigidbody Body => _rb;

        public void SetHandling(ShipHandlingConfig config) => handling = config;
        public void SetHelmWheel(Transform wheel) => helmWheel = wheel;

        public void SetThrottle01(float value)
        {
            _throttleTarget = Mathf.Clamp01(value);
        }

        public void ToggleThrottle()
        {
            SetThrottle01(_throttleTarget > 0.15f ? 0f : 1f);
        }

        public void SetRudder(float value, bool held)
        {
            _rudderTarget = Mathf.Clamp(value, -1f, 1f);
            _rudderHeld = held;
        }

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.isKinematic = true;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        void FixedUpdate()
        {
            if (handling == null) return;

            float dt = Time.fixedDeltaTime;
            _throttle = Mathf.MoveTowards(_throttle, _throttleTarget, handling.throttleLerp * dt);

            if (_rudderHeld)
                _rudder = Mathf.MoveTowards(_rudder, _rudderTarget, handling.rudderLerp * dt);
            else
                _rudder = Mathf.MoveTowards(_rudder, 0f, handling.rudderReturn * dt);

            float speedTarget = _throttle * handling.maxSpeed;
            float rate = speedTarget >= _speed ? handling.acceleration : handling.deceleration;
            _speed = Mathf.MoveTowards(_speed, speedTarget, rate * dt);

            float speedFactor = handling.maxSpeed > 0.01f ? Mathf.Abs(_speed) / handling.maxSpeed : 0f;
            float turn = handling.maxTurnSpeed * Mathf.Lerp(handling.minTurnSpeedFactor, 1f, speedFactor);
            Quaternion yaw = Quaternion.Euler(0f, _rudder * turn * dt, 0f);

            _rb.MoveRotation(yaw * _rb.rotation);
            _rb.MovePosition(_rb.position + transform.forward * _speed * dt);

            if (helmWheel != null)
            {
                if (!_helmRestCached)
                {
                    _helmRest = helmWheel.localRotation;
                    _helmRestCached = true;
                }

                helmWheel.localRotation = _helmRest * Quaternion.Euler(0f, 0f, -_rudder * helmWheelTilt);
            }
        }
    }
}
