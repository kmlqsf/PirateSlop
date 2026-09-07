using FishNet.Broadcast;

namespace PirateSlop.Networking
{
    public struct WorldManifestMessage : IBroadcast { public string Json, Checksum; }
    public struct WorldReadyMessage : IBroadcast { public string Checksum; }
}
