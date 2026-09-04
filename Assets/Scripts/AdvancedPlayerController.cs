using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class AdvancedPlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float crouchSpeed = 2.5f;
    [SerializeField] private float slideSpeed = 9f;
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float gravity = -25f;

    [Header("Crouch & Slide")]
    [SerializeField] private float standingHeight = 1.8f;
    [SerializeField] private float crouchHeight = 0.9f;
    [SerializeField] private float slideDuration = 0.6f;
    [SerializeField] private float slideCooldown = 1f;

    [Header("Camera")]
    [SerializeField] private float mouseSensitivity = 2f;

    private CharacterController controller;
    private Camera playerCamera;
    
    private float currentSpeed;
    private float verticalRotation = 0f;
    private float verticalVelocity = 0f;
    
    private bool isCrouching = false;
    private bool isSliding = false;
    private Vector3 slideDirection;
    private float slideTimer = 0f;
    private float slideCooldownTimer = 0f;

    private void Start()
    {
        controller = GetComponent<CharacterController>();
        playerCamera = Camera.main;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        currentSpeed = walkSpeed;
    }

    private void Update()
    {
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (keyboard == null) return;

        // 1. Мышь (Взгляд от 1-го лица)
        if (mouse != null)
        {
            Vector2 mouseDelta = mouse.delta.ReadValue() * mouseSensitivity * 0.15f;
            transform.Rotate(Vector3.up * mouseDelta.x);

            verticalRotation -= mouseDelta.y;
            verticalRotation = Mathf.Clamp(verticalRotation, -85f, 85f);
            
            if (playerCamera != null)
            {
                playerCamera.transform.localEulerAngles = new Vector3(verticalRotation, 0f, 0f);
            }
        }

        // Кулдауны таймеров
        if (slideCooldownTimer > 0f) slideCooldownTimer -= Time.deltaTime;

        // 2. Обработка подката (Slide) и приседания (Crouch)
        bool crouchPressed = keyboard.leftCtrlKey.isPressed || keyboard.cKey.isPressed;

        if (isSliding)
        {
            slideTimer -= Time.deltaTime;
            currentSpeed = Mathf.Lerp(slideSpeed, crouchSpeed, 1f - (slideTimer / slideDuration));

            if (slideTimer <= 0f || !controller.isGrounded)
            {
                isSliding = false;
            }
        }
        else if (crouchPressed)
        {
            // Если бежали/шли и нажали присев в воздухе или на бегу — делаем подкат
            if (!isCrouching && controller.isGrounded && slideCooldownTimer <= 0f)
            {
                float moveX = 0f; float moveZ = 0f;
                if (keyboard.wKey.isPressed) moveZ += 1f;
                if (keyboard.sKey.isPressed) moveZ -= 1f;
                if (keyboard.dKey.isPressed) moveX += 1f;
                if (keyboard.aKey.isPressed) moveX -= 1f;

                Vector3 inputDir = (transform.right * moveX + transform.forward * moveZ).normalized;
                if (inputDir.sqrMagnitude > 0.1f)
                {
                    isSliding = true;
                    slideTimer = slideDuration;
                    slideCooldownTimer = slideCooldown;
                    slideDirection = inputDir;
                }
            }

            SetCrouching(true);
        }
        else
        {
            SetCrouching(false);
        }

        // 3. Ввод перемещения WASD
        float finalMoveX = 0f;
        float finalMoveZ = 0f;

        if (isSliding)
        {
            // Во время подката направление зафиксировано
            finalMoveX = slideDirection.x * slideSpeed;
            finalMoveZ = slideDirection.z * slideSpeed;
        }
        else
        {
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) finalMoveZ += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) finalMoveZ -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) finalMoveX += 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) finalMoveX -= 1f;
        }

        Vector3 move = isSliding ? slideDirection * currentSpeed : (transform.right * finalMoveX + transform.forward * finalMoveZ).normalized * currentSpeed;

        // 4. Гравитация и прыжок (во время подката прыгать нельзя)
        if (controller.isGrounded)
        {
            verticalVelocity = -2f;
            if (keyboard.spaceKey.wasPressedThisFrame && !isCrouching && !isSliding)
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        Vector3 motion = move;
        motion.y = verticalVelocity;

        controller.Move(motion * Time.deltaTime);
    }

    private void SetCrouching(bool crouch)
    {
        isCrouching = crouch;
        if (isCrouching)
        {
            controller.height = crouchHeight;
            controller.center = new Vector3(0, crouchHeight / 2f, 0);
            currentSpeed = crouchSpeed;
            if (playerCamera != null)
                playerCamera.transform.localPosition = Vector3.Lerp(playerCamera.transform.localPosition, new Vector3(0, 0.45f, 0), 0.2f);
        }
        else
        {
            controller.height = standingHeight;
            controller.center = new Vector3(0, standingHeight / 2f, 0);
            if (!isSliding) currentSpeed = walkSpeed;
            if (playerCamera != null)
                playerCamera.transform.localPosition = Vector3.Lerp(playerCamera.transform.localPosition, new Vector3(0, 0.7f, 0), 0.2f);
        }
    }
}
