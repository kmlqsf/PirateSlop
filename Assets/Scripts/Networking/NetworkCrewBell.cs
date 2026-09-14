using FishNet.Object;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PirateSlop.Networking
{
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
        void Update()
        {
            aimed = null;
            if (!motor.IsDead) { requested = false; if (IsServerInitialized) serverRequested = false; }
            if (IsOwner && motor.IsDead && !player.Eliminated.Value && !requested && !SessionController.MenuOpen && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            { requested = true; RequestRescueServerRpc(); }
            if (!IsOwner || !motor.InputActive || motor.IsDead || motor.LocomotionLocked || PlayerInventory.LootWindowOpen) return;
            if (!FirearmTrace.Cast(gameObject, motor.PlayerCamera.transform.position, motor.PlayerCamera.transform.position + motor.PlayerCamera.transform.forward * 3f, out var hit)) return;
            if (hit.collider.name != "CrewBell") return;
            var ship = hit.collider.GetComponentInParent<NetworkShip>();
            if (ship == null || ship != player.Ship) return;
            aimed = hit.collider.transform;
            if (Time.unscaledTime >= statusAt || bellStatus == null)
            {
                statusAt = Time.unscaledTime + .25f;
                RefreshBellStatus();
            }
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) RingServerRpc();
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
            else text.Append("\nМожно вернуть: ").Append(Mathf.Min(waiting.Count, ship.RumCount)).Append(" из ").Append(waiting.Count).Append("\nE — позвонить (1 ром за пирата)");
            bellStatus = text.ToString();
        }
        [ServerRpc]
        void RingServerRpc()
        {
            var ship = player.Ship;
            if (ship == null || ship.IsSinking || motor.IsDead || motor.LocomotionLocked || Time.time < nextRing) return;
            var bell = ship.transform.Find("CrewBell");
            if (bell == null || !bell.gameObject.activeInHierarchy || Vector3.Distance(transform.position + Vector3.up, bell.position) > 3.5f) return;
            if (!GetComponent<NetworkWeapon>().CanReach(bell.position, bell)) return;
            nextRing = Time.time + 2f;
            foreach (var member in FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None))
                if (member.Ship == ship && member.TeamId.Value == player.TeamId.Value) member.GetComponent<CombatHealth>()?.RespawnFromBell(ship);
            RingObserversRpc(bell.position);
        }
        [ObserversRpc(RunLocally = true)]
        void RingObserversRpc(Vector3 point) => GameAudio.Play(SoundCue.ShipBell, point);
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
                PirateHudStyle.Panel(new Rect(Screen.width * .5f - 240, Screen.height * .5f + 65, 480, 32), requested ? "Просьба позвонить в колокол отправлена" : "R — попросить экипаж позвонить в колокол");
            if (aimed == null) return;
            ContextPrompt.Offer(bellStatus, 60);
        }
    }
}
