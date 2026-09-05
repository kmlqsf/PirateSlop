using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-100)]
public class HelmInteraction : MonoBehaviour
{
    [SerializeField] float interactionRadius = 2.2f, turnSpeed = 2f;
    [SerializeField] Transform wheelMesh;
    AdvancedPlayerController player;
    SailSystem sail;
    Quaternion wheelRest;
    float rudder;
    public float CurrentRudderNormalized => rudder;
    public bool IsControlling { get; private set; }
    public void Configure(Transform wheel) { wheelMesh = wheel; }
    void Awake()
    {
        sail = GetComponentInParent<SailSystem>();
        if (wheelMesh != null) wheelRest = wheelMesh.localRotation;
    }
    bool InRange() => player != null && Vector3.Distance(transform.position, player.transform.position + Vector3.up) <= interactionRadius;
    public bool TryTakeControl(AdvancedPlayerController candidate)
    {
        if (IsControlling || candidate == null || candidate.LocomotionLocked) return false;
        player = candidate;
        if (!InRange()) return false;
        IsControlling = true;
        player.SetLocomotionLocked(true);
        var passenger = player.GetComponent<ShipDeckPassenger>();
        if (passenger != null) passenger.Attach(GetComponentInParent<Rigidbody>());
        return true;
    }
    public void ReleaseControl()
    {
        IsControlling = false;
        if (player != null) player.SetLocomotionLocked(false);
    }
    void OnDisable() { ReleaseControl(); }
    void Update()
    {
        if (player == null) player = Object.FindAnyObjectByType<AdvancedPlayerController>();
        var kb = Keyboard.current;
        if (kb == null) return;
        if (IsControlling && (kb.eKey.wasPressedThisFrame || kb.qKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)) ReleaseControl();
        else if (!IsControlling && kb.eKey.wasPressedThisFrame && Cursor.lockState == CursorLockMode.Locked) TryTakeControl(player);
        float steer = 0f;
        if (IsControlling && Cursor.lockState == CursorLockMode.Locked)
        {
            steer = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
            float throttle = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
            if (sail != null) sail.AdjustSail(throttle * 0.5f * Time.deltaTime);
        }
        rudder = Mathf.MoveTowards(rudder, steer, turnSpeed * Time.deltaTime);
        if (wheelMesh != null) wheelMesh.localRotation = wheelRest * Quaternion.Euler(0f, 0f, -rudder * 120f);
    }
    void OnGUI()
    {
        if (!IsControlling && !InRange()) return;
        GUI.Box(new Rect(Screen.width / 2f - 240f, Screen.height - 85f, 480f, 55f),
            IsControlling ? "W/S — ход / торможение   A/D — поворот\nE / Q — отпустить штурвал" : "E — взяться за штурвал");
    }
}
