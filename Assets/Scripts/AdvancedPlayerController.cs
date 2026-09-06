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
    [SerializeField] float swimSpeed = 3f, fastSwimSpeed = 4.5f, swimAcceleration = 6f, breathSeconds = 25f;
    Vector3 swimVelocity;
    public bool IsSwimming { get; private set; }
    public float Breath { get; private set; } = 25f;
    public float BreathFraction => Mathf.Clamp01(Breath / breathSeconds);
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
    PlayerInventory inventory;
    DirectShipControls shipControls;
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
        inventory = GetComponent<PlayerInventory>();
        shipControls = GetComponent<DirectShipControls>();
        controller = GetComponent<CharacterController>(); playerCamera = GetComponentInChildren<Camera>(true);
        modelVisibility = GetComponentsInChildren<FirstPersonModelVisibility>(true);
        lookYaw = transform.eulerAngles.y; SetHeight(false);
        cameraHeight = standingHeight - .15f;
        Breath = breathSeconds;
        if (GetComponent<SwimPresentation>() == null) gameObject.AddComponent<SwimPresentation>();
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
        if (InputActive && mouse != null && (shipControls == null || !shipControls.IsDragging)) { var d = mouse.delta.ReadValue() * mouseSensitivity; lookYaw = Mathf.Repeat(lookYaw + d.x, 360f); pitch = Mathf.Clamp(pitch - d.y, -85f, 85f); }
        pending.Move = InputActive ? Vector2.ClampMagnitude(new Vector2((kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1 : 0) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1 : 0), (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1 : 0) - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1 : 0)), 1) : Vector2.zero;
        pending.Yaw = lookYaw;
        pending.Pitch = pitch;
        pending.Rise = InputActive && kb.spaceKey.isPressed;
        pending.Sprint = InputActive && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
        pending.Crouch = InputActive && (kb.cKey.isPressed || kb.leftCtrlKey.isPressed);
        pending.Slide |= InputActive && kb.cKey.wasPressedThisFrame;
        pending.Jump |= InputActive && kb.spaceKey.wasPressedThisFrame;
        pending.Use |= InputActive && kb.eKey.wasPressedThisFrame && (IsSwimming || LocomotionLocked || inventory == null || !inventory.AimingAtPickup());
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
        var ocean = OceanSurface.Instance;
        float water = ocean != null ? ocean.Height(transform.position) : float.NegativeInfinity;
        bool wasSwimming = IsSwimming;
        IsSwimming = ocean != null && water - transform.position.y > (IsSwimming ? .65f : 1.1f);
        if (IsSwimming)
        {
            SimulateSwimming(command, dt, water, wasSwimming);
            return;
        }
        swimVelocity = Vector3.zero;
        Breath = Mathf.MoveTowards(Breath, breathSeconds, dt * 8f);
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
    void SimulateSwimming(PlayerCommand command, float dt, float water, bool wasSwimming)
    {
        GetComponent<ShipDeckPassenger>()?.Attach(null);
        slideTimer = 0; IsGrounded = false;
        SetHeight(!CanStand());
        if (!wasSwimming) swimVelocity = Vector3.up * Mathf.Max(verticalVelocity * .25f, -4f);
        if (command.Use && TryBoard()) return;
        float depth = water - (transform.position.y + standingHeight - .15f);
        bool submerged = depth > .1f;
        Breath = Mathf.Clamp(Breath + dt * (submerged ? -1 : 8), 0, breathSeconds);
        if (Breath <= 0 && health != null && (!networked || GetComponent<PirateSlop.Networking.NetworkPlayer>().IsServerInitialized)) health.Damage(12f * dt);
        var direction = transform.right * command.Move.x + transform.forward * command.Move.y;
        float vertical = command.Rise ? 1f : command.Crouch ? -1f : 0f;
        if (submerged && vertical == 0 && command.Move.y != 0)
        {
            float angle = command.Pitch * Mathf.Deg2Rad;
            direction = transform.right * command.Move.x + transform.forward * (command.Move.y * Mathf.Cos(angle));
            vertical = -Mathf.Sin(angle) * command.Move.y;
        }
        var target = Vector3.ClampMagnitude(direction + Vector3.up * vertical, 1) * (command.Sprint ? fastSwimSpeed : swimSpeed);
        if (!command.Crouch && !submerged && !command.Rise) target.y = Mathf.Clamp((water - 1.25f - transform.position.y) * 5f, -2f, 2f);
        else if (command.Rise && transform.position.y > water - 1.2f) target.y = 0;
        else if (submerged && Mathf.Abs(vertical) < .05f) target.y = .35f;
        swimVelocity = Vector3.MoveTowards(swimVelocity, target, swimAcceleration * dt);
        var flags = controller.Move(swimVelocity * dt);
        if ((flags & (CollisionFlags.Above | CollisionFlags.Below)) != 0) swimVelocity.y = 0;
        verticalVelocity = swimVelocity.y;
        PlanarSpeed = new Vector2(swimVelocity.x, swimVelocity.z).magnitude;
    }
    bool TryBoard()
    {
        if (!Physics.Raycast(transform.position + Vector3.up, transform.forward, out var wall, 2.5f, ~0, QueryTriggerInteraction.Ignore)) return false;
        var body = wall.rigidbody;
        if (body == null || body.GetComponent<ShipController>() == null) return false;
        Vector3 origin = wall.point + transform.forward * 1.1f;
        origin.y = transform.position.y + 6f;
        foreach (var top in Physics.RaycastAll(origin, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (top.rigidbody != body || top.normal.y < .75f || top.point.y < transform.position.y + .5f) continue;
            Vector3 destination = top.point + Vector3.up * .1f;
            bool blocked = false;
            foreach (var hit in Physics.OverlapCapsule(destination + Vector3.up * controller.radius, destination + Vector3.up * (standingHeight - controller.radius), controller.radius * .95f, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform)) { blocked = true; break; }
            if (blocked) continue;
            controller.enabled = false; transform.position = destination; controller.enabled = true;
            SetHeight(false); IsSwimming = false; verticalVelocity = -2f; swimVelocity = Vector3.zero; IsGrounded = true;
            GetComponent<ShipDeckPassenger>()?.Attach(body);
            return true;
        }
        return false;
    }
    public PlayerState Capture() => new PlayerState { Position = transform.position, Yaw = transform.eulerAngles.y, VerticalVelocity = verticalVelocity, SlideDirection = slideDirection, SlideTimer = slideTimer, Cooldown = cooldown, Crouched = crouched, Locked = LocomotionLocked, PlanarSpeed = PlanarSpeed, Grounded = IsGrounded, Swimming = IsSwimming, SwimVelocity = swimVelocity, Breath = Breath };
    public void Restore(PlayerState s)
    {
        controller.enabled = false; transform.SetPositionAndRotation(s.Position, Quaternion.Euler(0, s.Yaw, 0)); controller.enabled = !IsDead;
        verticalVelocity = s.VerticalVelocity; slideTimer = s.SlideTimer; cooldown = s.Cooldown; slideDirection = s.SlideDirection; ApplyAnimationState(s);
    }
    public void ApplyAnimationState(PlayerState s)
    {
        PlanarSpeed = s.PlanarSpeed; IsGrounded = s.Grounded; verticalVelocity = s.VerticalVelocity;
        IsSwimming = s.Swimming; swimVelocity = s.SwimVelocity; Breath = s.Swimming ? s.Breath : breathSeconds;
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
