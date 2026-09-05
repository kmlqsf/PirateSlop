using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class AdvancedPlayerController : MonoBehaviour
{
    [SerializeField] float walkSpeed = 5f, sprintSpeed = 8f, crouchSpeed = 2.5f, slideSpeed = 10f;
    [SerializeField] float jumpHeight = 1.2f, gravity = -25f;
    [SerializeField] float standingHeight = 1.8f, crouchHeight = 0.9f;
    [SerializeField] float slideDuration = 0.65f, slideCooldown = 1f;
    [SerializeField] float mouseSensitivity = 0.12f;
    CharacterController controller;
    Camera playerCamera;
    float pitch, verticalVelocity, slideTimer, cooldown;
    Vector3 slideDirection;
    bool crouched;
    public bool LocomotionLocked { get; private set; }
    public bool IsSliding => slideTimer > 0f;
    public float PlanarSpeed { get; private set; }

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerCamera = GetComponentInChildren<Camera>();
        SetHeight(false);
    }
    void Start() { SetCursor(true); }
    void OnDisable() { SetCursor(false); }
    static void SetCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
    public void SetLocomotionLocked(bool value)
    {
        LocomotionLocked = value;
        verticalVelocity = 0f;
        slideTimer = 0f;
        PlanarSpeed = 0f;
    }
    void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null) return;
        if (kb.escapeKey.wasPressedThisFrame) SetCursor(false);
        if (mouse != null && mouse.leftButton.wasPressedThisFrame) SetCursor(true);
        bool inputActive = Cursor.lockState == CursorLockMode.Locked;
        if (mouse != null && inputActive)
        {
            var delta = mouse.delta.ReadValue() * mouseSensitivity;
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y + delta.x, 0f);
            pitch = Mathf.Clamp(pitch - delta.y, -85f, 85f);
            if (playerCamera != null) playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
        float dt = Time.deltaTime;
        cooldown = Mathf.Max(0f, cooldown - dt);
        if (LocomotionLocked) { UpdateCamera(dt); return; }
        Vector2 axes = new Vector2(
            (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1 : 0) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1 : 0),
            (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1 : 0) - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1 : 0));
        axes = Vector2.ClampMagnitude(axes, 1f);
        if (!inputActive) axes = Vector2.zero;
        Vector3 direction = transform.right * axes.x + transform.forward * axes.y;
        bool sprint = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
        bool crouchHeld = kb.cKey.isPressed || kb.leftCtrlKey.isPressed;
        bool grounded = controller.isGrounded || Physics.SphereCast(transform.position + Vector3.up * 0.35f, 0.25f, Vector3.down, out _, 0.16f, ~0, QueryTriggerInteraction.Ignore);
        if (inputActive && kb.cKey.wasPressedThisFrame && sprint && direction.sqrMagnitude > 0.1f && grounded && !crouched && cooldown <= 0f)
        {
            slideTimer = slideDuration;
            cooldown = slideDuration + slideCooldown;
            slideDirection = direction.normalized;
        }
        if (IsSliding)
        {
            slideTimer = Mathf.Max(0f, slideTimer - dt);
            if (!grounded) slideTimer = 0f;
        }
        bool wantCrouch = crouchHeld || IsSliding;
        if (!wantCrouch && crouched && !CanStand()) wantCrouch = true;
        SetHeight(wantCrouch);
        Vector3 planar = IsSliding
            ? slideDirection * Mathf.Lerp(crouchSpeed, slideSpeed, slideTimer / Mathf.Max(0.01f, slideDuration))
            : direction * (crouched ? crouchSpeed : sprint ? sprintSpeed : walkSpeed);
        PlanarSpeed = planar.magnitude;
        if (grounded && verticalVelocity < 0f) verticalVelocity = -2f;
        if (inputActive && kb.spaceKey.wasPressedThisFrame && grounded && !crouched)
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        verticalVelocity += gravity * dt;
        controller.Move((planar + Vector3.up * verticalVelocity) * dt);
        UpdateCamera(dt);
    }
    bool CanStand()
    {
        float r = controller.radius * 0.95f;
        foreach (var hit in Physics.OverlapCapsule(transform.position + Vector3.up * (crouchHeight + r),
                     transform.position + Vector3.up * (standingHeight - r), r, ~0, QueryTriggerInteraction.Ignore))
            if (!hit.transform.IsChildOf(transform)) return false;
        return true;
    }
    void SetHeight(bool value)
    {
        crouched = value;
        float height = value ? crouchHeight : standingHeight;
        if (!Mathf.Approximately(controller.height, height) || controller.center != Vector3.up * height * 0.5f)
        {
            controller.height = height;
            controller.center = Vector3.up * height * 0.5f;
        }
    }
    void UpdateCamera(float dt)
    {
        if (playerCamera != null)
            playerCamera.transform.localPosition = Vector3.Lerp(playerCamera.transform.localPosition,
                Vector3.up * (crouched ? crouchHeight - 0.15f : standingHeight - 0.15f), 1f - Mathf.Exp(-16f * dt));
    }
}
