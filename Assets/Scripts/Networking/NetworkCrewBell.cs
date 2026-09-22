using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PirateSlop.Networking
{
    [DefaultExecutionOrder(-40)]
    public sealed class NetworkCrewBell : NetworkBehaviour
    {
        AdvancedPlayerController motor;
        NetworkPlayer player;
        Transform aimed;
        float nextRing, requestAt = -30f, messageUntil;
        string crewMessage;
        string bellStatus;
        float statusAt;
        bool requested, serverRequested;
        void Awake() { motor = GetComponent<AdvancedPlayerController>(); player = GetComponent<NetworkPlayer>(); }
        readonly SyncVar<bool> pulling = new();
        readonly SyncVar<float> pullAmount = new();
        CrewBellMotion motion;
        bool localPull, wasPulling;
        float localAmount, nextSend, lastPullAt, serverAmount, serverStarted;
        public uint ServerRingCount { get; private set; }
        public bool IsPulling => localPull || pulling.Value;
        public Vector3 HandPoint => motion != null ? motion.GripPoint : transform.position;
        CrewBellMotion GetMotion()
        {
            if (player.Ship == null) return null;
            var bell = player.Ship.transform.Find("CrewBell");
            if (bell == null || !bell.gameObject.activeInHierarchy) return null;
            var result = bell.GetComponent<CrewBellMotion>();
            return result != null ? result : bell.gameObject.AddComponent<CrewBellMotion>();
        }
        void Update()
        {
            aimed = null;
            if (!motor.IsDead) { requested = false; if (IsServerInitialized) serverRequested = false; }
            if (IsServerInitialized && pulling.Value && (motor.IsDead || player.Ship == null || player.Ship.IsSinking || motion == null || !motion.gameObject.activeInHierarchy || Vector3.Distance(transform.position + Vector3.up, motion.GripPoint) > 3.5f || Time.time - lastPullAt > .75f || !player.IsBot.Value && (Owner == null || !Owner.IsActive))) StopServerPull();
            if (IsPulling)
            {
                if (motion == null) motion = GetMotion();
                if (motion != null) motion.SetPull(IsOwner && localPull ? localAmount : pullAmount.Value);
            }
            if (wasPulling && !IsPulling && motion != null) motion.SetPull(0);
            wasPulling = IsPulling;
            motor.BellPullLocked = IsPulling;
            if (IsOwner && motor.IsDead && !player.Eliminated.Value && !requested && !SessionController.MenuOpen && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            { requested = true; RequestRescueServerRpc(); }
            if (!IsOwner) return;
            var mouse = Mouse.current;
            if (localPull)
            {
                if (motor.IsDead || motor.ActiveParrot != null || SessionController.MenuOpen || DeveloperMenu.IsOpen || PlayerInventory.LootWindowOpen || Cursor.lockState != CursorLockMode.Locked || mouse == null || !mouse.leftButton.isPressed || motion == null || Vector3.Distance(transform.position + Vector3.up, motion.GripPoint) > 3.5f)
                { StopLocalPull(); CancelPullServerRpc(); return; }
                aimed = motion.transform;
                localAmount = Mathf.Clamp01(localAmount - mouse.delta.ReadValue().y / 220f);
                motion.SetPull(localAmount);
                if (Time.unscaledTime >= nextSend)
                {
                    nextSend = Time.unscaledTime + .05f;
                    PullServerRpc(localAmount);
                }
                return;
            }
            if (!motor.InputActive || motor.IsDead || motor.LocomotionLocked || PlayerInventory.LootWindowOpen) return;
            if (!FirearmTrace.Cast(gameObject, motor.PlayerCamera.transform.position, motor.PlayerCamera.transform.position + motor.PlayerCamera.transform.forward * 3f, out var hit)) return;
            var candidate = hit.collider.GetComponentInParent<CrewBellMotion>();
            if (candidate == null || hit.collider.name != "BellRopeGrip" || candidate.GetComponentInParent<NetworkShip>() != player.Ship) return;
            aimed = candidate.transform;
            if (Time.unscaledTime >= statusAt || bellStatus == null)
            { statusAt = Time.unscaledTime + .25f; RefreshBellStatus(); }
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                motion = candidate; localPull = true; localAmount = 0;
                motor.BellPullLocked = true;
                BeginPullServerRpc();
            }
        }
        [ServerRpc]
        void BeginPullServerRpc() { BeginServerPull(); }
        public bool BeginBotPull() => IsServerInitialized && player.IsBot.Value && BeginServerPull();
        public void PullBot(float amount) { if (IsServerInitialized && player.IsBot.Value) PullServer(amount); }
        public void CancelBotPull() { if (IsServerInitialized && player.IsBot.Value) StopServerPull(); }
        bool BeginServerPull()
        {
            var candidate = GetMotion();
            if (pulling.Value || motor.IsDead || motor.ActiveParrot != null || motor.ActiveCannon != null || motor.IsSwimming || motor.IsClimbing || player.Ship == null || player.Ship.IsSinking || candidate == null || Time.time < nextRing || (candidate.Holder != null && candidate.Holder != this) || Vector3.Distance(transform.position + Vector3.up, candidate.GripPoint) > 3.5f || !GetComponent<NetworkWeapon>().CanReach(candidate.GripPoint, candidate.transform))
            { if (Owner != null && Owner.IsActive) EndPullTargetRpc(Owner); return false; }
            motion = candidate; motion.Holder = this; pulling.Value = true; pullAmount.Value = 0; serverAmount = 0;
            serverStarted = lastPullAt = Time.time; motor.BellPullLocked = true;
            return true;
        }
        [ServerRpc]
        void PullServerRpc(float amount) { PullServer(amount); }
        void PullServer(float amount)
        {
            if (!pulling.Value || !float.IsFinite(amount) || motion == null || motor.IsDead || player.Ship == null || player.Ship.IsSinking) return;
            if (Vector3.Distance(transform.position + Vector3.up, motion.GripPoint) > 3.5f || !GetComponent<NetworkWeapon>().CanReach(motion.GripPoint, motion.transform)) { StopServerPull(); return; }
            serverAmount = Mathf.MoveTowards(serverAmount, Mathf.Clamp01(amount), Mathf.Max(0, Time.time - lastPullAt) * 2f);
            lastPullAt = Time.time; pullAmount.Value = serverAmount;
            if (serverAmount >= .99f && Time.time - serverStarted >= .5f)
            {
                var ship = player.Ship;
                nextRing = Time.time + 2f;
                foreach (var member in FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None))
                    if (member.Ship == ship && member.TeamId.Value == player.TeamId.Value && member.GetComponent<CombatHealth>() is { } health && health.RespawnFromBell(ship)) break;
                ServerRingCount++;
                RingObserversRpc(motion.transform.position);
                StopServerPull();
            }
        }
        [ServerRpc] void CancelPullServerRpc() { StopServerPull(); }
        void StopServerPull()
        {
            pulling.Value = false; pullAmount.Value = 0;
            if (motion != null && motion.Holder == this) { motion.Holder = null; motion.SetPull(0); }
            motor.BellPullLocked = false;
            if (Owner != null && Owner.IsActive) EndPullTargetRpc(Owner);
        }
        [TargetRpc] void EndPullTargetRpc(FishNet.Connection.NetworkConnection recipient) { StopLocalPull(); }
        void StopLocalPull()
        {
            localPull = false; localAmount = 0; motor.BellPullLocked = false;
            if (motion != null) motion.SetPull(0);
        }
        public override void OnStopNetwork()
        {
            if (IsServerInitialized) StopServerPull();
            StopLocalPull();
            base.OnStopNetwork();
        }
        void RefreshBellStatus()
        {
            var ship = player.Ship;
            if (ship == null) { bellStatus = null; return; }
            var waiting = new System.Collections.Generic.List<NetworkPlayer>();
            int eliminated = 0;
            foreach (var member in FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None))
            {
                if (member.Ship != ship || member.TeamId.Value != player.TeamId.Value || member.Motor == null || !member.Motor.IsDead) continue;
                if (member.Eliminated.Value) eliminated++;
                else waiting.Add(member);
            }
            waiting.Sort((a,b) => a.ParticipantId.Value.CompareTo(b.ParticipantId.Value));
            var text = new System.Text.StringBuilder("КОЛОКОЛ ЭКИПАЖА");
            for (int i = 0; i < Mathf.Min(4, waiting.Count); i++)
                text.Append("\n").Append(waiting[i].IsBot.Value ? "Бот " : "Пират ").Append(waiting[i].ParticipantId.Value).Append(" — ждёт возрождения");
            if (waiting.Count > 4) text.Append("\nИ ещё: ").Append(waiting.Count - 4);
            if (eliminated > 0) text.Append("\nВыбыли без возможности возрождения: ").Append(eliminated);
            if (ship.IsSinking) text.Append("\nКорабль погибает — возрождение недоступно");
            else if (waiting.Count == 0) text.Append(eliminated > 0 ? "\nНекого вернуть колоколом" : "\nВесь экипаж жив");
            else if (ship.RumCount == 0) text.Append("\nНет рома — нужен 1 ром на пирата");
            else text.Append("\nМожно вернуть: ").Append(Mathf.Min(waiting.Count, ship.RumCount)).Append(" из ").Append(waiting.Count).Append("\nЛКМ на верёвке + потянуть мышь вниз (1 ром за пирата)");
            bellStatus = text.ToString();
        }
        [ObserversRpc(RunLocally = true)]
        void RingObserversRpc(Vector3 point)
        {
            if (motion == null) motion = GetMotion();
            if (motion != null) motion.Ring();
            GameAudio.Play(SoundCue.ShipBell, point);
        }
        [ServerRpc]
        void RequestRescueServerRpc()
        {
            if (!motor.IsDead || player.Eliminated.Value || player.Ship == null || player.Ship.IsSinking || serverRequested || Time.time - requestAt < 10f) return;
            serverRequested = true; requestAt = Time.time;
            foreach (var member in FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None))
                if (member.TeamId.Value == player.TeamId.Value && member.Ship == player.Ship && member.Owner != null && member.Owner.IsActive && !member.IsBot.Value)
                    RescueTargetRpc(member.Owner, player.ParticipantId.Value);
        }
        [TargetRpc]
        void RescueTargetRpc(FishNet.Connection.NetworkConnection recipient, int id)
        {
            crewMessage = "Пират " + id + " ждёт колокола в рубке"; messageUntil = Time.unscaledTime + 5f;
            GameAudio.Play(SoundCue.Select, transform.position, .7f, true);
        }
        void OnGUI()
        {
            if (Time.unscaledTime < messageUntil && !SessionController.MenuOpen)
                PirateHudStyle.Panel(new Rect(Screen.width * .5f - 250, 100, 500, 32), crewMessage);
            if (!IsOwner || SessionController.MenuOpen) return;
            if (motor.IsDead && !player.Eliminated.Value)
                ContextPrompt.Draw(requested ? "ЭКИПАЖ · Просьба позвонить в колокол отправлена" : "ЭКИПАЖ · R — попросить позвонить в колокол");
            if (aimed == null) return;
            ContextPrompt.Offer(localPull ? "Удерживайте ЛКМ и тяните мышь вниз · " + Mathf.RoundToInt(localAmount * 100) + "%" : bellStatus, 60);
        }
    }
}
