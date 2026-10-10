using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Transporting;

namespace PirateSlop.Networking
{
    public struct CombatRulesMessage : IBroadcast
    {
        public bool FriendlyFire;
    }

    public sealed partial class SessionController
    {
        public bool FriendlyFireEnabled { get; private set; }
        public static bool FriendlyFire => Instance != null && Instance.FriendlyFireEnabled;

        public bool SetFriendlyFire(bool enabled)
        {
            if (!DeveloperMenu.Available || manager == null || !manager.ServerManager.Started) return false;
            FriendlyFireEnabled = enabled;
            manager.ServerManager.Broadcast(new CombatRulesMessage { FriendlyFire = enabled });
            return true;
        }

        void SendCombatRules(NetworkConnection connection) => manager.ServerManager.Broadcast(connection,
            new CombatRulesMessage { FriendlyFire = FriendlyFireEnabled });

        void ReceiveCombatRules(CombatRulesMessage message, Channel channel)
        {
            if (!manager.ServerManager.Started) FriendlyFireEnabled = message.FriendlyFire;
        }
    }
}
