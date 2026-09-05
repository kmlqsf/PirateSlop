using UnityEngine;

public class HelmInteraction : MonoBehaviour
{
    [Header("Helm Settings")]
    [SerializeField] private float interactionRadius = 6f;
    [SerializeField] private float maxAngle = 40f;
    [SerializeField] private float turnSpeed = 60f;

    private bool isControlling = false;
    private float currentAngle = 0f;
    private Transform playerTransform;

    public float CurrentRudderNormalized => currentAngle / maxAngle;
    public bool IsControlling => isControlling;

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
            
            // Нажатие E переключает управление штурвалом
            if (dist <= interactionRadius)
            {
                if (keyboard.eKey.wasPressedThisFrame)
                {
                    isControlling = !isControlling;
                    Debug.Log($"<color=cyan>HELM INTERACTION: Active = {isControlling}</color>");

                    // Отключаем/включаем движение игрока при входе/выходе из штурвала
                    var playerMover = playerTransform.GetComponent<AdvancedPlayerController>();
                    if (playerMover != null)
                    {
                        playerMover.enabled = !isControlling;
                    }
                }
            }
        }

        // Если игрок у штурвала
        if (isControlling)
        {
            float steerInput = 0f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) steerInput += 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) steerInput -= 1f;

            currentAngle += steerInput * turnSpeed * Time.deltaTime;
            currentAngle = Mathf.Clamp(currentAngle, -maxAngle, maxAngle);

            // Визуальный поворот меша штурвала
            transform.localRotation = Quaternion.Euler(0f, 0f, -currentAngle * 3f);
        }
        else
        {
            // Плавный возврат руля в центр при отпускании
            currentAngle = Mathf.MoveTowards(currentAngle, 0f, turnSpeed * Time.deltaTime);
            transform.localRotation = Quaternion.Euler(0f, 0f, -currentAngle * 3f);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
    }
}
