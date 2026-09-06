using UnityEngine;
namespace PirateSlop.Networking
{
    [CreateAssetMenu(menuName = "PirateSlop/Session Config")]
    public sealed class SessionConfig : ScriptableObject
    {
        public ushort Port = 7777;
        [Range(1, 128)] public int MaxPlayers = 32;
        public ushort TickRate = 30;
        public int ProtocolVersion = 2;
        public float ConnectTimeout = 15, ObserverRadius = 1000, ObserverHysteresis = .2f;
        public float SpawnSpacing = 60;
        public Vector3 ShipOrigin, PlayerLocalSpawn;
        public string GameScene = "NetworkOcean";
    }
}
