using System;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using PirateSlop.Networking;
using UnityEngine;

namespace PirateSlop.Ships
{
    public enum ShipV3TargetKind : byte { Lantern, Door, Bell, Dice, Dispenser, Candle }

    [Serializable]
    public sealed class ShipV3Lantern
    {
        public Light Light;
        public Renderer Glass;
        public int GlassSlot;
        public Transform Grip;
    }

    [Serializable]
    public sealed class ShipV3DiceSlot
    {
        public Rigidbody Cup;
        public Transform CupVisual;
        public Rigidbody[] Dice;
        public Transform[] DiceVisuals;
        public Vector3[] FaceNormals;
        public int[] FaceValues;
        public Vector3 RestCup;
        public Quaternion RestRotation;
        public float CupHeight = .22f;
    }

    public struct ShipV3PhysicsPose
    {
        public int Index;
        public Vector3 Position;
        public Quaternion Rotation;
        public int Phase;
        public float Cable;
        public Vector3 Velocity;
    }

    [Serializable]
    public sealed class ShipV3Support
    {
        public ShipDamageSection[] Sections;
        public int[] Fragments;
        public bool Alive()
        {
            for (int i = 0; i < Sections.Length; i++)
                if (Sections[i] != null && (Sections[i].RemovedFragments & (1UL << Fragments[i])) == 0) return true;
            return false;
        }
    }

    [Serializable]
    public sealed class ShipV3Attachment
    {
        public Transform Object;
        public Transform[] Dependencies;
        public ShipV3Support[] Supports;
        public bool Fall;
        [NonSerialized] public bool Detached;
    }

    [DefaultExecutionOrder(-30)]
    public sealed class ShipV3Features : NetworkBehaviour
    {
        public const float LanternIntensity = 1.05f;
        public const float LanternRange = 7.2f;
        public static readonly Color LanternEmission = new Color(1f, .42f, .10f) * .6f;
        public static readonly List<ShipV3Features> Active = new();
        void OnEnable() { Active.Add(this); }
        void OnDisable() { Active.Remove(this); }
        public ShipV3Lantern[] Lanterns;
        public Transform DoorHinge, DoorGrip, BellGrip, DiceTable, DiceView, RespawnPoint, DispenserMouth;
        public Vector3 DoorAxis = Vector3.up;
        public Transform[] DoorAssembly = Array.Empty<Transform>();
        public Rigidbody BellClapper;
        public Rigidbody[] PhysicsBodies;
        public bool[] Pendulums;
        public ShipV3DiceSlot[] DiceSlots;
        public ShipV3Attachment[] Attachments;
        public NetworkFish CannonballPrefab;
        public Vector3 DispenserDirection;
        public Transform DispenserLever, DispenserGrip;
        public Vector3 DispenserLeverAxis = Vector3.forward;
        public float DispenserLeverAngle = -65f;
        public GameObject DiceSupport;
        public float DiceRadius = .54f;
        public Transform DiceCandle;
        public Light CandleLight;
        public ParticleSystem CandleFlame;
        readonly SyncVar<bool> candleLit = new(true);
        public Transform AnchorTravel;
        public ConfigurableJoint MovingAnchorJoint;
        readonly SyncVar<int> lights = new(63);
        readonly SyncVar<float> door = new();
        readonly SyncVar<int> doorHolder = new(-1), bellHolder = new(-1);
        readonly SyncVar<int> dispenserHolder = new(-1);
        readonly SyncVar<float> dispenserPull = new(), dispenserReturnAt = new();
        readonly SyncList<int> diceOwners = new();
        readonly SyncList<string> diceResults = new();
        readonly SyncList<byte> dicePhases = new();
        Quaternion doorRest, clapperRest;
        Quaternion dispenserRest;
        Vector3[][] restingDice;
        Quaternion[][] restingDiceRotation;
        NetworkShip ship;
        ShipDestruction destruction;
        ShipV3RenderBudget renderBudget;
        float[] slotHeartbeat, collectionStart, settledFor, rollStart;
        Vector3[][] collectionFrom;
        Vector2[] cupPosition, smoothCupPosition, cupSmoothVelocity, releaseVelocity, previewCupPosition;
        Vector3[] renderedCupPosition;
        Quaternion[] renderedCupRotation;
        Vector3[][] renderedDicePosition;
        Quaternion[][] renderedDiceRotation;
        int[] cupBodyIndices, bodyDiceSlots, bodyDieIndices;
        int[][] diceBodyIndices;
        ShipV3PhysicsPose[] bodyPoses;
        bool[] previewCup;
        Vector2[] lastShake;
        float[,] nextDiceAudio;
        Quaternion[][] shakenRotation;
        byte[] diceMode;
        float nextPublish, doorHeartbeat, bellHeartbeat, nextRing, dispenserHeartbeat;
        Vector2 bellForce;
        float lastBellDrag = float.NegativeInfinity;
        int bellTeam;
        readonly Dictionary<int, int> rescueRings = new();
        ShipV3PhysicsPose[] remotePoses;
        uint remoteRevision, serverRevision;
        MaterialPropertyBlock glassBlock;
        readonly List<ShipV3PhysicsPose> frame = new();
        ShipV3PhysicsPose[] publishedFrame;
        float nextPhysicsHeartbeat;
        static readonly Unity.Profiling.ProfilerMarker attachmentMarker = new("Ships.Attachments");
        static readonly Unity.Profiling.ProfilerMarker physicsPublishMarker = new("Ships.PhysicsPublish");
        readonly RaycastHit[] diceObstacles = new RaycastHit[32];
        bool localInteractionReady;
        int appliedLights = -1;
        float nextAttachmentCheck;
        uint attachmentRevision;
        public int LocalDiceSlot(int clientId)
        {
            for (int i = 0; i < diceOwners.Count; i++) if (diceOwners[i] == clientId) return i;
            return -1;
        }
        public string DiceResult(int slot) => slot >= 0 && slot < diceResults.Count ? diceResults[slot] : "";
        public byte DicePhase(int slot) => slot >= 0 && slot < dicePhases.Count ? dicePhases[slot] : (byte)0;
        public string DiceInstructions(int slot)
        {
            byte phase = slot >= 0 && slot < dicePhases.Count ? dicePhases[slot] : (byte)0;
            return phase == 1 ? "Кости собираются в стакан · F — выйти" :
                phase == 2 || phase == 3 ? "Удерживать ЛКМ и двигать мышь — трясти кружку · отпустить ЛКМ — бросить · F — выйти" :
                phase == 4 || phase == 6 ? "Кости бросаются · F — выйти" : "E — насыпать кости в стакан · F — выйти";
        }
        public string DiceSummary()
        {
            var lines = new List<string>();
            for (int i = 0; i < diceResults.Count; i++) lines.Add((i + 1) + ": " + (string.IsNullOrEmpty(diceResults[i]) ? "—" : diceResults[i]));
            return string.Join("\n", lines);
        }
        public bool CanUseDice => DiceTable != null && DiceTable.gameObject.activeInHierarchy && (DiceSupport == null || DiceSupport.activeInHierarchy);
        public bool DispenserCooling => dispenserReturnAt.Value > 0f;
        public bool CandleBurning => candleLit.Value;

        public int NearestDiceSlot(Vector3 point)
        {
            int nearest = -1; float distance = float.PositiveInfinity;
            for (int i = 0; i < DiceSlots.Length; i++)
            {
                float candidate = Vector3.ProjectOnPlane(transform.TransformPoint(DiceSlots[i].RestCup) - point, transform.up).sqrMagnitude;
                if (candidate < distance) { distance = candidate; nearest = i; }
            }
            return nearest;
        }

        public void DiceCameraPose(int index, out Vector3 point, out Quaternion rotation)
        {
            Vector3 outward = Vector3.ProjectOnPlane(transform.TransformPoint(DiceSlots[index].RestCup) - DiceTable.position, transform.up).normalized;
            point = DiceTable.position + outward * .92f + transform.up * .95f;
            rotation = Quaternion.LookRotation(DiceTable.position + outward * .12f - point, transform.up);
        }

        [ServerRpc(RequireOwnership = false)]
        public void ToggleCandle(NetworkConnection sender = null)
        {
            var player = Sender(sender);
            if (player == null || LocalDiceSlot(sender.ClientId) >= 0 || DiceCandle == null || !CanReachDice(player)) return;
            candleLit.Value = !candleLit.Value;
            LightSound(DiceCandle.position, candleLit.Value);
        }

        public bool CanReachDice(NetworkPlayer player)
        {
            if (!CanUseDice || player == null || player.Motor == null || player.Motor.IsDead || player.Motor.IsSwimming || player.Motor.IsClimbing) return false;
            int slot = NearestDiceSlot(player.transform.position);
            if (slot < 0) return false;
            Vector3 point = transform.TransformPoint(DiceSlots[slot].RestCup) + transform.up * .2f;
            if (Vector3.Distance(player.transform.position + Vector3.up, point) > 3.2f) return false;
            return CanSeeDice(player, point);
        }

        public bool CanSeeDice(NetworkPlayer player, Vector3 point)
        {
            Vector3 origin = player.transform.position + Vector3.up * 1.5f;
            Vector3 delta = point - origin;
            int count = Physics.RaycastNonAlloc(origin, delta.normalized, diceObstacles, Mathf.Max(0f, delta.magnitude - .025f), ~0, QueryTriggerInteraction.Ignore);
            var hits = count == diceObstacles.Length
                ? Physics.RaycastAll(origin, delta.normalized, Mathf.Max(0f, delta.magnitude - .025f), ~0, QueryTriggerInteraction.Ignore)
                : diceObstacles;
            if (hits != diceObstacles) count = hits.Length;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.transform.IsChildOf(player.transform) || hit.transform.IsChildOf(DiceTable.parent) ||
                    DiceSupport != null && hit.transform.IsChildOf(DiceSupport.transform) ||
                    hit.collider.GetComponentInParent<AdvancedPlayerController>() != null || hit.collider.GetComponentInParent<Cannonball>() != null) continue;
                var batch = hit.collider.GetComponent<ShipV3CollisionBatch>();
                if (batch != null && (batch.ContainsSurface(DiceTable.parent, hit.point) ||
                    DiceSupport != null && batch.ContainsSurface(DiceSupport.transform, hit.point))) continue;
                return false;
            }
            return true;
        }

        void Awake()
        {
            ship = GetComponent<NetworkShip>();
            destruction = GetComponent<ShipDestruction>();
            renderBudget = GetComponent<ShipV3RenderBudget>();
            doorRest = DoorHinge != null ? DoorHinge.localRotation : Quaternion.identity;
            clapperRest = BellClapper != null ? BellClapper.transform.localRotation : Quaternion.identity;
            dispenserRest = DispenserLever != null ? DispenserLever.localRotation : Quaternion.identity;
            slotHeartbeat = new float[DiceSlots.Length];
            collectionStart = new float[DiceSlots.Length];
            collectionFrom = new Vector3[DiceSlots.Length][];
            settledFor = new float[DiceSlots.Length];
            rollStart = new float[DiceSlots.Length];
            cupPosition = new Vector2[DiceSlots.Length];
            smoothCupPosition = new Vector2[DiceSlots.Length];
            cupSmoothVelocity = new Vector2[DiceSlots.Length];
            releaseVelocity = new Vector2[DiceSlots.Length];
            previewCupPosition = new Vector2[DiceSlots.Length];
            previewCup = new bool[DiceSlots.Length];
            renderedCupPosition = new Vector3[DiceSlots.Length];
            renderedCupRotation = new Quaternion[DiceSlots.Length];
            renderedDicePosition = new Vector3[DiceSlots.Length][];
            renderedDiceRotation = new Quaternion[DiceSlots.Length][];
            cupBodyIndices = new int[DiceSlots.Length];
            diceBodyIndices = new int[DiceSlots.Length][];
            bodyDiceSlots = new int[PhysicsBodies.Length];
            bodyDieIndices = new int[PhysicsBodies.Length];
            bodyPoses = new ShipV3PhysicsPose[PhysicsBodies.Length];
            for (int i = 0; i < PhysicsBodies.Length; i++)
            {
                bodyDiceSlots[i] = bodyDieIndices[i] = -1;
                if (PhysicsBodies[i] != null) bodyPoses[i] = new ShipV3PhysicsPose { Index = i,
                    Position = transform.InverseTransformPoint(PhysicsBodies[i].position), Rotation = Quaternion.Inverse(transform.rotation) * PhysicsBodies[i].rotation };
            }
            lastShake = new Vector2[DiceSlots.Length];
            nextDiceAudio = new float[DiceSlots.Length, 3];
            shakenRotation = new Quaternion[DiceSlots.Length][];
            diceMode = new byte[DiceSlots.Length];
            restingDice = new Vector3[DiceSlots.Length][];
            restingDiceRotation = new Quaternion[DiceSlots.Length][];
            for (int i = 0; i < DiceSlots.Length; i++)
            {
                if (DiceSlots[i].CupVisual != null)
                {
                    var original = DiceSlots[i].Cup.GetComponent<MeshRenderer>();
                    original.enabled = false; original.forceRenderingOff = true;
                }
                renderedCupPosition[i] = transform.InverseTransformPoint(DiceSlots[i].Cup.position);
                renderedCupRotation[i] = Quaternion.Inverse(transform.rotation) * DiceSlots[i].Cup.rotation;
                restingDice[i] = Array.ConvertAll(DiceSlots[i].Dice, die => transform.InverseTransformPoint(die.transform.position));
                restingDiceRotation[i] = Array.ConvertAll(DiceSlots[i].Dice, die => Quaternion.Inverse(transform.rotation) * die.transform.rotation);
                renderedDicePosition[i] = (Vector3[])restingDice[i].Clone();
                renderedDiceRotation[i] = (Quaternion[])restingDiceRotation[i].Clone();
                shakenRotation[i] = (Quaternion[])restingDiceRotation[i].Clone();
                cupBodyIndices[i] = Array.IndexOf(PhysicsBodies, DiceSlots[i].Cup);
                if (cupBodyIndices[i] >= 0) bodyDiceSlots[cupBodyIndices[i]] = i;
                diceBodyIndices[i] = new int[DiceSlots[i].Dice.Length];
                for (int die = 0; die < DiceSlots[i].Dice.Length; die++)
                {
                    int body = Array.IndexOf(PhysicsBodies, DiceSlots[i].Dice[die]);
                    diceBodyIndices[i][die] = body;
                    if (body >= 0) { bodyDiceSlots[body] = i; bodyDieIndices[body] = die; }
                    if (DiceSlots[i].DiceVisuals != null && die < DiceSlots[i].DiceVisuals.Length && DiceSlots[i].DiceVisuals[die] != null)
                    {
                        var original = DiceSlots[i].Dice[die].GetComponent<MeshRenderer>();
                        original.enabled = false; original.forceRenderingOff = true;
                    }
                }
            }
            glassBlock = new MaterialPropertyBlock();
        }

        public override void OnStartServer()
        {
            for (int i = 0; i < DiceSlots.Length; i++) { diceOwners.Add(-1); diceResults.Add(""); dicePhases.Add(0); }
            foreach (var body in PhysicsBodies) if (body != null) body.isKinematic = false;
            foreach (var slot in DiceSlots) if (slot.Cup != null) slot.Cup.isKinematic = true;
            foreach (var slot in DiceSlots) foreach (var die in slot.Dice) if (die != null) { die.isKinematic = true; die.interpolation = RigidbodyInterpolation.None; }
        }

        public override void OnStartClient()
        {
            if (!IsServerInitialized)
            {
                foreach (var body in PhysicsBodies) if (body != null) body.isKinematic = true;
                foreach (var slot in DiceSlots) foreach (var die in slot.Dice) if (die != null) die.interpolation = RigidbodyInterpolation.None;
            }
            foreach (var player in NetworkPlayer.Active)
                if (player.IsOwner && !player.IsBot.Value && player.GetComponent<ShipV3PlayerInteraction>() == null)
                    player.gameObject.AddComponent<ShipV3PlayerInteraction>();
        }

        NetworkPlayer Sender(NetworkConnection sender)
        {
            if (sender == null || !sender.IsActive || ship.IsSinking) return null;
            var player = SessionController.Instance.GetPlayer(sender.ClientId);
            return player != null && !player.Motor.IsDead && !player.Motor.IsSwimming && !player.Motor.IsClimbing ? player : null;
        }

        bool Reach(NetworkPlayer player, Transform target)
        {
            return player != null && target != null && target.gameObject.activeInHierarchy &&
                Vector3.Distance(player.transform.position + Vector3.up, target.position) <= 3.2f &&
                player.GetComponent<NetworkWeapon>().CanReach(target.position, target.parent);
        }

        bool ReachDoor(NetworkPlayer player)
        {
            if (player == null || DoorGrip == null || DoorHinge == null || !DoorGrip.gameObject.activeInHierarchy ||
                Vector3.Distance(player.transform.position + Vector3.up, DoorGrip.position) > 3.2f) return false;
            Vector3 origin = player.transform.position + Vector3.up * 1.5f, delta = DoorGrip.position - origin;
            foreach (var hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(player.transform) || hit.collider.GetComponentInParent<AdvancedPlayerController>() != null ||
                    hit.collider.GetComponentInParent<Cannonball>() != null) continue;
                bool doorPart = false;
                foreach (var part in DoorAssembly) if (part != null && hit.transform.IsChildOf(part)) { doorPart = true; break; }
                if (!doorPart) return false;
            }
            return true;
        }

        [ServerRpc(RequireOwnership = false)]
        public void ToggleLantern(int index, NetworkConnection sender = null)
        {
            var player = Sender(sender);
            if (index < 0 || index >= Lanterns.Length || !Reach(player, Lanterns[index].Grip)) return;
            lights.Value ^= 1 << index;
            LightSound(Lanterns[index].Grip.position, (lights.Value & (1 << index)) != 0);
        }

        [ObserversRpc(RunLocally = true)]
        void LightSound(Vector3 point, bool lit) => GameAudio.Play(lit ? SoundCue.FlameLight : SoundCue.FlameExtinguish, point);

        [ServerRpc(RequireOwnership = false)]
        public void DragDoor(float delta, bool holding, NetworkConnection sender = null)
        {
            if (sender == null) return;
            if (!holding) { if (doorHolder.Value == sender.ClientId) doorHolder.Value = -1; return; }
            var player = Sender(sender);
            if (!float.IsFinite(delta) || !ReachDoor(player) || doorHolder.Value != -1 && doorHolder.Value != sender.ClientId) return;
            bool first = doorHolder.Value == -1;
            float allowed = first ? 0f : Mathf.Clamp(Time.time - doorHeartbeat, 0f, .15f) * 110f;
            doorHolder.Value = sender.ClientId; doorHeartbeat = Time.time;
            door.Value = Mathf.Clamp(door.Value + Mathf.Clamp(delta, -allowed, allowed), -100f, 0f);
        }

        [ServerRpc(RequireOwnership = false)]
        public void PullBell(Vector2 delta, bool holding, NetworkConnection sender = null)
        {
            if (sender == null) return;
            if (!holding) { if (bellHolder.Value == sender.ClientId) { bellHolder.Value = -1; bellForce = Vector2.zero; } return; }
            var player = Sender(sender);
            if (!float.IsFinite(delta.sqrMagnitude) || !Reach(player, BellGrip) || bellHolder.Value != -1 && bellHolder.Value != sender.ClientId) return;
            bellHolder.Value = sender.ClientId; bellHeartbeat = Time.time; bellTeam = player.TeamId.Value;
            if (delta.sqrMagnitude > .01f)
            {
                bellForce = new Vector2(Mathf.Clamp(bellForce.x + Mathf.Clamp(delta.x, -100f, 100f) * 1.8f, -36f, 36f), 0f);
                lastBellDrag = Time.time;
                BellClapper.WakeUp();
            }
        }

        public void ClapperContact(float speed, Vector3 point)
        {
            if (!IsServerInitialized || speed < .18f || Time.time < nextRing) return;
            if (bellHolder.Value >= 0 && Time.time - lastBellDrag > 1.2f) return;
            nextRing = Time.time + .12f;
            BellSound(point, Mathf.Clamp(speed * .8f, .2f, .85f));
            if (Time.time - bellHeartbeat > 1.2f) return;
            rescueRings.TryGetValue(bellTeam, out int rings);
            bool dead = false;
            foreach (var member in NetworkPlayer.Active)
                if (member.TeamId.Value == bellTeam && !member.Eliminated.Value && member.Motor.IsDead) dead = true;
            if (!dead) { rescueRings[bellTeam] = 0; return; }
            rescueRings[bellTeam] = ++rings;
            if (rings < 3) return;
            foreach (var member in NetworkPlayer.Active)
            {
                if (member.TeamId.Value != bellTeam || member.Eliminated.Value || !member.Motor.IsDead) continue;
                member.GetComponent<NetworkHealth>().Respawn(RespawnPoint.position, transform.eulerAngles.y);
                rescueRings[bellTeam] = 0;
                break;
            }
        }

        [ObserversRpc(RunLocally = true)]
        void BellSound(Vector3 point, float volume) => GameAudio.Play(SoundCue.ShipBell, point, volume);

        [ServerRpc(RequireOwnership = false)]
        public void PullDispenser(float delta, bool holding, NetworkConnection sender = null)
        {
            if (sender == null) return;
            if (!holding) { if (dispenserHolder.Value == sender.ClientId) dispenserHolder.Value = -1; return; }
            var player = Sender(sender);
            if (DispenserCooling || !float.IsFinite(delta) || player == null || DispenserGrip == null || !DispenserGrip.gameObject.activeInHierarchy ||
                Vector3.Distance(player.transform.position + Vector3.up, DispenserGrip.position) > 3.2f ||
                !player.GetComponent<NetworkWeapon>().CanReach(DispenserGrip.position, DispenserLever.parent) ||
                dispenserHolder.Value >= 0 && dispenserHolder.Value != sender.ClientId) return;
            float allowed = Mathf.Clamp(Time.time - dispenserHeartbeat, 0f, .15f) * 3f;
            dispenserHeartbeat = Time.time; dispenserHolder.Value = sender.ClientId;
            dispenserPull.Value = Mathf.Clamp01(dispenserPull.Value + Mathf.Clamp(delta, -allowed, allowed));
            if (dispenserPull.Value < .999f) return;
            Dispense();
            dispenserHolder.Value = -1;
            dispenserReturnAt.Value = (float)TimeManager.Tick * (float)TimeManager.TickDelta + 5f;
        }

        [ServerRpc(RequireOwnership = false)]
        public void JoinDice(NetworkConnection sender = null)
        {
            if (sender == null) return;
            var player = Sender(sender);
            if (!CanReachDice(player) || LocalDiceSlot(sender.ClientId) >= 0) return;
            int index = NearestDiceSlot(player.transform.position);
            if (index < 0 || diceOwners[index] != -1 || diceMode[index] == 4 || diceMode[index] == 6) return;
            diceOwners[index] = sender.ClientId; slotHeartbeat[index] = Time.time;
            cupPosition[index] = Vector2.zero;
            player.Motor.ShipActivityLocked = true;
        }

        readonly bool[] upgradeLuckyRoll = new bool[3];

        [ServerRpc(RequireOwnership = false)]

        public void DiceInput(Vector2 position, Vector2 velocity, bool holding, bool gather, bool leave, NetworkConnection sender = null)
        {
            if (sender == null) return;
            int index = LocalDiceSlot(sender.ClientId);
            if (index < 0) return;
            var player = Sender(sender);
            if (leave || !CanReachDice(player)) { ReleaseSlot(index); return; }
            if (!float.IsFinite(position.sqrMagnitude) || !float.IsFinite(velocity.sqrMagnitude)) return;
            slotHeartbeat[index] = Time.time;
            byte previousMode = diceMode[index];
            if (gather && (previousMode == 0 || previousMode == 5))
            {
                diceMode[index] = 1; collectionStart[index] = Time.time;
                collectionFrom[index] = new Vector3[DiceSlots[index].Dice.Length];
                for (int i = 0; i < collectionFrom[index].Length; i++)
                {
                    var die = DiceSlots[index].Dice[i];
                    collectionFrom[index][i] = transform.InverseTransformPoint(die.position);
                    die.isKinematic = true;
                }
                lastShake[index] = Vector2.zero;
                releaseVelocity[index] = Vector2.zero;
                PlayDiceSound(index, SoundCue.DiceCup, DiceSlots[index].Cup.position, .65f);
            }
            if ((holding || previousMode == 3) && diceMode[index] != 4 && diceMode[index] != 6)
            {
                Vector2 next = Vector2.ClampMagnitude(position, .24f);
                Vector2 drag = next - cupPosition[index];
                cupPosition[index] = next;
                releaseVelocity[index] = Vector2.ClampMagnitude(velocity, 2.8f);
                if (holding && diceMode[index] == 2) diceMode[index] = 3;
                if (diceMode[index] == 3 && drag.sqrMagnitude > .0000001f)
                {
                    lastShake[index] = drag;
                    PlayDiceSound(index, SoundCue.DiceCup, DiceSlots[index].Cup.position, Mathf.Clamp01(drag.magnitude / .035f));
                    PlayDiceSound(index, SoundCue.DiceImpact, DiceSlots[index].Cup.position, .35f);
                    for (int i = 0; i < shakenRotation[index].Length; i++)
                        shakenRotation[index][i] = Quaternion.Euler(drag.y * 2600f, drag.x * 1900f, (drag.x - drag.y) * (1700f + i * 170f)) * shakenRotation[index][i];
                }
            }
            if (!holding && diceMode[index] == 3)
            {
                upgradeLuckyRoll[index] = player.HasUpgrade(UpgradeEffect.LuckyDie);
                diceMode[index] = 6; settledFor[index] = 0f; rollStart[index] = Time.time;
            }
        }

        public void PlayDiceSound(int index, SoundCue cue, Vector3 point, float volume)
        {
            if (!IsServerInitialized || index < 0 || index >= DiceSlots.Length ||
                cue != SoundCue.DiceSlide && cue != SoundCue.DiceImpact && cue != SoundCue.DiceCup) return;
            int channel = cue == SoundCue.DiceSlide ? 0 : cue == SoundCue.DiceImpact ? 1 : 2;
            if (Time.time < nextDiceAudio[index, channel]) return;
            nextDiceAudio[index, channel] = Time.time + (channel == 0 ? .22f : .14f);
            DiceSound(cue, point, Mathf.Clamp01(volume));
        }

        [ObserversRpc(RunLocally = true)]
        void DiceSound(SoundCue cue, Vector3 point, float volume) => GameAudio.Play(cue, point, volume);

        void ReleaseSlot(int index)
        {
            var player = SessionController.Instance.GetPlayer(diceOwners[index]);
            if (player != null) player.Motor.ShipActivityLocked = false;
            diceOwners[index] = -1;
            if (diceMode[index] == 4 || diceMode[index] == 6) return;
            foreach (var die in DiceSlots[index].Dice) if (die != null) die.isKinematic = true;
            diceMode[index] = 0;
            cupPosition[index] = Vector2.zero;
        }

        public Vector2 DiceCupOffset(int index, Vector2 position)
        {
            Vector3 center = transform.InverseTransformPoint(DiceTable.position);
            Vector3 rest = DiceSlots[index].RestCup - center;
            return ConstrainSector(index, new Vector2(rest.x, rest.z) + DiceDragVelocity(index, position), .105f);
        }

        public Vector2 DiceDragVelocity(int index, Vector2 movement)
        {
            Vector2 axis = SectorDirection(index);
            return new Vector2(-axis.y, axis.x) * movement.x - axis * movement.y;
        }

        public void PreviewDiceCup(int index, Vector2 position)
        {
            previewCup[index] = true;
            previewCupPosition[index] = position;
        }

        public void EndDicePreview(int index)
        {
            if (index >= 0 && index < previewCup.Length) previewCup[index] = false;
        }

        Vector2 SectorDirection(int index)
        {
            Vector3 center = transform.InverseTransformPoint(DiceTable.position);
            Vector3 delta = DiceSlots[index].RestCup - center;
            return new Vector2(delta.x, delta.z).normalized;
        }

        Vector2 ConstrainSector(int index, Vector2 point, float margin)
        {
            Vector2 axis = SectorDirection(index);
            Vector2 side = new Vector2(-axis.y, axis.x);
            for (int pass = 0; pass < 4; pass++)
            {
                foreach (int sign in new[] { -1, 1 })
                {
                    Vector2 normal = axis * .8660254f + side * (.5f * sign);
                    float distance = Vector2.Dot(point, normal);
                    if (distance < margin) point += normal * (margin - distance);
                }
                if (point.magnitude < .105f + margin) point = point.normalized * (.105f + margin);
                point = Vector2.ClampMagnitude(point, DiceRadius - margin);
            }
            return point;
        }

        void KeepDiceOnTable(int index, Rigidbody die)
        {
            Vector3 local = transform.InverseTransformPoint(die.position);
            Vector3 center = transform.InverseTransformPoint(DiceTable.position);
            var offset = new Vector2(local.x - center.x, local.z - center.z);
            Vector2 edge = ConstrainSector(index, offset, .055f);
            bool corrected = false;
            if ((edge - offset).sqrMagnitude > .000001f)
            {
                corrected = true;
                local.x = center.x + edge.x; local.z = center.z + edge.y;
                Vector2 outward = (offset - edge).normalized;
                Vector3 normal = transform.TransformDirection(new Vector3(outward.x, 0, outward.y));
                Vector3 relative = die.linearVelocity - ship.Motor.CannonPointVelocity(die.position);
                die.linearVelocity -= normal * Mathf.Max(0f, Vector3.Dot(relative, normal)) * 1.25f;
            }
            if (local.y < center.y - .04f || local.y > center.y + .75f)
            {
                corrected = true;
                local.y = Mathf.Clamp(local.y, center.y + .03f, center.y + .6f);
                die.linearVelocity = ship.Motor.CannonPointVelocity(transform.TransformPoint(local));
            }
            if (corrected) die.position = transform.TransformPoint(local);
        }

        static Vector3 DiceInsideCup(ShipV3DiceSlot slot, int die) => new((die % 2 - .5f) * .043f, slot.CupHeight - .04f - die / 2 * .044f, 0);

        void FreezeDice(int index)
        {
            for (int i = 0; i < DiceSlots[index].Dice.Length; i++)
            {
                var die = DiceSlots[index].Dice[i]; KeepDiceOnTable(index, die);
                die.linearVelocity = die.angularVelocity = Vector3.zero;
                restingDice[index][i] = transform.InverseTransformPoint(die.position);
                restingDiceRotation[index][i] = Quaternion.Inverse(transform.rotation) * die.rotation;
                die.isKinematic = true;
            }
        }

        void FixedUpdate()
        {
            if (!IsServerInitialized || !IsSpawned) return;
            if (DispenserCooling)
            {
                float remaining = dispenserReturnAt.Value - (float)TimeManager.Tick * (float)TimeManager.TickDelta;
                dispenserPull.Value = Mathf.Clamp01(remaining / 5f);
                if (remaining <= 0f) dispenserReturnAt.Value = 0f;
            }
            else if (dispenserHolder.Value >= 0 && Time.time - dispenserHeartbeat > .65f) dispenserHolder.Value = -1;
            if (MovingAnchorJoint != null && AnchorTravel != null)
                MovingAnchorJoint.connectedAnchor = transform.InverseTransformPoint(AnchorTravel.position);
            for (int i = 0; i < PhysicsBodies.Length; i++)
            {
                var body = PhysicsBodies[i];
                if (body == null || body.isKinematic || i >= Pendulums.Length || !Pendulums[i]) continue;
                var wind = transform.right * Mathf.Sin((float)TimeManager.Tick * .017f + i) * .03f;
                body.AddForce(wind - ship.Motor.CannonPointVelocity(body.position) * .004f, ForceMode.Acceleration);
                if (body.name.Contains("Lamp_Hold"))
                    body.AddTorque(transform.TransformDirection(new Vector3(Mathf.Sin((float)TimeManager.Tick * .04f + i) * .12f, 0, -ship.Motor.MotionAngularVelocity.z * 1.8f)), ForceMode.Acceleration);
            }
            if (bellHolder.Value >= 0 && BellClapper != null)
            {
                Quaternion desired = Quaternion.AngleAxis(-bellForce.x, transform.forward)
                    * BellClapper.transform.parent.rotation * clapperRest;
                Quaternion error = desired * Quaternion.Inverse(BellClapper.rotation);
                error.ToAngleAxis(out float angle, out Vector3 axis);
                if (angle > 180f) angle -= 360f;
                if (axis.sqrMagnitude > .001f && float.IsFinite(axis.sqrMagnitude))
                    BellClapper.AddTorque(axis.normalized * (angle * Mathf.Deg2Rad * 160f) - BellClapper.angularVelocity * 7f, ForceMode.Acceleration);
            }
            for (int index = 0; index < DiceSlots.Length; index++)
            {
                var slot = DiceSlots[index];
                bool owned = diceOwners[index] >= 0;
                byte phase = diceMode[index];
                Vector2 axis = SectorDirection(index);
                Vector2 target = owned || phase == 4 || phase == 6 ? cupPosition[index] : Vector2.zero;
                if (phase == 4) target *= 1f - Mathf.Clamp01((Time.time - rollStart[index] - .16f) / .4f);
                smoothCupPosition[index] = Vector2.SmoothDamp(smoothCupPosition[index], target, ref cupSmoothVelocity[index], .045f, Mathf.Infinity, Time.fixedDeltaTime);
                Vector3 center = transform.InverseTransformPoint(DiceTable.position);
                Vector2 offset = DiceCupOffset(index, smoothCupPosition[index]);
                Vector3 throwVelocity = transform.TransformDirection(new Vector3(releaseVelocity[index].x, 0, releaseVelocity[index].y));
                Vector3 inward = -transform.TransformDirection(new Vector3(axis.x, 0, axis.y));
                Vector3 releaseDirection = throwVelocity.sqrMagnitude > .0025f ? throwVelocity.normalized : inward;
                float openingDuration = Mathf.Lerp(.16f, .085f, Mathf.Clamp01(throwVelocity.magnitude / 2f));
                float opening = phase == 6 ? Mathf.SmoothStep(0, 1, Mathf.Clamp01((Time.time - rollStart[index]) / openingDuration)) : 0f;
                float collect = phase == 1 ? Mathf.Clamp01((Time.time - collectionStart[index]) / .7f) : 1f;
                float tip = phase == 2 || phase == 3 ? 180f : phase == 1 ? Mathf.SmoothStep(0, 180f, Mathf.Clamp01((collect - .7f) / .3f)) :
                    phase == 6 || phase == 4 ? 180f : 0f;
                float lift = phase == 2 || phase == 3 ? slot.CupHeight + .006f : phase == 1 ? slot.CupHeight * Mathf.Sin(tip * Mathf.Deg2Rad * .5f) :
                    phase == 6 ? slot.CupHeight + .006f + opening * .035f : phase == 4 ? (slot.CupHeight + .041f) * (1f - Mathf.Clamp01((Time.time - rollStart[index] - openingDuration) / .4f)) : 0f;
                Vector3 desired = transform.TransformPoint(new Vector3(center.x + offset.x, slot.RestCup.y + lift, center.z + offset.y));
                slot.Cup.MovePosition(desired);
                Quaternion cupRotation = Quaternion.AngleAxis(tip, Vector3.Cross(transform.up, inward)) * transform.rotation * slot.RestRotation;
                float releaseTilt = 10f + Mathf.Clamp01(throwVelocity.magnitude / 2f) * 16f;
                if (phase == 6 || phase == 4)
                {
                    cupRotation = Quaternion.AngleAxis(-releaseTilt * (phase == 6 ? opening : 1f), Vector3.Cross(transform.up, releaseDirection)) * cupRotation;
                    if (phase == 4) cupRotation = Quaternion.Slerp(cupRotation, transform.rotation * slot.RestRotation, Mathf.Clamp01((Time.time - rollStart[index] - openingDuration) / .4f));
                }
                slot.Cup.MoveRotation(cupRotation);
                if (!owned && phase != 4 && phase != 6 || phase == 0 || phase == 5)
                {
                    for (int i = 0; i < slot.Dice.Length; i++)
                    {
                        slot.Dice[i].MovePosition(transform.TransformPoint(restingDice[index][i]));
                        slot.Dice[i].MoveRotation(transform.rotation * restingDiceRotation[index][i]);
                    }
                }
                else if (phase == 1)
                {
                    float gather = Mathf.Clamp01(collect / .7f);
                    for (int i = 0; i < slot.Dice.Length; i++)
                    {
                        Vector3 inside = DiceInsideCup(slot, i);
                        Vector3 destination = desired + cupRotation * inside;
                        slot.Dice[i].MovePosition(Vector3.Lerp(transform.TransformPoint(collectionFrom[index][i]), destination, gather) + transform.up * (.25f * 4 * gather * (1 - gather)));
                    }
                    if (collect >= 1) diceMode[index] = 2;
                }
                else if (phase == 2 || phase == 3 || phase == 6)
                {
                    for (int i = 0; i < slot.Dice.Length; i++)
                    {
                        Vector3 inside = DiceInsideCup(slot, i);
                        slot.Dice[i].MovePosition(desired + cupRotation * inside);
                        slot.Dice[i].MoveRotation(transform.rotation * shakenRotation[index][i]);
                    }
                    if (phase == 6 && opening >= 1f)
                    {
                        Vector3 mouth = desired + cupRotation * Vector3.up * slot.CupHeight;
                        Vector3 launchPoint = transform.InverseTransformPoint(mouth);
                        Vector2 launchOffset = ConstrainSector(index, new Vector2(launchPoint.x - center.x, launchPoint.z - center.z), .145f);
                        launchPoint.x = center.x + launchOffset.x; launchPoint.z = center.z + launchOffset.y;
                        mouth = transform.TransformPoint(launchPoint);
                        Vector3 lateral = Vector3.Cross(transform.up, releaseDirection);
                        for (int i = 0; i < slot.Dice.Length; i++)
                        {
                            var die = slot.Dice[i];
                            die.isKinematic = false;
                            die.position = mouth + transform.up * .029f + releaseDirection * (.02f + (i / 2 - 1) * .056f) + lateral * ((i % 2 - .5f) * .056f);
                            die.linearVelocity = ship.Motor.CannonPointVelocity(die.position) + throwVelocity - transform.up * .08f;
                            die.angularVelocity = ship.Motor.MotionAngularVelocity + Vector3.Cross(transform.up, throwVelocity) * (12f + i * 1.1f);
                            KeepDiceOnTable(index, die);
                            die.WakeUp();
                        }
                        diceMode[index] = 4;
                    }
                }
                else if (diceMode[index] == 4)
                {
                    bool settled = true;
                    foreach (var die in slot.Dice)
                    {
                        KeepDiceOnTable(index, die);
                        if ((die.linearVelocity - ship.Motor.CannonPointVelocity(die.position)).sqrMagnitude > .012f || (die.angularVelocity - ship.Motor.MotionAngularVelocity).sqrMagnitude > .08f) settled = false;
                    }
                    settledFor[index] = settled ? settledFor[index] + Time.fixedDeltaTime : 0f;
                    if (settledFor[index] > .5f || Time.time - rollStart[index] > 6f)
                    {
                        int total = 0;
                        foreach (var die in slot.Dice)
                        {
                            int best = 0; float dot = -2f;
                            for (int face = 0; face < slot.FaceNormals.Length; face++)
                            {
                                float score = Vector3.Dot(die.transform.TransformDirection(slot.FaceNormals[face]), transform.up);
                                if (score > dot) { dot = score; best = face; }
                            }
                            if (upgradeLuckyRoll[index] && die == slot.Dice[0])
                                for (int face = 0; face < slot.FaceValues.Length; face++) if (slot.FaceValues[face] == 6) { best = face; break; }
                            total += slot.FaceValues[best];
                            die.rotation = Quaternion.FromToRotation(die.transform.TransformDirection(slot.FaceNormals[best]), transform.up) * die.rotation;
                        }
                        diceResults[index] = total.ToString(); FreezeDice(index); diceMode[index] = 5; cupPosition[index] = Vector2.zero;
                    }
                }
                if (dicePhases[index] != diceMode[index]) dicePhases[index] = diceMode[index];
            }
        }

        void Update()
        {
            if (!IsSpawned) return;
            if (IsClientInitialized && !localInteractionReady)
                foreach (var player in NetworkPlayer.Active)
                    if (player.IsOwner && !player.IsBot.Value)
                    {
                        if (player.GetComponent<ShipV3PlayerInteraction>() == null) player.gameObject.AddComponent<ShipV3PlayerInteraction>();
                        localInteractionReady = true;
                    }
            if (DoorHinge != null) DoorHinge.localRotation = doorRest * Quaternion.AngleAxis(door.Value, DoorAxis);
            if (DispenserLever != null) DispenserLever.localRotation = dispenserRest * Quaternion.AngleAxis(dispenserPull.Value * DispenserLeverAngle, DispenserLeverAxis);
            float time = (float)TimeManager.Tick * (float)TimeManager.TickDelta;
            bool lightsVisible = renderBudget == null || renderBudget.LocalLightsVisible;
            if (CandleLight != null) { CandleLight.enabled = candleLit.Value && CanUseDice && lightsVisible; CandleLight.intensity = 1.1f + .035f * Mathf.Sin(time * 6f); }
            if (CandleFlame != null)
            {
                bool burn = candleLit.Value && CanUseDice;
                if (burn && !CandleFlame.isPlaying) CandleFlame.Play();
                else if (!burn && CandleFlame.isPlaying) CandleFlame.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            bool lampsChanged = appliedLights != lights.Value;
            for (int i = 0; i < Lanterns.Length; i++)
            {
                bool lit = (lights.Value & (1 << i)) != 0;
                var lamp = Lanterns[i];
                if (lamp.Light != null)
                {
                    lamp.Light.enabled = lit && lightsVisible;
                    lamp.Light.intensity = LanternIntensity + .035f * Mathf.Sin(time * 4.7f + i * 2.1f) + .025f * Mathf.Sin(time * 7.3f + i);
                }
                if (lampsChanged && lamp.Glass != null)
                {
                    lamp.Glass.GetPropertyBlock(glassBlock, lamp.GlassSlot);
                    glassBlock.SetColor("_EmissionColor", lit ? LanternEmission : Color.black);
                    lamp.Glass.SetPropertyBlock(glassBlock, lamp.GlassSlot);
                }
            }
            appliedLights = lights.Value;
            uint currentRevision = destruction != null ? destruction.Revision : 0;
            if (currentRevision != attachmentRevision || Time.unscaledTime >= nextAttachmentCheck)
            {
                attachmentRevision = currentRevision;
                nextAttachmentCheck = Time.unscaledTime + 1f;
                UpdateAttachments();
            }
            if (!IsServerInitialized) return;
            if (doorHolder.Value >= 0 && Time.time - doorHeartbeat > .65f) doorHolder.Value = -1;
            if (bellHolder.Value >= 0 && Time.time - bellHeartbeat > .65f) { bellHolder.Value = -1; bellForce = Vector2.zero; }
            for (int i = 0; i < diceOwners.Count; i++)
                if (diceOwners[i] >= 0 && (Time.time - slotHeartbeat[i] > .9f || SessionController.Instance.GetPlayer(diceOwners[i]) is not { } player || !CanReachDice(player))) ReleaseSlot(i);
            if (Time.unscaledTime >= nextPublish)
            {
                using var sample = physicsPublishMarker.Auto();
                nextPublish = Time.unscaledTime + .05f;
                frame.Clear();
                for (int gunIndex = 0; gunIndex < 2; gunIndex++)
                {
                    var gun = ship.HarpoonMount.GetGun(gunIndex);
                    var projectile = gun != null ? gun.ActiveProjectile : null;
                    frame.Add(new ShipV3PhysicsPose { Index = -1 - gunIndex, Phase = projectile != null ? (int)projectile.State : -1,
                        Position = projectile != null ? transform.InverseTransformPoint(projectile.transform.position) : Vector3.zero,
                        Rotation = projectile != null ? Quaternion.Inverse(transform.rotation) * projectile.transform.rotation : Quaternion.identity,
                        Cable = projectile != null ? projectile.CurrentCableLength : 0f,
                        Velocity = projectile != null ? projectile.GetComponent<Rigidbody>().linearVelocity : Vector3.zero });
                }
                for (int i = 0; i < PhysicsBodies.Length; i++)
                {
                    if (PhysicsBodies[i] == null) continue;
                    var pose = new ShipV3PhysicsPose { Index = i, Position = transform.InverseTransformPoint(PhysicsBodies[i].position), Rotation = Quaternion.Inverse(transform.rotation) * PhysicsBodies[i].rotation };
                    int slot = bodyDiceSlots[i], die = bodyDieIndices[i];
                    if (slot >= 0)
                    {
                        pose.Phase = diceMode[slot];
                        if ((pose.Phase == 0 || pose.Phase == 5) && die >= 0)
                        { pose.Position = restingDice[slot][die]; pose.Rotation = restingDiceRotation[slot][die]; }
                    }
                    frame.Add(pose);
                }
                for (int i = 0; i < Attachments.Length; i++)
                    if (Attachments[i].Detached && Attachments[i].Object != null)
                        frame.Add(new ShipV3PhysicsPose { Index = PhysicsBodies.Length + i, Position = transform.InverseTransformPoint(Attachments[i].Object.position), Rotation = Quaternion.Inverse(transform.rotation) * Attachments[i].Object.rotation });
                bool changed = publishedFrame == null || publishedFrame.Length != frame.Count;
                if (changed) publishedFrame = new ShipV3PhysicsPose[frame.Count];
                for (int i = 0; i < frame.Count && !changed; i++)
                {
                    var current = frame[i]; var previous = publishedFrame[i];
                    changed = current.Index != previous.Index || current.Phase != previous.Phase ||
                        (current.Position - previous.Position).sqrMagnitude > .000001f || Quaternion.Angle(current.Rotation, previous.Rotation) > .1f ||
                        Mathf.Abs(current.Cable - previous.Cable) > .001f || (current.Velocity - previous.Velocity).sqrMagnitude > .000001f;
                }
                if (changed || Time.unscaledTime >= nextPhysicsHeartbeat)
                {
                    frame.CopyTo(publishedFrame);
                    nextPhysicsHeartbeat = Time.unscaledTime + .5f;
                    ReceivePhysics(++serverRevision, publishedFrame);
                }
            }
        }

        void UpdateAttachments()
        {
            using var sample = attachmentMarker.Auto();
            foreach (var item in Attachments)
            {
                if (item.Object == null || item.Detached) continue;
                bool supported = true;
                foreach (var group in item.Supports) if (!group.Alive()) supported = false;
                foreach (var dependency in item.Dependencies) if (dependency == null || !dependency.gameObject.activeInHierarchy) supported = false;
                if (supported) { if (!item.Object.gameObject.activeSelf) item.Object.gameObject.SetActive(true); continue; }
                if (!item.Fall) { item.Object.gameObject.SetActive(false); continue; }
                item.Detached = true;
                var body = item.Object.GetComponent<Rigidbody>();
                if (body == null) body = item.Object.gameObject.AddComponent<Rigidbody>();
                foreach (var joint in item.Object.GetComponents<Joint>()) Destroy(joint);
                body.isKinematic = !IsServerInitialized;
                if (IsServerInitialized) { body.linearVelocity = ship.Motor.CannonPointVelocity(item.Object.position); body.WakeUp(); }
            }
        }

        void Dispense()
        {
            if (ship.IsSinking || CannonballPrefab == null || DispenserMouth == null || !DispenserMouth.gameObject.activeInHierarchy) return;
            var item = Instantiate(CannonballPrefab, DispenserMouth.position, transform.rotation);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(item.gameObject, gameObject.scene);
            item.SetAmmoItem(InventoryItem.Cannonball);
            item.Place(NetworkObject, item.transform.position, item.transform.rotation);
            ServerManager.Spawn(item.NetworkObject);
            var ball = item.GetComponent<Cannonball>();
            ball.Release();
            ball.AttachToPlatform(null);
            ball.Body.isKinematic = false;
            ball.Body.useGravity = true;
            ball.Body.linearVelocity = ship.Motor.CannonPointVelocity(item.transform.position) + transform.TransformDirection(DispenserDirection).normalized * .35f;
        }

        [ObserversRpc(BufferLast = true)]
        void ReceivePhysics(uint revision, ShipV3PhysicsPose[] poses)
        {
            if (IsServerInitialized || revision < remoteRevision) return;
            remoteRevision = revision; remotePoses = poses;
            foreach (var pose in poses)
                if (pose.Index >= 0 && pose.Index < bodyPoses.Length) bodyPoses[pose.Index] = pose;
        }

        void LateUpdate()
        {
            if (!IsSpawned) return;
            if (IsServerInitialized || remotePoses == null) { RenderDiceCups(); return; }
            float blend = 1f - Mathf.Exp(-30f * Time.deltaTime);
            foreach (var pose in remotePoses)
            {
                if (pose.Index < 0)
                {
                    var gun = ship.HarpoonMount.GetGun(-1 - pose.Index);
                    if (gun == null) continue;
                    if (pose.Phase < 0)
                    {
                        if (gun.ActiveProjectile != null) { var old = gun.ActiveProjectile; gun.OnProjectileReturned(old); Destroy(old.gameObject); }
                        continue;
                    }
                    if (gun.ActiveProjectile == null) gun.LaunchFromNetwork(transform.TransformPoint(pose.Position), pose.Velocity.sqrMagnitude > .01f ? pose.Velocity : transform.forward);
                    gun.ActiveProjectile?.ApplyAuthoritativePose(transform.TransformPoint(pose.Position), transform.rotation * pose.Rotation, pose.Phase, pose.Cable);
                    continue;
                }
                Transform target = pose.Index < PhysicsBodies.Length ? PhysicsBodies[pose.Index]?.transform : Attachments[pose.Index - PhysicsBodies.Length].Object;
                if (target == null) continue;
                target.SetPositionAndRotation(Vector3.Lerp(target.position, transform.TransformPoint(pose.Position), blend), Quaternion.Slerp(target.rotation, transform.rotation * pose.Rotation, blend));
            }
            RenderDiceCups();
        }

        void RenderDiceCups()
        {
            float blend = 1f - Mathf.Exp(-35f * Time.deltaTime);
            Vector3 center = transform.InverseTransformPoint(DiceTable.position);
            for (int i = 0; i < DiceSlots.Length; i++)
            {
                var slot = DiceSlots[i];
                if (slot.Cup == null || slot.CupVisual == null || !slot.Cup.gameObject.activeInHierarchy) continue;
                var cupPose = DiceBodyPose(cupBodyIndices[i], slot.Cup);
                int phase = IsServerInitialized ? diceMode[i] : cupPose.Phase;
                bool resting = phase == 0 || phase == 5;
                Vector3 point = resting ? slot.RestCup : cupPose.Position;
                Quaternion rotation = resting ? slot.RestRotation : cupPose.Rotation;
                if (previewCup[i] && phase != 4 && phase != 5)
                {
                    Vector2 offset = DiceCupOffset(i, previewCupPosition[i]);
                    point.x = center.x + offset.x; point.z = center.z + offset.y;
                }
                renderedCupPosition[i] = Vector3.Lerp(renderedCupPosition[i], point, blend);
                renderedCupRotation[i] = Quaternion.Slerp(renderedCupRotation[i], rotation, blend);
                slot.CupVisual.SetPositionAndRotation(transform.TransformPoint(renderedCupPosition[i]), transform.rotation * renderedCupRotation[i]);
                for (int die = 0; die < slot.Dice.Length; die++)
                {
                    if (slot.DiceVisuals == null || die >= slot.DiceVisuals.Length || slot.DiceVisuals[die] == null || !slot.Dice[die].gameObject.activeInHierarchy) continue;
                    var pose = DiceBodyPose(diceBodyIndices[i][die], slot.Dice[die]);
                    if (phase == 2 || phase == 3 || phase == 6)
                    {
                        renderedDicePosition[i][die] = renderedCupPosition[i] + renderedCupRotation[i] * DiceInsideCup(slot, die);
                        renderedDiceRotation[i][die] = renderedCupRotation[i] * Quaternion.Inverse(cupPose.Rotation) * pose.Rotation;
                    }
                    else if (resting)
                    {
                        renderedDicePosition[i][die] = IsServerInitialized ? restingDice[i][die] : pose.Position;
                        renderedDiceRotation[i][die] = IsServerInitialized ? restingDiceRotation[i][die] : pose.Rotation;
                    }
                    else
                    {
                        renderedDicePosition[i][die] = Vector3.Lerp(renderedDicePosition[i][die], pose.Position, blend);
                        renderedDiceRotation[i][die] = Quaternion.Slerp(renderedDiceRotation[i][die], pose.Rotation, blend);
                    }
                    slot.DiceVisuals[die].SetPositionAndRotation(transform.TransformPoint(renderedDicePosition[i][die]), transform.rotation * renderedDiceRotation[i][die]);
                }
            }
        }

        ShipV3PhysicsPose DiceBodyPose(int index, Rigidbody body) => !IsServerInitialized && index >= 0 ? bodyPoses[index] : new ShipV3PhysicsPose
        { Position = transform.InverseTransformPoint(body.position), Rotation = Quaternion.Inverse(transform.rotation) * body.rotation };

        public override void OnStopServer()
        {
            for (int i = 0; i < diceOwners.Count; i++) if (diceOwners[i] >= 0) ReleaseSlot(i);
        }
    }

}
