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
    float nextCollisionAudio;
    [SerializeField] float buoyancyResponse = 2.5f, floatLength = 8f, floatWidth = 3f;
    float pitch, waveRoll;
    public bool Networked { get; set; }
    public float Speed => speed;
    public float MaxSpeed => maxSpeed;
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
        var next = rb.position + Quaternion.Euler(0, yaw, 0) * Vector3.forward * speed * dt; next.y = waterHeight;
        var ocean = OceanSurface.Instance;
        if (ocean != null)
        {
            var forward = Quaternion.Euler(0, yaw, 0) * Vector3.forward * floatLength;
            var right = Quaternion.Euler(0, yaw, 0) * Vector3.right * floatWidth;
            float bow = ocean.Height(next + forward), stern = ocean.Height(next - forward);
            float starboard = ocean.Height(next + right), port = ocean.Height(next - right);
            float blend = 1f - Mathf.Exp(-buoyancyResponse * dt);
            next.y = Mathf.Lerp(rb.position.y, waterHeight + (bow + stern + starboard + port) * .25f - ocean.SeaLevel, blend);
            pitch = Mathf.Lerp(pitch, Mathf.Clamp(-Mathf.Atan2(bow - stern, floatLength * 2) * Mathf.Rad2Deg, -12, 12), blend);
            waveRoll = Mathf.Lerp(waveRoll, Mathf.Clamp(Mathf.Atan2(starboard - port, floatWidth * 2) * Mathf.Rad2Deg, -15, 15), blend);
        }
        var rotation = Quaternion.Euler(pitch, yaw, bank + waveRoll);
        if (Networked) { rb.position = next; rb.rotation = rotation; transform.SetPositionAndRotation(next, rotation); }
        else { rb.MoveRotation(rotation); rb.MovePosition(next); }
    }
    public ShipState Capture() => new ShipState { Position = rb.position, Yaw = yaw, Speed = speed, Bank = bank, Pitch = pitch, WaveRoll = waveRoll, WaveTime = OceanSurface.Instance != null ? OceanSurface.Instance.WaveTime : Time.time, Sail = sailSystem.DeployPercentage, Rudder = helm.CurrentRudderNormalized, Controlling = helm.IsControlling };
    public void Restore(ShipState s, AdvancedPlayerController driver)
    {
        speed = s.Speed; yaw = s.Yaw; bank = s.Bank; pitch = s.Pitch; waveRoll = s.WaveRoll;
        rb.position = s.Position; rb.rotation = Quaternion.Euler(pitch, yaw, bank + waveRoll); transform.SetPositionAndRotation(rb.position, rb.rotation);
        sailSystem.SetDeploy(s.Sail); helm.Restore(s.Rudder, s.Controlling, driver);
    }
    public void ApplyRemoteState(ShipState s, float blend, AdvancedPlayerController driver = null)
    {
        speed = Mathf.Lerp(speed, s.Speed, blend); yaw = Mathf.LerpAngle(yaw, s.Yaw, blend); bank = Mathf.Lerp(bank, s.Bank, blend);
        pitch = Mathf.Lerp(pitch, s.Pitch, blend); waveRoll = Mathf.Lerp(waveRoll, s.WaveRoll, blend);
        var position = Vector3.Lerp(rb.position, s.Position, blend);
        var rotation = Quaternion.Slerp(rb.rotation, Quaternion.Euler(s.Pitch, s.Yaw, s.Bank + s.WaveRoll), blend);
        rb.position = position; rb.rotation = rotation;
        transform.SetPositionAndRotation(position, rotation);
        sailSystem.SetDeploy(s.Sail); helm.Restore(s.Rudder, s.Controlling, driver);
    }
    public void ResolveCollision(Vector3 displacement)
    {
        if (Time.time >= nextCollisionAudio && displacement.sqrMagnitude > .00001f)
        {
            var network = GetComponent<PirateSlop.Networking.NetworkShip>();
            if (network != null) network.CollisionAudio();
            else GameAudio.Play(SoundCue.ShipCollision, transform.position);
            nextCollisionAudio = Time.time + 2f;
        }
        var position = rb.position + displacement; position.y = rb.position.y;
        rb.position = position;
        transform.position = position;
        speed *= .35f;
    }
}
