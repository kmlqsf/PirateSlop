using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class SimplePlayerMover : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float gravity = -25f;

    private CharacterController controller;
    private float verticalRotation = 0f;
    private float verticalVelocity = 0f;

    private void Start()
    {
        controller = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (keyboard == null) return;

        // 1. Плавное и точное вращение от первого лица
        if (mouse != null)
        {
            Vector2 mouseDelta = mouse.delta.ReadValue() * mouseSensitivity * 0.15f;
            transform.Rotate(Vector3.up * mouseDelta.x);

            verticalRotation -= mouseDelta.y;
            verticalRotation = Mathf.Clamp(verticalRotation, -85f, 85f);
            
            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.transform.localEulerAngles = new Vector3(verticalRotation, 0f, 0f);
            }
        }

        // 2. Классическое движение WASD относительно взгляда игрока
        float moveX = 0f;
        float moveZ = 0f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) moveZ += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) moveZ -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) moveX += 1f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) moveX -= 1f;

        Vector3 move = (transform.right * moveX + transform.forward * moveZ).normalized;
        
        // 3. Прыжок и гравитация
        if (controller.isGrounded)
        {
            verticalVelocity = -2f;
            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        Vector3 motion = move * moveSpeed;
        motion.y = verticalVelocity;

        // 4. Движение через CharacterController (поддерживает палубу корабля)
        controller.Move(motion * Time.deltaTime);
    }
}
