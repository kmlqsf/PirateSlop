using UnityEngine;
using PirateSlop;

[DefaultExecutionOrder(-100)]
public class HelmInteraction : MonoBehaviour
{
    [SerializeField] float interactionRadius = 2.8f;
    [SerializeField, Min(1f)] float wheelTurnsToFullSteer = 2f;
    [SerializeField, Min(.01f)] float centeringSpeed = .35f;
    [SerializeField] Transform wheelMesh;
    AdvancedPlayerController player;
    Quaternion wheelRest;
    float rudder, lastGrip;
    public bool Networked { get; set; }
    public bool StructurallyAvailable { get; set; } = true;
    public float CurrentRudderNormalized => rudder;
    public float LastHumanControlTime { get; private set; } = float.NegativeInfinity;
    public float DragToRudder(float target) => (rudder - Mathf.Clamp(target, -1f, 1f)) * 360f * wheelTurnsToFullSteer;
    public bool IsControlling { get; private set; }
    public AdvancedPlayerController Driver => IsControlling ? player : null;
    public Transform Wheel => wheelMesh;
    public bool IsControlledBy(AdvancedPlayerController candidate) => IsControlling && player == candidate;
    public void Configure(Transform wheel) { wheelMesh = wheel; if (wheelMesh != null) { wheelRest = wheelMesh.localRotation; HelmCenterMark.Add(wheelMesh); } }
    public void Bind(AdvancedPlayerController value) { player = value; }
    void Awake() { if (wheelMesh != null) { wheelRest = wheelMesh.localRotation; HelmCenterMark.Add(wheelMesh); } }
    public bool InRange(AdvancedPlayerController candidate) => StructurallyAvailable && candidate != null && Vector3.Distance(transform.position, candidate.transform.position + Vector3.up) <= interactionRadius;
    public bool TryTakeControl(AdvancedPlayerController candidate)
    {
        ValidateGrip();
        if (candidate != null && !candidate.IsDead && InRange(candidate) && IsControlling && player != candidate)
        {
            var incoming = candidate.GetComponent<PirateSlop.Networking.NetworkPlayer>();
            var current = player.GetComponent<PirateSlop.Networking.NetworkPlayer>();
            if (incoming != null && incoming.IsServerInitialized && !incoming.IsBot.Value && current != null && current.IsBot.Value && incoming.TeamId.Value == current.TeamId.Value)
            {
                player.SetLocomotionLocked(false);
                ReleaseControl();
            }
        }
        if (candidate == null || candidate.IsDead || !InRange(candidate) || (IsControlling && player != candidate)) return false;
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
        UpdateWheel();
    }
    public void ReleaseControl() { IsControlling = false; player = null; }
    public void Restore(float value, bool controlling, AdvancedPlayerController driver)
    {
        rudder = value; player = driver; IsControlling = controlling; UpdateWheel();
    }
    public void ValidateGrip()
    {
        if (IsControlling && (player == null || player.IsDead || !InRange(player) || Time.time - lastGrip > .75f)) ReleaseControl();
    }
    void OnDisable() { ReleaseControl(); }
    public void Simulate(PlayerCommand command, AdvancedPlayerController candidate, float dt)
    {
        ValidateGrip();
        if (!IsControlling) { rudder = Mathf.MoveTowards(rudder, 0f, centeringSpeed * dt); UpdateWheel(); }
    }
    void UpdateWheel() { if (wheelMesh != null) wheelMesh.localRotation = wheelRest * Quaternion.Euler(0, 0, -rudder * 360f * wheelTurnsToFullSteer); }
    void Update() { if (!Networked) Simulate(default, null, Time.deltaTime); }
}
