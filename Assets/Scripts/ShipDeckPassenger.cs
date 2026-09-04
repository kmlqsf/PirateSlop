using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class ShipDeckPassenger : MonoBehaviour
{
    [SerializeField] private LayerMask shipLayer;
    [SerializeField] private float rayLength = 1.5f;

    private CharacterController characterController;
    private Rigidbody currentShipRb;
    private Vector3 lastShipPos;
    private Quaternion lastShipRot;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    private void Update()
    {
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, rayLength, shipLayer))
        {
            Rigidbody shipRb = hit.collider.attachedRigidbody;
            if (shipRb != null && shipRb.CompareTag("Ship"))
            {
                if (currentShipRb != shipRb)
                {
                    currentShipRb = shipRb;
                    lastShipPos = shipRb.position;
                    lastShipRot = shipRb.rotation;
                }

                Vector3 deltaPosition = shipRb.position - lastShipPos;
                Quaternion deltaRotation = shipRb.rotation * Quaternion.Inverse(lastShipRot);

                Vector3 offsetFromShip = transform.position - shipRb.position;
                Vector3 rotatedOffset = deltaRotation * offsetFromShip;
                Vector3 rotationMovement = rotatedOffset - offsetFromShip;

                characterController.Move(deltaPosition + rotationMovement);
                transform.rotation = deltaRotation * transform.rotation;

                lastShipPos = shipRb.position;
                lastShipRot = shipRb.rotation;
                return;
            }
        }

        currentShipRb = null;
    }
}
