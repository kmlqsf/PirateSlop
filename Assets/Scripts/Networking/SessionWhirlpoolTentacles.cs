using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Transporting;
using PirateSlop.World;
using UnityEngine;

namespace PirateSlop.Networking
{
    public struct WhirlpoolTentacleState
    {
        public float Health;
        public int ShipId;
        public bool Attacking;
        public double StartedAt, DefeatedAt;
        public Vector3 TargetLocal;
    }

    public struct WhirlpoolTentaclesMessage : IBroadcast
    {
        public WhirlpoolTentacleState[] Tentacles;
    }

    public sealed partial class SessionController
    {
        public bool WhirlpoolServer => manager != null && manager.ServerManager.Started && ModernOceanActive;
        public double WhirlpoolClock => manager != null ? manager.TimeManager.TicksToTime(manager.TimeManager.Tick) : Time.timeAsDouble;

        public void PublishWhirlpoolTentacles(WhirlpoolTentacleState[] state)
        {
            if(WhirlpoolServer) manager.ServerManager.Broadcast(new WhirlpoolTentaclesMessage { Tentacles=state });
        }

        void SendWhirlpoolTentacles(NetworkConnection connection)
        {
            if(WhirlpoolServer && WhirlpoolTentacleRing.Instance != null)
                manager.ServerManager.Broadcast(connection,new WhirlpoolTentaclesMessage { Tentacles=WhirlpoolTentacleRing.Instance.Snapshot });
        }

        void ReceiveWhirlpoolTentacles(WhirlpoolTentaclesMessage message,Channel channel)
        {
            if(manager.ServerManager.Started || !ModernOceanActive) return;
            WhirlpoolTentacleRing.Instance?.ApplySnapshot(message.Tentacles);
        }
    }
}
