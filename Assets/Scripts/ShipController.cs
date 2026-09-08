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
    [SerializeField] float pushImpulse = 450000f, pushLinearDrag = .55f, pushAngularDrag = .7f;
    [SerializeField] float impulseMass = 15000f, hullLength = 46f, hullWidth = 13f;
    Vector3 pushVelocity;
    float pushYawVelocity, freezeRemaining;
    public bool IsFrozen => freezeRemaining > 0f;
    public void Freeze(float duration)
    {
        freezeRemaining = Mathf.Max(freezeRemaining, duration);
        speed = 0f; pushVelocity = cannonShove = motionVelocity = motionAngularVelocity = Vector3.zero;
        pushYawVelocity = 0f; cannonTiltVelocity = Vector2.zero;
    }
    public void ApplyPushImpulse(Vector3 point, Vector3 direction)
    {
        if (IsFrozen || !float.IsFinite(point.sqrMagnitude) || !float.IsFinite(direction.sqrMagnitude)) return;
        Vector3 impulse = Vector3.ProjectOnPlane(direction, Vector3.up).normalized * pushImpulse;
        float mass = Mathf.Max(1f, impulseMass);
        pushVelocity += impulse / mass;
        Vector3 offset = point - rb.worldCenterOfMass;
        float inertia = mass * (hullLength * hullLength + hullWidth * hullWidth) / 12f;
        pushYawVelocity += Vector3.Cross(offset, impulse).y / Mathf.Max(1f, inertia) * Mathf.Rad2Deg;
    }
    [SerializeField] float buoyancyResponse = 2.5f, floatLength = 8f, floatWidth = 3f;
    float pitch, waveRoll;
    [SerializeField] float cannonRockStrength=20f, cannonRockSpring=9f, cannonRockDamping=3.5f;
    Vector2 cannonTilt, cannonTiltVelocity;
    Vector3 cannonShove, motionVelocity, motionAngularVelocity;
    public Vector3 CannonPointVelocity(Vector3 point) => motionVelocity+Vector3.Cross(motionAngularVelocity,point-transform.position);
    public void ApplyCannonImpulse(Vector3 point,Vector3 impulse,float strength)
    {
        if(IsFrozen || !float.IsFinite(impulse.sqrMagnitude) || !float.IsFinite(point.sqrMagnitude)) return;
        var local=Quaternion.Inverse(Quaternion.Euler(0,yaw,0))*Vector3.ClampMagnitude(impulse,1.5f);
        var offset=transform.InverseTransformPoint(point);
        float leverage=Mathf.Clamp(Mathf.Abs(offset.y)/5f,.65f,1.3f);
        cannonTiltVelocity += new Vector2(local.z,-local.x)*cannonRockStrength*strength*leverage;
        cannonTiltVelocity += new Vector2(offset.z/23f,-offset.x/6.5f)*(-local.y)*cannonRockStrength*.25f*strength;
        cannonTiltVelocity=Vector2.ClampMagnitude(cannonTiltVelocity,55f);
        cannonShove=Vector3.ClampMagnitude(cannonShove+Vector3.ProjectOnPlane(impulse,Vector3.up)*(.3f*strength),1.2f);
    }
    void SimulateCannonRock(float dt)
    {
        int steps=Mathf.Max(1,Mathf.CeilToInt(dt/.02f));float step=dt/steps;
        for(int i=0;i<steps;i++)
        {
            cannonTiltVelocity+=(-cannonTilt*cannonRockSpring-cannonTiltVelocity*cannonRockDamping)*step;
            cannonTilt=Vector2.ClampMagnitude(cannonTilt+cannonTiltVelocity*step,8f);
        }
        cannonShove*=Mathf.Exp(-2f*dt);
    }
    public bool Networked { get; set; }
    public float Speed => speed;
    public float MaxSpeed => maxSpeed;
    public float Bank => bank;
    public void Configure(HelmInteraction value) { helm = value; sailSystem = GetComponent<SailSystem>(); }
    void Awake()
    {
        rb = GetComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false; rb.constraints = RigidbodyConstraints.None;
        rb.interpolation = Networked ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate; rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        yaw = transform.eulerAngles.y; waterHeight = transform.position.y;
        if (sailSystem == null) sailSystem = GetComponent<SailSystem>();
        if (helm == null) helm = GetComponentInChildren<HelmInteraction>();
    }
    void FixedUpdate() { if (!Networked) Simulate(Time.fixedDeltaTime); }
    public void Simulate(float dt)
    {
        if (IsFrozen)
        {
            freezeRemaining = Mathf.Max(0f, freezeRemaining - dt);
            speed = 0f; motionVelocity = motionAngularVelocity = Vector3.zero;
            return;
        }
        SimulateCannonRock(dt);
        pushVelocity *= Mathf.Exp(-pushLinearDrag * dt);
        pushYawVelocity *= Mathf.Exp(-pushAngularDrag * dt);
        yaw += pushYawVelocity * dt;
        float target = (sailSystem != null ? sailSystem.DeployPercentage : 0) * maxSpeed;
        speed = Mathf.MoveTowards(speed, target, (target > speed ? acceleration : deceleration) * dt);
        float rudder = helm != null ? helm.CurrentRudderNormalized : 0;
        float factor = Mathf.Clamp01(speed / Mathf.Max(.1f, maxSpeed));
        yaw += rudder * turnSpeed * Mathf.Lerp(.15f, 1, factor) * dt;
        bank = Mathf.Lerp(bank, -rudder * maxBankAngle * factor, 1 - Mathf.Exp(-bankResponse * dt));
        var next = rb.position + Quaternion.Euler(0, yaw, 0) * Vector3.forward * speed * dt + (cannonShove + pushVelocity) * dt; next.y = waterHeight;
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
        var rotation = Quaternion.Euler(pitch + cannonTilt.x, yaw, bank + waveRoll + cannonTilt.y);
        var world = PirateSlop.World.ProceduralWorld.Instance;
        if (world != null && world.Ready && !world.CanSail(next, yaw))
        {
            float previousYaw = rb.rotation.eulerAngles.y;
            if (!world.CanSail(rb.position, yaw)) { yaw = previousYaw; rotation = Quaternion.Euler(pitch + cannonTilt.x, yaw, bank + waveRoll + cannonTilt.y); }
            next.x = rb.position.x; next.z = rb.position.z; speed = 0;
            pushVelocity = Vector3.zero; pushYawVelocity = 0f;
        }
        motionVelocity=(next-rb.position)/Mathf.Max(.001f,dt);
        var rotationDelta=rotation*Quaternion.Inverse(rb.rotation);
        rotationDelta.ToAngleAxis(out float angle,out Vector3 axis);
        if(angle>180f) angle-=360f;
        motionAngularVelocity=float.IsFinite(axis.sqrMagnitude) ? axis*(angle*Mathf.Deg2Rad/Mathf.Max(.001f,dt)) : Vector3.zero;
        if (Networked) { rb.position = next; rb.rotation = rotation; transform.SetPositionAndRotation(next, rotation); }
        else { rb.MoveRotation(rotation); rb.MovePosition(next); }
    }
    public ShipState Capture() => new ShipState { Position = rb.position, Yaw = yaw, Speed = speed, Bank = bank, Pitch = pitch + cannonTilt.x, WaveRoll = waveRoll + cannonTilt.y, WaveTime = OceanSurface.Instance != null ? OceanSurface.Instance.WaveTime : Time.time, Sail = sailSystem.DeployPercentage, Rudder = helm.CurrentRudderNormalized, Controlling = helm.IsControlling };
    public void Restore(ShipState s, AdvancedPlayerController driver)
    {
        cannonTilt=Vector2.zero; cannonTiltVelocity=Vector2.zero;
        speed = s.Speed; yaw = s.Yaw; bank = s.Bank; pitch = s.Pitch; waveRoll = s.WaveRoll;
        rb.position = s.Position; rb.rotation = Quaternion.Euler(pitch + cannonTilt.x, yaw, bank + waveRoll + cannonTilt.y); transform.SetPositionAndRotation(rb.position, rb.rotation);
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
        if (IsFrozen) return;
        if (displacement.sqrMagnitude > .00001f)
        {
            Vector3 normal = displacement.normalized;
            float inward = Vector3.Dot(pushVelocity, normal);
            if (inward < 0f) pushVelocity -= normal * inward;
        }
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
