using UnityEngine;
[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(CharacterController))]
public class ShipDeckPassenger : MonoBehaviour
{
    static readonly System.Collections.Generic.List<ShipDeckPassenger> active = new();
    public static System.Collections.Generic.IReadOnlyList<ShipDeckPassenger> Active => active;
    void OnEnable() => active.Add(this);
    void OnDisable() => active.Remove(this);

    CharacterController controller;
    AdvancedPlayerController player;
    Rigidbody ship;
    Rigidbody raft;
    Rigidbody presentationSupport;
    Vector3 presentationOrigin;
    PirateSlop.BoardingWalkSurface bridge;
    float bridgeProgress, bridgeLateral;
    Vector3 bridgeAnchor;
    Vector3 lastPosition;
    Quaternion lastRotation;
    readonly RaycastHit[] supportHits = new RaycastHit[16];
    public bool Networked { get; set; }
    public Rigidbody Ship => ship != null && ship.GetComponent<ShipController>() != null ? ship : null;
    public Rigidbody Support => ship != null ? ship : raft;
    public Rigidbody PresentationSupport
    {
        get
        {
            if (player != null && (player.IsDead || player.IsSwimming || player.DeveloperNoclip)) return null;
            if (Support != null) return Support;
            if (presentationSupport == null || player != null && player.IsGrounded) return null;
            return (presentationSupport.transform.InverseTransformPoint(transform.position) - presentationOrigin).sqrMagnitude <= 144f
                ? presentationSupport : null;
        }
    }
    public FishNet.Object.NetworkObject PlatformObject => ship != null ? ship.GetComponent<FishNet.Object.NetworkObject>() :
        raft != null ? raft.GetComponent<PirateSlop.Networking.RaftPlatform>().Chest.NetworkObject : null;
    void Awake() { controller = GetComponent<CharacterController>(); player = GetComponent<AdvancedPlayerController>(); }
    public void Attach(Rigidbody body)
    {
        if (body != null)
        {
            presentationSupport = body;
            presentationOrigin = body.transform.InverseTransformPoint(transform.position);
        }
        bool isRaft = body != null && body.GetComponent<PirateSlop.Networking.RaftPlatform>() != null;
        raft = isRaft ? body : null;
        ship = isRaft ? null : body;
        bridge = null;
        if (ship != null)
            foreach (var candidate in PirateSlop.BoardingWalkSurface.Active)
                if (candidate.Body == ship && candidate.Locate(transform.position, out bridgeProgress, out bridgeLateral, out bridgeAnchor))
                { bridge = candidate; break; }
        ResetAnchor();
    }
    public void ResetAnchor()
    {
        if (Support != null) { lastPosition = Support.transform.position; lastRotation = Support.transform.rotation; }
        if (bridge != null && bridge.Ready) bridge.Locate(transform.position, out bridgeProgress, out bridgeLateral, out bridgeAnchor);
    }
    public void ReleaseToAir()
    {
        var body = Support;
        if (body == null) return;
        var motor = body.GetComponent<ShipController>();
        presentationSupport = body;
        presentationOrigin = body.transform.InverseTransformPoint(transform.position);
        player?.InheritPlatformVelocity(motor != null ? motor.CannonPointVelocity(transform.position) : body.GetPointVelocity(transform.position));
        Attach(null);
    }
    public void UpdateBoardingSupport(PirateSlop.BoardingWalkSurface surface)
    {
        if (bridge != surface) return;
        if (!surface.Locate(transform.position, out bridgeProgress, out bridgeLateral, out bridgeAnchor)) Attach(null);
        else ResetAnchor();
    }
    public void Carry(bool updateLook = false)
    {
        if (player != null && player.DeveloperNoclip) { presentationSupport = null; Attach(null); return; }
        var body = Support;
        if (body == null) return;
        if (bridge != null)
        {
            if (!bridge.Ready) { Attach(null); return; }
            transform.position += bridge.Point(bridgeProgress, bridgeLateral) - bridgeAnchor;
            if (!Networked) Physics.SyncTransforms();
            ResetAnchor(); return;
        }
        var delta = body.transform.rotation * Quaternion.Inverse(lastRotation);
        var next = body.transform.position + delta * (transform.position - lastPosition);
        float yawDelta = Mathf.DeltaAngle(lastRotation.eulerAngles.y, body.transform.eulerAngles.y);
        transform.SetPositionAndRotation(next, Quaternion.Euler(0, transform.eulerAngles.y + yawDelta, 0));
        if (!Networked) Physics.SyncTransforms();
        if (updateLook) player.AddPlatformYaw(yawDelta);
        ResetAnchor();
    }
    public void Detect()
    {
        if (player != null && player.DeveloperNoclip) { Attach(null); return; }
        if (player != null && player.IsKnockedBack) { if (Support != null) Attach(null); return; }
        if (player != null && player.ShipActivityLocked && Support != null) return;
        if (player != null && player.IsClimbing) return;
        if (player != null && player.IsSwimming) { presentationSupport = null; if (Support != null) Attach(null); return; }
        if (player != null && player.LocomotionLocked) return;
        if (player != null && player.VerticalSpeed > 0f && !player.IsGrounded) { ReleaseToAir(); return; }
        if (Support == null && player != null && !player.IsGrounded) return;
        Rigidbody nextShip = null;
        PirateSlop.BoardingWalkSurface nextBridge = null;
        float supportRadius = player != null && player.IsGrounded ? Mathf.Max(.2f, controller.radius) : .2f;
        if (nextShip == null && Physics.SphereCast(transform.position + Vector3.up * .4f, supportRadius, Vector3.down, out var hit, .35f, ~0, QueryTriggerInteraction.Ignore) && hit.rigidbody != null &&
            (hit.rigidbody.GetComponent<ShipController>() != null || hit.rigidbody.GetComponent<PirateSlop.Networking.RaftPlatform>() != null || hit.rigidbody.GetComponent<PirateSlop.WhaleLootPoint>() != null || hit.rigidbody.GetComponent<PirateSlop.Networking.NetworkClamEvent>() != null || hit.rigidbody.GetComponent<PirateSlop.Networking.NetworkRiggingEvent>() != null))
        { nextShip = hit.rigidbody; nextBridge = hit.collider.GetComponentInParent<PirateSlop.BoardingWalkSurface>(); }
        if (nextShip == null && Support != null) ReleaseToAir();
        else if (nextShip != Support) Attach(nextShip);
        if (bridge != nextBridge) { bridge = nextBridge; ResetAnchor(); }
        if (nextShip == null && player != null && player.IsGrounded) presentationSupport = null;
    }
    void Update() { if (!Networked && (player == null || !player.IsDead)) { Carry(true); if (player == null) Detect(); } }
    public bool HasDryDeckSupport()
    {
        if (ship == null) return false;
        if (ship.TryGetComponent<ShipController>(out var motor)) { if (motor.IsFlooded) return false; }
        else if (ship.GetComponent<PirateSlop.Networking.NetworkClamEvent>() == null && ship.GetComponent<PirateSlop.Networking.NetworkRiggingEvent>() == null && (!ship.TryGetComponent<PirateSlop.WhaleLootPoint>(out var whale) || whale.State == PirateSlop.WhalePOIState.Diving)) return false;
        int count = Physics.SphereCastNonAlloc(transform.position + Vector3.up * .3f, .2f, Vector3.down,
            supportHits, .8f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
            if (supportHits[i].rigidbody == ship && supportHits[i].normal.y > .5f) return true;
        return false;
    }
}
