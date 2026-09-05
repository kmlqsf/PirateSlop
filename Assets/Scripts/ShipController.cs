using UnityEngine;
using PirateSlop;
[RequireComponent(typeof(Rigidbody))]
public class ShipController : MonoBehaviour
{
    [SerializeField] SailSystem sailSystem;
    [SerializeField] HelmInteraction helm;
    [SerializeField] float maxSpeed = 10f, acceleration = 2f, deceleration = 3f;
    [SerializeField] float turnSpeed = 22f, maxBankAngle = 5f, bankResponse = 2f;
    Rigidbody rb;
    float speed, yaw, bank, waterHeight;
    public bool Networked { get; set; }
    public float Speed => speed;
    public float Bank => bank;
    public void Configure(HelmInteraction value) { helm = value; sailSystem = GetComponent<SailSystem>(); }
    void Awake()
    {
        rb = GetComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false; rb.constraints = RigidbodyConstraints.None;
        rb.interpolation = RigidbodyInterpolation.Interpolate; rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        yaw = transform.eulerAngles.y; waterHeight = transform.position.y;
        if (sailSystem == null) sailSystem = GetComponent<SailSystem>();
        if (helm == null) helm = GetComponentInChildren<HelmInteraction>();
    }
    void FixedUpdate() { if (!Networked) Simulate(Time.fixedDeltaTime); }
    public void Simulate(float dt)
    {
        float target = (sailSystem != null ? sailSystem.DeployPercentage : 0) * maxSpeed;
        speed = Mathf.MoveTowards(speed, target, (target > speed ? acceleration : deceleration) * dt);
        float rudder = helm != null ? helm.CurrentRudderNormalized : 0;
        float factor = Mathf.Clamp01(speed / Mathf.Max(.1f, maxSpeed));
        yaw += rudder * turnSpeed * Mathf.Lerp(.15f, 1, factor) * dt;
        bank = Mathf.Lerp(bank, -rudder * maxBankAngle * factor, 1 - Mathf.Exp(-bankResponse * dt));
        var rotation = Quaternion.Euler(0, yaw, bank);
        var next = rb.position + Quaternion.Euler(0, yaw, 0) * Vector3.forward * speed * dt; next.y = waterHeight;
        if (Networked) { rb.interpolation = RigidbodyInterpolation.None; rb.position = next; rb.rotation = rotation; transform.SetPositionAndRotation(next, rotation); }
        else { rb.MoveRotation(rotation); rb.MovePosition(next); }
    }
    public ShipState Capture() => new ShipState { Position = rb.position, Yaw = yaw, Speed = speed, Bank = bank, Sail = sailSystem.DeployPercentage, Rudder = helm.CurrentRudderNormalized, Controlling = helm.IsControlling };
    public void Restore(ShipState s, AdvancedPlayerController driver)
    {
        speed = s.Speed; yaw = s.Yaw; bank = s.Bank; waterHeight = s.Position.y;
        rb.position = s.Position; rb.rotation = Quaternion.Euler(0, yaw, bank); transform.SetPositionAndRotation(rb.position, rb.rotation);
        sailSystem.SetDeploy(s.Sail); helm.Restore(s.Rudder, s.Controlling, driver);
    }
}
