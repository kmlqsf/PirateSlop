using System;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using PirateSlop.Networking;
using UnityEngine;

namespace PirateSlop.Ships
{
    public enum ShipV3TargetKind : byte { Lantern, Door, Bell, Dice, Dispenser }

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
        public Rigidbody[] Dice;
        public Vector3[] FaceNormals;
        public int[] FaceValues;
        public Vector3 RestCup;
        public Quaternion RestRotation;
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
        public Transform AnchorTravel;
        public ConfigurableJoint MovingAnchorJoint;
        readonly SyncVar<int> lights = new(63);
        readonly SyncVar<float> door = new();
        readonly SyncVar<int> doorHolder = new(-1), bellHolder = new(-1);
        readonly SyncVar<int> dispenserHolder = new(-1);
        readonly SyncVar<float> dispenserPull = new(), dispenserReturnAt = new();
        readonly SyncList<int> diceOwners = new();
        readonly SyncList<string> diceResults = new();
        Quaternion doorRest, clapperRest;
        Quaternion dispenserRest;
        Vector3[][] restingDice;
        Quaternion[][] restingDiceRotation;
        NetworkShip ship;
        float[] slotHeartbeat, collectionStart, settledFor, rollStart;
        Vector3[][] collectionFrom;
        Vector2[] cupPosition;
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
        public bool CanUseDice => DiceTable != null && DiceTable.gameObject.activeInHierarchy && (DiceSupport == null || DiceSupport.activeInHierarchy);
        public bool DispenserCooling => dispenserReturnAt.Value > 0f;

        public bool CanReachDice(NetworkPlayer player)
        {
            if (!CanUseDice || player == null || player.Motor == null || player.Motor.IsDead || player.Motor.IsSwimming || player.Motor.IsClimbing) return false;
            Vector3 point = DiceTable.position + transform.up * .12f;
            if (Vector3.Distance(player.transform.position + Vector3.up, point) > 3.2f) return false;
            Vector3 origin = player.transform.position + Vector3.up * 1.5f;
            Vector3 delta = point - origin;
            int count = Physics.RaycastNonAlloc(origin, delta.normalized, diceObstacles, Mathf.Max(0f, delta.magnitude - .025f), ~0, QueryTriggerInteraction.Ignore);
            if (count == diceObstacles.Length) return false;
            for (int i = 0; i < count; i++)
            {
                var hit = diceObstacles[i];
                if (hit.transform.IsChildOf(player.transform) || hit.transform.IsChildOf(DiceTable.parent) ||
                    hit.collider.GetComponentInParent<AdvancedPlayerController>() != null || hit.collider.GetComponentInParent<Cannonball>() != null) continue;
                return false;
            }
            return true;
        }

        void Awake()
        {
            ship = GetComponent<NetworkShip>();
            doorRest = DoorHinge != null ? DoorHinge.localRotation : Quaternion.identity;
            clapperRest = BellClapper != null ? BellClapper.transform.localRotation : Quaternion.identity;
            dispenserRest = DispenserLever != null ? DispenserLever.localRotation : Quaternion.identity;
            slotHeartbeat = new float[DiceSlots.Length];
            collectionStart = new float[DiceSlots.Length];
            collectionFrom = new Vector3[DiceSlots.Length][];
            settledFor = new float[DiceSlots.Length];
            rollStart = new float[DiceSlots.Length];
            cupPosition = new Vector2[DiceSlots.Length];
            diceMode = new byte[DiceSlots.Length];
            restingDice = new Vector3[DiceSlots.Length][];
            restingDiceRotation = new Quaternion[DiceSlots.Length][];
            for (int i = 0; i < DiceSlots.Length; i++)
            {
                restingDice[i] = Array.ConvertAll(DiceSlots[i].Dice, die => transform.InverseTransformPoint(die.transform.position));
                restingDiceRotation[i] = Array.ConvertAll(DiceSlots[i].Dice, die => Quaternion.Inverse(transform.rotation) * die.transform.rotation);
            }
            glassBlock = new MaterialPropertyBlock();
        }

        public override void OnStartServer()
        {
            for (int i = 0; i < DiceSlots.Length; i++) { diceOwners.Add(-1); diceResults.Add(""); }
            foreach (var body in PhysicsBodies) if (body != null) body.isKinematic = false;
            foreach (var slot in DiceSlots) if (slot.Cup != null) slot.Cup.isKinematic = true;
            foreach (var slot in DiceSlots) foreach (var die in slot.Dice) if (die != null) die.isKinematic = true;
        }

        public override void OnStartClient()
        {
            if (!IsServerInitialized) foreach (var body in PhysicsBodies) if (body != null) body.isKinematic = true;
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
        }

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
                bellForce = Vector2.ClampMagnitude(bellForce + Vector2.ClampMagnitude(delta, 60f) * .55f, 28f);
                lastBellDrag = Time.time;
                BellClapper.WakeUp();
            }
        }

        public void ClapperContact(float speed, Vector3 point)
        {
            if (!IsServerInitialized || speed < .18f || Time.time < nextRing) return;
            if (bellHolder.Value >= 0 && Time.time - lastBellDrag > 1.2f) return;
            nextRing = Time.time + .28f;
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
            if (DispenserCooling || !float.IsFinite(delta) || !Reach(Sender(sender), DispenserGrip) || dispenserHolder.Value >= 0 && dispenserHolder.Value != sender.ClientId) return;
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
            for (int i = 0; i < diceOwners.Count; i++)
            {
                if (diceOwners[i] != -1) continue;
                diceOwners[i] = sender.ClientId; slotHeartbeat[i] = Time.time;
                player.Motor.ShipActivityLocked = true;
                return;
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void DiceInput(Vector2 movement, bool holding, bool gather, bool leave, NetworkConnection sender = null)
        {
            if (sender == null) return;
            int index = LocalDiceSlot(sender.ClientId);
            if (index < 0) return;
            var player = Sender(sender);
            if (leave || !CanReachDice(player)) { ReleaseSlot(index); return; }
            if (!float.IsFinite(movement.sqrMagnitude)) return;
            slotHeartbeat[index] = Time.time;
            if (gather && (diceMode[index] == 0 || diceMode[index] == 5))
            {
                diceMode[index] = 1; collectionStart[index] = Time.time;
                collectionFrom[index] = new Vector3[DiceSlots[index].Dice.Length];
                for (int i = 0; i < collectionFrom[index].Length; i++)
                {
                    var die = DiceSlots[index].Dice[i];
                    collectionFrom[index][i] = transform.InverseTransformPoint(die.position);
                    die.isKinematic = true;
                }
                diceResults[index] = "";
            }
            if (holding)
            {
                cupPosition[index] = Vector2.ClampMagnitude(cupPosition[index] + Vector2.ClampMagnitude(movement, .045f), .16f);
                if (diceMode[index] == 2) diceMode[index] = 3;
            }
            else if (diceMode[index] == 3)
            {
                diceMode[index] = 4; settledFor[index] = 0f; rollStart[index] = Time.time;
                var cup = DiceSlots[index].Cup;
                for (int i = 0; i < DiceSlots[index].Dice.Length; i++)
                {
                    var die = DiceSlots[index].Dice[i];
                    die.isKinematic = false;
                    die.linearVelocity = ship.Motor.CannonPointVelocity(die.position) + transform.up * UnityEngine.Random.Range(.6f, 1.1f)
                        + transform.TransformDirection(new Vector3(UnityEngine.Random.Range(-.45f, .45f), 0, UnityEngine.Random.Range(-.45f, .45f)));
                    die.angularVelocity = UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(12f, 25f);
                    die.WakeUp();
                }
                cup.MoveRotation(transform.rotation * Quaternion.Euler(0, 0, -125f) * DiceSlots[index].RestRotation);
            }
        }

        void ReleaseSlot(int index)
        {
            var player = SessionController.Instance.GetPlayer(diceOwners[index]);
            if (player != null) player.Motor.ShipActivityLocked = false;
            diceOwners[index] = -1;
            if (diceMode[index] == 4) FreezeDice(index);
            foreach (var die in DiceSlots[index].Dice) if (die != null) die.isKinematic = true;
            diceMode[index] = 0;
        }

        void KeepDiceOnTable(Rigidbody die)
        {
            Vector3 local = transform.InverseTransformPoint(die.position);
            Vector3 center = transform.InverseTransformPoint(DiceTable.position);
            var offset = new Vector2(local.x - center.x, local.z - center.z);
            float limit = Mathf.Max(.1f, DiceRadius - .035f);
            if (offset.magnitude > limit)
            {
                var edge = offset.normalized * limit;
                local.x = center.x + edge.x; local.z = center.z + edge.y;
                Vector3 normal = transform.TransformDirection(new Vector3(offset.normalized.x, 0, offset.normalized.y));
                Vector3 relative = die.linearVelocity - ship.Motor.CannonPointVelocity(die.position);
                die.linearVelocity -= normal * Mathf.Max(0f, Vector3.Dot(relative, normal)) * 1.25f;
            }
            if (local.y < center.y - .04f || local.y > center.y + .75f)
            {
                local.y = Mathf.Clamp(local.y, center.y + .03f, center.y + .6f);
                die.linearVelocity = ship.Motor.CannonPointVelocity(transform.TransformPoint(local));
            }
            die.position = transform.TransformPoint(local);
        }

        void FreezeDice(int index)
        {
            for (int i = 0; i < DiceSlots[index].Dice.Length; i++)
            {
                var die = DiceSlots[index].Dice[i]; KeepDiceOnTable(die);
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
                Quaternion desired = Quaternion.AngleAxis(-bellForce.y, transform.right) * Quaternion.AngleAxis(-bellForce.x, transform.forward)
                    * BellClapper.transform.parent.rotation * clapperRest;
                Quaternion error = desired * Quaternion.Inverse(BellClapper.rotation);
                error.ToAngleAxis(out float angle, out Vector3 axis);
                if (angle > 180f) angle -= 360f;
                if (axis.sqrMagnitude > .001f && float.IsFinite(axis.sqrMagnitude))
                    BellClapper.AddTorque(axis.normalized * (angle * Mathf.Deg2Rad * 55f) - BellClapper.angularVelocity * 10f, ForceMode.Acceleration);
            }
            for (int index = 0; index < DiceSlots.Length; index++)
            {
                var slot = DiceSlots[index];
                bool owned = diceOwners[index] >= 0;
                Vector3 desired = transform.TransformPoint(slot.RestCup + new Vector3(owned ? cupPosition[index].x : 0, diceMode[index] == 3 || diceMode[index] == 4 ? .16f : .005f, owned ? cupPosition[index].y : 0));
                Vector3 cupDelta = Vector3.ProjectOnPlane(desired - DiceTable.position, transform.up);
                desired -= cupDelta - Vector3.ClampMagnitude(cupDelta, Mathf.Max(.1f, DiceRadius - .09f));
                slot.Cup.MovePosition(desired);
                if (diceMode[index] != 4) slot.Cup.MoveRotation(transform.rotation * slot.RestRotation);
                if (!owned || diceMode[index] == 0 || diceMode[index] == 5)
                {
                    for (int i = 0; i < slot.Dice.Length; i++)
                    {
                        slot.Dice[i].MovePosition(transform.TransformPoint(restingDice[index][i]));
                        slot.Dice[i].MoveRotation(transform.rotation * restingDiceRotation[index][i]);
                    }
                }
                else if (diceMode[index] == 1)
                {
                    float t = Mathf.Clamp01((Time.time - collectionStart[index]) / .55f);
                    for (int i = 0; i < slot.Dice.Length; i++)
                    {
                        Vector3 destination = transform.InverseTransformPoint(desired) + new Vector3((i % 2 - .5f) * .034f, .07f + i / 2 * .033f, 0);
                        slot.Dice[i].MovePosition(transform.TransformPoint(Vector3.Lerp(collectionFrom[index][i], destination, t) + Vector3.up * (.3f * 4 * t * (1 - t))));
                    }
                    if (t >= 1) diceMode[index] = 2;
                }
                else if (diceMode[index] == 2 || diceMode[index] == 3)
                {
                    for (int i = 0; i < slot.Dice.Length; i++)
                    {
                        float shake = diceMode[index] == 3 ? Mathf.Sin(Time.time * 32 + i) * .015f : 0f;
                        slot.Dice[i].MovePosition(desired + transform.TransformDirection(new Vector3((i % 2 - .5f) * .035f + shake, .07f + i / 2 * .033f, shake)));
                        if (diceMode[index] == 3) slot.Dice[i].MoveRotation(UnityEngine.Random.rotation);
                    }
                }
                else if (diceMode[index] == 4)
                {
                    bool settled = true;
                    foreach (var die in slot.Dice)
                    {
                        KeepDiceOnTable(die);
                        if ((die.linearVelocity - ship.Motor.CannonPointVelocity(die.position)).sqrMagnitude > .012f || (die.angularVelocity - ship.Motor.MotionAngularVelocity).sqrMagnitude > .08f) settled = false;
                    }
                    settledFor[index] = settled ? settledFor[index] + Time.fixedDeltaTime : 0f;
                    if (settledFor[index] > .5f || Time.time - rollStart[index] > 6f)
                    {
                        var values = new List<string>();
                        foreach (var die in slot.Dice)
                        {
                            int best = 0; float dot = -2f;
                            for (int face = 0; face < slot.FaceNormals.Length; face++)
                            {
                                float score = Vector3.Dot(die.transform.TransformDirection(slot.FaceNormals[face]), transform.up);
                                if (score > dot) { dot = score; best = face; }
                            }
                            values.Add(slot.FaceValues[best].ToString());
                            die.rotation = Quaternion.FromToRotation(die.transform.TransformDirection(slot.FaceNormals[best]), transform.up) * die.rotation;
                        }
                        diceResults[index] = string.Join(" · ", values); FreezeDice(index); diceMode[index] = 5;
                    }
                }
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
            bool lampsChanged = appliedLights != lights.Value;
            for (int i = 0; i < Lanterns.Length; i++)
            {
                bool lit = (lights.Value & (1 << i)) != 0;
                var lamp = Lanterns[i];
                if (lamp.Light != null)
                {
                    lamp.Light.enabled = lit;
                    lamp.Light.intensity = .72f + .055f * Mathf.Sin(time * 9.7f + i * 2.1f) + .035f * Mathf.Sin(time * 16.3f + i);
                }
                if (lampsChanged && lamp.Glass != null)
                {
                    lamp.Glass.GetPropertyBlock(glassBlock, lamp.GlassSlot);
                    glassBlock.SetColor("_EmissionColor", lit ? new Color(.65f, .22f, .045f) * 1.1f : Color.black);
                    lamp.Glass.SetPropertyBlock(glassBlock, lamp.GlassSlot);
                }
            }
            appliedLights = lights.Value;
            uint currentRevision = ship.GetComponent<ShipDestruction>().Revision;
            if (currentRevision != attachmentRevision || Time.unscaledTime >= nextAttachmentCheck)
            {
                attachmentRevision = currentRevision;
                nextAttachmentCheck = Time.unscaledTime + .25f;
                UpdateAttachments();
            }
            if (!IsServerInitialized) return;
            if (doorHolder.Value >= 0 && Time.time - doorHeartbeat > .65f) doorHolder.Value = -1;
            if (bellHolder.Value >= 0 && Time.time - bellHeartbeat > .65f) { bellHolder.Value = -1; bellForce = Vector2.zero; }
            for (int i = 0; i < diceOwners.Count; i++)
                if (diceOwners[i] >= 0 && (Time.time - slotHeartbeat[i] > .9f || SessionController.Instance.GetPlayer(diceOwners[i]) is not { } player || !CanReachDice(player))) ReleaseSlot(i);
            if (Time.unscaledTime >= nextPublish)
            {
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
                    if (PhysicsBodies[i] != null) frame.Add(new ShipV3PhysicsPose { Index = i, Position = transform.InverseTransformPoint(PhysicsBodies[i].position), Rotation = Quaternion.Inverse(transform.rotation) * PhysicsBodies[i].rotation });
                for (int i = 0; i < Attachments.Length; i++)
                    if (Attachments[i].Detached && Attachments[i].Object != null)
                        frame.Add(new ShipV3PhysicsPose { Index = PhysicsBodies.Length + i, Position = transform.InverseTransformPoint(Attachments[i].Object.position), Rotation = Quaternion.Inverse(transform.rotation) * Attachments[i].Object.rotation });
                ReceivePhysics(++serverRevision, frame.ToArray());
            }
        }

        void UpdateAttachments()
        {
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
        }

        void LateUpdate()
        {
            if (IsServerInitialized || remotePoses == null) return;
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
        }

        public override void OnStopServer()
        {
            for (int i = 0; i < diceOwners.Count; i++) if (diceOwners[i] >= 0) ReleaseSlot(i);
        }
    }

}
