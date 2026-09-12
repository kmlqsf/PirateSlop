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
        readonly Dictionary<NetworkShip, float> captures = new();
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
        public SeaLootKind Kind => kind.Value;
        public bool Available => IsSpawned && phase.Value == SeaLootState.Ready && carrier.Value == null;
        public int LockRound => lockRound.Value;
        public int LockKey => lockKey.Value;
        public float Progress => progress.Value;
        public string WorkHint => Kind == SeaLootKind.Raft
            ? $"Взлом: {lockSteps.Value}/{Mathf.Max(1, Catalog.LockpickSteps)} · нажмите {LockKey + 1}\nQ / E — отменить"
            : $"Отвязывание: {Mathf.RoundToInt(Progress * 100)}% · удерживайте E";
        public string OceanHint => phase.Value == SeaLootState.Rising ? "Ящик всплывает"
            : carrier.Value != null ? "Ящик несут"
            : phase.Value == SeaLootState.Ready ? "E — открыть · F — нести ящик"
            : Kind == SeaLootKind.Capture ? $"Захват: {Mathf.RoundToInt(Progress * 100)}%" + (contested.Value ? " · оспаривается" : " · удерживайте корабль в круге")
            : occupied.Value ? "Ящик занят"
            : Kind == SeaLootKind.Raft ? "E — взломать ящик" : "Удерживайте E — отвязать ящик";

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
                : motor.IsSwimming && worker.transform.position.y + 1.65f < eventPoint.Value.y;
        }

        public void StopWork()
        {
            if (!IsServerInitialized) return;
            if (worker != null) worker.SetLootWork(null);
            worker = null;
            occupied.Value = false;
            if (phase.Value == SeaLootState.Locked && Kind != SeaLootKind.Capture) progress.Value = 0;
        }

        void Unlock()
        {
            StopWork();
            progress.Value = 1;
            if (Kind == SeaLootKind.Sunken)
            {
                phase.Value = SeaLootState.Rising;
                riseStarted = Time.time;
            }
            else
            {
                phase.Value = SeaLootState.Ready;
                anchor.Value = eventPoint.Value + (Kind == SeaLootKind.Raft ? Vector3.right * 4.5f : Vector3.zero);
            }
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
            if (count != 1) return;
            captures.TryGetValue(only, out float elapsed);
            elapsed += .2f;
            captures[only] = elapsed;
            captureShip.Value = only.ParticipantId.Value;
            progress.Value = Mathf.Clamp01(elapsed / Mathf.Max(1, Catalog.CaptureSeconds));
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
                        if (progress.Value >= 1) Unlock();
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
                    var point = eventPoint.Value;
                    point.y -= Mathf.Max(0, Mathf.Max(3, Catalog.SunkenDepth) - (Time.time - riseStarted) * Mathf.Max(.1f, Catalog.LootRiseSpeed));
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
                if (phase.Value == SeaLootState.Ready) point.y = OceanSurface.Instance != null ? OceanSurface.Instance.Height(point) - .12f : eventPoint.Value.y;
                transform.SetPositionAndRotation(point, facing.Value);
            }
            bool show = Kind != SeaLootKind.Capture || phase.Value == SeaLootState.Ready;
            foreach (var renderer in chestRenderers) if (renderer != null) renderer.enabled = show;
            if (chestCollider != null) chestCollider.enabled = show && carrier.Value == null;
            if (ring != null)
            {
                ring.enabled = phase.Value == SeaLootState.Locked;
                ring.startColor = ring.endColor = contested.Value ? Color.red : new Color(1, .75f, .15f);
                markerMaterial.color = ring.startColor;
                if (markerMaterial.HasProperty("_BaseColor")) markerMaterial.SetColor("_BaseColor", ring.startColor);
            }
            if (rope != null) rope.enabled = phase.Value == SeaLootState.Locked;
        }

        public override void OnStopServer()
        {
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
            visualCreated = false;
            base.OnStopNetwork();
        }
    }
}
