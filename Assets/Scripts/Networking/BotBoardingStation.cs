using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed class BotBoardingStation : IBotShipStation
    {
        readonly SimpleCannon cannon;
        float started, next;
        bool acquired, done;
        public BotBoardingStation(SimpleCannon cannon) { this.cannon = cannon; }
        public string Name => "Абордажный трос: подтянуть и освободить";
        public Vector3 Position => cannon.transform.position + Vector3.up;
        public bool Available => cannon != null && cannon.Network != null && cannon.gameObject.activeInHierarchy;
        public bool Busy => cannon.Operator != null || cannon.RemoteOccupied;
        public bool Complete => done || !cannon.Network.HasBoarding(cannon.Index);
        public void Validate() { }
        public bool Owned(NetworkPlayer player) => acquired;
        public bool Acquire(NetworkPlayer player) { acquired = !Busy; started = Time.time; return acquired; }
        public void Work(NetworkPlayer player, float delta)
        {
            if (Time.time < next) return;
            next = Time.time + .5f;
            bool release = Time.time - started > 5f || player.Ship.IsSinking || Mathf.Abs(player.Ship.Motor.Speed) > 2f;
            if (cannon.Network.BotManageCable(player, cannon.Index, release) && release) done = true;
        }
        public void Release(NetworkPlayer player)
        {
            acquired = false;
        }
    }
}
