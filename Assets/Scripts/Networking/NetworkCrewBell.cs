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
        float nextRing;
        void Awake() { motor = GetComponent<AdvancedPlayerController>(); player = GetComponent<NetworkPlayer>(); }
        void Update()
        {
            aimed = null;
            if (!IsOwner || !motor.InputActive || motor.IsDead || motor.LocomotionLocked || PlayerInventory.LootWindowOpen) return;
            if (!FirearmTrace.Cast(gameObject, motor.PlayerCamera.transform.position, motor.PlayerCamera.transform.position + motor.PlayerCamera.transform.forward * 3f, out var hit)) return;
            if (hit.collider.name != "CrewBell") return;
            var ship = hit.collider.GetComponentInParent<NetworkShip>();
            if (ship == null || ship != player.Ship) return;
            aimed = hit.collider.transform;
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) RingServerRpc();
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
        void OnGUI()
        {
            if (aimed != null && IsOwner) PirateHudStyle.Panel(new Rect(Screen.width * .5f - 220, Screen.height - 155, 440, 32), "E — позвонить: возродить экипаж (1 ром за пирата)");
        }
    }
}
