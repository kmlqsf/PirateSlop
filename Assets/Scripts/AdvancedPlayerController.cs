using UnityEngine;
using UnityEngine.InputSystem;
using PirateSlop;

[RequireComponent(typeof(CharacterController))]
public partial class AdvancedPlayerController : MonoBehaviour
{
    [SerializeField] float walkSpeed = 5f, sprintSpeed = 8f, crouchSpeed = 2.5f, slideSpeed = 6f;
    [SerializeField] float jumpHeight = 1.2f, gravity = -25f;
    [SerializeField] float standingHeight = 1.8f, crouchHeight = 0.9f;
    [SerializeField] float slideDuration = 1.2f, slideCooldown = 1f, mouseSensitivity = 0.12f;
    [SerializeField] float thirdPersonDistance = 3f;
    [SerializeField, Min(100f)] float viewDistance = 1000f;
    [SerializeField] float swimSpeed = 3f, fastSwimSpeed = 4.5f, swimAcceleration = 6f, breathSeconds = 25f;
    Vector3 swimVelocity;
    Vector3 knockbackVelocity;
    Vector3 kickPushVelocity;
    public void ApplyKickPush(Vector3 velocity)
    {
        if (IsDead || IsFrozen || !float.IsFinite(velocity.sqrMagnitude)) return;
        kickPushVelocity = Vector3.ClampMagnitude(Vector3.ProjectOnPlane(velocity, Vector3.up), 11.3f);
        if (velocity.y > 0f)
        {
            verticalVelocity = Mathf.Max(verticalVelocity, Mathf.Min(velocity.y, 3.6f));
            IsGrounded = false;
            slideTimer = jumpBuffer = groundGrace = 0f;
        }
        if (IsClimbing) { IsClimbing = false; ladderCooldown = Mathf.Max(ladderCooldown, .35f); }
    }
    float knockbackTime, knockdownTime;
    public bool IsDowned => knockdownTime > 0f;
    public float KnockdownTime => knockdownTime;
    public Vector3 KnockdownVelocity => knockbackVelocity + Vector3.up * verticalVelocity;
    public void ClearKnockdown() => knockdownTime = 0f;
    public void ApplyKnockdown(Vector3 velocity, float duration)
    {
        if (IsDead || IsFrozen || !float.IsFinite(duration) || !float.IsFinite(velocity.sqrMagnitude)) return;
        bool starting = !IsDowned;
        kickPushVelocity = Vector3.zero;
        ActiveCannon?.ReleaseControl();
        ActiveHarpoon?.ReleaseControl();
        lootNetwork?.CancelLootWork();
        lootNetwork?.ReleaseGrapple();
        BellPullLocked = ShipActivityLocked = SailPullLocked = PickupLocked = false;
        ApplyKnockback(velocity);
        knockdownTime = Mathf.Clamp(duration, 1f, 5f);
        knockbackTime = ladderCooldown = knockdownTime;
        if (starting)
        {
            GetComponent<PlayerKnockdown>()?.Begin(KnockdownVelocity);
            GameAudio.Play(SoundCue.KnockdownBody, transform.position + Vector3.up, 1.5f, IsLocal);
        }
    }
    public bool IsKnockedBack => knockbackTime > 0f;
    public void ApplyKnockback(Vector3 velocity)
    {
        if (IsDead || IsFrozen || !float.IsFinite(velocity.sqrMagnitude)) return;
        foreach (var helm in HelmInteraction.Active)
            if (helm.IsControlledBy(this)) helm.ReleaseControl();
        SetLocomotionLocked(false);
        velocity *= Upgrade(UpgradeEffect.LeadResolve) ? RoguelikeTuning.Current.knockbackMultiplier : 1f;
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
    bool ladderExiting;
    public bool IsClimbing { get; private set; }
    public float ClimbVelocity { get; private set; }
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
    float pitch, verticalVelocity, slideTimer, cooldown, lookYaw, bodyYaw;
    Vector2 aimRecoil;
    public float AimSensitivityScale { get; set; } = 1;
    public Vector3 AimEuler => new Vector3(Mathf.Clamp(pitch-aimRecoil.x,-85,85),lookYaw+aimRecoil.y,0);
    public Vector3 AimDirection => Quaternion.Euler(AimEuler)*Vector3.forward;
    public void AddAimRecoil(float vertical,float horizontal) { if(local) aimRecoil+=new Vector2(vertical,horizontal); }
    float cameraHeight;
    Transform turningShip;
    float turningShipYaw;
    float jumpBuffer, groundGrace;
    public Vector2 LocalMove => pending.Move;
    public bool IsSprinting => pending.Sprint && !crouched;
    public FirstPersonMotion ViewMotion { get; private set; }
    Vector3 slideDirection;
    bool crouched, networked, local = true;
    public bool IsLocal => local;
    PlayerCommand pending;
    CombatHealth health;
    PirateSlop.Networking.NetworkHealth networkHealth;
    public bool IsFrozen => networkHealth != null && networkHealth.IsFrozen;
    PlayerInventory inventory;
    PirateSlop.Networking.NetworkEquipment equipment;
    DirectShipControls shipControls;
    PirateSlop.Ships.ShipV3PlayerInteraction shipV3Interaction;
    public bool IsDead => health != null && health.IsDead;
    bool locomotionLocked;
    public SimpleCannon ActiveCannon { get; set; }
    public PirateSlop.Harpoon.HarpoonGun ActiveHarpoon { get; set; }
    public bool PickupLocked { get; set; }
    public PirateSlop.Networking.NetworkParrotDrone ActiveParrot { get; set; }
    public bool BellPullLocked { get; set; }
    public bool ShipActivityLocked { get; set; }
    public bool SailPullLocked { get; set; }
    public float LookSensitivity => mouseSensitivity;
    public bool OtherLocomotionLocked => IsFrozen || ActiveParrot != null || BellPullLocked || ShipActivityLocked || locomotionLocked || PickupLocked || ActiveCannon != null || ActiveHarpoon != null || (lootNetwork != null && lootNetwork.LootWorkLocked);
    public bool LocomotionLocked => SailPullLocked || OtherLocomotionLocked;
    PirateSlop.Networking.NetworkWeapon lootNetwork;
    public bool IsSliding => slideTimer > 0f;
    public bool IsCrouched => crouched;
    float crouchBlend;
    public float CrouchBlend => crouchBlend;
    public float PlanarSpeed { get; private set; }
    public bool InputActive => !IsFrozen && !RoguelikeUpgradeUI.BlocksInput && ActiveParrot == null && !BellPullLocked && !ShipActivityLocked && !DeveloperMenu.IsOpen && !BotDebugPanel.ConsumedInput && !ShipSpyglassView.IsViewing && local && !IsDead && !IsDowned && !(GetComponent<PlayerKnockdown>()?.IsRecovering ?? false) && Cursor.lockState == CursorLockMode.Locked;
    public Camera PlayerCamera => playerCamera;
    void Awake()
    {
        WaterImpactBody.Ensure(gameObject);
        health = GetComponent<CombatHealth>();
        networkHealth = GetComponent<PirateSlop.Networking.NetworkHealth>();
        if (GetComponent<PlayerFreezeScreen>() == null) gameObject.AddComponent<PlayerFreezeScreen>();
        if (GetComponent<PlayerKnockdown>() == null) gameObject.AddComponent<PlayerKnockdown>();
        lootNetwork = GetComponent<PirateSlop.Networking.NetworkWeapon>();
        inventory = GetComponent<PlayerInventory>();
        equipment = GetComponent<PirateSlop.Networking.NetworkEquipment>();
        shipControls = GetComponent<DirectShipControls>();
        shipV3Interaction = GetComponent<PirateSlop.Ships.ShipV3PlayerInteraction>();
        controller = GetComponent<CharacterController>(); playerCamera = GetComponentInChildren<Camera>(true);
        ConfigureDistanceCulling();
        if (GetComponent<ContextPrompt>() == null) gameObject.AddComponent<ContextPrompt>();
        ViewMotion = GetComponent<FirstPersonMotion>();
        if (ViewMotion == null) ViewMotion = gameObject.AddComponent<FirstPersonMotion>();
        modelVisibility = GetComponentsInChildren<FirstPersonModelVisibility>(true);
        lookYaw = transform.eulerAngles.y; bodyYaw = lookYaw; SetHeight(false);
        cameraHeight = standingHeight - .15f;
        Breath = breathSeconds;
        if (GetComponent<SwimPresentation>() == null) gameObject.AddComponent<SwimPresentation>();
    }
    void Start() { if (local) SetCursor(true); }
    void ConfigureDistanceCulling()
    {
        if (playerCamera == null) return;
        var distances = playerCamera.layerCullDistances;
        float maximum = 0f;
        for (int i = 0; i < distances.Length; i++)
        {
            if (distances[i] <= 0f) distances[i] = viewDistance;
            maximum = Mathf.Max(maximum, distances[i]);
        }
        playerCamera.layerCullDistances = distances;
        playerCamera.layerCullSpherical = true;
        playerCamera.farClipPlane = Mathf.Max(playerCamera.farClipPlane, Mathf.Max(5000f, maximum * 2f));
    }
    void OnDisable() { if (local) SetCursor(false); }
    public static void SetCursor(bool locked) { Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None; Cursor.visible = !locked; }
    public void ConfigureNetwork(bool owner)
    {
        networked = true; local = owner;
        if (playerCamera != null) { playerCamera.enabled = owner; var listener = playerCamera.GetComponent<AudioListener>(); if (listener != null) listener.enabled = owner; }
        if (owner) { lookYaw = transform.eulerAngles.y; bodyYaw = lookYaw; SetCursor(true); }
    }
    public void LookAtPoint(Vector3 worldPoint, float speed = 14f)
    {
        if (playerCamera == null) return;
        Vector3 dir = worldPoint - playerCamera.transform.position;
        if (dir.sqrMagnitude < 0.001f) return;
        float targetYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        float targetPitch = -Mathf.Asin(Mathf.Clamp(dir.y / dir.magnitude, -0.99f, 0.99f)) * Mathf.Rad2Deg;
        lookYaw = Mathf.LerpAngle(lookYaw, targetYaw, 1f - Mathf.Exp(-speed * Time.deltaTime));
        pitch = Mathf.Lerp(pitch, targetPitch, 1f - Mathf.Exp(-speed * Time.deltaTime));
    }
    public void SetLookAngles(float yaw, float elevation)
    {
        if (!local) return;
        lookYaw = Mathf.Repeat(yaw, 360f);
        bodyYaw = lookYaw;
        pitch = Mathf.Clamp(elevation, -85f, 85f);
        aimRecoil = Vector2.zero;
        pending.Yaw = lookYaw;
        pending.Pitch = pitch;
    }
    public void SetLocomotionLocked(bool value)
    {
        if (locomotionLocked == value) return;
        locomotionLocked = value; verticalVelocity = 0; slideTimer = 0; PlanarSpeed = 0;
    }
    public PirateSlop.Networking.NetworkPlayer SpectatorTarget { get; private set; }

    void CycleSpectatorTarget()
    {
        var networkSelf = GetComponent<PirateSlop.Networking.NetworkPlayer>();
        if (networkSelf == null) return;
        var teammates = new System.Collections.Generic.List<PirateSlop.Networking.NetworkPlayer>();
        foreach (var p in PirateSlop.Networking.NetworkPlayer.Active)
        {
            if (p.IsSpawned && p.TeamId.Value == networkSelf.TeamId.Value && p != networkSelf && !p.Eliminated.Value && p.Motor != null && !p.Motor.IsDead)
                teammates.Add(p);
        }
        if (teammates.Count == 0)
        {
            SpectatorTarget = null;
            return;
        }
        int index = SpectatorTarget != null ? teammates.IndexOf(SpectatorTarget) : -1;
        index = (index + 1) % teammates.Count;
        SpectatorTarget = teammates[index];
    }

    void Update()
    {
        if (!local) return;
        if (RoguelikeUpgradeUI.BlocksInput)
        {
            pending = new PlayerCommand { Yaw = lookYaw, Pitch = pitch, Release = true };
            return;
        }
        if (IsDead)
        {
            UpdateDeathSpectator();
            pending = new PlayerCommand { Yaw = lookYaw, Pitch = pitch, Release = true };
            return;
        }
        ResetDeathSpectator();
        if (BotDebugPanel.ConsumedInput) { pending = new PlayerCommand { Yaw = lookYaw, Pitch = pitch, Release = true }; return; }
        aimRecoil=Vector2.Lerp(aimRecoil,Vector2.zero,1-Mathf.Exp(-7*Time.deltaTime));
        var kb = Keyboard.current; var mouse = Mouse.current;
        if (kb == null) return;
        if (kb.f1Key.wasPressedThisFrame && ActiveCannon == null && ActiveHarpoon == null && ActiveParrot == null && !BellPullLocked) SetThirdPerson(!IsThirdPerson);
        if (kb.escapeKey.wasPressedThisFrame) { pending.Release = true; SetCursor(false); }
        if (mouse != null && mouse.leftButton.wasPressedThisFrame && !PlayerInventory.LootWindowOpen && !PirateSlop.Networking.SessionController.MenuOpen) SetCursor(true);
        if (InputActive && ActiveCannon == null && ActiveHarpoon == null && mouse != null && (lootNetwork == null || !lootNetwork.LootWorkLocked) && (shipControls == null || !shipControls.IsDragging)) { var d = mouse.delta.ReadValue() * mouseSensitivity * AimSensitivityScale; lookYaw = Mathf.Repeat(lookYaw + d.x, 360f); pitch = Mathf.Clamp(pitch - d.y, -85f, 85f); }
        if (IsThirdPerson && mouse != null && (inventory == null || !inventory.PlacementActive))
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f) thirdPersonDistance = Mathf.Clamp(thirdPersonDistance - Mathf.Sign(scroll) * 0.5f, 1.2f, 6f);
        }
        var rawMove = InputActive ? Vector2.ClampMagnitude(new Vector2((kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1 : 0) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1 : 0), (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1 : 0) - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1 : 0)), 1) : Vector2.zero;
        if (IsClimbing)
        {
            pending.Move = rawMove;
            pending.Yaw = bodyYaw;
        }
        else if (IsThirdPerson)
        {
            if (rawMove.sqrMagnitude > 0.001f)
            {
                var camForward = Quaternion.Euler(0, lookYaw, 0) * Vector3.forward;
                var camRight = Quaternion.Euler(0, lookYaw, 0) * Vector3.right;
                var moveWorld = camRight * rawMove.x + camForward * rawMove.y;
                float moveAngle = Mathf.Atan2(moveWorld.x, moveWorld.z) * Mathf.Rad2Deg;
                bodyYaw = Mathf.MoveTowardsAngle(bodyYaw, moveAngle, 720f * Time.deltaTime);
                var localMove = Quaternion.Euler(0, -bodyYaw, 0) * moveWorld;
                pending.Move = new Vector2(localMove.x, localMove.z);
            }
            else pending.Move = Vector2.zero;
            pending.Yaw = bodyYaw;
        }
        else
        {
            bodyYaw = lookYaw;
            pending.Move = rawMove;
            pending.Yaw = lookYaw;
        }
        pending.Pitch = pitch;
        pending.Rise = InputActive && kb.spaceKey.isPressed;
        pending.Sprint = InputActive && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
        pending.Crouch = InputActive && (kb.cKey.isPressed || kb.leftCtrlKey.isPressed);
        pending.Slide |= InputActive && kb.cKey.wasPressedThisFrame;
        pending.Jump |= InputActive && kb.spaceKey.wasPressedThisFrame;
        if (shipV3Interaction == null) shipV3Interaction = GetComponent<PirateSlop.Ships.ShipV3PlayerInteraction>();
        var pumpInput = GetComponent<PirateSlop.Ships.ShipBilgePumpPlayer>();
        if (shipV3Interaction != null && shipV3Interaction.ConsumedInput || pumpInput != null && pumpInput.ConsumedInput) pending.Use = false;
        else pending.Use |= InputActive && (kb.eKey.wasPressedThisFrame || kb.fKey.wasPressedThisFrame) && (IsSwimming || LocomotionLocked || inventory == null || !inventory.AimingAtPickup());
        pending.Release |= kb.qKey.wasPressedThisFrame || !InputActive;
        if (!networked) Simulate(ConsumeCommand(), Time.deltaTime);
    }
    void LateUpdate()
    {
        if (!local || playerCamera == null) return;
        if (IsDead)
        {
            PositionDeathSpectator();
            return;
        }
        if (IsDowned && !IsThirdPerson && GetComponent<PlayerKnockdown>()?.PositionCamera() == true) return;
        var helm = shipControls != null ? shipControls.TurningHelm : null;
        var ship = helm != null ? helm.GetComponentInParent<ShipController>() : null;
        if (ship != null)
        {
            float shipYaw = ship.transform.eulerAngles.y;
            if (turningShip == ship.transform) AddPlatformYaw(Mathf.DeltaAngle(turningShipYaw, shipYaw));
            turningShip = ship.transform;
            turningShipYaw = shipYaw;
        }
        else turningShip = null;
        if (ActiveHarpoon != null && ActiveHarpoon.CameraMount != null)
        {
            playerCamera.transform.SetPositionAndRotation(ActiveHarpoon.CameraMount.position, ActiveHarpoon.CameraMount.rotation);
            return;
        }
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
        var cameraAim = AimEuler;
        cameraAim.z = IsDowned ? 58f * Mathf.Clamp01(knockdownTime / .45f) : 0f;
        playerCamera.transform.rotation = Quaternion.Euler(cameraAim);
        cameraHeight = Mathf.Lerp(cameraHeight, IsDowned ? Mathf.Lerp(standingHeight - .15f, .38f, Mathf.Clamp01(knockdownTime / .45f)) : (crouched ? crouchHeight : standingHeight) - .15f, 1f - Mathf.Exp(-16f * Time.deltaTime));
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
        if (value && !IsThirdPerson) bodyYaw = transform.eulerAngles.y;
        else if (!value && IsThirdPerson) lookYaw = bodyYaw;
        IsThirdPerson = value;
        foreach (var visibility in modelVisibility) visibility.SetFirstPerson(!value);
    }
    public PlayerCommand ConsumeCommand()
    {
        var result = pending; pending.Jump = pending.Slide = pending.Use = pending.Release = false; return result;
    }
    public void BeginFreeze()
    {
        ClearFrozenMotion();
        pending = new PlayerCommand { Yaw = transform.eulerAngles.y };
        foreach (var helm in HelmInteraction.Active) if (helm.IsControlledBy(this)) helm.ReleaseControl();
        ActiveCannon?.ReleaseControl();
        ActiveHarpoon?.ReleaseControl();
        lootNetwork?.CancelLootWork();
        lootNetwork?.ReleaseGrapple();
        var participant = GetComponent<PirateSlop.Networking.NetworkPlayer>();
        if (participant != null && participant.IsServerInitialized)
            foreach (var ship in PirateSlop.Networking.NetworkShip.ActiveShips) if (ship != null) ship.GetComponent<SailSystem>()?.ReleasePlayer(this);
        locomotionLocked = PickupLocked = BellPullLocked = SailPullLocked = false;
    }
    void ClearFrozenMotion()
    {
        PlanarSpeed = verticalVelocity = slideTimer = jumpBuffer = groundGrace = knockbackTime = 0f;
        swimVelocity = knockbackVelocity = Vector3.zero;
        kickPushVelocity = Vector3.zero;
        ClimbVelocity = 0f;
    }
    public void AddPlatformYaw(float delta) { if (local) { lookYaw = Mathf.Repeat(lookYaw + delta, 360); bodyYaw = Mathf.Repeat(bodyYaw + delta, 360); } }
    public void Simulate(PlayerCommand command, float dt)
    {
        if (IsDead || controller == null || !controller.enabled || !float.IsFinite(dt) || dt <= 0f) { jumpBuffer = groundGrace = 0f; return; }
        if (IsFrozen) { ClearFrozenMotion(); return; }
        if (!command.IsValid) command = default;
        bool downed = IsDowned;
        if (downed) command = new PlayerCommand { Yaw = transform.eulerAngles.y, Release = true };
        knockdownTime = Mathf.Max(0f, knockdownTime - dt);
        var fall = downed ? GetComponent<PlayerKnockdown>() : null;
        if (fall != null && fall.TryGetPhysicsState(knockdownTime <= 0f, out var fallPosition, out var fallVelocity))
        {
            controller.enabled = false;
            transform.position = fallPosition;
            controller.enabled = true;
            GetComponent<ShipDeckPassenger>()?.Attach(null);
            knockbackVelocity = Vector3.ProjectOnPlane(fallVelocity, Vector3.up);
            verticalVelocity = fallVelocity.y;
            IsGrounded = IsSwimming = IsClimbing = false;
            PlanarSpeed = 0f;
            slideTimer = jumpBuffer = groundGrace = 0f;
            knockbackTime = ladderCooldown = knockdownTime;
            if (knockdownTime > 0f) return;
        }
        transform.rotation = Quaternion.Euler(0, command.Yaw, 0);
        cooldown = Mathf.Max(0, cooldown - dt);
        jumpBuffer = command.Jump ? .12f : Mathf.Max(0f, jumpBuffer - dt);
        groundGrace = Mathf.Max(0f, groundGrace - dt);
        if (!downed && LocomotionLocked && !(GetComponent<PirateSlop.Networking.NetworkWeapon>()?.BeingHooked ?? false)) { PlanarSpeed = 0f; verticalVelocity = 0f; slideTimer = 0f; jumpBuffer = groundGrace = 0f; return; }
        ladderCooldown = Mathf.Max(0, ladderCooldown - dt);
        knockbackTime = Mathf.Max(0f, knockbackTime - dt);
        knockbackVelocity *= Mathf.Exp(-(IsSwimming ? 3f : downed && IsGrounded ? 4f : .65f) * dt);
        kickPushVelocity *= Mathf.Exp(-6f * dt);
        if (!downed && SimulateGrapple(command, dt)) { jumpBuffer = groundGrace = 0f; return; }
        if (!IsKnockedBack && SimulateLadder(command, dt)) { jumpBuffer = groundGrace = 0f; return; }
        var ocean = OceanSurface.Instance;
        float water = ocean != null ? ocean.Height(transform.position) : float.NegativeInfinity;
        bool insideHull = PirateSlop.Ships.ShipWaterInterior.TryWaterHeight(transform.position, out float bilgeWater);
        if (insideHull) water = bilgeWater;
        var deckPassenger = GetComponent<ShipDeckPassenger>();
        bool dryDeck = !insideHull && PirateSlop.World.EnvironmentTestGallery.IsTest(PirateSlop.World.ProceduralWorld.Instance != null ? PirateSlop.World.ProceduralWorld.Instance.Layout : null)
            && !IsKnockedBack && deckPassenger != null && deckPassenger.HasDryDeckSupport();
        bool solidGround = verticalVelocity <= 0 && HasGround() && !IsSwimming;
        bool upgradeWater = SimulateUpgradeWater(dt, solidGround);
        bool waterSupport = ocean != null && ((equipment != null && equipment.WaterRunning) || upgradeWater) && transform.position.y >= water-1.2f;
        bool wasSwimming = IsSwimming;
        bool risingFromWater = !wasSwimming && verticalVelocity > 0f;
        IsSwimming = !dryDeck && !risingFromWater && !waterSupport && ocean != null && water - transform.position.y > (wasSwimming ? .65f : 1.1f);
        if (IsSwimming && jumpBuffer > 0f && !command.Crouch && !IsKnockedBack && water - transform.position.y <= 1.4f && CanStand())
        {
            GetComponent<ShipDeckPassenger>()?.Attach(null);
            verticalVelocity = Mathf.Sqrt((Mathf.Max(0f, water - transform.position.y) + jumpHeight * (Upgrade(UpgradeEffect.DeckAcrobat) ? RoguelikeTuning.Current.jumpHeightMultiplier : 1f)) * -2f * gravity);
            IsSwimming = IsGrounded = false;
            slideTimer = jumpBuffer = groundGrace = 0f;
        }
        if (IsSwimming)
        {
            jumpBuffer = groundGrace = 0f;
            SimulateSwimming(command, dt, water, wasSwimming);
            return;
        }
        swimVelocity = Vector3.zero;
        Breath = Mathf.MoveTowards(Breath, breathSeconds, dt * 8f);
        var direction = transform.right * command.Move.x + transform.forward * command.Move.y;
        bool grounded = verticalVelocity <= 0 && (HasGround() || (waterSupport && transform.position.y <= water+.15f));
        if (grounded && !IsKnockedBack) groundGrace = .10f;
        if (command.Slide && command.Sprint && direction.sqrMagnitude > .1f && grounded && !crouched && cooldown <= 0) { slideTimer = slideDuration; cooldown = slideDuration + slideCooldown; slideDirection = direction.normalized; }
        if (IsSliding) { slideTimer = Mathf.Max(0, slideTimer - dt); if (!grounded) slideTimer = 0; }
        bool wantCrouch = downed || command.Crouch || IsSliding;
        if (!wantCrouch && crouched && !CanStand()) wantCrouch = true;
        SetHeight(wantCrouch);
        crouchBlend = Mathf.MoveTowards(crouchBlend, crouched ? 1f : 0f, dt * 6f);
        float slideProgress = 1f - slideTimer / Mathf.Max(.01f, slideDuration);
        var planar = IsSliding ? slideDirection * Mathf.Lerp(slideSpeed, crouchSpeed, slideProgress * slideProgress) : direction * Mathf.Lerp(command.Sprint ? sprintSpeed : walkSpeed, crouchSpeed, crouchBlend);
        if (command.Sprint && !crouched && !IsSliding && Upgrade(UpgradeEffect.LightBoots)) planar *= RoguelikeTuning.Current.runMultiplier;
        PlanarSpeed = planar.magnitude;
        if (solidGround) airJumpUsed = false;
        if (grounded && verticalVelocity < 0) verticalVelocity = -2;
        if (jumpBuffer > 0f && groundGrace > 0f && !crouched && !IsKnockedBack)
        { verticalVelocity = Mathf.Sqrt(jumpHeight * (Upgrade(UpgradeEffect.DeckAcrobat) ? RoguelikeTuning.Current.jumpHeightMultiplier : 1f) * -2 * gravity); jumpBuffer = groundGrace = 0f; grounded = false; }
        else if (command.Jump && !grounded && !airJumpUsed && !crouched && !IsKnockedBack && Upgrade(UpgradeEffect.DoubleJump))
        { verticalVelocity = Mathf.Sqrt(jumpHeight * -2 * gravity); airJumpUsed = true; jumpBuffer = groundGrace = 0f; if (IsLocal && Time.unscaledTime >= airJumpAudioAt) { GameAudio.Play(SoundCue.AirJump, transform.position); airJumpAudioAt = Time.unscaledTime + .25f; } }
        verticalVelocity += gravity * dt;
        controller.stepOffset = grounded ? Mathf.Min(.32f, controller.height * .4f) : 0f;
        cannonPushDirection=planar; cannonPushDelta=grounded ? dt : 0; pushedCannons.Clear();
        var flags = controller.Move((planar + knockbackVelocity + kickPushVelocity + Vector3.up * verticalVelocity) * dt);
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
        if (Breath <= 0 && health != null && (!networked || GetComponent<PirateSlop.Networking.NetworkPlayer>().IsServerInitialized)) health.Damage(12f * dt, drowning: true);
        var direction = transform.right * command.Move.x + transform.forward * command.Move.y;
        float vertical = command.Rise ? 1f : command.Crouch ? -1f : 0f;
        if (submerged && vertical == 0 && command.Move.y != 0)
        {
            float angle = command.Pitch * Mathf.Deg2Rad;
            direction = transform.right * command.Move.x + transform.forward * (command.Move.y * Mathf.Cos(angle));
            vertical = -Mathf.Sin(angle) * command.Move.y;
        }
        var target = Vector3.ClampMagnitude(direction + Vector3.up * vertical, 1) * UpgradeSwimSpeed(submerged, command.Sprint);
        if (!command.Crouch && !submerged && !command.Rise) target.y = Mathf.Clamp((water - 1.25f - transform.position.y) * 5f, -2f, 2f);
        else if (command.Rise && transform.position.y > water - 1.2f) target.y = 0;
        else if (submerged && Mathf.Abs(vertical) < .05f) target.y = .35f;
        swimVelocity = Vector3.MoveTowards(swimVelocity, target, swimAcceleration * dt);
        var flags = controller.Move((swimVelocity + knockbackVelocity + kickPushVelocity) * dt);
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
            if (!IsClimbing && !ladderExiting && (!command.Use || !candidate.CanGrab(transform.position, command.Yaw, command.Pitch))) continue;
            if ((IsClimbing || ladderExiting) && !candidate.Contains(transform.position, true)) continue;
            var p = candidate.transform.InverseTransformPoint(transform.position);
            float depth = p.z - (candidate.RopeClimb ? candidate.RopeDepth(p.y) : 0f);
            float side = p.x - candidate.RopeSide(p.y);
            float distance = side * side + depth * depth;
            if (distance < best) { best = distance; ladder = candidate; }
        }
        if (ladder == null) { IsClimbing = false; ladderExiting = false; return false; }
        if (ladder.RopeClimb) return SimulateRigging(ladder, command, dt);
        var localPosition = ladder.transform.InverseTransformPoint(transform.position);
        var move = transform.right * command.Move.x + transform.forward * command.Move.y;
        float approach = Vector3.Dot(move, -ladder.transform.forward);
        bool fromTop = localPosition.z < .5f && localPosition.y > ladder.Height - 1.5f && approach < -.1f;
        var passenger = GetComponent<ShipDeckPassenger>();
        if (command.Jump || command.Release)
        {
            ladderExiting = false;
            IsClimbing = false; ladderCooldown = .65f; verticalVelocity = 4;
            passenger?.Attach(null);
            controller.Move((ladder.transform.forward * 4 + Vector3.up * 4) * dt);
            return false;
        }
        IsClimbing = true; IsSwimming = false; IsGrounded = false; slideTimer = 0; swimVelocity = Vector3.zero;
        SetHeight(false); Breath = Mathf.MoveTowards(Breath, breathSeconds, dt * 8);
        passenger?.Attach(ladder.Body);
        float ladderYaw = Quaternion.LookRotation(-ladder.transform.forward, Vector3.up).eulerAngles.y;
        bodyYaw = ladderYaw;
        transform.rotation = Quaternion.Euler(0, ladderYaw, 0);
        float vertical = command.Move.y;
        if (command.Crouch || command.Pitch > 55) vertical = -Mathf.Abs(command.Move.y);
        float sideSign = Vector3.Dot(transform.right, ladder.transform.right) >= 0 ? 1f : -1f;
        if (ladder.BoardingAccess && (ladderExiting || !fromTop && localPosition.y >= ladder.Height - .15f && vertical > 0f))
        {
            if (localPosition.y >= ladder.Height + ladder.ExitClearance - .08f) ladderExiting = true;
            var target = new Vector3(Mathf.Clamp(localPosition.x, -ladder.HalfWidth + .3f, ladder.HalfWidth - .3f), ladder.Height + ladder.ExitClearance,
                ladderExiting ? -ladder.ExitDepth : .38f);
            controller.Move(Vector3.ClampMagnitude(ladder.transform.TransformPoint(target) - transform.position, ladder.Speed * dt));
            verticalVelocity = 0f; PlanarSpeed = ladder.Speed;
            if (ladderExiting && ladder.transform.InverseTransformPoint(transform.position).z <= -ladder.ExitDepth + .15f)
            { IsClimbing = false; ladderExiting = false; ladderCooldown = .65f; }
            return true;
        }
        float climbSpeed = vertical < 0 ? ladder.Speed * 0.5f : ladder.Speed * 0.75f;
        float horizontal = command.Move.x;
        if (localPosition.x > ladder.HalfWidth - 0.15f && horizontal > 0) horizontal = 0;
        if (localPosition.x < -ladder.HalfWidth + 0.15f && horizontal < 0) horizontal = 0;
        float horizontalSpeed = ladder.Speed * 0.5f;
        Vector3 velocity = ladder.transform.up * vertical * climbSpeed + ladder.transform.right * horizontal * horizontalSpeed;
        if (fromTop)
        {
            velocity += ladder.transform.forward * Mathf.Abs(command.Move.y) * climbSpeed;
            if (localPosition.y < ladder.Height + .05f) velocity += ladder.transform.up * climbSpeed;
        }
        else if (localPosition.y >= ladder.Height && vertical > 0)
        {
            velocity += -ladder.transform.forward * climbSpeed + ladder.transform.up * .3f;
        }
        else velocity += ladder.transform.forward * Mathf.Clamp((.38f - localPosition.z) * 5, -2, 2);
        controller.Move(velocity * dt);
        verticalVelocity = 0;
        PlanarSpeed = Mathf.Max(Mathf.Abs(vertical * climbSpeed), Mathf.Abs(horizontal * horizontalSpeed));
        ClimbVelocity = vertical * climbSpeed;
        localPosition = ladder.transform.InverseTransformPoint(transform.position);
        bool dismountTop = !fromTop && localPosition.y >= ladder.Height && localPosition.z < -ladder.ExitDepth;
        bool dismountBottom = (localPosition.y <= 0.15f || controller.isGrounded) && vertical < 0;
        bool dismountSide = Mathf.Abs(localPosition.x) > ladder.HalfWidth + .25f;
        if (dismountTop || dismountBottom || dismountSide)
        {
            IsClimbing = false;
            ladderCooldown = .3f;
            passenger?.Attach(null);
            if (dismountBottom) controller.Move(ladder.transform.forward * 0.25f);
        }
        return true;
    }
    void OnGUI()
    {
        if (!InputActive) return;
        if (IsClimbing)
        {
            ContextPrompt.Offer("W/S — вверх/вниз · A/D — в сторону · Space — отпустить", 30);
            return;
        }
        foreach (var ladder in ShipLadder.Active)
            if (ladder.CanGrab(transform.position, lookYaw, pitch))
            {
                ContextPrompt.Offer("F — залезть", 30);
                break;
            }
    }
    bool SimulateRigging(ShipLadder ladder, PlayerCommand command, float dt)
    {
        var passenger = GetComponent<ShipDeckPassenger>();
        if (command.Jump || command.Release || (IsClimbing && command.Use))
        {
            IsClimbing = false; ladderCooldown = .75f;
            ladderExiting = false;
            verticalVelocity = command.Jump ? 4f : 0f;
            passenger?.Attach(null);
            controller.Move(ladder.transform.forward * 2f * dt);
            return false;
        }
        IsClimbing = true; IsSwimming = false; IsGrounded = false; slideTimer = 0; swimVelocity = Vector3.zero;
        SetHeight(false); passenger?.Attach(ladder.Body);
        float standOff = ladder.FollowRopePath ? ladder.StandOff(transform.position) : .38f;
        float ladderYaw = Quaternion.LookRotation(-ladder.transform.forward * (ladder.FollowRopePath ? Mathf.Sign(standOff) : 1f), Vector3.up).eulerAngles.y;
        bodyYaw = ladderYaw;
        transform.rotation = Quaternion.Euler(0, ladderYaw, 0);
        var p = ladder.transform.InverseTransformPoint(transform.position);
        float rise = command.Crouch ? -Mathf.Abs(command.Move.y) : command.Move.y;
        float ropeSpeed = ladder.Speed * (Upgrade(UpgradeEffect.RopeSprinter) ? RoguelikeTuning.Current.ropeMultiplier : 1f);
        float climbSpeed = rise < 0 ? ropeSpeed * 0.5f : ropeSpeed * 0.75f;
        Vector3 target;
        if (ladderExiting || p.y >= ladder.Height - .05f && rise > 0)
        {
            ladderExiting = true;
            IsClimbing = false;
            IsGrounded = true;
            target = ladder.transform.TransformPoint(ladder.ExitPoint);
            Vector3 walking = Vector3.ProjectOnPlane(target - transform.position, Vector3.up);
            if (walking.sqrMagnitude > .001f) { bodyYaw = Quaternion.LookRotation(walking).eulerAngles.y; transform.rotation = Quaternion.Euler(0, bodyYaw, 0); }
            controller.Move(Vector3.ClampMagnitude(target - transform.position, climbSpeed * dt));
            if (Vector3.Distance(transform.position, target) < .2f) { IsClimbing = false; ladderExiting = false; ladderCooldown = .5f; ClimbVelocity = 0f; IsGrounded = controller.isGrounded; return true; }
        }
        else
        {
            float height = Mathf.Clamp(p.y + rise * climbSpeed * dt, 0, ladder.Height);
            float sideSign = Vector3.Dot(transform.right, ladder.transform.right) >= 0 ? 1f : -1f;
            float sideways = ladder.RopeSide(height) + Mathf.Clamp(p.x - ladder.RopeSide(p.y) + command.Move.x * (ladder.FollowRopePath ? sideSign : 1f) * ropeSpeed * 0.5f * dt, -ladder.HalfWidth + 0.15f, ladder.HalfWidth - 0.15f);
            target = ladder.transform.TransformPoint(new Vector3(sideways, height, ladder.RopeDepth(height) + standOff));
            controller.Move(Vector3.ClampMagnitude(target - transform.position, climbSpeed * 1.5f * dt));
            if ((height <= 0.15f || controller.isGrounded) && rise < 0) { IsClimbing = false; ladderCooldown = .5f; passenger?.Attach(null); controller.Move(ladder.transform.forward * (standOff < 0f ? -.25f : .25f)); }
        }
        verticalVelocity = 0; PlanarSpeed = ladderExiting ? climbSpeed : Mathf.Abs(rise * climbSpeed); ClimbVelocity = ladderExiting ? 0f : rise * climbSpeed;
        return true;
    }
    public PlayerState Capture() => new PlayerState { Position = transform.position, Yaw = transform.eulerAngles.y, VerticalVelocity = verticalVelocity, SlideDirection = slideDirection, SlideTimer = slideTimer, Cooldown = cooldown, Crouched = crouched, CrouchBlend = crouchBlend, Locked = locomotionLocked, PlanarSpeed = PlanarSpeed, Grounded = IsGrounded, Swimming = IsSwimming, SwimVelocity = swimVelocity, Breath = Breath, Climbing = IsClimbing, LadderCooldown = ladderCooldown, KnockbackVelocity = knockbackVelocity, KickPushVelocity = kickPushVelocity, KnockbackTime = knockbackTime, KnockdownTime = knockdownTime, JumpBuffer = jumpBuffer, GroundGrace = groundGrace, LadderExiting = ladderExiting, AirJumpUsed = airJumpUsed, WaterReady = waterReady, WaterWasSolid = waterWasSolid, WaterRunRemaining = waterRunRemaining, WaterRecharge = waterRecharge };
    public void Restore(PlayerState s)
    {
        controller.enabled = false; transform.SetPositionAndRotation(s.Position, Quaternion.Euler(0, s.Yaw, 0)); controller.enabled = !IsDead;
        jumpBuffer = s.JumpBuffer; groundGrace = s.GroundGrace;
        airJumpUsed = s.AirJumpUsed; waterReady = s.WaterReady; waterWasSolid = s.WaterWasSolid; waterRunRemaining = s.WaterRunRemaining; waterRecharge = s.WaterRecharge;
        verticalVelocity = s.VerticalVelocity; slideTimer = s.SlideTimer; cooldown = s.Cooldown; slideDirection = s.SlideDirection; ApplyAnimationState(s);
    }
    public void ApplyAnimationState(PlayerState s)
    {
        PlanarSpeed = s.PlanarSpeed; IsGrounded = s.Grounded; verticalVelocity = s.VerticalVelocity;
        IsSwimming = s.Swimming; swimVelocity = s.SwimVelocity; Breath = s.Swimming ? s.Breath : breathSeconds;
        IsClimbing = s.Climbing; ladderCooldown = s.LadderCooldown; ladderExiting = s.LadderExiting;
        knockbackVelocity = s.KnockbackVelocity; kickPushVelocity = s.KickPushVelocity; knockbackTime = s.KnockbackTime; knockdownTime = s.KnockdownTime;
        slideTimer = s.SlideTimer; locomotionLocked = s.Locked; SetHeight(s.Crouched);
        crouchBlend = Mathf.Clamp01(s.CrouchBlend);
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
