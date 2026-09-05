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
    void Awake() { controller = GetComponent<CharacterController>(); player = GetComponent<AdvancedPlayerController>(); }
    public void Attach(Rigidbody body)
    {
        ship = body;
        if (ship != null) { lastPosition = ship.transform.position; lastRotation = ship.transform.rotation; }
    }
    void Update()
    {
        if (ship != null)
        {
            var delta = ship.transform.rotation * Quaternion.Inverse(lastRotation);
            Vector3 next = ship.transform.position + delta * (transform.position - lastPosition);
            controller.enabled = false;
            transform.position = next;
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y + Mathf.DeltaAngle(lastRotation.eulerAngles.y, ship.transform.eulerAngles.y), 0f);
            controller.enabled = true;
            lastPosition = ship.transform.position;
            lastRotation = ship.transform.rotation;
        }
        if (player != null && player.LocomotionLocked) return;
        Rigidbody nextShip = null;
        if (Physics.SphereCast(transform.position + Vector3.up * 0.4f, 0.2f, Vector3.down, out var hit, 0.35f, ~0, QueryTriggerInteraction.Ignore)
            && hit.rigidbody != null && hit.rigidbody.GetComponent<ShipController>() != null)
            nextShip = hit.rigidbody;
        if (nextShip != ship) Attach(nextShip);
    }
}
