using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace PirateSlop.Networking
{
    public enum SeaLootKind : byte { None, Capture, Raft, Sunken, Shark, Skull, FloatingReward, Clam, Balance, Rigging, SternCargo, CannonBreach, SeagullKey, Platforming, Kamikaze }
    public enum SeaLootState : byte { Locked, Rising, Ready, Flying }

    public sealed partial class NetworkLootChest
    {
        readonly SyncVar<SeaLootKind> kind = new();
        readonly SyncVar<SeaLootState> phase = new(SeaLootState.Ready);
        readonly SyncVar<Vector3> eventPoint = new();
        readonly SyncVar<Vector3> anchor = new();
        readonly SyncVar<Quaternion> facing = new(Quaternion.identity);
        readonly SyncVar<NetworkObject> carrier = new();
        readonly SyncVar<NetworkObject> support = new();
        readonly SyncVar<int> supportId = new();
        readonly SyncVar<float> progress = new();
        readonly SyncVar<bool> contested = new();
        readonly SyncVar<int> captureShip = new();
        readonly SyncList<string> captureShipNames = new();
        readonly SyncVar<string> captureOwnerName = new("");
        readonly List<NetworkShip> captureParticipants = new();
        readonly SyncVar<int> lockRound = new();
        readonly SyncVar<bool> occupied = new();
        readonly SyncVar<bool> capturing = new();
        readonly SyncVar<float> sharksDistractedUntil = new();
        readonly SyncVar<Vector3> sharkBaitPoint = new();
        readonly SyncVar<NetworkObject> sharkTargetPlayer = new();
        NetworkWeapon worker;
        NetworkWeapon carryingPlayer;
        Vector3 workPosition;
        float heartbeat, riseStarted, nextCapture;
        NetworkShip deck;
        Collider chestCollider;
        Collider[] chestColliders;
        Renderer[] chestRenderers;
        GameObject eventVisual;
        SharkSwarmVisual swarm;
        bool visualCreated;
        internal bool BotLootCandidate => IsSpawned && !OnPlatforming && !captureRewardTaken.Value && carrier.Value == null && supportId.Value == 0 && (!InSeagullWreck || SeagullChestAvailable) && (!InFort || Fort != null && Fort.ChestAvailable) && (!InTow || TowedCargo != null && TowedCargo.LootReady) && (!OnWhale || Whale != null && Whale.ChestAvailable) && (!InClam || Clam != null && Clam.ChestAvailable) && (!OnBalance || Balance != null && Balance.ChestAvailable) && (!InRigging || Rigging != null && Rigging.ChestAvailable);
        internal bool BotCapturePending => Kind == SeaLootKind.Capture && phase.Value == SeaLootState.Locked;
        internal bool BotLootRising => phase.Value == SeaLootState.Rising || phase.Value == SeaLootState.Flying;
        internal Vector3 BotLootPoint => Kind == SeaLootKind.None ? transform.position : eventPoint.Value;
        public SeaLootKind Kind => kind.Value;
        public Vector3 EventPoint => eventPoint.Value;
        public NetworkObject Carrier => carrier.Value;
        public NetworkShip SupportingShip => support.Value != null ? support.Value.GetComponent<NetworkShip>() : deck;
        public void RefreshDeveloperShipPose()
        {
            if (Carrier != null) UpdateCarriedChestPose();
            else if (SupportingShip != null) transform.SetPositionAndRotation(SupportingShip.transform.TransformPoint(anchor.Value), SupportingShip.transform.rotation * facing.Value);
        }
        public bool SharksDistracted => Time.time < sharksDistractedUntil.Value;
        public Vector3 SharkBaitPoint => sharkBaitPoint.Value;
        public NetworkObject SharkTargetPlayer => sharkTargetPlayer.Value;
        public bool Available => IsSpawned && !captureRewardTaken.Value && phase.Value == SeaLootState.Ready && carrier.Value == null && (!InSeagullWreck || SeagullChestAvailable) && (!InFort || Fort != null && Fort.ChestAvailable) && (!InTow || TowedCargo != null && TowedCargo.LootReady) && (!OnWhale || Whale != null && Whale.ChestAvailable) && (!InClam || Clam != null && Clam.ChestAvailable) && (!OnBalance || Balance != null && Balance.ChestAvailable) && (!InRigging || Rigging != null && Rigging.ChestAvailable);
        public bool LootHintEligible
        {
            get
            {
                if (!IsSpawned || Kind == SeaLootKind.None || carrier.Value != null || supportId.Value != 0 || InTow && (TowedCargo == null || !TowedCargo.LootReady)) return false;
                if (Kind == SeaLootKind.FloatingReward || phase.Value == SeaLootState.Ready &&
                    (Kind == SeaLootKind.Sunken || Kind == SeaLootKind.Capture || Kind == SeaLootKind.Raft || Kind == SeaLootKind.Skull)) return false;
                for (int i = 0; i < contents.Count; i++) if (contents[i].Count > 0) return true;
                return false;
            }
        }
        public Vector3 LootHintPoint
        {
            get
            {
                var point = phase.Value == SeaLootState.Ready ? transform.position : eventPoint.Value;
                if (Kind == SeaLootKind.Sunken && phase.Value != SeaLootState.Ready && OceanSurface.Instance != null) point.y = OceanSurface.Instance.Height(point);
                return point + Vector3.up * 2f;
            }
        }
        public int LockRound => lockRound.Value;
        public float Progress => progress.Value;
        public string WorkHint => Kind == SeaLootKind.Raft
            ? "Мышь — угол отмычки · ЛКМ — повернуть замок · Q / E / Esc — отменить"
            : $"Верёвка: {Mathf.RoundToInt(Progress * 100)}% · плыви вокруг сундука · E / Q — отпустить";
        public string OceanHint => OnPlatforming ? PlatformingHint : InFort ? FortHint : InTow ? TowedCargo != null && TowedCargo.LootReady ? "E — открыть · F — снять сундук со стропы" : "Груз закреплён на канате" : InRigging ? RiggingHint : OnBalance ? BalanceHint : InClam ? ClamHint : OnWhale ? WhaleHint : phase.Value == SeaLootState.Flying ? "Череп выплюнул сундук"
            : phase.Value == SeaLootState.Rising ? "Ящик всплывает"
            : carrier.Value != null ? "Ящик несут"
            : phase.Value == SeaLootState.Ready ? (Kind == SeaLootKind.Shark && !SharksDistracted ? "E — открыть · F — нести ящик · Акулы агрессивны!" : "E — открыть · F — нести ящик")
            : Kind == SeaLootKind.Capture ? $"Захват: {Mathf.RoundToInt(Progress * 100)}%" + (contested.Value ? " · оспаривается" : " · удерживайте корабль в круге")
            : occupied.Value ? "Ящик занят"
            : Kind == SeaLootKind.Raft ? "E — взломать ящик" : "E — схватить верёвку · разматывай, плавая вокруг сундука";

        public Vector3 WorkPoint(Vector3 origin)
        {
            if (Kind != SeaLootKind.Sunken || phase.Value != SeaLootState.Locked) return transform.position + Vector3.up * .4f;
            return occupied.Value ? transform.position + Vector3.up * .4f : SunkenFreeEnd;
        }

        public void BoardRaft(NetworkWeapon player)
        {
            if (!IsServerInitialized || Kind != SeaLootKind.Raft || phase.Value != SeaLootState.Locked || !player.CanHandleLoot(this)) return;
            var motor = player.GetComponent<AdvancedPlayerController>();
            if (!motor.IsSwimming || Vector3.Distance(player.transform.position, eventPoint.Value) > 5) return;
            var rotation = RaftBody != null ? RaftBody.transform.rotation : Quaternion.identity;
            var center = RaftBody != null ? RaftBody.transform.position : eventPoint.Value;
            Vector3 relative = Quaternion.Inverse(rotation) * (player.transform.position - center);
            Vector3 destination = center + rotation * new Vector3(relative.x >= 0 ? 1.3f : -1.3f, .48f, Mathf.Clamp(relative.z, -1.25f, 1.25f));
            foreach (var hit in Physics.OverlapCapsule(destination + Vector3.up * .35f, destination + Vector3.up * 1.45f, .3f, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(player.transform)) return;
            player.GetComponent<NetworkPlayer>().Teleport(new PlayerState { Position = destination, Yaw = player.transform.eulerAngles.y });
            if (RaftBody != null) player.GetComponent<ShipDeckPassenger>().Attach(RaftBody);
        }

        public void ConfigureOcean(SeaLootKind value, Vector3 center)
        {
            kind.Value = value;
            phase.Value = value == SeaLootKind.Shark ? SeaLootState.Ready : SeaLootState.Locked;
            eventPoint.Value = center;
            anchor.Value = center + Vector3.up * (value == SeaLootKind.Sunken ? -Mathf.Max(3, Catalog.SunkenDepth) : value == SeaLootKind.Capture ? -CaptureChestDepth : value == SeaLootKind.Raft ? .45f : 0);
            transform.position = anchor.Value;
            if (value == SeaLootKind.Raft) InitializeRaft();
            if (value == SeaLootKind.Sunken) InitializeSunkenRope();
            if (value == SeaLootKind.Shark) InitializeSharks();
        }

        public void PlaceOnDeck(NetworkShip ship, Vector3 point, Quaternion rotation)
        {
            kind.Value = SeaLootKind.FloatingReward;
            phase.Value = SeaLootState.Ready;
            eventPoint.Value = point;
            deck = ship;
            support.Value = ship.NetworkObject;
            supportId.Value = ship.ParticipantId.Value;
            anchor.Value = ship.transform.InverseTransformPoint(point);
            facing.Value = Quaternion.Inverse(ship.transform.rotation) * rotation;
            transform.SetPositionAndRotation(point, rotation);
        }

        public void FeedSharks(InventoryItem fishType, Vector3 dropPoint)
        {
            if (!IsServerInitialized || Kind != SeaLootKind.Shark) return;
            float duration = fishType == InventoryItem.Swordfish ? 15f : 10f;
            sharksDistractedUntil.Value = Time.time + duration;
            sharkBaitPoint.Value = dropPoint;
            phase.Value = SeaLootState.Ready;
            FeedSharksRpc(dropPoint);
        }

        [ObserversRpc(RunLocally = true)]
        void FeedSharksRpc(Vector3 dropPoint)
        {
            GameAudio.Play(SoundCue.WaterSplash, dropPoint);
            GameAudio.Play(SoundCue.FishEat, dropPoint);
            CombatVfx.Splash(dropPoint);
        }

        public void StartWork(NetworkWeapon player)
        {
            if (!IsServerInitialized || phase.Value != SeaLootState.Locked || occupied.Value || (Kind != SeaLootKind.Raft && Kind != SeaLootKind.Sunken)) return;
            var motor = player.GetComponent<AdvancedPlayerController>();
            if (Kind == SeaLootKind.Raft && (motor.IsSwimming || !motor.IsGrounded || RaftBody == null || player.GetComponent<ShipDeckPassenger>().Support != RaftBody || Vector3.Distance(player.transform.position, eventPoint.Value) > 4)) return;
            if (Kind == SeaLootKind.Sunken)
            {
                if (!ValidSunkenPosition(player) || Vector3.Distance(player.transform.position + Vector3.up, SunkenFreeEnd) > 3.5f) return;
            }
            worker = player;
            workPosition = Kind == SeaLootKind.Raft && RaftBody != null ? RaftBody.transform.InverseTransformPoint(player.transform.position) : player.transform.position;
            occupied.Value = true;
            if (Kind != SeaLootKind.Sunken) progress.Value = 0;
            heartbeat = Time.time + (Kind == SeaLootKind.Sunken ? 2.5f : 1.5f);
            player.SetLootWork(this);
            if (Kind == SeaLootKind.Raft) BeginRaftLock();
            if (Kind == SeaLootKind.Sunken) BeginSunkenUnwrap();
        }

        public void WorkInput(NetworkWeapon player, int key, int round)
        {
            if (!IsServerInitialized || player != worker || !ValidWork()) return;
            heartbeat = Time.time + (Kind == SeaLootKind.Sunken ? 2f : 1f);
        }

        bool ValidWork()
        {
            if (worker == null || !worker.IsSpawned || !worker.CanHandleLoot(this, true)) return false;
            var motor = worker.GetComponent<AdvancedPlayerController>();
            return Kind == SeaLootKind.Raft
                ? !motor.IsSwimming && RaftBody != null && Vector3.Distance(RaftBody.transform.InverseTransformPoint(worker.transform.position), workPosition) < .6f
                : CanKeepSunkenWork(worker);
        }

        public void StopWork()
        {
            if (!IsServerInitialized) return;
            if (worker != null) worker.SetLootWork(null);
            worker = null;
            occupied.Value = false;
            lockTorque = false;
            if (Kind == SeaLootKind.Sunken) sunkenRopeEnd.Value = SunkenFreeEnd;
            if (phase.Value == SeaLootState.Locked && Kind == SeaLootKind.Raft) progress.Value = 0;
        }

        void Unlock()
        {
            StopWork();
            progress.Value = 1;
            if (Kind == SeaLootKind.Raft)
            {
                phase.Value = SeaLootState.Ready;
                anchor.Value = eventPoint.Value + Vector3.up * .45f;
                return;
            }
            phase.Value = SeaLootState.Rising;
            if (Kind == SeaLootKind.Capture)
            {
                captureBreakAge.Value = 0;
                captureReleasePoint = transform.position;
                anchor.Value = captureReleasePoint;
                riseStarted = Time.time;
                return;
            }
            anchor.Value = eventPoint.Value + (Kind == SeaLootKind.Raft ? Vector3.right * 4.5f : Vector3.zero) + Vector3.down * (Kind == SeaLootKind.Sunken ? Mathf.Max(3, Catalog.SunkenDepth) : 3f);
            transform.position = anchor.Value;
            riseStarted = Time.time;
        }

        void TickCapture()
        {
            captureParticipants.Clear();
            NetworkShip only = null;
            int count = 0;
            foreach (var ship in NetworkShip.ActiveShips)
            {
                if (ship == null || !ship.IsSpawned || ship.IsSinking) continue;
                Vector3 delta = ship.transform.position - eventPoint.Value;
                delta.y = 0;
                if (delta.sqrMagnitude > Catalog.CaptureRadius * Catalog.CaptureRadius) continue;
                count++;
                captureParticipants.Add(ship);
                only = ship;
            }
            PublishCaptureNames();
            contested.Value = count > 1;
            capturing.Value = count == 1;
            if (count > 1) return;
            if (count == 0 || (captureShip.Value != 0 && captureShip.Value != only.ParticipantId.Value && progress.Value > 0))
            {
                capturing.Value = false;
                progress.Value = Mathf.Max(0, progress.Value - .2f / Mathf.Max(1, Catalog.CaptureSeconds));
                if (progress.Value == 0) { captureShip.Value = 0; captureTeam.Value = 0; captureOwnerName.Value = ""; }
                return;
            }
            captureShip.Value = only.ParticipantId.Value;
            captureTeam.Value = only.TeamId.Value;
            captureOwnerName.Value = CaptureShipName(only);
            progress.Value = Mathf.Clamp01(progress.Value + .2f / Mathf.Max(1, Catalog.CaptureSeconds));
            if (progress.Value >= 1) Unlock();
        }

        static string CaptureShipName(NetworkShip ship)
        {
            var name = ship.GetComponent<PirateSlop.Customization.SailCustomizer>()?.CurrentData?.shipName;
            name = PirateSlop.Customization.ShipCustomizationData.NormalizeName(name);
            return string.IsNullOrEmpty(name) ? "Корабль " + ship.ParticipantId.Value : name;
        }

        void PublishCaptureNames()
        {
            captureParticipants.Sort((a, b) => a.ParticipantId.Value.CompareTo(b.ParticipantId.Value));
            bool changed = captureShipNames.Count != captureParticipants.Count;
            if (!changed)
                for (int i = 0; i < captureParticipants.Count; i++)
                    if (captureShipNames[i] != CaptureShipName(captureParticipants[i])) { changed = true; break; }
            if (!changed) return;
            captureShipNames.Clear();
            foreach (var ship in captureParticipants) captureShipNames.Add(CaptureShipName(ship));
        }

        public void Carry(NetworkWeapon player)
        {
            if (!IsServerInitialized || !Available || Kind == SeaLootKind.None) return;
            ResetLooseChestMotion();
            if (InKamikazeEvent) kamikazeSupport.Value = null;
            if (OnWhale) { whaleAttached.Value = false; whaleSupport.Value = null; }
            if (InClam) { inClam.Value = false; clamSupport.Value = null; }
            if (OnBalance) balanceSupport.Value = null;
            if (InRigging) riggingSupport.Value = null;
            if (InFort) fortSupport.Value = null;
            if (InSeagullWreck) seagullSupport.Value = null;
            if (OnPlatforming) { platformingSupport.Value = null; platformingLinked.Value = false; }
            if (InTow) { TowedCargo?.ChestTaken(player.GetComponent<ShipDeckPassenger>()?.Ship?.GetComponent<NetworkShip>()); ReleaseTowChest(); }
            if (Kind == SeaLootKind.Raft) raftDetachedState.Value = true;
            carryingPlayer = player;
            carrier.Value = player.NetworkObject;
            support.Value = null;
            supportId.Value = 0;
            deck = null;
            opened.Value = false;
            player.SetCarriedLoot(this);
            ChaseSharkCarrier(player.GetComponent<NetworkPlayer>());
        }

        public void Drop()
        {
            if (!IsServerInitialized) return;
            ResetLooseChestMotion();
            wasCarried = false;
            var player = carryingPlayer;
            Vector3 point = player != null ? player.transform.position + Vector3.up * .65f : transform.position;
            if (player != null)
            {
                Vector3 offset = player.transform.forward * 1.1f;
                float distance = offset.magnitude;
                foreach (var hit in Physics.SphereCastAll(point + Vector3.up * .4f, .45f, offset.normalized, distance, ~0, QueryTriggerInteraction.Ignore))
                    if (!hit.transform.IsChildOf(player.transform) && !hit.transform.IsChildOf(transform)) distance = Mathf.Min(distance, Mathf.Max(0, hit.distance - .05f));
                point += offset.normalized * distance;
            }
            Quaternion rotation = player != null ? player.transform.rotation : transform.rotation;
            if (player != null) player.SetCarriedLoot(null);
            carryingPlayer = null;
            carrier.Value = null;
            deck = null;
            float nearest = 4;
            RaycastHit floor = default;
            foreach (var hit in Physics.RaycastAll(point + Vector3.up * .3f, Vector3.down, 4, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!IsLooseChestFloor(hit) || hit.distance >= nearest) continue;
                var ship = hit.collider.GetComponentInParent<NetworkShip>();
                nearest = hit.distance;
                floor = hit;
                deck = ship;
            }
            support.Value = deck != null ? deck.NetworkObject : null;
            supportId.Value = deck != null ? deck.ParticipantId.Value : 0;
            anchor.Value = deck != null ? deck.transform.InverseTransformPoint(floor.point + Vector3.up * .03f) : point;
            facing.Value = deck != null ? Quaternion.Inverse(deck.transform.rotation) * rotation : rotation;
        }

        void UpdateOceanLoot()
        {
            if (UpdateKamikazeChest()) return;
            if (UpdatePlatformingChest()) return;
            if (UpdateSeagullChest()) return;
            if (UpdateFortChest()) return;
            if (UpdateTowChest()) return;
            if (UpdateRiggingChest()) return;
            if (UpdateBalanceChest()) return;
            if (UpdateClamChest()) return;
            if (UpdateWhaleChest()) return;
            if (!IsSpawned || Kind == SeaLootKind.None) return;
            if (!visualCreated) CreateOceanVisual();
            if (IsServerInitialized)
            {
                if (Kind == SeaLootKind.Capture && captureBreakAge.Value >= 0 && captureBreakAge.Value < 5f && phase.Value != SeaLootState.Rising)
                    captureBreakAge.Value = Mathf.Min(5f, Time.time - riseStarted);
                TickRewardFlight();
                if (Kind == SeaLootKind.Raft) TickRaft();
                if (occupied.Value)
                {
                    if (!ValidWork() || Time.time > heartbeat) StopWork();
                    else if (Kind == SeaLootKind.Sunken) TickSunkenUnwrap();
                    else if (Kind == SeaLootKind.Raft) TickRaftLock();
                }
                if (Kind == SeaLootKind.Capture && phase.Value == SeaLootState.Locked && Time.time >= nextCapture)
                {
                    nextCapture = Time.time + .2f;
                    TickCapture();
                }
                if (Kind == SeaLootKind.Shark)
                    UpdateSharkBehavior();
                if (phase.Value == SeaLootState.Rising)
                {
                    float age = Time.time - riseStarted;
                    var point = eventPoint.Value + (Kind == SeaLootKind.Raft ? Vector3.right * 4.5f : Vector3.zero);
                    if (Kind == SeaLootKind.Capture)
                    {
                        captureBreakAge.Value = age;
                        point = captureReleasePoint + Vector3.up * Mathf.Max(0, age - 1.1f) * Mathf.Max(.1f, Catalog.LootRiseSpeed);
                    }
                    else point.y -= Mathf.Max(0, (Kind == SeaLootKind.Sunken ? Mathf.Max(3, Catalog.SunkenDepth) : 3f) - age * Mathf.Max(.1f, Catalog.LootRiseSpeed));
                    anchor.Value = point;
                    float surface = Kind == SeaLootKind.Capture && OceanSurface.Instance != null ? OceanSurface.Instance.Height(point) : eventPoint.Value.y;
                    if (point.y >= surface) phase.Value = SeaLootState.Ready;
                }
                if (carryingPlayer != null && (!carryingPlayer.IsSpawned || carryingPlayer.GetComponent<AdvancedPlayerController>().IsDead)) Drop();
                if (carryingPlayer == null && carrier.Value != null) Drop();
                if (carryingPlayer == null && carrier.Value == null && wasCarried) Drop();
            }
            if (wasCarried != (carrier.Value != null)) ResetLooseChestMotion();
            wasCarried = carrier.Value != null;
            if (Kind == SeaLootKind.Raft) UpdateRaftVisual();
            if (support.Value != null) deck = support.Value.GetComponent<NetworkShip>();
            if (deck == null && supportId.Value != 0)
                foreach (var ship in NetworkShip.ActiveShips)
                    if (ship != null && ship.ParticipantId.Value == supportId.Value) { deck = ship; break; }
            if (IsServerInitialized && supportId.Value != 0 && (deck == null || !deck.IsSpawned || deck.IsSinking))
            {
                anchor.Value = transform.position;
                facing.Value = transform.rotation;
                support.Value = null;
                supportId.Value = 0;
                deck = null;
            }
            if (phase.Value == SeaLootState.Flying)
                transform.SetPositionAndRotation(RewardFlightPoint(), deck != null ? deck.transform.rotation * facing.Value : facing.Value);
            else if (carrier.Value != null)
            {
                UpdateCarriedChestPose();
            }
            else if (deck != null && supportId.Value != 0)
                transform.SetPositionAndRotation(deck.transform.TransformPoint(anchor.Value), deck.transform.rotation * facing.Value);
            else if (supportId.Value == 0)
            {
                Vector3 point = anchor.Value;
                if (Kind == SeaLootKind.Raft && !raftDetachedState.Value)
                    transform.SetPositionAndRotation(eventVisual.transform.TransformPoint(Vector3.up * .45f), eventVisual.transform.rotation);
                else if (Kind == SeaLootKind.Capture && phase.Value == SeaLootState.Locked && captureStation != null)
                    transform.SetPositionAndRotation(captureStation.position - captureStation.up * CaptureChestDepth, captureStation.rotation);
                else if (phase.Value == SeaLootState.Ready || (Kind == SeaLootKind.Shark && phase.Value == SeaLootState.Locked)) FloatChest(point);
                else transform.SetPositionAndRotation(Vector3.Lerp(transform.position, point, 1f - Mathf.Exp(-12f * Time.deltaTime)), facing.Value);
            }
            bool show = !captureRewardTaken.Value;
            foreach (var renderer in chestRenderers) if (renderer != null) renderer.enabled = show;
            if (chestColliders != null)
                foreach (var collider in chestColliders)
                    if (collider != null) collider.enabled = show && carrier.Value == null && phase.Value != SeaLootState.Flying && !(Kind == SeaLootKind.Capture && phase.Value != SeaLootState.Ready);
            UpdateObjectivePresentation();
        }

        public override void OnStopServer()
        {
            ServerChests.Remove(this);
            if (worker != null) worker.SetLootWork(null);
            if (carryingPlayer != null) carryingPlayer.SetCarriedLoot(null);
            worker = carryingPlayer = null;
            base.OnStopServer();
        }

        bool wasCarried;

        public override void OnStopNetwork()
        {
            ResetLooseChestMotion();
            presentedCarrier = null;
            presentedCarrierPlayer = null;
            wasCarried = false;
            ClientChests.Remove(this);
            if (eventVisual != null) Destroy(eventVisual);
            DestroyCaptureMeshes();
            if (sunkenGrip != null) Destroy(sunkenGrip);
            visualCreated = false;
            base.OnStopNetwork();
        }
    }
}
