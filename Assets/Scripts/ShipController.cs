using UnityEngine;
using PirateSlop;
using PirateSlop.World;
[RequireComponent(typeof(Rigidbody))]
public class ShipController : MonoBehaviour
{
    [SerializeField] SailSystem sailSystem;
    [SerializeField] HelmInteraction helm;
    [SerializeField] float maxSpeed = 10f, acceleration = 2f, deceleration = 3f;
    [SerializeField] float turnSpeed = 22f, maxBankAngle = 5f, bankResponse = 2f;
    Rigidbody rb;
    float speed, yaw, bank, waterHeight;
    float nextCollisionAudio, lastCollisionContact = -10f;
    BoxCollider hullMotionProbe;
    Vector3 hullMotionCenter, hullMotionExtents;
    Collider[] hullOverlapHits = new Collider[512];
    RaycastHit[] hullCastHits = new RaycastHit[512];
    readonly System.Collections.Generic.List<Rigidbody> poseBodies = new(64);
    bool hullContact;
    [SerializeField] float pushImpulse = 450000f, pushLinearDrag = .55f, pushAngularDrag = .7f;
    [SerializeField] float impulseMass = 15000f, hullLength = 46f, hullWidth = 13f;
    public Vector2 HullFootprint => new Vector2(hullWidth, hullLength);
    Vector3 pushVelocity;
    public Vector3 PushVelocity => pushVelocity;
    float pushYawVelocity, freezeRemaining, manualPushUntil;
    public bool IsFrozen => freezeRemaining > 0f;
    float floodLevel, fullWaterline, fullBowPitch;
    public float InitialDraft;
    public bool IsFlooded => floodLevel >= 1f;
    public static readonly System.Collections.Generic.List<ShipController> ActiveControllers = new();
    void OnEnable() { ActiveControllers.Add(this); }
    void OnDisable() { ActiveControllers.Remove(this); PirateSlop.Ships.ShipHullQueryWorld.Release(this); }
    void OnDestroy() { PirateSlop.Ships.ShipHullQueryWorld.Release(this); }
    public void SetFlooding(float level, float waterline, float bowPitch)
    {
        floodLevel = Mathf.Clamp01(level);
        fullWaterline = waterline;
        fullBowPitch = bowPitch;
    }
    public void ApplyManualPush(Vector3 direction)
    {
        if(IsFrozen || !float.IsFinite(direction.sqrMagnitude)) return;
        direction=Vector3.ProjectOnPlane(direction,Vector3.up).normalized;
        pushVelocity=Vector3.ClampMagnitude(pushVelocity+direction*.65f,1.8f);
        manualPushUntil=Time.time+1.1f;
        speed=Mathf.Min(speed,.35f);
    }
    public void Freeze(float duration)
    {
        freezeRemaining = Mathf.Max(freezeRemaining, duration);
        speed = 0f; pushVelocity = cannonShove = motionVelocity = motionAngularVelocity = Vector3.zero;
        pushYawVelocity = 0f; cannonTiltVelocity = Vector2.zero;
        buoyantVelocity = buoyantTargetVelocity = Vector3.zero; buoyantResponseReady = false;
    }
    public void Thaw() => freezeRemaining = 0f;
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
    public float ImpulseMass => impulseMass;
    public float SailDeploy => sailSystem != null ? sailSystem.EffectiveDeploy : 0f;
    float towingSpeedLimit = -1f;
    public void SetTowingSpeedLimit(float limit)
    {
        if (limit < 0f) return;
        towingSpeedLimit = towingSpeedLimit < 0f ? limit : Mathf.Min(towingSpeedLimit, limit);
    }
    public void ApplyTowing(Vector3 point, Vector3 pullForce, float speedCap = -1f)
    {
        if (IsFrozen || isAnchored || !float.IsFinite(point.sqrMagnitude) || !float.IsFinite(pullForce.sqrMagnitude)) return;
        Vector3 flatPull = Vector3.ProjectOnPlane(pullForce, Vector3.up);
        float dt = Time.fixedDeltaTime;
        float mass = Mathf.Max(1f, impulseMass);
        pushVelocity += (flatPull / mass) * dt;
        Vector3 offset = point - rb.worldCenterOfMass;
        float inertia = mass * (hullLength * hullLength + hullWidth * hullWidth) / 12f;
        pushYawVelocity += (Vector3.Cross(offset, flatPull).y / Mathf.Max(1f, inertia) * Mathf.Rad2Deg) * dt;
        pushYawVelocity = Mathf.Clamp(pushYawVelocity, -45f, 45f);

        if (speedCap >= 0f && !VortexBoosted)
        {
            SetTowingSpeedLimit(speedCap);
            if (speed > speedCap)
            {
                speed = Mathf.MoveTowards(speed, speedCap, deceleration * dt);
            }
        }
    }
    [SerializeField] float buoyancyResponse = 2.5f, floatLength = 8f, floatWidth = 3f;
    float pitch, waveRoll;
    Vector3 buoyantVelocity, previousBuoyantTarget, buoyantTargetVelocity;
    bool buoyantResponseReady;
    ShipBuoyancy buoyancy;
    OceanSurface buoyancyOcean;
    System.Func<Vector3, float> sampleWaterHeight;
    [SerializeField] float cannonRockStrength=20f, cannonRockSpring=9f, cannonRockDamping=3.5f;
    Vector2 cannonTilt, cannonTiltVelocity;
    Vector3 cannonShove, motionVelocity, motionAngularVelocity;
    public Vector3 CannonPointVelocity(Vector3 point) => motionVelocity+Vector3.Cross(motionAngularVelocity,point-transform.position);
    public Vector3 MotionAngularVelocity => motionAngularVelocity;
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
        [SerializeField] bool isAnchored;
    Vector3 anchorPoint;
    float anchorCableLength = 6f;
    public bool IsAnchored => isAnchored;
    public Vector3 AnchorPoint => anchorPoint;
    public void SetAnchored(bool value, Vector3? dropPoint = null)
    {
        if (isAnchored == value) return;
        isAnchored = value;
        if (isAnchored)
        {
            anchorPoint = dropPoint ?? (transform.position + transform.forward * 5f);
            anchorCableLength = Mathf.Max(3f, Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), new Vector3(anchorPoint.x, 0, anchorPoint.z)));
            float initialSpeed = speed;
            if (!VortexBoosted) speed = 0f;
            pushVelocity = Vector3.zero;
            if (!VortexBoosted && Mathf.Abs(initialSpeed) > .5f)
            {
                float rudder = helm != null ? helm.CurrentRudderNormalized : 0f;
                float swingDir = Mathf.Abs(rudder) > .1f ? Mathf.Sign(rudder) : 1f;
                pushYawVelocity += swingDir * initialSpeed * 4f;
            }
        }
    }
    public float Speed => speed;
    float destructionSpeed = 1f, destructionAcceleration = 1f, destructionRudder = 1f, destructionHeel;
    public void SetDestructionModifiers(float speedFactor, float accelerationFactor, float rudderFactor, float heel)
    {
        destructionSpeed = Mathf.Clamp01(speedFactor); destructionAcceleration = Mathf.Clamp01(accelerationFactor);
        destructionRudder = Mathf.Clamp01(rudderFactor); destructionHeel = heel;
    }
    PirateSlop.Networking.NetworkShip networkShip;
    public float DeveloperSpeedMultiplier => networkShip != null ? Mathf.Clamp(networkShip.DeveloperSpeedMultiplier.Value, 1f, 5f) : 1f;
    public bool VortexBoosted => networkShip != null && networkShip.VortexBoostActive.Value;
    public float SpeedMultiplier => Mathf.Max(DeveloperSpeedMultiplier, VortexBoosted ? PirateSlop.Networking.NetworkShip.VortexSpeedMultiplier : 1f);
    public float BaseMaxSpeed => maxSpeed;
    public float MaxSpeed => maxSpeed * SpeedMultiplier;
    public void FinishVortexBoost()
    {
        speed = isAnchored ? 0f : Mathf.Min(speed, MaxSpeed * destructionSpeed * Mathf.Lerp(1f, .1f, floodLevel));
        if (towingSpeedLimit >= 0f) speed = Mathf.Min(speed, towingSpeedLimit);
    }
    public float Bank => bank;
    public void Configure(HelmInteraction value) { helm = value; sailSystem = GetComponent<SailSystem>(); }
    void Awake()
    {
        networkShip = GetComponent<PirateSlop.Networking.NetworkShip>();
        rb = GetComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false; rb.constraints = RigidbodyConstraints.None;
        rb.interpolation = Networked ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate; rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        yaw = transform.eulerAngles.y;
        waterHeight = InitialDraft > 0f ? OceanSurface.Instance != null ? OceanSurface.Instance.SeaLevel : 0f : transform.position.y;
        buoyancy = new ShipBuoyancy(floatLength, floatWidth, HullFootprint);
        if (sailSystem == null) sailSystem = GetComponent<SailSystem>();
        if (helm == null) helm = GetComponentInChildren<HelmInteraction>();
        var probe = new GameObject("HullMotionProbe");
        probe.transform.SetParent(transform, false);
        hullMotionProbe = probe.AddComponent<BoxCollider>();
        hullMotionProbe.enabled = false;
        hullMotionProbe.isTrigger = true;
        var flooding = GetComponent<ShipFlooding>();
        float bottom = flooding != null ? flooding.EmptyHeight : -1f;
        float top = flooding != null ? flooding.FullHeight + .5f : 4f;
        var footprint = networkShip != null ? networkShip.HullHalfExtents : HullFootprint * .5f;
        hullMotionCenter = new Vector3(0, (bottom + top) * .5f, 0);
        hullMotionExtents = new Vector3(footprint.x, (top - bottom) * .5f, footprint.y);
        hullMotionProbe.size = hullMotionExtents * 2f;
    }
    float SampleWaterForBuoyancy(Vector3 position)
    {
        float height = buoyancyOcean.Height(position);
        var foam = PirateSlop.WaterShipFoam.Instance;
        return foam != null && buoyancyOcean.HeightSource is PirateSlop.BoatAttackOcean ? height - foam.BowHeight(position) + foam.BowHeight(position, this) : height;
    }
    void FixedUpdate() { if (!Networked) Simulate(Time.fixedDeltaTime); }
    public void Simulate(float dt)
    {
        bool immobilized = IsFrozen;
        bool vortexBoost = VortexBoosted;
        bool anchorHolds = isAnchored && !vortexBoost;
        if (IsFrozen)
        {
            freezeRemaining = Mathf.Max(0f, freezeRemaining - dt);
            speed = 0f; motionVelocity = motionAngularVelocity = Vector3.zero;
            if (floodLevel <= 0f) return;
        }
        SimulateCannonRock(dt);
        pushVelocity *= Mathf.Exp(-pushLinearDrag * dt);
        pushYawVelocity *= Mathf.Exp(-pushAngularDrag * dt);
        yaw += pushYawVelocity * dt;
        float mobility = Mathf.Lerp(1f, .1f, floodLevel);
        float target = vortexBoost ? maxSpeed * PirateSlop.Networking.NetworkShip.VortexSpeedMultiplier : (sailSystem != null ? sailSystem.EffectiveDeploy : 0) * MaxSpeed * destructionSpeed * mobility;
        if (immobilized || anchorHolds) target = 0f;
        if (!vortexBoost && towingSpeedLimit >= 0f) target = Mathf.Min(target, towingSpeedLimit);
        towingSpeedLimit = -1f;
        float speedResponse = vortexBoost ? maxSpeed * PirateSlop.Networking.NetworkShip.VortexSpeedMultiplier / PirateSlop.Networking.NetworkShip.VortexBoostRampSeconds : (target > speed ? acceleration * destructionAcceleration * mobility : deceleration) * SpeedMultiplier;
        speed = (immobilized || anchorHolds) ? 0f : Mathf.MoveTowards(speed, target, speedResponse * dt);
        float rudder = !immobilized && helm != null ? helm.CurrentRudderNormalized * mobility : 0;
        float factor = Mathf.Clamp01(speed / Mathf.Max(.1f, MaxSpeed * mobility));
        float yawDelta = (anchorHolds ? rudder * turnSpeed * .35f : rudder * turnSpeed * destructionRudder * Mathf.Lerp(.15f, 1, factor)) * dt;
        yaw += yawDelta;
        bank = Mathf.Lerp(bank, -rudder * maxBankAngle * factor + destructionHeel, 1 - Mathf.Exp(-bankResponse * dt));
        var next = rb.position + Quaternion.Euler(0, yaw, 0) * Vector3.forward * speed * dt + (cannonShove + pushVelocity) * dt; next.y = waterHeight;
        if (OceanSurface.Instance != null && OceanSurface.Instance.WhirlpoolDepth > 0f && !anchorHolds && !immobilized)
        {
            float w_R = OceanSurface.Instance.WhirlpoolRadius;
            Vector2 shipPosXZ = new Vector2(next.x, next.z);
            Vector2 centerXZ = new Vector2(OceanSurface.Instance.WhirlpoolCenter.x, OceanSurface.Instance.WhirlpoolCenter.z);
            float w_dist = Vector2.Distance(shipPosXZ, centerXZ);
            if (w_dist < w_R && w_dist > 1f)
            {
                float w_t = Mathf.Clamp01(w_dist / w_R);
                  float tangentSpeed = (1f - w_t) * 15f; 
                  float inwardSpeed = (1f - w_t) * 6f;   
                  
                  Vector2 dirToCenter = (centerXZ - shipPosXZ).normalized;
                  Vector2 tangent = new Vector2(-dirToCenter.y, dirToCenter.x);
                  
                  Vector2 velocityAdd = dirToCenter * inwardSpeed + tangent * tangentSpeed;
                  next.x += velocityAdd.x * dt;
                  next.z += velocityAdd.y * dt;
                  
                  float targetYaw = Mathf.Atan2(tangent.x, tangent.y) * Mathf.Rad2Deg;
                  yaw = Mathf.MoveTowardsAngle(yaw, targetYaw, (1f - w_t) * 10f * dt);
            }
        }
        if (anchorHolds)
        {
            Vector3 anchorFlat = new Vector3(anchorPoint.x, 0, anchorPoint.z);
            Vector3 shipFlat = new Vector3(next.x, 0, next.z);
            Vector3 arm = shipFlat - anchorFlat;
            float totalYawDelta = (pushYawVelocity * dt) + yawDelta;
            if (Mathf.Abs(totalYawDelta) > .0001f && arm.sqrMagnitude > .01f)
            {
                arm = Quaternion.Euler(0, totalYawDelta, 0) * arm;
                shipFlat = anchorFlat + arm;
            }
            if (arm.magnitude > anchorCableLength)
            {
                shipFlat = anchorFlat + arm.normalized * anchorCableLength;
                pushVelocity = Vector3.ProjectOnPlane(pushVelocity, arm.normalized);
            }
            next.x = shipFlat.x;
            next.z = shipFlat.z;
        }
        var ocean = OceanSurface.Instance;
        if (ocean != null)
        {
            using var buoyancyTiming = PirateSlop.Performance.PerformanceCpuTimings.Measure(PirateSlop.Performance.PerformanceCpuSection.ShipBuoyancy);
            if (buoyancyOcean != ocean)
            {
                buoyancyOcean = ocean;
                sampleWaterHeight = SampleWaterForBuoyancy;
                buoyantResponseReady = false;
            }
            var support = buoyancy.Evaluate(next, yaw, sampleWaterHeight);
            float blend = 1f - Mathf.Exp(-buoyancyResponse * dt);
            float draftHeight = Mathf.Lerp(waterHeight - InitialDraft, ocean.SeaLevel - fullWaterline, floodLevel);
            var buoyantTarget = new Vector3(draftHeight + support.x - ocean.SeaLevel,
                Mathf.Clamp(support.y, -12, 12) + fullBowPitch * floodLevel, Mathf.Clamp(support.z, -15, 15));
            if (EnvironmentTestGallery.IsTest(ProceduralWorld.Instance != null ? ProceduralWorld.Instance.Layout : null))
            {
                buoyantTarget.x += buoyancy.ExposedHullLift(new Vector3(next.x, rb.position.y, next.z),
                    Quaternion.Euler(pitch, yaw, bank + waveRoll));
                if (!buoyantResponseReady) { previousBuoyantTarget = buoyantTarget; buoyantResponseReady = true; }
                var targetVelocity = (buoyantTarget - previousBuoyantTarget) / Mathf.Max(.001f, dt);
                targetVelocity.x = Mathf.Clamp(targetVelocity.x, -20, 20);
                targetVelocity.y = Mathf.Clamp(targetVelocity.y, -45, 45);
                targetVelocity.z = Mathf.Clamp(targetVelocity.z, -45, 45);
                buoyantTargetVelocity = Vector3.Lerp(buoyantTargetVelocity, targetVelocity, 1 - Mathf.Exp(-8 * dt));
                previousBuoyantTarget = buoyantTarget;
                var state = new Vector3(rb.position.y, pitch, waveRoll);
                float frequency = Mathf.Clamp(buoyancyResponse * 2, 3, 10);
                int steps = Mathf.Max(1, Mathf.CeilToInt(dt / .01f));
                float step = dt / steps;
                for (int i = 0; i < steps; i++)
                {
                    var force = (buoyantTarget - state) * (frequency * frequency)
                        + (buoyantTargetVelocity - buoyantVelocity) * (1.7f * frequency);
                    force.x = Mathf.Clamp(force.x, -40, 40);
                    buoyantVelocity += force * step;
                    state += buoyantVelocity * step;
                }
                next.y = state.x; pitch = state.y; waveRoll = state.z;
            }
            else
            {
                next.y = Mathf.Lerp(rb.position.y, buoyantTarget.x, blend);
                pitch = Mathf.Lerp(pitch, buoyantTarget.y, blend);
                waveRoll = Mathf.Lerp(waveRoll, buoyantTarget.z, blend);
            }
        }
        var rotation = Quaternion.Euler(pitch + cannonTilt.x, yaw, bank + waveRoll + cannonTilt.y);
        PirateSlop.Networking.NetworkCannon.ConstrainBoarding(this, ref next, rotation);
        PirateSlop.Harpoon.HarpoonProjectile.ConstrainHarpoons(this, ref next, rotation);
        var world = PirateSlop.World.ProceduralWorld.Instance;
        using (PirateSlop.Performance.PerformanceCpuTimings.Measure(PirateSlop.Performance.PerformanceCpuSection.ShipWorldChecks))
        if (world != null && world.Ready && !world.CanSail(next, yaw))
        {
            bool canEscape=Time.time<manualPushUntil && world.SailingObstruction(next,yaw)<world.SailingObstruction(rb.position,yaw)-.001f;
            if(!canEscape)
            {
                float previousYaw = rb.rotation.eulerAngles.y;
                if (!world.CanSail(rb.position, yaw)) { yaw = previousYaw; rotation = Quaternion.Euler(pitch + cannonTilt.x, yaw, bank + waveRoll + cannonTilt.y); }
                next.x = rb.position.x; next.z = rb.position.z; speed = 0;
                pushVelocity = Vector3.zero; pushYawVelocity = 0f;
            }
        }
        GetComponent<PirateSlop.Networking.NetworkSternCargoEvent>()?.ConstrainMotion(ref next);
        ConstrainHullMotion(ref next, ref rotation);
        if (vortexBoost && isAnchored) anchorPoint += next - rb.position;
        motionVelocity=(next-rb.position)/Mathf.Max(.001f,dt);
        var rotationDelta=rotation*Quaternion.Inverse(rb.rotation);
        rotationDelta.ToAngleAxis(out float angle,out Vector3 axis);
        if(angle>180f) angle-=360f;
        motionAngularVelocity=float.IsFinite(axis.sqrMagnitude) ? axis*(angle*Mathf.Deg2Rad/Mathf.Max(.001f,dt)) : Vector3.zero;
        using var transformTiming = PirateSlop.Performance.PerformanceCpuTimings.Measure(PirateSlop.Performance.PerformanceCpuSection.ShipTransformWrite);
        if (Networked) PirateSlop.Ships.ShipBodyPose.Apply(rb, next, rotation, poseBodies, hullContact);
        else { rb.MoveRotation(rotation); rb.MovePosition(next); transform.SetPositionAndRotation(next, rotation); }
    }
    public static bool HullSweepFastPathEnabled { get; set; } = true;
    double nextHullQueryWarning;
    static Bounds HullSweepBounds(Vector3 start, Vector3 destination, Vector3 localCenter, Vector3 halfExtents, Quaternion initial, Quaternion final)
    {
        var delta = destination - start;
        var localDelta = Quaternion.Inverse(initial) * delta;
        float radius = localCenter.magnitude + halfExtents.magnitude;
        var change = Quaternion.Inverse(initial) * final;
        float vectorSquared = change.x * change.x + change.y * change.y + change.z * change.z;
        float magnitude = Mathf.Sqrt(vectorSquared + change.w * change.w);
        float sineHalf = magnitude > .000001f ? Mathf.Sqrt(vectorSquared) / magnitude : 1f;
        float rotationMargin = 2f * radius * Mathf.Min(1f, sineHalf) + .05f;
        var size = halfExtents * 2f + new Vector3(Mathf.Abs(localDelta.x), Mathf.Abs(localDelta.y), Mathf.Abs(localDelta.z)) + Vector3.one * (rotationMargin * 2f);
        return new Bounds((start + destination) * .5f + initial * localCenter, size);
    }

    int HullSweepCandidates(Vector3 start, Vector3 destination, Quaternion initial, Quaternion final)
    {
        using var timing = PirateSlop.Performance.PerformanceCpuTimings.Measure(PirateSlop.Performance.PerformanceCpuSection.ShipHullBroadphase);
        var bounds = HullSweepBounds(start, destination, hullMotionCenter, hullMotionExtents, initial, final);
        int count;
        while (true)
        {
            count = Physics.OverlapBoxNonAlloc(bounds.center, bounds.extents, hullOverlapHits, initial, ~0, QueryTriggerInteraction.Ignore);
            if (count < hullOverlapHits.Length) break;
            if (hullOverlapHits.Length >= 4096)
            {
                if (PirateSlop.Performance.PerformanceCapture.Active != null && Time.realtimeSinceStartupAsDouble >= nextHullQueryWarning)
                {
                    nextHullQueryWarning = Time.realtimeSinceStartupAsDouble + 1;
                    PirateSlop.Performance.PerformanceCapture.Event("HullQueryFallback:broadphase-capacity", this, start, count);
                }
                return -1;
            }
            System.Array.Resize(ref hullOverlapHits, hullOverlapHits.Length * 2);
        }
        int blockers = 0;
        for (int i = 0; i < count; i++) if (HullBlocker(hullOverlapHits[i])) hullOverlapHits[blockers++] = hullOverlapHits[i];
        return blockers;
    }

    bool HullBlocker(Collider collider)
    {
        if (collider == null || !collider.enabled || collider.isTrigger || collider.attachedRigidbody == rb || collider.transform.IsChildOf(transform)) return false;
        if (collider.GetComponentInParent<AdvancedPlayerController>() != null || collider.GetComponentInParent<PirateSlop.Networking.NetworkLootChest>() != null || collider.GetComponentInParent<PirateSlop.Networking.NetworkFish>() != null) return false;
        if (collider.GetComponentInParent<PirateSlop.Networking.NetworkTowedCargo>() != null) return false;
        return collider.attachedRigidbody == null || collider.attachedRigidbody.isKinematic;
    }
    int HullOverlaps(Vector3 position, Quaternion rotation)
    {
        using var timing = PirateSlop.Performance.PerformanceCpuTimings.Measure(PirateSlop.Performance.PerformanceCpuSection.ShipHullOverlaps);
        int count;
        while (true)
        {
            count = Physics.OverlapBoxNonAlloc(position + rotation * hullMotionCenter, hullMotionExtents, hullOverlapHits, rotation, ~0, QueryTriggerInteraction.Ignore);
            if (count < hullOverlapHits.Length || hullOverlapHits.Length >= 4096) return count;
            System.Array.Resize(ref hullOverlapHits, hullOverlapHits.Length * 2);
        }
    }
    bool HullPenetration(Vector3 position, Quaternion rotation, Collider obstacle, out Vector3 direction, out float depth)
    {
        direction = Vector3.zero; depth = 0;
        return HullBlocker(obstacle) && Physics.ComputePenetration(hullMotionProbe, position + rotation * hullMotionCenter, rotation, obstacle, obstacle.transform.position, obstacle.transform.rotation, out direction, out depth) && depth > .001f;
    }
    void ConstrainHullMotion(ref Vector3 next, ref Quaternion rotation)
    {
        using var timing = PirateSlop.Performance.PerformanceCpuTimings.Measure(PirateSlop.Performance.PerformanceCpuSection.ShipHullMotion);
        using (PirateSlop.Performance.PerformanceCpuTimings.Measure(PirateSlop.Performance.PerformanceCpuSection.ShipPhysicsSync)) Physics.SyncTransforms();
        hullContact = false;
        if (HullSweepFastPathEnabled && HullSweepCandidates(rb.position, next, rb.rotation, rotation) == 0) return;
        hullContact = true;
        var start = rb.position;
        var startRotation = rb.rotation;
        var recovery = Vector3.zero;
        Vector3 normal = Vector3.zero, contact = start;
        bool collided = false;
        for (int pass = 0; pass < 4; pass++)
        {
            bool moved = false;
            int count = HullOverlaps(start, startRotation);
            for (int i = 0; i < count; i++)
            {
                var obstacle = hullOverlapHits[i];
                if (!HullPenetration(start, startRotation, obstacle, out var direction, out float depth)) continue;
                var horizontal = Vector3.ProjectOnPlane(direction, Vector3.up);
                float length = horizontal.magnitude;
                if (length < .1f) continue;
                normal = horizontal / length;
                var correction = normal * (depth / length + .03f);
                start += correction; recovery += correction;
                contact = obstacle.ClosestPoint(start + startRotation * hullMotionCenter);
                collided = moved = true;
            }
            if (!moved) break;
        }
        var destination = next + recovery;
        bool queryWorld = false;
        if (HullSweepFastPathEnabled)
        {
            int candidates = HullSweepCandidates(start, destination, startRotation, rotation);
            if (candidates >= 0) queryWorld = PirateSlop.Ships.ShipHullQueryWorld.Prepare(this, hullOverlapHits, candidates);
        }
        var accepted = start;
        var acceptedRotation = startRotation;
        int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(Vector3.Distance(start, destination) / .75f, Quaternion.Angle(startRotation, rotation) / 2f)));
        for (int step = 1; step <= steps; step++)
        {
            var candidate = Vector3.Lerp(start, destination, step / (float)steps);
            var candidateRotation = Quaternion.Slerp(startRotation, rotation, step / (float)steps);
            var center = accepted + acceptedRotation * hullMotionCenter;
            var movement = candidate + acceptedRotation * hullMotionCenter - center;
            float distance = movement.magnitude, allowed = distance;
            bool blocked = false;
            if (distance > .0001f)
            {
                int count;
                while (true)
                {
                    using (PirateSlop.Performance.PerformanceCpuTimings.Measure(PirateSlop.Performance.PerformanceCpuSection.ShipHullCasts))
                        count = queryWorld ? PirateSlop.Ships.ShipHullQueryWorld.Cast(center, hullMotionExtents, movement / distance, hullCastHits, acceptedRotation, distance + .03f) : Physics.BoxCastNonAlloc(center, hullMotionExtents, movement / distance, hullCastHits, acceptedRotation, distance + .03f, ~0, QueryTriggerInteraction.Ignore);
                    if (count < hullCastHits.Length || hullCastHits.Length >= 4096) break;
                    System.Array.Resize(ref hullCastHits, hullCastHits.Length * 2);
                }
                for (int i = 0; i < count; i++)
                {
                    var hit = hullCastHits[i];
                    var obstacle = queryWorld ? PirateSlop.Ships.ShipHullQueryWorld.Source(hit.collider) : hit.collider;
                    if (!HullBlocker(obstacle) || Vector3.Dot(movement, hit.normal) >= -.0001f || hit.distance > allowed + .03f) continue;
                    allowed = Mathf.Max(0, hit.distance - .03f);
                    normal = hit.normal; contact = hit.point; blocked = true;
                }
                if (blocked)
                {
                    accepted += movement * Mathf.Clamp01(allowed / distance);
                    collided = true;
                    break;
                }
            }
            int overlaps = HullOverlaps(candidate, candidateRotation);
            for (int i = 0; i < overlaps; i++)
            {
                var obstacle = hullOverlapHits[i];
                if (!HullPenetration(candidate, candidateRotation, obstacle, out var direction, out _)) continue;
                normal = direction; contact = obstacle.ClosestPoint(candidate + candidateRotation * hullMotionCenter);
                blocked = true; break;
            }
            if (blocked) { collided = true; break; }
            accepted = candidate; acceptedRotation = candidateRotation;
        }
        next = accepted; rotation = acceptedRotation;
        if (!collided) return;
        RegisterCollision(normal * .03f, contact, true, Mathf.Clamp01(Mathf.Abs(speed) / Mathf.Max(1f, maxSpeed)));
        speed = 0; pushYawVelocity = 0;
        var horizontalNormal = Vector3.ProjectOnPlane(normal, Vector3.up).normalized;
        float inward = Vector3.Dot(pushVelocity, horizontalNormal);
        if (inward < 0) pushVelocity -= horizontalNormal * inward;
        inward = Vector3.Dot(cannonShove, horizontalNormal);
        if (inward < 0) cannonShove -= horizontalNormal * inward;
        var angles = rotation.eulerAngles;
        yaw = angles.y; pitch = Mathf.DeltaAngle(0, angles.x) - cannonTilt.x;
        waveRoll = Mathf.DeltaAngle(0, angles.z) - bank - cannonTilt.y;
    }
    public ShipState Capture() => new ShipState { Position = rb.position, Yaw = yaw, Speed = speed, Bank = bank, Pitch = pitch + cannonTilt.x, WaveRoll = waveRoll + cannonTilt.y, WaveTime = OceanSurface.Instance != null ? OceanSurface.Instance.WaveTime : Time.time, Sail = sailSystem.DeployPercentage, Rudder = helm.CurrentRudderNormalized, Controlling = helm.IsControlling };
    public void Restore(ShipState s, AdvancedPlayerController driver, Vector3? relocatedAnchor = null)
    {
        cannonTilt=Vector2.zero; cannonTiltVelocity=Vector2.zero;
        buoyantVelocity = buoyantTargetVelocity = Vector3.zero; buoyantResponseReady = false;
        speed = s.Speed; yaw = s.Yaw; bank = s.Bank; pitch = s.Pitch; waveRoll = s.WaveRoll;
        if (relocatedAnchor.HasValue) anchorPoint = relocatedAnchor.Value;
        PirateSlop.Ships.ShipBodyPose.Apply(rb, s.Position, Quaternion.Euler(pitch + cannonTilt.x, yaw, bank + waveRoll + cannonTilt.y), poseBodies, false);
        sailSystem.ApplyAggregate(s.Sail); helm.Restore(s.Rudder, s.Controlling, driver);
    }
    public void ApplyRemoteState(ShipState s, float blend, AdvancedPlayerController driver = null)
    {
        speed = Mathf.Lerp(speed, s.Speed, blend); yaw = Mathf.LerpAngle(yaw, s.Yaw, blend); bank = Mathf.Lerp(bank, s.Bank, blend);
        pitch = Mathf.Lerp(pitch, s.Pitch, blend); waveRoll = Mathf.Lerp(waveRoll, s.WaveRoll, blend);
        var position = Vector3.Lerp(rb.position, s.Position, blend);
        var rotation = Quaternion.Slerp(rb.rotation, Quaternion.Euler(s.Pitch, s.Yaw, s.Bank + s.WaveRoll), blend);
        float dt = Mathf.Max(.001f, Time.deltaTime);
        motionVelocity = (position - rb.position) / dt;
        var rotationDelta = rotation * Quaternion.Inverse(rb.rotation);
        rotationDelta.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f) angle -= 360f;
        motionAngularVelocity = float.IsFinite(axis.sqrMagnitude) ? axis * (angle * Mathf.Deg2Rad / dt) : Vector3.zero;
        PirateSlop.Ships.ShipBodyPose.Apply(rb, position, rotation, poseBodies, Mathf.Abs(s.Pitch) > 15f || Mathf.Abs(s.Bank + s.WaveRoll) > 15f);
        sailSystem.ApplyAggregate(s.Sail); helm.Restore(s.Rudder, s.Controlling, driver);
    }
    public void ResolveCollision(Vector3 displacement, Vector3? contact = null, bool showEffect = true, float impactStrength = -1f)
    {
        if (IsFrozen) return;
        if (displacement.sqrMagnitude > .00001f)
        {
            Vector3 normal = displacement.normalized;
            float inward = Vector3.Dot(pushVelocity, normal);
            if (inward < 0f) pushVelocity -= normal * inward;
        }
        RegisterCollision(displacement, contact, showEffect, impactStrength);
        var position = rb.position + displacement; position.y = rb.position.y;
        PirateSlop.Ships.ShipBodyPose.Apply(rb, position, rb.rotation, poseBodies, true);
        speed *= .35f;
    }
    void RegisterCollision(Vector3 displacement, Vector3? contact, bool showEffect, float impactStrength)
    {
        if (Time.time >= nextCollisionAudio && Time.time - lastCollisionContact > .75f && displacement.sqrMagnitude > .00001f)
        {
            var network = GetComponent<PirateSlop.Networking.NetworkShip>();
            float strength = impactStrength >= 0f ? Mathf.Clamp01(impactStrength) : Mathf.Clamp01(Mathf.Abs(speed) / Mathf.Max(1f, maxSpeed) + displacement.magnitude * .2f);
            ApplyCannonImpulse(transform.position - displacement.normalized * 5f, displacement.normalized, strength * .3f);
            if (network != null) network.CollisionAudio(strength, contact ?? transform.position, contact.HasValue && showEffect);
            else GameAudio.Play(SoundCue.ShipCollision, transform.position);
            nextCollisionAudio = Time.time + 2f;
        }
        if (displacement.sqrMagnitude > .00001f) lastCollisionContact = Time.time;
    }
}
