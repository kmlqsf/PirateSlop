using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed class BotBellStation : IBotShipStation, IBotApproachConstraint, IBotApproachRange
    {
        readonly NetworkShip ship;
        readonly NetworkPlayer casualty;
        readonly CrewBellMotion bell;
        NetworkCrewBell handler;
        bool acquired;
        uint initialRings;
        public BotBellStation(NetworkShip ship, NetworkPlayer casualty, CrewBellMotion bell)
        { this.ship = ship; this.casualty = casualty; this.bell = bell; }
        readonly RaycastHit[] approachHits = new RaycastHit[32];
        public float ApproachRadius => 3.4f;
        public bool AllowsApproach(Vector3 worldPosition)
        {
            var origin = worldPosition + Vector3.up * 1.5f;
            var delta = Position - origin;
            if (Vector3.Distance(worldPosition + Vector3.up, Position) > 3.3f) return false;
            int count = Physics.RaycastNonAlloc(origin, delta.normalized, approachHits, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
            if (count == approachHits.Length) return false;
            for (int i = 0; i < count; i++)
                if (!approachHits[i].transform.IsChildOf(bell.transform) &&
                    approachHits[i].collider.GetComponentInParent<AdvancedPlayerController>() == null &&
                    approachHits[i].collider.GetComponentInParent<Cannonball>() == null) return false;
            return true;
        }
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
