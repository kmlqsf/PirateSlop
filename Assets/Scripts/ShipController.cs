using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ShipController : MonoBehaviour
{
    [Header("Subsystems")]
    [SerializeField] private SailSystem sailSystem;
    [SerializeField] private WheelInteraction wheel;

    [Header("Movement Settings")]
    [SerializeField] private float maxThrustForce = 25000f;
    [SerializeField] private float turnTorque = 15000f;
    [SerializeField] private float maxSpeed = 15f;

    [Header("Water & Buoyancy Stabilization")]
    [SerializeField] private float lateralDrag = 3f;
    [SerializeField] private float angularDrag = 2f;
    [SerializeField] private float waterLevel = 0f;
    [SerializeField] private float waterSpringStrength = 15000f;
    [SerializeField] private float waterSpringDamping = 4000f;

    private Rigidbody rb;

    public Rigidbody Rigidbody => rb;
    public float CurrentSpeedKmh => Vector3.Dot(rb.linearVelocity, transform.forward) * 3.6f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.linearDamping = 0.5f;
        rb.angularDamping = angularDrag;
    }

    private void FixedUpdate()
    {
        ApplyWaterStabilization();
        ApplyPropulsion();
        ApplySteering();
        ApplyLateralResistance();
    }

    private void ApplyPropulsion()
    {
        if (sailSystem == null) return;
        float deployRatio = sailSystem.DeployPercentage;
        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);

        if (forwardSpeed < maxSpeed)
        {
            Vector3 thrust = transform.forward * (deployRatio * maxThrustForce);
            rb.AddForce(thrust, ForceMode.Force);
        }
    }

    private void ApplySteering()
    {
        if (wheel == null) return;
        float steerInput = wheel.CurrentRudderNormalized;
        float forwardSpeedRatio = Mathf.Clamp(Vector3.Dot(rb.linearVelocity, transform.forward) / 5f, -1f, 1f);
        float effectiveTorque = steerInput * turnTorque * forwardSpeedRatio;
        rb.AddTorque(Vector3.up * effectiveTorque, ForceMode.Force);
    }

    private void ApplyLateralResistance()
    {
        Vector3 lateralVelocity = transform.right * Vector3.Dot(rb.linearVelocity, transform.right);
        rb.AddForce(-lateralVelocity * (rb.mass * lateralDrag), ForceMode.Force);
    }

    private void ApplyWaterStabilization()
    {
        float heightError = waterLevel - transform.position.y;
        float springForce = (heightError * waterSpringStrength) - (rb.linearVelocity.y * waterSpringDamping);
        rb.AddForce(Vector3.up * springForce, ForceMode.Force);

        Vector3 targetUp = Vector3.up;
        Vector3 torqueCorrection = Vector3.Cross(transform.up, targetUp);
        rb.AddTorque(torqueCorrection * (rb.mass * 5f), ForceMode.Force);
    }
}
