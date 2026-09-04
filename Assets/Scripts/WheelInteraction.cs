using UnityEngine;

public class WheelInteraction : MonoBehaviour
{
    [Header("Wheel Parameters")]
    [SerializeField] private float maxRudderAngle = 35f;
    [SerializeField] private float turnSensitivity = 40f;
    [SerializeField] private float autoReturnSpeed = 15f;

    [Header("Visual Transform")]
    [SerializeField] private Transform wheelMesh;

    [Header("Interaction State")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private float interactionRadius = 2.5f;
    [SerializeField] private Transform playerStandPosition;

    private float currentRudderAngle = 0f;
    private bool isPlayerAtWheel = false;
    private Transform currentPlayer = null;

    public float CurrentRudderNormalized => currentRudderAngle / maxRudderAngle;
    public bool IsOccupied => isPlayerAtWheel;

    private void Update()
    {
        HandleInteractionPrompt();

        if (isPlayerAtWheel)
        {
            float steerInput = 0f;
            if (Input.GetKey(KeyCode.D)) steerInput += 1f;
            if (Input.GetKey(KeyCode.A)) steerInput -= 1f;

            currentRudderAngle += steerInput * turnSensitivity * Time.deltaTime;
            currentRudderAngle = Mathf.Clamp(currentRudderAngle, -maxRudderAngle, maxRudderAngle);
        }
        else
        {
            currentRudderAngle = Mathf.MoveTowards(currentRudderAngle, 0f, autoReturnSpeed * Time.deltaTime);
        }

        if (wheelMesh != null)
        {
            wheelMesh.localRotation = Quaternion.Euler(0f, 0f, -currentRudderAngle * 4f);
        }
    }

    private void HandleInteractionPrompt()
    {
        if (Input.GetKeyDown(interactKey))
        {
            if (isPlayerAtWheel)
            {
                ReleaseWheel();
            }
            else
            {
                TryOccupyWheel();
            }
        }
    }

    private void TryOccupyWheel()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null && Vector3.Distance(transform.position, playerObj.transform.position) <= interactionRadius)
        {
            isPlayerAtWheel = true;
            currentPlayer = playerObj.transform;

            if (playerStandPosition != null)
            {
                currentPlayer.position = playerStandPosition.position;
                currentPlayer.rotation = playerStandPosition.rotation;
            }
        }
    }

    public void ReleaseWheel()
    {
        isPlayerAtWheel = false;
        currentPlayer = null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
    }
}
