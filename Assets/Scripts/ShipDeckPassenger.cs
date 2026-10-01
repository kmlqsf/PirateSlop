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
        ResetAnchor();
    }
    public void ResetAnchor() { if (Support != null) { lastPosition = Support.transform.position; lastRotation = Support.transform.rotation; } }
    public void Carry(bool updateLook = false)
    {
        var body = Support;
        if (body == null) return;
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
        if (Physics.SphereCast(transform.position + Vector3.up * .4f, .2f, Vector3.down, out var hit, .35f, ~0, QueryTriggerInteraction.Ignore) && hit.rigidbody != null &&
            (hit.rigidbody.GetComponent<ShipController>() != null || hit.rigidbody.GetComponent<PirateSlop.Networking.RaftPlatform>() != null)) nextShip = hit.rigidbody;
        if (nextShip == null && Support != null && player != null)
        {
            int count = Physics.SphereCastNonAlloc(transform.position + Vector3.up * .3f, .2f, Vector3.down,
                supportHits, player.IsGrounded ? .75f : 3.5f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (supportHits[i].rigidbody == Support && supportHits[i].normal.y > .5f) return;
        }
        if (nextShip != Support) Attach(nextShip);
    }
    void Update() { if (!Networked && (player == null || !player.IsDead)) { Carry(true); Detect(); } }
}
