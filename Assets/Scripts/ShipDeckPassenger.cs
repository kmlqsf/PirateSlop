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
    PirateSlop.BoardingWalkSurface bridge;
    float bridgeProgress, bridgeLateral;
    Vector3 bridgeAnchor;
    Vector3 lastPosition;
    Quaternion lastRotation;
    readonly RaycastHit[] supportHits = new RaycastHit[16];
    public bool Networked { get; set; }
    public Rigidbody Ship => ship;
    public Rigidbody Support => ship != null ? ship : raft;
    public FishNet.Object.NetworkObject PlatformObject => ship != null ? ship.GetComponent<FishNet.Object.NetworkObject>() :
        raft != null ? raft.GetComponent<PirateSlop.Networking.RaftPlatform>().Chest.NetworkObject : null;
    void Awake() { controller = GetComponent<CharacterController>(); player = GetComponent<AdvancedPlayerController>(); }
    public void Attach(Rigidbody body)
    {
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
    public void UpdateBoardingSupport(PirateSlop.BoardingWalkSurface surface)
    {
        if (bridge != surface) return;
        if (!surface.Locate(transform.position, out bridgeProgress, out bridgeLateral, out bridgeAnchor)) Attach(null);
        else ResetAnchor();
    }
    public void Carry(bool updateLook = false)
    {
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
        if (player != null && player.IsKnockedBack) { if (Support != null) Attach(null); return; }
        if (player != null && player.IsClimbing) return;
        if (player != null && player.IsSwimming) { if (Support != null) Attach(null); return; }
        if (player != null && player.LocomotionLocked) return;
        Rigidbody nextShip = null;
        PirateSlop.BoardingWalkSurface nextBridge = null;
        if (Physics.SphereCast(transform.position + Vector3.up * .4f, .2f, Vector3.down, out var hit, .35f, ~0, QueryTriggerInteraction.Ignore) && hit.rigidbody != null &&
            (hit.rigidbody.GetComponent<ShipController>() != null || hit.rigidbody.GetComponent<PirateSlop.Networking.RaftPlatform>() != null))
        { nextShip = hit.rigidbody; nextBridge = hit.collider.GetComponentInParent<PirateSlop.BoardingWalkSurface>(); }
        if (nextShip == null && Support != null && player != null)
        {
            int count = Physics.SphereCastNonAlloc(transform.position + Vector3.up * .3f, .2f, Vector3.down,
                supportHits, player.IsGrounded ? .75f : 3.5f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (supportHits[i].rigidbody == Support && supportHits[i].normal.y > .5f) return;
        }
        if (nextShip != Support) Attach(nextShip);
        if (bridge != nextBridge) { bridge = nextBridge; ResetAnchor(); }
    }
    void Update() { if (!Networked && (player == null || !player.IsDead)) { Carry(true); Detect(); } }
}
