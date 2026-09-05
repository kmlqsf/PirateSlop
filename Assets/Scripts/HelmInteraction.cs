using UnityEngine;
using UnityEngine.InputSystem;
using PirateSlop;
[DefaultExecutionOrder(-100)]
public class HelmInteraction : MonoBehaviour
{
    [SerializeField] float interactionRadius = 2.2f, turnSpeed = 2f;
    [SerializeField] Transform wheelMesh;
    AdvancedPlayerController player;
    SailSystem sail;
    Quaternion wheelRest;
    float rudder;
    public bool Networked { get; set; }
    public float CurrentRudderNormalized => rudder;
    public bool IsControlling { get; private set; }
    public void Configure(Transform wheel) { wheelMesh = wheel; }
    public void Bind(AdvancedPlayerController value) { player = value; }
    void Awake() { sail = GetComponentInParent<SailSystem>(); if (wheelMesh != null) wheelRest = wheelMesh.localRotation; }
    void Start() { if (!Networked) { var go = GameObject.Find("PlayerCharacter"); if (go != null) player = go.GetComponent<AdvancedPlayerController>(); } }
    public bool InRange(AdvancedPlayerController candidate) => candidate != null && Vector3.Distance(transform.position, candidate.transform.position + Vector3.up) <= interactionRadius;
    public bool TryTakeControl(AdvancedPlayerController candidate)
    {
        if (IsControlling || candidate == null || candidate.LocomotionLocked || !InRange(candidate)) return false;
        player = candidate; IsControlling = true; player.SetLocomotionLocked(true);
        player.GetComponent<ShipDeckPassenger>()?.Attach(GetComponentInParent<Rigidbody>()); return true;
    }
    public void ReleaseControl() { IsControlling = false; if (player != null) player.SetLocomotionLocked(false); }
    public void Restore(float value, bool controlling, AdvancedPlayerController driver) { rudder = value; player = driver; IsControlling = controlling; if (player != null) player.SetLocomotionLocked(controlling); UpdateWheel(); }
    void OnDisable() { ReleaseControl(); }
    public void Simulate(PlayerCommand command, AdvancedPlayerController candidate, float dt)
    {
        if (command.Release) ReleaseControl();
        else if (command.Use) { if (IsControlling) ReleaseControl(); else TryTakeControl(candidate); }
        float steer = IsControlling ? command.Move.x : 0;
        if (IsControlling) sail.AdjustSail(command.Move.y * .5f * dt);
        rudder = Mathf.MoveTowards(rudder, steer, turnSpeed * dt); UpdateWheel();
    }
    void UpdateWheel() { if (wheelMesh != null) wheelMesh.localRotation = wheelRest * Quaternion.Euler(0, 0, -rudder * 120); }
    void Update()
    {
        if (Networked) return;
        var kb = Keyboard.current; if (kb == null) return;
        bool active = Cursor.lockState == CursorLockMode.Locked;
        Simulate(new PlayerCommand { Move = active ? new Vector2((kb.dKey.isPressed ? 1:0)-(kb.aKey.isPressed ? 1:0), (kb.wKey.isPressed ? 1:0)-(kb.sKey.isPressed ? 1:0)) : Vector2.zero, Use = active && kb.eKey.wasPressedThisFrame, Release = kb.qKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame }, player, Time.deltaTime);
    }
    void OnGUI()
    {
        if (player == null || !player.InputActive || (!IsControlling && !InRange(player))) return;
        GUI.Box(new Rect(Screen.width / 2f - 240, Screen.height - 85, 480, 55), IsControlling ? "W/S — ход / торможение   A/D — поворот\nE / Q — отпустить штурвал" : "E — взяться за штурвал");
    }
}
