using UnityEngine;
[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(CharacterController))]
public class ShipDeckPassenger : MonoBehaviour
{
    CharacterController controller;
    AdvancedPlayerController player;
    Rigidbody ship;
    Vector3 lastPosition;
    Quaternion lastRotation;
    public bool Networked { get; set; }
    public Rigidbody Ship => ship;
    void Awake() { controller = GetComponent<CharacterController>(); player = GetComponent<AdvancedPlayerController>(); }
    public void Attach(Rigidbody body) { ship = body; ResetAnchor(); }
    public void ResetAnchor() { if (ship != null) { lastPosition = ship.transform.position; lastRotation = ship.transform.rotation; } }
    public void Carry(bool updateLook = false)
    {
        if (ship == null) return;
        var delta = ship.transform.rotation * Quaternion.Inverse(lastRotation);
        var next = ship.transform.position + delta * (transform.position - lastPosition);
        float yawDelta = Mathf.DeltaAngle(lastRotation.eulerAngles.y, ship.transform.eulerAngles.y);
        transform.SetPositionAndRotation(next, Quaternion.Euler(0, transform.eulerAngles.y + yawDelta, 0));
        Physics.SyncTransforms();
        if (updateLook) player.AddPlatformYaw(yawDelta);
        ResetAnchor();
    }
    public void Detect()
    {
        if (player != null && player.LocomotionLocked) return;
        if (player != null && player.VerticalSpeed > 0) { if (ship != null) Attach(null); return; }
        Rigidbody nextShip = null;
        if (Physics.SphereCast(transform.position + Vector3.up * .4f, .2f, Vector3.down, out var hit, .35f, ~0, QueryTriggerInteraction.Ignore) && hit.rigidbody != null && hit.rigidbody.GetComponent<ShipController>() != null) nextShip = hit.rigidbody;
        if (nextShip != ship) Attach(nextShip);
    }
    void Update() { if (!Networked) { Carry(true); Detect(); } }
}
