using UnityEngine;
using PirateSlop;

[DefaultExecutionOrder(-100)]
public class HelmInteraction : MonoBehaviour
{
    public static readonly System.Collections.Generic.List<HelmInteraction> Active = new();
    void OnEnable() { Active.Add(this); }
    [SerializeField] float interactionRadius = 2.8f;
    [SerializeField, Min(1f)] float wheelTurnsToFullSteer = 2f;
    [SerializeField, Min(.01f)] float centeringSpeed = .35f;
    [SerializeField] Transform wheelMesh;
    [SerializeField] bool fixedWheelCenter;
    AdvancedPlayerController player;
    Quaternion wheelRest;
    Vector3 wheelRestPosition;
    float rudder, lastGrip, monkeyGripUntil, visualRudder;
    public bool Networked { get; set; }
    public bool StructurallyAvailable { get; set; } = true;
    public float CurrentRudderNormalized => rudder;
    public float LastHumanControlTime { get; private set; } = float.NegativeInfinity;
    public float DragToRudder(float target) => (rudder - Mathf.Clamp(target, -1f, 1f)) * 360f * wheelTurnsToFullSteer;
    public bool IsControlling { get; private set; }
    public AdvancedPlayerController Driver => IsControlling ? player : null;
    public Transform Wheel => wheelMesh;
    public bool IsControlledBy(AdvancedPlayerController candidate) => IsControlling && player == candidate;
    public void Configure(Transform wheel, bool fixCenter = false) { wheelMesh = wheel; fixedWheelCenter = fixCenter; if (wheelMesh != null) { wheelRest = wheelMesh.localRotation; wheelRestPosition = wheelMesh.localPosition; } }
    public void Bind(AdvancedPlayerController value) { player = value; }
    void Awake() { if (wheelMesh != null) { wheelRest = wheelMesh.localRotation; wheelRestPosition = wheelMesh.localPosition; } }
    public bool InRange(AdvancedPlayerController candidate) => StructurallyAvailable && candidate != null && Vector3.Distance(transform.position, candidate.transform.position + Vector3.up) <= interactionRadius;
    public bool TryTakeControl(AdvancedPlayerController candidate)
    {
        ValidateGrip();
        if (candidate != null && !candidate.IsDead && !candidate.IsFrozen && InRange(candidate) && IsControlling && player != candidate)
        {
            var incoming = candidate.GetComponent<PirateSlop.Networking.NetworkPlayer>();
            var current = player.GetComponent<PirateSlop.Networking.NetworkPlayer>();
            if (incoming != null && incoming.IsServerInitialized && !incoming.IsBot.Value && current != null && current.IsBot.Value && incoming.TeamId.Value == current.TeamId.Value)
            {
                player.SetLocomotionLocked(false);
                ReleaseControl();
            }
        }
        if (candidate == null || candidate.IsDead || candidate.IsFrozen || !InRange(candidate) || (IsControlling && player != candidate)) return false;
        player = candidate; IsControlling = true; lastGrip = Time.time;
        var participant = candidate.GetComponent<PirateSlop.Networking.NetworkPlayer>();
        if (participant != null && participant.IsServerInitialized && !participant.IsBot.Value) LastHumanControlTime = Time.time;
        return true;
    }
    public void Drag(AdvancedPlayerController candidate, float degrees, bool holding)
    {
        if (!holding) { if (IsControlledBy(candidate)) ReleaseControl(); return; }
        if (!float.IsFinite(degrees) || !TryTakeControl(candidate)) return;
        rudder = Mathf.Clamp(rudder - Mathf.Clamp(degrees, -180f, 180f) / (360f * wheelTurnsToFullSteer), -1f, 1f);
    }
    public void ReleaseControl() { IsControlling = false; player = null; }
    public bool MonkeyAdjust(float delta)
    {
        var ship = GetComponentInParent<PirateSlop.Networking.NetworkShip>();
        if (ship == null || !ship.IsServerInitialized || ship.IsSinking || IsControlling || !StructurallyAvailable || !float.IsFinite(delta)) return false;
        rudder = Mathf.Clamp(rudder + Mathf.Clamp(delta, -.1f, .1f), -1f, 1f);
        monkeyGripUntil = Time.time + .2f;
        UpdateWheel(); return true;
    }
    public void Restore(float value, bool controlling, AdvancedPlayerController driver)
    {
        rudder = value; player = driver; IsControlling = controlling;
    }
    public void ValidateGrip()
    {
        if (IsControlling && (player == null || player.IsDead || player.IsFrozen || !InRange(player) || Time.time - lastGrip > .75f)) ReleaseControl();
    }
    void OnDisable() { Active.Remove(this); ReleaseControl(); }
    public void Simulate(PlayerCommand command, AdvancedPlayerController candidate, float dt)
    {
        ValidateGrip();
        if (!IsControlling && Time.time >= monkeyGripUntil) rudder = Mathf.MoveTowards(rudder, 0f, centeringSpeed * dt);
    }
    void UpdateWheel()
    {
        if (wheelMesh == null) return;
        if (fixedWheelCenter) wheelMesh.localPosition = wheelRestPosition;
        wheelMesh.localRotation = wheelRest * Quaternion.AngleAxis(-visualRudder * 360f * wheelTurnsToFullSteer, Vector3.forward);
    }
    void Update() { if (!Networked) Simulate(default, null, Time.deltaTime); }
    void LateUpdate()
    {
        visualRudder = Mathf.Lerp(visualRudder, rudder, 1f - Mathf.Exp(-22f * Time.deltaTime));
        UpdateWheel();
    }
}
