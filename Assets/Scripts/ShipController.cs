using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ShipController : MonoBehaviour
{
    [Header("Subsystems")]
    [SerializeField] private SailSystem sailSystem;
    [SerializeField] private HelmInteraction helm;

    [Header("Movement Settings")]
    [SerializeField] private float maxThrustForce = 60000f;
    [SerializeField] private float turnTorque = 40000f;
    [SerializeField] private float maxSpeed = 15f;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.useGravity = true;
        // Жестко фиксируем крен и тангаж, чтобы корабль никогда не переворачивался от ходьбы игрока
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rb.linearDamping = 1.5f;
        rb.angularDamping = 4f;

        if (sailSystem == null) sailSystem = GetComponent<SailSystem>();
        if (helm == null) helm = Object.FindAnyObjectByType<HelmInteraction>();
    }

    private void FixedUpdate()
    {
        ApplyPropulsion();
        ApplySteering();
    }

    private void ApplyPropulsion()
    {
        if (sailSystem == null) return;

        float deployRatio = sailSystem.DeployPercentage; // 0.0 до 1.0
        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);

        if (forwardSpeed < maxSpeed && deployRatio > 0.01f)
        {
            Vector3 thrust = transform.forward * (deployRatio * maxThrustForce);
            rb.AddForce(thrust, ForceMode.Force);
        }
    }

    private void ApplySteering()
    {
        if (helm == null) return;

        float rudderNormalized = helm.CurrentRudderNormalized; // от -1 до 1
        
        // Руль должен поворачивать корабль даже на минимальной скорости или при разгоне
        float speedFactor = Mathf.Clamp(rb.linearVelocity.magnitude / 2f, 0.2f, 1.0f);
        
        float torque = rudderNormalized * turnTorque * speedFactor;
        rb.AddTorque(Vector3.up * torque, ForceMode.Force);
    }
}
