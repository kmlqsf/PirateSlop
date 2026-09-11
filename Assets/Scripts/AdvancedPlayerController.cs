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
    Vector3 knockbackVelocity;
    float knockbackTime;
    public bool IsKnockedBack => knockbackTime > 0f;
    public void ApplyKnockback(Vector3 velocity)
    {
        if (IsDead || !float.IsFinite(velocity.sqrMagnitude)) return;
        foreach (var helm in FindObjectsByType<HelmInteraction>(FindObjectsSortMode.None))
            if (helm.IsControlledBy(this)) helm.ReleaseControl();
        SetLocomotionLocked(false);
        var passenger = GetComponent<ShipDeckPassenger>();
        var ship = passenger != null && passenger.Ship != null ? passenger.Ship.GetComponent<ShipController>() : null;
        Vector3 inherited = ship != null ? ship.CannonPointVelocity(transform.position) : Vector3.zero;
        passenger?.Attach(null);
        knockbackVelocity = Vector3.ProjectOnPlane(velocity + inherited, Vector3.up);
        verticalVelocity = Mathf.Max(0f, velocity.y + inherited.y);
        knockbackTime = 1.25f;
        ladderCooldown = 1.25f;
        IsClimbing = false; IsGrounded = false; slideTimer = 0f;
    }
    float ladderCooldown;
    public bool IsClimbing { get; private set; }
    public bool IsSwimming { get; private set; }
    public float Breath { get; private set; } = 25f;
    public float BreathFraction => Mathf.Clamp01(Breath / breathSeconds);
    FirstPersonModelVisibility[] modelVisibility;
    public bool IsThirdPerson { get; private set; }
    public bool IsGrounded { get; private set; }
    public float VerticalSpeed => verticalVelocity;
    Vector3 cannonPushDirection;
    float cannonPushDelta;
    readonly System.Collections.Generic.HashSet<CannonCarriage> pushedCannons = new();
    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (cannonPushDelta <= 0 || Mathf.Abs(hit.normal.y) > .5f) return;
        var carriage=hit.collider.GetComponentInParent<CannonCarriage>();
        if(carriage!=null && pushedCannons.Add(carriage) && Vector3.Dot(cannonPushDirection,hit.normal)<-.1f)
            carriage.Push(this,cannonPushDirection,cannonPushDelta);
    }
    CharacterController controller;
    Camera playerCamera;
    float pitch, verticalVelocity, slideTimer, cooldown, lookYaw;
    Vector2 aimRecoil;
    public float AimSensitivityScale { get; set; } = 1;
    public Vector3 AimEuler => new Vector3(Mathf.Clamp(pitch-aimRecoil.x,-85,85),lookYaw+aimRecoil.y,0);
    public Vector3 AimDirection => Quaternion.Euler(AimEuler)*Vector3.forward;
    public void AddAimRecoil(float vertical,float horizontal) { if(local) aimRecoil+=new Vector2(vertical,horizontal); }
    float cameraHeight;
    Vector3 slideDirection;
    bool crouched, networked, local = true;
    PlayerCommand pending;
    CombatHealth health;
    PlayerInventory inventory;
    PirateSlop.Networking.NetworkEquipment equipment;
    DirectShipControls shipControls;
    public bool IsDead => health != null && health.IsDead;
    bool locomotionLocked;
    public SimpleCannon ActiveCannon { get; set; }
    public bool LocomotionLocked => locomotionLocked || ActiveCannon != null;
    public bool IsSliding => slideTimer > 0f;
    public bool IsCrouched => crouched;
    public float PlanarSpeed { get; private set; }
    public bool InputActive => !DeveloperMenu.IsOpen && !ShipSpyglassView.IsViewing && local && !IsDead && Cursor.lockState == CursorLockMode.Locked;
    public Camera PlayerCamera => playerCamera;
    void Awake()
    {
        health = GetComponent<CombatHealth>();
        inventory = GetComponent<PlayerInventory>();
        equipment = GetComponent<PirateSlop.Networking.NetworkEquipment>();
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
        if (locomotionLocked == value) return;
        locomotionLocked = value; verticalVelocity = 0; slideTimer = 0; PlanarSpeed = 0;
    }
    void Update()
    {
        if (!local) return;
        aimRecoil=Vector2.Lerp(aimRecoil,Vector2.zero,1-Mathf.Exp(-7*Time.deltaTime));
        var kb = Keyboard.current; var mouse = Mouse.current;
        if (kb == null) return;
        if (kb.f1Key.wasPressedThisFrame && ActiveCannon == null) SetThirdPerson(!IsThirdPerson);
        if (kb.escapeKey.wasPressedThisFrame) { pending.Release = true; SetCursor(false); }
        if (mouse != null && mouse.leftButton.wasPressedThisFrame && !PlayerInventory.LootWindowOpen && !PirateSlop.Networking.SessionController.MenuOpen) SetCursor(true);
        if (InputActive && ActiveCannon == null && mouse != null && (shipControls == null || !shipControls.IsDragging)) { var d = mouse.delta.ReadValue() * mouseSensitivity * AimSensitivityScale; lookYaw = Mathf.Repeat(lookYaw + d.x, 360f); pitch = Mathf.Clamp(pitch - d.y, -85f, 85f); }
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
        if (ActiveCannon != null && ActiveCannon.Muzzle != null)
        {
            var cannon = ActiveCannon;
            if (cannon.IsMortar)
            {
                cannon.GetComponent<MortarTrajectory>().PositionCamera(playerCamera);
                return;
            }
            Vector3 origin = cannon.Breech != null ? cannon.Breech.position : cannon.BarrelPivot.position;
            playerCamera.transform.SetPositionAndRotation(origin - cannon.Muzzle.forward * .35f + cannon.Muzzle.up * .42f, cannon.Muzzle.rotation);
            return;
        }
        playerCamera.transform.rotation = Quaternion.Euler(AimEuler);
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
        if (LocomotionLocked && !(GetComponent<PirateSlop.Networking.NetworkWeapon>()?.BeingHooked ?? false)) { PlanarSpeed = 0f; verticalVelocity = 0f; slideTimer = 0f; return; }
        ladderCooldown = Mathf.Max(0, ladderCooldown - dt);
        knockbackTime = Mathf.Max(0f, knockbackTime - dt);
        knockbackVelocity *= Mathf.Exp(-(IsSwimming ? 3f : .65f) * dt);
        if (SimulateGrapple(command, dt)) return;
        if (!IsKnockedBack && SimulateLadder(command, dt)) return;
        var ocean = OceanSurface.Instance;
        float water = ocean != null ? ocean.Height(transform.position) : float.NegativeInfinity;
        bool waterSupport = ocean != null && equipment != null && equipment.WaterRunning && transform.position.y >= water-1.2f;
        bool wasSwimming = IsSwimming;
        IsSwimming = !waterSupport && ocean != null && water - transform.position.y > (IsSwimming ? .65f : 1.1f);
        if (IsSwimming)
        {
            SimulateSwimming(command, dt, water, wasSwimming);
            return;
        }
        swimVelocity = Vector3.zero;
        Breath = Mathf.MoveTowards(Breath, breathSeconds, dt * 8f);
        var direction = transform.right * command.Move.x + transform.forward * command.Move.y;
        bool grounded = verticalVelocity <= 0 && (HasGround() || (waterSupport && transform.position.y <= water+.15f));
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
        cannonPushDirection=planar; cannonPushDelta=grounded ? dt : 0; pushedCannons.Clear();
        var flags = controller.Move((planar + knockbackVelocity + Vector3.up * verticalVelocity) * dt);
        if(waterSupport)
        {
            float surface=ocean.Height(transform.position)+.035f;
            if(transform.position.y<surface)
            { controller.Move(Vector3.up*(surface-transform.position.y)); if(verticalVelocity<0) verticalVelocity=0; }
        }
        if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f;
        cannonPushDelta=0;
        IsGrounded = verticalVelocity <= 0 && (HasGround() || (waterSupport && transform.position.y <= ocean.Height(transform.position)+.15f));
    }
    void SimulateSwimming(PlayerCommand command, float dt, float water, bool wasSwimming)
    {
        GetComponent<ShipDeckPassenger>()?.Attach(null);
        slideTimer = 0; IsGrounded = false;
        SetHeight(!CanStand());
        if (!wasSwimming) swimVelocity = Vector3.up * Mathf.Max(verticalVelocity * .25f, -4f);
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
        var flags = controller.Move((swimVelocity + knockbackVelocity) * dt);
        if ((flags & (CollisionFlags.Above | CollisionFlags.Below)) != 0) swimVelocity.y = 0;
        verticalVelocity = swimVelocity.y;
        PlanarSpeed = new Vector2(swimVelocity.x, swimVelocity.z).magnitude;
    }
    bool SimulateGrapple(PlayerCommand command, float dt)
    {
        var hook = GetComponent<PirateSlop.Networking.NetworkWeapon>();
        if (hook == null || (!hook.GrappleActive && !hook.BeingHooked)) return false;
        bool captured = hook.BeingHooked;
        if (!captured && (command.Jump || command.Release))
        {
            hook.ReleaseGrapple(); IsClimbing = false;
            ApplyKnockback(swimVelocity);
            return false;
        }
        if (!IsClimbing)
        {
            var passenger = GetComponent<ShipDeckPassenger>();
            var ship = passenger != null && passenger.Ship != null ? passenger.Ship.GetComponent<ShipController>() : null;
            swimVelocity = knockbackVelocity + Vector3.up * Mathf.Clamp(verticalVelocity, -8f, 8f);
            if (ship != null) swimVelocity += ship.CannonPointVelocity(transform.position);
        }
        locomotionLocked = false;
        GetComponent<ShipDeckPassenger>()?.Attach(null);
        IsClimbing = true; IsSwimming = false; IsGrounded = false;
        slideTimer = 0f; knockbackTime = 0f; ladderCooldown = .5f;
        Vector3 delta = hook.PullPoint - (transform.position + Vector3.up);
        float distance = delta.magnitude;
        Vector3 radial = distance > .001f ? delta / distance : Vector3.up;
        float desiredSpeed = Mathf.Min(14f, distance * 3f);
        var anchorBody = captured ? null : hook.GrappleBody;
        var anchorShip = anchorBody != null ? anchorBody.GetComponent<ShipController>() : null;
        Vector3 anchorVelocity = anchorShip != null ? anchorShip.CannonPointVelocity(hook.PullPoint) : Vector3.zero;
        Vector3 relativeVelocity = swimVelocity - anchorVelocity;
        float tension = Mathf.Clamp((desiredSpeed - Vector3.Dot(relativeVelocity, radial)) * 7f, -35f, 65f);
        Vector3 steering = captured ? Vector3.zero : Vector3.ProjectOnPlane(transform.right * command.Move.x + transform.forward * command.Move.y, radial) * 8f;
        relativeVelocity += (radial * tension + Vector3.down * 10f + steering) * dt;
        relativeVelocity *= Mathf.Exp(-.3f * dt);
        swimVelocity = anchorVelocity + Vector3.ClampMagnitude(relativeVelocity, 20f);
        var before = transform.position;
        var flags = controller.Move(swimVelocity * dt);
        Vector3 actual = (transform.position - before) / Mathf.Max(.001f, dt);
        if (flags != CollisionFlags.None) swimVelocity = actual;
        verticalVelocity = swimVelocity.y;
        knockbackVelocity = Vector3.ProjectOnPlane(swimVelocity, Vector3.up);
        PlanarSpeed = Vector3.ProjectOnPlane(actual, Vector3.up).magnitude;
        Breath = Mathf.MoveTowards(Breath, breathSeconds, dt * 8f);
        return true;
    }
    bool SimulateLadder(PlayerCommand command, float dt)
    {
        ShipLadder ladder = null;
        float best = float.PositiveInfinity;
        if (ladderCooldown <= 0) foreach (var candidate in ShipLadder.Active)
        {
            if (!candidate.Contains(transform.position, IsClimbing)) continue;
            if (candidate.RopeClimb && !IsClimbing && (!command.Use || !candidate.CanGrab(transform.position, command.Yaw, command.Pitch))) continue;
            var p = candidate.transform.InverseTransformPoint(transform.position);
            float depth = p.z - (candidate.RopeClimb ? candidate.RopeDepth(p.y) : 0f);
            float distance = p.x * p.x + depth * depth;
            if (distance < best) { best = distance; ladder = candidate; }
        }
        if (ladder == null) { IsClimbing = false; return false; }
        if (ladder.RopeClimb) return SimulateRigging(ladder, command, dt);
        var localPosition = ladder.transform.InverseTransformPoint(transform.position);
        var move = transform.right * command.Move.x + transform.forward * command.Move.y;
        float approach = Vector3.Dot(move, -ladder.transform.forward);
        bool fromTop = localPosition.z < .5f && localPosition.y > ladder.Height - 1.5f && approach < -.1f;
        if (!IsClimbing && (fromTop ? approach > -.1f : approach < .1f)) return false;
        var passenger = GetComponent<ShipDeckPassenger>();
        if (command.Jump)
        {
            IsClimbing = false; ladderCooldown = .65f; verticalVelocity = 4;
            passenger?.Attach(null);
            controller.Move((ladder.transform.forward * 4 + Vector3.up * 4) * dt);
            return false;
        }
        IsClimbing = true; IsSwimming = false; IsGrounded = false; slideTimer = 0; swimVelocity = Vector3.zero;
        SetHeight(false); Breath = Mathf.MoveTowards(Breath, breathSeconds, dt * 8);
        passenger?.Attach(ladder.Body);
        float vertical = command.Move.y * (Vector3.Dot(transform.forward, -ladder.transform.forward) >= 0 ? 1 : -1);
        if (command.Crouch || command.Pitch > 55) vertical = -Mathf.Abs(command.Move.y);
        float sideSign = Vector3.Dot(transform.right, ladder.transform.right) >= 0 ? 1f : -1f;
        Vector3 velocity = ladder.transform.up * vertical * ladder.Speed + ladder.transform.right * command.Move.x * sideSign * walkSpeed;
        if (fromTop)
        {
            velocity = ladder.transform.forward * Mathf.Abs(command.Move.y) * ladder.Speed;
            if (localPosition.y < ladder.Height + .05f) velocity += ladder.transform.up * ladder.Speed;
        }
        else if (localPosition.y >= ladder.Height && vertical > 0)
        {
            velocity = -ladder.transform.forward * ladder.Speed + ladder.transform.up * .3f;
        }
        else velocity += ladder.transform.forward * Mathf.Clamp((.65f - localPosition.z) * 5, -2, 2);
        controller.Move(velocity * dt); verticalVelocity = 0; PlanarSpeed = new Vector2(vertical * ladder.Speed, command.Move.x * walkSpeed).magnitude;
        localPosition = ladder.transform.InverseTransformPoint(transform.position);
        if (localPosition.y >= ladder.Height && localPosition.z < -ladder.ExitDepth && !fromTop || localPosition.y <= 0 && vertical < 0 || Mathf.Abs(localPosition.x) > ladder.HalfWidth + .2f)
        { IsClimbing = false; ladderCooldown = .3f; }
        return true;
    }
    void OnGUI()
    {
        if (!InputActive) return;
        if (IsClimbing)
        {
            PirateSlop.PirateHudStyle.Panel(new Rect(Screen.width * .5f - 260, Screen.height - 260, 520, 28), "W/S — вверх/вниз · A/D — в сторону · Space — отпустить");
            return;
        }
        foreach (var ladder in ShipLadder.Active)
            if (ladder.CanGrab(transform.position, lookYaw, pitch))
            {
                PirateSlop.PirateHudStyle.Panel(new Rect(Screen.width * .5f - 180, Screen.height - 260, 360, 28), "E — схватиться за ванты");
                break;
            }
    }
    bool SimulateRigging(ShipLadder ladder, PlayerCommand command, float dt)
    {
        var passenger = GetComponent<ShipDeckPassenger>();
        if (command.Jump || command.Release || (IsClimbing && command.Use))
        {
            IsClimbing = false; ladderCooldown = .75f;
            verticalVelocity = command.Jump ? 4f : 0f;
            passenger?.Attach(null);
            controller.Move(ladder.transform.forward * 2f * dt);
            return false;
        }
        IsClimbing = true; IsSwimming = false; IsGrounded = false; slideTimer = 0; swimVelocity = Vector3.zero;
        SetHeight(false); passenger?.Attach(ladder.Body);
        var p = ladder.transform.InverseTransformPoint(transform.position);
        float rise = command.Crouch ? -Mathf.Abs(command.Move.y) : command.Move.y;
        Vector3 target;
        if (p.y >= ladder.Height - .05f && rise > 0)
        {
            target = ladder.transform.TransformPoint(ladder.ExitPoint);
            controller.Move(Vector3.ClampMagnitude(target - transform.position, ladder.Speed * dt));
            if (Vector3.Distance(transform.position, target) < .2f) { IsClimbing = false; ladderCooldown = .5f; }
        }
        else
        {
            float height = Mathf.Clamp(p.y + rise * ladder.Speed * dt, 0, ladder.Height);
            float sideSign = Vector3.Dot(transform.right, ladder.transform.right) >= 0 ? 1f : -1f;
            float sideways = Mathf.Clamp(p.x + command.Move.x * sideSign * ladder.Speed * .45f * dt, -.6f, .6f);
            target = ladder.transform.TransformPoint(new Vector3(sideways, height, ladder.RopeDepth(height) + .5f));
            controller.Move(Vector3.ClampMagnitude(target - transform.position, ladder.Speed * 1.5f * dt));
            if (height <= 0 && rise < 0) { IsClimbing = false; ladderCooldown = .5f; }
        }
        verticalVelocity = 0; PlanarSpeed = new Vector2(rise, command.Move.x * .45f).magnitude * ladder.Speed;
        return true;
    }
    public PlayerState Capture() => new PlayerState { Position = transform.position, Yaw = transform.eulerAngles.y, VerticalVelocity = verticalVelocity, SlideDirection = slideDirection, SlideTimer = slideTimer, Cooldown = cooldown, Crouched = crouched, Locked = locomotionLocked, PlanarSpeed = PlanarSpeed, Grounded = IsGrounded, Swimming = IsSwimming, SwimVelocity = swimVelocity, Breath = Breath, Climbing = IsClimbing, LadderCooldown = ladderCooldown, KnockbackVelocity = knockbackVelocity, KnockbackTime = knockbackTime };
    public void Restore(PlayerState s)
    {
        controller.enabled = false; transform.SetPositionAndRotation(s.Position, Quaternion.Euler(0, s.Yaw, 0)); controller.enabled = !IsDead;
        verticalVelocity = s.VerticalVelocity; slideTimer = s.SlideTimer; cooldown = s.Cooldown; slideDirection = s.SlideDirection; ApplyAnimationState(s);
    }
    public void ApplyAnimationState(PlayerState s)
    {
        PlanarSpeed = s.PlanarSpeed; IsGrounded = s.Grounded; verticalVelocity = s.VerticalVelocity;
        IsSwimming = s.Swimming; swimVelocity = s.SwimVelocity; Breath = s.Swimming ? s.Breath : breathSeconds;
        IsClimbing = s.Climbing; ladderCooldown = s.LadderCooldown;
        knockbackVelocity = s.KnockbackVelocity; knockbackTime = s.KnockbackTime;
        slideTimer = s.SlideTimer; locomotionLocked = s.Locked; SetHeight(s.Crouched);
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


