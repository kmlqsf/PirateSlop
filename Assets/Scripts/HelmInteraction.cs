using UnityEngine;

public class HelmInteraction : MonoBehaviour
{
    [Header("Helm Settings")]
    [SerializeField] private float interactionRadius = 4f;
    [SerializeField] private float maxAngle = 35f;
    [SerializeField] private float turnSpeed = 40f;

    private bool isControlling = false;
    private float currentAngle = 0f;
    private ShipController shipController;
    private Transform playerTransform;
    private GameObject promptUI;

    public float CurrentRudderNormalized => currentAngle / maxAngle;
    public bool IsControlling => isControlling;

    private void Start()
    {
        shipController = GetComponentInParent<ShipController>();
        if (shipController == null)
        {
            shipController = Object.FindAnyObjectByType<ShipController>();
        }
    }

    private void Update()
    {
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard == null) return;

        if (playerTransform == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null) playerTransform = p.transform;
        }

        if (playerTransform != null)
        {
            float dist = Vector3.Distance(transform.position, playerTransform.position);
            
            if (dist <= interactionRadius)
            {
                // Нажатие E переключает управление штурвалом
                if (keyboard.eKey.wasPressedThisFrame)
                {
                    isControlling = !isControlling;
                    Debug.Log($"<color=cyan>Helm interaction toggled: {isControlling}</color>");
                }
            }
            else if (isControlling && dist > interactionRadius + 2f)
            {
                // Если отошли слишком далеко — отпускаем штурвал
                isControlling = false;
            }
        }

        // Если игрок у штурвала, блокируем его передвижение и крутим руль
        if (isControlling)
        {
            float input = 0f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input += 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input -= 1f;

            currentAngle += input * turnSpeed * Time.deltaTime;
            currentAngle = Mathf.Clamp(currentAngle, -maxAngle, maxAngle);

            transform.localRotation = Quaternion.Euler(0f, 0f, -currentAngle * 3f);

            // Передаем значение в ShipController если он есть
            if (shipController != null)
            {
                // Если в ShipController используется WheelInteraction, подмешиваем наш угол
                // (При необходимости ShipController может считывать HelmInteraction напрямую)
            }
        }
        else
        {
            currentAngle = Mathf.MoveTowards(currentAngle, 0f, turnSpeed * Time.deltaTime);
            transform.localRotation = Quaternion.Euler(0f, 0f, -currentAngle * 3f);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
    }
}
