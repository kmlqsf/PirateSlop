using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ShipController : MonoBehaviour
{
    [SerializeField] SailSystem sailSystem;
    [SerializeField] HelmInteraction helm;
    [SerializeField] float maxSpeed = 10f, acceleration = 2f, deceleration = 3f;
    [SerializeField] float turnSpeed = 22f, maxBankAngle = 5f, bankResponse = 2f;
    Rigidbody rb;
    float speed, yaw, bank, waterHeight;
    public float Speed => speed;
    public float Bank => bank;
    public void Configure(HelmInteraction value) { helm = value; sailSystem = GetComponent<SailSystem>(); }
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.None;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        yaw = transform.eulerAngles.y;
        waterHeight = transform.position.y;
        if (sailSystem == null) sailSystem = GetComponent<SailSystem>();
        if (helm == null) helm = GetComponentInChildren<HelmInteraction>();
    }
    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        float target = (sailSystem != null ? sailSystem.DeployPercentage : 0f) * maxSpeed;
        speed = Mathf.MoveTowards(speed, target, (target > speed ? acceleration : deceleration) * dt);
        float rudder = helm != null ? helm.CurrentRudderNormalized : 0f;
        float factor = Mathf.Clamp01(speed / Mathf.Max(0.1f, maxSpeed));
        yaw += rudder * turnSpeed * Mathf.Lerp(0.15f, 1f, factor) * dt;
        bank = Mathf.Lerp(bank, -rudder * maxBankAngle * factor, 1f - Mathf.Exp(-bankResponse * dt));
        rb.MoveRotation(Quaternion.Euler(0f, yaw, bank));
        Vector3 next = rb.position + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * speed * dt;
        next.y = waterHeight;
        rb.MovePosition(next);
    }
}
