using System;
using FishNet.Authenticating;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
namespace PirateSlop.Networking
{
    public struct HelloMessage : IBroadcast { public int Version; }
    public struct AdmissionMessage : IBroadcast { public string Error; }
    public struct PopulationMessage : IBroadcast { public int Count; public string SessionId; }
    public sealed class SessionAuthenticator : Authenticator
    {
        public override event Action<NetworkConnection, bool> OnAuthenticationResult;
        public override void InitializeOnce(NetworkManager manager)
        {
            base.InitializeOnce(manager);
            manager.ClientManager.OnClientConnectionState += ClientState;
            manager.ClientManager.RegisterBroadcast<AdmissionMessage>(Response);
            manager.ServerManager.RegisterBroadcast<HelloMessage>(Hello, false);
        }
        void ClientState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started)
                NetworkManager.ClientManager.Broadcast(new HelloMessage { Version = SessionController.Instance.ProtocolVersion });
        }
        void Hello(NetworkConnection conn, HelloMessage message, Channel channel)
        {
            if (conn.IsAuthenticated) return;
            var session = SessionController.Instance;
            int admitted = 0;
            foreach (var other in NetworkManager.ServerManager.Clients.Values) if (other.IsAuthenticated) admitted++;
            string error = message.Version != session.Config.ProtocolVersion ? "Несовместимая версия игры" : admitted >= session.MaxPlayers ? "Сессия заполнена" : "";
            NetworkManager.ServerManager.Broadcast(conn, new AdmissionMessage { Error = error }, false);
            UnityEngine.Debug.Log($"ADMISSION connection={conn.ClientId} result={(error == "" ? "accepted" : error)}");
            OnAuthenticationResult?.Invoke(conn, error == "");
        }
        void Response(AdmissionMessage message, Channel channel) { if (!string.IsNullOrEmpty(message.Error)) SessionController.Instance.SetError(message.Error); }
    }
}
