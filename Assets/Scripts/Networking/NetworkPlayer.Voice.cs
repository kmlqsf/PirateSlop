using System;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Transporting;
using MetaVoiceChat;
using MetaVoiceChat.NetProviders;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkPlayer : INetProvider
    {
        public PirateVoiceChat Voice { get; private set; }
        static readonly List<NetworkPlayer> voicePlayers = new();
        int voiceIndex = -1;
        float voiceTokens = 12f, voiceTokenAt;
        bool INetProvider.IsLocalPlayerDeafened => !PirateVoiceChat.VoiceEnabled;
        void StartVoice()
        {
            if (!voicePlayers.Contains(this)) voicePlayers.Add(this);
            if (IsBot.Value || SessionController.Instance.Automated || Voice != null) return;
            Voice = PirateVoiceChat.Create(this);
        }
        void StopVoice()
        {
            voicePlayers.Remove(this);
            if (Voice != null) { Voice.Shutdown(); Destroy(Voice.gameObject); Voice = null; }
        }
        void INetProvider.RelayFrame(int index, double timestamp, ReadOnlySpan<byte> data)
        {
            if (!IsOwner || !IsClientInitialized) return;
            byte[] payload = data.ToArray();
            if (IsServerInitialized) RelayVoice(index, timestamp, payload);
            else SendVoice(index, timestamp, payload);
        }
        [ServerRpc]
        void SendVoice(int index, double timestamp, byte[] data, Channel channel = Channel.Unreliable)
            => RelayVoice(index, timestamp, data);
        void RelayVoice(int index, double timestamp, byte[] data)
        {
            if (data == null || data.Length > 900 || index < 0 || index <= voiceIndex || double.IsNaN(timestamp) || double.IsInfinity(timestamp) || timestamp < 0 || IsBot.Value) return;
            float now = Time.unscaledTime;
            voiceTokens = Mathf.Min(12f, voiceTokens + Mathf.Max(0, now - voiceTokenAt) * 60f);
            voiceTokenAt = now;
            if (voiceTokens < 1f) return;
            voiceTokens -= 1f;
            voiceIndex = index;
            if (Motor.IsDead) return;
            foreach (var listener in voicePlayers)
            {
                if (listener == null || listener == this || listener.IsBot.Value || !listener.IsServerInitialized || listener.Owner == null || !listener.Owner.IsActive) continue;
                if ((listener.transform.position - transform.position).sqrMagnitude > 3600f) continue;
                ReceiveVoice(listener.Owner, index, timestamp, data);
            }
        }
        [TargetRpc]
        void ReceiveVoice(NetworkConnection connection, int index, double timestamp, byte[] data, Channel channel = Channel.Unreliable)
        {
            if (Voice != null && !IsOwner) Voice.Receive(index, timestamp, data);
        }
    }
}
