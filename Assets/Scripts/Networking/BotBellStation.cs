using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed class BotBellStation : IBotShipStation
    {
        readonly NetworkShip ship;
        readonly NetworkPlayer casualty;
        readonly CrewBellMotion bell;
        NetworkCrewBell handler;
        bool acquired;
        uint initialRings;
        public BotBellStation(NetworkShip ship, NetworkPlayer casualty, CrewBellMotion bell)
        { this.ship = ship; this.casualty = casualty; this.bell = bell; }
        public string Name => "Возрождение товарища через колокол";
        public Vector3 Position => bell.GripPoint;
        public bool Complete => acquired && (handler != null && handler.ServerRingCount != initialRings || casualty == null || !casualty.Motor.IsDead || casualty.Eliminated.Value);
        public bool Available => ship != null && !ship.IsSinking && bell != null && bell.isActiveAndEnabled &&
            (Complete || ship.RumCount > 0 && casualty != null && casualty.Motor.IsDead && !casualty.Eliminated.Value);
        public bool Busy => bell.Holder != null;
        public void Validate() { }
        public bool Owned(NetworkPlayer player) => acquired && (Complete || handler != null && handler.IsPulling);
        public bool Acquire(NetworkPlayer player)
        {
            handler = player.GetComponent<NetworkCrewBell>();
            initialRings = handler != null ? handler.ServerRingCount : 0;
            acquired = handler != null && handler.BeginBotPull();
            return acquired;
        }
        public void Work(NetworkPlayer player, float delta) { if (handler != null && !Complete) handler.PullBot(1f); }
        public void Release(NetworkPlayer player) { if (handler != null) handler.CancelBotPull(); }
    }
}
