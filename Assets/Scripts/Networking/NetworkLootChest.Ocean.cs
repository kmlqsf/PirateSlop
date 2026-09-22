using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace PirateSlop.Networking
{
    public enum SeaLootKind : byte { None, Capture, Raft, Sunken }
    public enum SeaLootState : byte { Locked, Rising, Ready }

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
        readonly SyncVar<int> lockKey = new();
        readonly SyncVar<int> lockRound = new();
        readonly SyncVar<int> lockSteps = new();
        readonly SyncVar<bool> occupied = new();
        readonly SyncVar<bool> capturing = new();
        readonly SyncVar<int> releasedTethers = new();
        readonly SyncVar<int> activeTether = new(-1);
        NetworkWeapon worker;
        NetworkWeapon carryingPlayer;
        Vector3 workPosition;
        float heartbeat, keyAfter, keyExpires, riseStarted, nextCapture;
        NetworkShip deck;
        Collider chestCollider;
        Renderer[] chestRenderers;
        GameObject eventVisual;
        LineRenderer ring;
        LineRenderer rope;
        Material markerMaterial;
        bool visualCreated;
        internal bool BotLootCandidate => IsSpawned && carrier.Value == null && supportId.Value == 0;
        internal bool BotCapturePending => Kind == SeaLootKind.Capture && phase.Value == SeaLootState.Locked;
        internal bool BotLootRising => phase.Value == SeaLootState.Rising;
        internal Vector3 BotLootPoint => Kind == SeaLootKind.None ? transform.position : eventPoint.Value;
        public SeaLootKind Kind => kind.Value;
        public bool Available => IsSpawned && phase.Value == SeaLootState.Ready && carrier.Value == null;
        public int LockRound => lockRound.Value;
        public int LockKey => lockKey.Value;
        public float Progress => progress.Value;
        public string WorkHint => Kind == SeaLootKind.Raft
            ? $"Взлом: {lockSteps.Value}/{Mathf.Max(1, Catalog.LockpickSteps)} · нажмите {LockKey + 1}\nQ / E — отменить"
            : $"Крепление {activeTether.Value + 1}/3: {Mathf.RoundToInt(Progress * 100)}% · удерживайте E";
        public string OceanHint => phase.Value == SeaLootState.Rising ? "Ящик всплывает"
            : carrier.Value != null ? "Ящик несут"
            : phase.Value == SeaLootState.Ready ? "E — открыть · F — нести ящик"
            : Kind == SeaLootKind.Capture ? $"Захват: {Mathf.RoundToInt(Progress * 100)}%" + (contested.Value ? " · оспаривается" : " · удерживайте корабль в круге")
            : occupied.Value ? "Ящик занят"
            : Kind == SeaLootKind.Raft ? "E — взломать ящик" : $"Освобождено {ReleasedCount}/3 · подплывите к креплению и удерживайте E";

        int ReleasedCount => ((releasedTethers.Value & 1) != 0 ? 1 : 0) + ((releasedTethers.Value & 2) != 0 ? 1 : 0) + ((releasedTethers.Value & 4) != 0 ? 1 : 0);
        Vector3 TetherPoint(int index)
        {
            float angle = index * Mathf.PI * 2f / 3f;
            return eventPoint.Value + new Vector3(Mathf.Cos(angle) * 2.3f, -Mathf.Max(3, Catalog.SunkenDepth), Mathf.Sin(angle) * 2.3f);
        }
        public Vector3 WorkPoint(Vector3 origin)
        {
            if (Kind != SeaLootKind.Sunken || phase.Value != SeaLootState.Locked) return transform.position + Vector3.up * .4f;
            int nearest = NearestTether(origin);
            return nearest < 0 ? transform.position : TetherPoint(nearest);
        }
        int NearestTether(Vector3 origin)
        {
            int nearest = -1;
            float distance = float.PositiveInfinity;
            for (int i = 0; i < 3; i++)
                if ((releasedTethers.Value & (1 << i)) == 0 && (TetherPoint(i) - origin).sqrMagnitude < distance)
                { nearest = i; distance = (TetherPoint(i) - origin).sqrMagnitude; }
            return nearest;
        }

        public void BoardRaft(NetworkWeapon player)
        {
            if (!IsServerInitialized || Kind != SeaLootKind.Raft || phase.Value != SeaLootState.Locked || !player.CanHandleLoot(this)) return;
            var motor = player.GetComponent<AdvancedPlayerController>();
            if (!motor.IsSwimming || Vector3.Distance(player.transform.position, eventPoint.Value) > 5) return;
            Vector3 relative = player.transform.position - eventPoint.Value;
            Vector3 destination = eventPoint.Value + new Vector3(relative.x >= 0 ? 1.3f : -1.3f, .48f, Mathf.Clamp(relative.z, -1.25f, 1.25f));
            foreach (var hit in Physics.OverlapCapsule(destination + Vector3.up * .35f, destination + Vector3.up * 1.45f, .3f, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(player.transform)) return;
            player.GetComponent<NetworkPlayer>().Teleport(new PlayerState { Position = destination, Yaw = player.transform.eulerAngles.y });
        }

        public void ConfigureOcean(SeaLootKind value, Vector3 center)
        {
            kind.Value = value;
            phase.Value = SeaLootState.Locked;
            eventPoint.Value = center;
            anchor.Value = center + Vector3.up * (value == SeaLootKind.Sunken ? -Mathf.Max(3, Catalog.SunkenDepth) : value == SeaLootKind.Raft ? .45f : 0);
            transform.position = anchor.Value;
        }

        public void StartWork(NetworkWeapon player)
        {
            if (!IsServerInitialized || phase.Value != SeaLootState.Locked || occupied.Value || (Kind != SeaLootKind.Raft && Kind != SeaLootKind.Sunken)) return;
            var motor = player.GetComponent<AdvancedPlayerController>();
            if (Kind == SeaLootKind.Raft && (motor.IsSwimming || !motor.IsGrounded || Vector3.Distance(player.transform.position, eventPoint.Value) > 4)) return;
            if (Kind == SeaLootKind.Sunken && (!motor.IsSwimming || player.transform.position.y + 1.65f >= eventPoint.Value.y)) return;
            if (Kind == SeaLootKind.Sunken)
            {
                int index = NearestTether(player.transform.position + Vector3.up);
                if (index < 0 || Vector3.Distance(player.transform.position + Vector3.up, TetherPoint(index)) > 1.8f) return;
                activeTether.Value = index;
            }
            worker = player;
            workPosition = player.transform.position;
            occupied.Value = true;
            progress.Value = 0;
            lockSteps.Value = 0;
            heartbeat = Time.time + 1.5f;
            player.SetLootWork(this);
            if (Kind == SeaLootKind.Raft) NextLock();
        }

        void NextLock()
        {
            lockKey.Value = Random.Range(0, 4);
            lockRound.Value++;
            keyAfter = Time.time + .75f;
            keyExpires = Time.time + 6;
        }

        public void WorkInput(NetworkWeapon player, int key, int round)
        {
            if (!IsServerInitialized || player != worker || !ValidWork()) return;
            heartbeat = Time.time + 1;
            if (Kind != SeaLootKind.Raft || key < 0 || round != lockRound.Value) return;
            if (key != lockKey.Value || Time.time < keyAfter || Time.time > keyExpires)
            {
                lockSteps.Value = 0;
                progress.Value = 0;
                NextLock();
                return;
            }
            lockSteps.Value++;
            progress.Value = (float)lockSteps.Value / Mathf.Max(1, Catalog.LockpickSteps);
            if (progress.Value >= 1) Unlock();
            else NextLock();
        }

        bool ValidWork()
        {
            if (worker == null || !worker.IsSpawned || !worker.CanHandleLoot(this, true)) return false;
            var motor = worker.GetComponent<AdvancedPlayerController>();
            return Kind == SeaLootKind.Raft
                ? !motor.IsSwimming && Vector3.Distance(worker.transform.position, workPosition) < .6f
                : motor.IsSwimming && activeTether.Value >= 0 && Vector3.Distance(worker.transform.position + Vector3.up, TetherPoint(activeTether.Value)) <= 1.8f && worker.transform.position.y + 1.65f < eventPoint.Value.y;
        }

        public void StopWork()
        {
            if (!IsServerInitialized) return;
            if (worker != null) worker.SetLootWork(null);
            worker = null;
            occupied.Value = false;
            activeTether.Value = -1;
            if (phase.Value == SeaLootState.Locked && Kind != SeaLootKind.Capture) progress.Value = 0;
        }

        void Unlock()
        {
            StopWork();
            progress.Value = 1;
            phase.Value = SeaLootState.Rising;
            anchor.Value = eventPoint.Value + (Kind == SeaLootKind.Raft ? Vector3.right * 4.5f : Vector3.zero) + Vector3.down * (Kind == SeaLootKind.Sunken ? Mathf.Max(3, Catalog.SunkenDepth) : 3f);
            transform.position = anchor.Value;
            riseStarted = Time.time;
        }

        void TickCapture()
        {
            NetworkShip only = null;
            int count = 0;
            foreach (var ship in NetworkShip.ActiveShips)
            {
                if (ship == null || !ship.IsSpawned || ship.IsSinking) continue;
                Vector3 delta = ship.transform.position - eventPoint.Value;
                delta.y = 0;
                if (delta.sqrMagnitude > Catalog.CaptureRadius * Catalog.CaptureRadius) continue;
                count++;
                only = ship;
            }
            contested.Value = count > 1;
            capturing.Value = count == 1;
            if (count > 1) return;
            if (count == 0 || (captureShip.Value != 0 && captureShip.Value != only.ParticipantId.Value && progress.Value > 0))
            {
                capturing.Value = false;
                progress.Value = Mathf.Max(0, progress.Value - .2f / Mathf.Max(1, Catalog.CaptureSeconds));
                if (progress.Value == 0) captureShip.Value = 0;
                return;
            }
            captureShip.Value = only.ParticipantId.Value;
            progress.Value = Mathf.Clamp01(progress.Value + .2f / Mathf.Max(1, Catalog.CaptureSeconds));
            if (progress.Value >= 1) Unlock();
        }

        public void Carry(NetworkWeapon player)
        {
            if (!IsServerInitialized || !Available || Kind == SeaLootKind.None) return;
            carryingPlayer = player;
            carrier.Value = player.NetworkObject;
            support.Value = null;
            supportId.Value = 0;
            deck = null;
            opened.Value = false;
            player.SetCarriedLoot(this);
        }

        public void Drop()
        {
            if (!IsServerInitialized) return;
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
                if (hit.transform.IsChildOf(transform) || (player != null && hit.transform.IsChildOf(player.transform)) || hit.normal.y < .7f) continue;
                var ship = hit.collider.GetComponentInParent<NetworkShip>();
                if (ship == null || ship.IsSinking || hit.distance >= nearest) continue;
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
            if (!IsSpawned || Kind == SeaLootKind.None) return;
            if (!visualCreated) CreateOceanVisual();
            if (IsServerInitialized)
            {
                if (occupied.Value)
                {
                    if (!ValidWork() || Time.time > heartbeat) StopWork();
                    else if (Kind == SeaLootKind.Sunken)
                    {
                        progress.Value += Time.deltaTime / Mathf.Max(1, Catalog.UntieSeconds);
                        if (progress.Value >= 1)
                        {
                            releasedTethers.Value |= 1 << activeTether.Value;
                            StopWork();
                            if (releasedTethers.Value == 7) Unlock();
                        }
                    }
                    else if (Time.time > keyExpires) { lockSteps.Value = 0; progress.Value = 0; NextLock(); }
                }
                if (Kind == SeaLootKind.Capture && phase.Value == SeaLootState.Locked && Time.time >= nextCapture)
                {
                    nextCapture = Time.time + .2f;
                    TickCapture();
                }
                if (phase.Value == SeaLootState.Rising)
                {
                    var point = eventPoint.Value + (Kind == SeaLootKind.Raft ? Vector3.right * 4.5f : Vector3.zero);
                    point.y -= Mathf.Max(0, (Kind == SeaLootKind.Sunken ? Mathf.Max(3, Catalog.SunkenDepth) : 3f) - (Time.time - riseStarted) * Mathf.Max(.1f, Catalog.LootRiseSpeed));
                    anchor.Value = point;
                    if (point.y >= eventPoint.Value.y) phase.Value = SeaLootState.Ready;
                }
                if (carryingPlayer != null && (!carryingPlayer.IsSpawned || carryingPlayer.GetComponent<AdvancedPlayerController>().IsDead)) Drop();
                if (carryingPlayer == null && carrier.Value != null) Drop();
                if (carryingPlayer == null && carrier.Value == null && wasCarried) Drop();
            }
            wasCarried = carrier.Value != null;
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
            if (carrier.Value != null)
            {
                var holder = carrier.Value.transform;
                transform.SetPositionAndRotation(holder.position + holder.forward * .9f + holder.right * .35f + Vector3.up * .65f, holder.rotation);
            }
            else if (deck != null && supportId.Value != 0)
                transform.SetPositionAndRotation(deck.transform.TransformPoint(anchor.Value), deck.transform.rotation * facing.Value);
            else if (supportId.Value == 0)
            {
                Vector3 point = anchor.Value;
                if (phase.Value == SeaLootState.Ready) FloatChest(point);
                else transform.SetPositionAndRotation(Vector3.Lerp(transform.position, point, 1f - Mathf.Exp(-12f * Time.deltaTime)), facing.Value);
            }
            bool show = Kind != SeaLootKind.Capture || phase.Value != SeaLootState.Locked;
            foreach (var renderer in chestRenderers) if (renderer != null) renderer.enabled = show;
            if (chestCollider != null) chestCollider.enabled = show && carrier.Value == null;
            if (ring != null)
            {
                ring.enabled = phase.Value == SeaLootState.Locked;
                ring.startColor = ring.endColor = new Color(.55f, .55f, .55f);
                markerMaterial.SetColor("_BaseColor", Color.white);
            }
            if (rope != null) rope.enabled = phase.Value == SeaLootState.Locked;
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
            if (eventVisual != null) Destroy(eventVisual);
            if (markerMaterial != null) Destroy(markerMaterial);
            if (arcMaterial != null) Destroy(arcMaterial);
            visualCreated = false;
            base.OnStopNetwork();
        }
    }
}
