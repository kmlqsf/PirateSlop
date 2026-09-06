using UnityEngine;
using UnityEngine.InputSystem;
using PirateSlop;

[RequireComponent(typeof(CharacterController))]
public class AdvancedPlayerController : MonoBehaviour
{
    [SerializeField] float walkSpeed = 5f, sprintSpeed = 8f, crouchSpeed = 2.5f, slideSpeed = 6f;
    [SerializeField] float jumpHeight = 1.2f, gravity = -25f;
    [SerializeField] float standingHeight = 1.8f, crouchHeight = 0.9f;
    [SerializeField] float slideDuration = 1.2f, slideCooldown = 1f, mouseSensitivity = 0.12f;
    [SerializeField] float thirdPersonDistance = 3f;
    FirstPersonModelVisibility[] modelVisibility;
    public bool IsThirdPerson { get; private set; }
    public bool IsGrounded { get; private set; }
    public float VerticalSpeed => verticalVelocity;
    CharacterController controller;
    Camera playerCamera;
    float pitch, verticalVelocity, slideTimer, cooldown, lookYaw;
    float cameraHeight;
    Vector3 slideDirection;
    bool crouched, networked, local = true;
    PlayerCommand pending;
    CombatHealth health;
    public bool IsDead => health != null && health.IsDead;
    public bool LocomotionLocked { get; private set; }
    public bool IsSliding => slideTimer > 0f;
    public bool IsCrouched => crouched;
    public float PlanarSpeed { get; private set; }
    public bool InputActive => local && !IsDead && Cursor.lockState == CursorLockMode.Locked;
    public Camera PlayerCamera => playerCamera;
    void Awake()
    {
        health = GetComponent<CombatHealth>();
        controller = GetComponent<CharacterController>(); playerCamera = GetComponentInChildren<Camera>(true);
        modelVisibility = GetComponentsInChildren<FirstPersonModelVisibility>(true);
        lookYaw = transform.eulerAngles.y; SetHeight(false);
        cameraHeight = standingHeight - .15f;
    }
    void Start() { if (local) SetCursor(true); }
    void OnDisable() { if (local) SetCursor(false); }
    public static void SetCursor(bool locked) { Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None; Cursor.visible = !locked; }
    public void ConfigureNetwork(bool owner)
    {
        networked = true; local = owner;
        if (playerCamera != null) { playerCamera.enabled = owner; var listener = playerCamera.GetComponent<AudioListener>(); if (listener != null) listener.enabled = owner; }
        if (owner) { lookYaw = transform.eulerAngles.y; SetCursor(true); }
    }
    public void SetLocomotionLocked(bool value)
    {
        if (LocomotionLocked == value) return;
        LocomotionLocked = value; verticalVelocity = 0; slideTimer = 0; PlanarSpeed = 0;
    }
    void Update()
    {
        if (!local) return;
        var kb = Keyboard.current; var mouse = Mouse.current;
        if (kb == null) return;
        if (kb.f1Key.wasPressedThisFrame) SetThirdPerson(!IsThirdPerson);
        if (kb.escapeKey.wasPressedThisFrame) { pending.Release = true; SetCursor(false); }
        if (mouse != null && mouse.leftButton.wasPressedThisFrame && !PirateSlop.Networking.SessionController.MenuOpen) SetCursor(true);
        if (InputActive && mouse != null) { var d = mouse.delta.ReadValue() * mouseSensitivity; lookYaw = Mathf.Repeat(lookYaw + d.x, 360f); pitch = Mathf.Clamp(pitch - d.y, -85f, 85f); }
        pending.Move = InputActive ? Vector2.ClampMagnitude(new Vector2((kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1 : 0) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1 : 0), (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1 : 0) - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1 : 0)), 1) : Vector2.zero;
        pending.Yaw = lookYaw;
        pending.Sprint = InputActive && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
        pending.Crouch = InputActive && (kb.cKey.isPressed || kb.leftCtrlKey.isPressed);
        pending.Slide |= InputActive && kb.cKey.wasPressedThisFrame;
        pending.Jump |= InputActive && kb.spaceKey.wasPressedThisFrame;
        pending.Use |= InputActive && kb.eKey.wasPressedThisFrame;
        pending.Release |= kb.qKey.wasPressedThisFrame || !InputActive;
        if (!networked) Simulate(ConsumeCommand(), Time.deltaTime);
    }
    void LateUpdate()
    {
        if (!local || playerCamera == null) return;
        playerCamera.transform.rotation = Quaternion.Euler(pitch, lookYaw, 0);
        cameraHeight = Mathf.Lerp(cameraHeight, (crouched ? crouchHeight : standingHeight) - .15f, 1f - Mathf.Exp(-16f * Time.deltaTime));
        var pivot = playerCamera.transform.parent.TransformPoint(Vector3.up * cameraHeight);
        if (IsThirdPerson)
        {
            var direction = -playerCamera.transform.forward;
            float distance = thirdPersonDistance;
            foreach (var hit in Physics.SphereCastAll(pivot, .15f, direction, distance, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform)) distance = Mathf.Min(distance, Mathf.Max(0, hit.distance - .05f));
            playerCamera.transform.position = pivot + direction * distance;
        }
        else playerCamera.transform.position = pivot;
    }
    public void SetThirdPerson(bool value)
    {
        if (!local) return;
        IsThirdPerson = value;
        foreach (var visibility in modelVisibility) visibility.SetFirstPerson(!value);
    }
    public PlayerCommand ConsumeCommand()
    {
        var result = pending; pending.Jump = pending.Slide = pending.Use = pending.Release = false; return result;
    }
    public void AddPlatformYaw(float delta) { if (local) lookYaw = Mathf.Repeat(lookYaw + delta, 360); }
    public void Simulate(PlayerCommand command, float dt)
    {
        if (IsDead) return;
        if (!command.IsValid) command = default;
        transform.rotation = Quaternion.Euler(0, command.Yaw, 0);
        cooldown = Mathf.Max(0, cooldown - dt);
        if (LocomotionLocked) return;
        var direction = transform.right * command.Move.x + transform.forward * command.Move.y;
        bool grounded = verticalVelocity <= 0 && HasGround();
        if (command.Slide && command.Sprint && direction.sqrMagnitude > .1f && grounded && !crouched && cooldown <= 0) { slideTimer = slideDuration; cooldown = slideDuration + slideCooldown; slideDirection = direction.normalized; }
        if (IsSliding) { slideTimer = Mathf.Max(0, slideTimer - dt); if (!grounded) slideTimer = 0; }
        bool wantCrouch = command.Crouch || IsSliding;
        if (!wantCrouch && crouched && !CanStand()) wantCrouch = true;
        SetHeight(wantCrouch);
        float slideProgress = 1f - slideTimer / Mathf.Max(.01f, slideDuration);
        var planar = IsSliding ? slideDirection * Mathf.Lerp(slideSpeed, crouchSpeed, slideProgress * slideProgress) : direction * (crouched ? crouchSpeed : command.Sprint ? sprintSpeed : walkSpeed);
        PlanarSpeed = planar.magnitude;
        if (grounded && verticalVelocity < 0) verticalVelocity = -2;
        if (command.Jump && grounded && !crouched) verticalVelocity = Mathf.Sqrt(jumpHeight * -2 * gravity);
        verticalVelocity += gravity * dt;
        controller.Move((planar + Vector3.up * verticalVelocity) * dt);
        IsGrounded = verticalVelocity <= 0 && HasGround();
    }
    public PlayerState Capture() => new PlayerState { Position = transform.position, Yaw = transform.eulerAngles.y, VerticalVelocity = verticalVelocity, SlideDirection = slideDirection, SlideTimer = slideTimer, Cooldown = cooldown, Crouched = crouched, Locked = LocomotionLocked, PlanarSpeed = PlanarSpeed, Grounded = IsGrounded };
    public void Restore(PlayerState s)
    {
        controller.enabled = false; transform.SetPositionAndRotation(s.Position, Quaternion.Euler(0, s.Yaw, 0)); controller.enabled = !IsDead;
        verticalVelocity = s.VerticalVelocity; slideTimer = s.SlideTimer; cooldown = s.Cooldown; slideDirection = s.SlideDirection; ApplyAnimationState(s);
    }
    public void ApplyAnimationState(PlayerState s)
    {
        PlanarSpeed = s.PlanarSpeed; IsGrounded = s.Grounded; verticalVelocity = s.VerticalVelocity;
        slideTimer = s.SlideTimer; LocomotionLocked = s.Locked; SetHeight(s.Crouched);
    }
    public void ApplyRemoteState(Vector3 position, float yawValue, float blend)
    {
        controller.enabled = false;
        transform.SetPositionAndRotation(Vector3.Lerp(transform.position, position, blend), Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, yawValue, 0), blend));
        controller.enabled = true;
    }
    bool CanStand()
    {
        float r = controller.radius * .95f;
        foreach (var hit in Physics.OverlapCapsule(transform.position + Vector3.up * (crouchHeight + r), transform.position + Vector3.up * (standingHeight - r), r, ~0, QueryTriggerInteraction.Ignore)) if (!hit.transform.IsChildOf(transform)) return false;
        return true;
    }
    bool HasGround()
    {
        if (controller.isGrounded) return true;
        foreach (var hit in Physics.SphereCastAll(transform.position + Vector3.up * .35f, .25f, Vector3.down, .16f, ~0, QueryTriggerInteraction.Ignore))
            if (!hit.transform.IsChildOf(transform) && hit.normal.y >= Mathf.Cos(controller.slopeLimit * Mathf.Deg2Rad)) return true;
        return false;
    }
    void SetHeight(bool value)
    {
        crouched = value;
        float height = value ? crouchHeight : standingHeight;
        if (Mathf.Abs(controller.height - height) > .001f) controller.height = height;
        var center = Vector3.up * height * .5f;
        if ((controller.center - center).sqrMagnitude > .000001f) controller.center = center;
    }
}
