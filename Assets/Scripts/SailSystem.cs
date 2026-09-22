using UnityEngine;
using PirateSlop;
using PirateSlop.Networking;

public class SailSystem : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] float deployPercentage;
    [SerializeField] Transform[] sailMeshes;
    public Collider[] MastControls;
    public ShipControlHandle[] RopeHandles;
    public string[] RopeNames;
    float[] tensions, visualDeploy, lastGrip;
    int[] owners;
    AdvancedPlayerController[] drivers;
    Vector3[] restScale;
    NetworkShip network;
    readonly System.Collections.Generic.Dictionary<int, float> humanUse = new();
    public float LastHumanControlTime(int index) => humanUse.TryGetValue(index, out float time) ? time : float.NegativeInfinity;
    readonly System.Collections.Generic.Dictionary<string, float> structuralEfficiency = new();
    public int RopeCount => RopeHandles == null ? 0 : RopeHandles.Length;
    public float DeployPercentage => deployPercentage;
    public float EffectiveDeploy
    {
        get
        {
            if (sailMeshes == null || sailMeshes.Length == 0) return deployPercentage;
            float total = 0f;
            for (int i = 0; i < sailMeshes.Length; i++) total += Tension(i) * Efficiency(i);
            return total / sailMeshes.Length;
        }
    }
    void Awake() { network = GetComponent<NetworkShip>(); Initialize(); }
    void Initialize()
    {
        int count = sailMeshes == null ? 0 : sailMeshes.Length;
        if (tensions != null && tensions.Length == count) return;
        tensions = new float[count]; visualDeploy = new float[count]; lastGrip = new float[count];
        owners = new int[count]; drivers = new AdvancedPlayerController[count]; restScale = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            tensions[i] = visualDeploy[i] = deployPercentage;
            restScale[i] = sailMeshes[i] != null ? sailMeshes[i].localScale : Vector3.one;
        }
    }
    public float Tension(int index) { Initialize(); return index >= 0 && index < tensions.Length ? tensions[index] : 0f; }
    public int Owner(int index) { Initialize(); return index >= 0 && index < owners.Length ? owners[index] : 0; }
    public string RopeName(int index) => RopeNames != null && index >= 0 && index < RopeNames.Length ? RopeNames[index] : "Парус " + (index + 1);
    public float Efficiency(int index)
    {
        if (sailMeshes == null || index < 0 || index >= sailMeshes.Length || sailMeshes[index] == null) return 0f;
        return structuralEfficiency.TryGetValue(sailMeshes[index].name, out float value) ? Mathf.Clamp01(value) : 1f;
    }
    public bool InRange(AdvancedPlayerController player, int index)
    {
        if (player == null || player.IsDead || index < 0 || index >= RopeCount || Efficiency(index) <= 0f) return false;
        var handle = RopeHandles[index];
        return handle != null && handle.gameObject.activeInHierarchy && Vector3.Distance(player.transform.position + Vector3.up, handle.transform.position) <= 2.8f;
    }
    public bool InRange(AdvancedPlayerController player)
    {
        for (int i = 0; i < RopeCount; i++) if (InRange(player, i)) return true;
        return false;
    }
    bool CanUse(AdvancedPlayerController player, int index)
    {
        if (!InRange(player, index) || player.OtherLocomotionLocked || player.IsSwimming || player.IsClimbing || player.IsKnockedBack) return false;
        var inventory = player.GetComponent<PlayerInventory>();
        var hands = player.GetComponent<CannonHands>();
        return (inventory == null || !inventory.HandsOccupied && !inventory.Placing && (inventory.Fishing == null || !inventory.Fishing.IsFishing)) && (hands == null || !hands.HasHeldBall);
    }
    public void Drag(int index, AdvancedPlayerController player, float amount, bool holding)
    {
        Initialize();
        if (index < 0 || index >= RopeCount || index >= tensions.Length || player == null || !float.IsFinite(amount)) return;
        ValidateGrips();
        if (!holding)
        {
            if (drivers[index] != player) return;
            if (CanUse(player, index)) tensions[index] = Mathf.Clamp01(tensions[index] + Mathf.Clamp(amount, -.15f, .15f));
            Release(index); Aggregate(); return;
        }
        if (!CanUse(player, index)) return;
        var incoming = player.GetComponent<NetworkPlayer>();
        if (incoming != null && incoming.IsServerInitialized && !incoming.IsBot.Value)
        {
            var current = drivers[index] != null ? drivers[index].GetComponent<NetworkPlayer>() : null;
            if (current != null && current.IsBot.Value && current.TeamId.Value == incoming.TeamId.Value) Release(index);
            if (drivers[index] == null || drivers[index] == player) humanUse[index] = Time.time;
        }
        if (drivers[index] != null && drivers[index] != player) return;
        for (int i = 0; i < drivers.Length; i++) if (i != index && drivers[i] == player) Release(i);
        bool first = drivers[index] == null;
        drivers[index] = player;
        var participant = player.GetComponent<NetworkPlayer>();
        owners[index] = participant != null ? participant.ParticipantId.Value : -1;
        float allowed = first ? 0f : Mathf.Clamp(Time.time - lastGrip[index], 0f, .2f) * 2f;
        lastGrip[index] = Time.time;
        player.SailPullLocked = true;
        tensions[index] = Mathf.Clamp01(tensions[index] + Mathf.Clamp(amount, -allowed, allowed));
        Aggregate();
    }
    void Release(int index)
    {
        if (drivers[index] != null) drivers[index].SailPullLocked = false;
        drivers[index] = null; owners[index] = 0;
    }
    public void ReleasePlayer(AdvancedPlayerController player)
    {
        Initialize();
        for (int i = 0; i < drivers.Length; i++) if (drivers[i] == player) Release(i);
    }
    public void StopAll()
    {
        Initialize();
        for (int i = 0; i < drivers.Length; i++) Release(i);
        SetDeploy(0f);
    }
    public void ValidateGrips()
    {
        Initialize();
        for (int i = 0; i < drivers.Length; i++)
            if (owners[i] != 0 && (drivers[i] == null || !CanUse(drivers[i], i) || Time.time - lastGrip[i] > .75f || network != null && network.IsSinking)) Release(i);
    }
    public float[] CaptureTensions() { Initialize(); return (float[])tensions.Clone(); }
    public int[] CaptureOwners() { Initialize(); return (int[])owners.Clone(); }
    public void ApplyRopes(float[] values, int[] holders)
    {
        Initialize();
        if (values == null || holders == null || values.Length != tensions.Length || holders.Length != owners.Length) return;
        for (int i = 0; i < tensions.Length; i++) { tensions[i] = Mathf.Clamp01(values[i]); owners[i] = holders[i]; }
        Aggregate();
    }
    void Aggregate()
    {
        float total = 0f;
        foreach (float value in tensions) total += value;
        deployPercentage = tensions.Length > 0 ? total / tensions.Length : 0f;
    }
    public void SetStructuralEfficiency(System.Collections.Generic.Dictionary<string, float> values)
    {
        structuralEfficiency.Clear();
        foreach (var entry in values) structuralEfficiency[entry.Key] = entry.Value;
    }
    public void AdjustSail(float delta) { if (RopeCount == 0) SetDeploy(deployPercentage + delta); }
    public void SetDeploy(float value)
    {
        Initialize();
        deployPercentage = Mathf.Clamp01(value);
        for (int i = 0; i < tensions.Length; i++) if (owners[i] == 0) tensions[i] = deployPercentage;
        Aggregate();
    }
    public void ApplyAggregate(float value) { if (RopeCount == 0) SetDeploy(value); }
    void Update()
    {
        Initialize();
        if (network == null || network.IsServerInitialized) ValidateGrips();
        for (int i = 0; i < tensions.Length; i++)
        {
            var sail = sailMeshes[i];
            if (sail == null) continue;
            visualDeploy[i] = Mathf.MoveTowards(visualDeploy[i], tensions[i], Time.deltaTime * 1.5f);
            var renderer = sail.GetComponent<Renderer>();
            if (renderer != null) renderer.enabled = Efficiency(i) > 0f;
            var scale = restScale[i]; scale.y *= Mathf.Lerp(.06f, 1f, visualDeploy[i]); sail.localScale = scale;
        }
    }
    void OnDisable()
    {
        if (drivers == null) return;
        for (int i = 0; i < drivers.Length; i++) Release(i);
    }
}

