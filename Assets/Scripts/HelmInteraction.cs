using UnityEngine;

public class HelmInteraction : MonoBehaviour
{
    [Header("Helm Settings")]
    [SerializeField] private float interactionRadius = 3f;
    [SerializeField] private Transform steeringWheel;
    [SerializeField] private float maxAngle = 35f;
    [SerializeField] private float turnSpeed = 40f;

    private bool isControlling = false;
    private float currentAngle = 0f;
    private ShipController shipController;
    private Transform playerTransform;

    public float CurrentRudderNormalized => currentAngle / maxAngle;
    public bool IsControlling => isControlling;

    private void Start()
    {
        shipController = GetComponentInParent<ShipController>();
        if (steeringWheel == null) steeringWheel = transform;
    }

    private void Update()
    {
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard == null) return;

        // Поиск игрока по тегу
        if (playerTransform == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null) playerTransform = p.transform;
        }

        if (playerTransform != null)
        {
            float dist = Vector3.Distance(transform.position, playerTransform.position);
            
            // Нажатие E для взаимодействия
            if (keyboard.eKey.wasPressedThisFrame && dist <= interactionRadius)
            {
                isControlling = !isControlling;
                Debug.Log(isControlling ? "Helm occupied!" : "Helm released!");
            }
        }

        if (isControlling)
        {
            float input = 0f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input += 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input -= 1f;

            currentAngle += input * turnSpeed * Time.deltaTime;
            currentAngle = Mathf.Clamp(currentAngle, -maxAngle, maxAngle);

            if (steeringWheel != null)
            {
                steeringWheel.localRotation = Quaternion.Euler(0f, 0f, -currentAngle * 3f);
            }
        }
        else
        {
            currentAngle = Mathf.MoveTowards(currentAngle, 0f, turnSpeed * Time.deltaTime);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
    }
}
