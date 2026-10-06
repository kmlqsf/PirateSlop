using System;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed class UpgradeReward
    {
        public int ChestNumber;
        public float[] Weights;
        public string[] Offers;
        public int RerollsUsed;
    }

    public sealed class PlayerUpgradeState
    {
        public string Token;
        public int Team, SteamCrew;
        public NetworkPlayer Player;
        public int Revision;
        public bool CoinUsed, PactUsed, PendingPact, PendingRum, PendingMonkey;
        public float PactAt, PactYaw;
        public Vector3 PactPosition, PactWorldPosition;
        public Transform PactPlatform;
        public readonly HashSet<string> Taken = new();
        public readonly List<UpgradeReward> Rewards = new();
    }

    [Serializable]
    public sealed class UpgradeSnapshot
    {
        public int revision, points, chestNumber, rerolls;
        public UpgradeCard[] offers = Array.Empty<UpgradeCard>();
        public UpgradeCard[] owned = Array.Empty<UpgradeCard>();
    }

    public sealed partial class NetworkPlayer
    {
        internal PlayerUpgradeState UpgradeState;
        float nextUpgradeRequest;
        public UpgradeSnapshot Upgrades { get; private set; } = new();
        public event Action UpgradesChanged;
        public event Action<UpgradeCard> ServerUpgradeChosen;
        public bool HasServerUpgrade(string id) => UpgradeState != null && UpgradeState.Taken.Contains(id);
        internal void NotifyUpgradeChosen(UpgradeCard card) { ApplyChosenUpgrade(card); ServerUpgradeChosen?.Invoke(card); }

        public void RequestUpgrades()
        {
            if (IsOwner && IsClientInitialized) RequestUpgradesServerRpc();
        }

        public void ChooseUpgrade(string id, int revision)
        {
            if (IsOwner && IsClientInitialized) ChooseUpgradeServerRpc(id, revision);
        }

        public void RerollUpgrade(int revision) { if (IsOwner && IsClientInitialized) RerollUpgradeServerRpc(revision); }
        [ServerRpc]
        void RerollUpgradeServerRpc(int revision)
        {
            if (UpgradeState != null && UpgradeState.Revision == revision) SessionController.Instance?.RerollUpgrade(this);
            SendUpgrades();
        }

        [ServerRpc]
        void RequestUpgradesServerRpc()
        {
            if (Time.unscaledTime < nextUpgradeRequest) return;
            nextUpgradeRequest = Time.unscaledTime + .15f;
            SendUpgrades();
        }

        [ServerRpc]
        void ChooseUpgradeServerRpc(string id, int revision)
        {
            if (UpgradeState == null || id == null || id.Length > 80 || UpgradeState.Revision != revision)
            {
                SendUpgrades();
                return;
            }
            SessionController.Instance?.ChooseUpgrade(this, id);
            SendUpgrades();
        }

        internal void SendUpgrades()
        {
            if (!IsServerInitialized || Owner == null || !Owner.IsActive || IsBot.Value || SessionController.Instance == null) return;
            var snapshot = SessionController.Instance.UpgradeSnapshotFor(this);
            UpgradeSnapshotTargetRpc(Owner, JsonUtility.ToJson(snapshot));
        }

        [TargetRpc]
        void UpgradeSnapshotTargetRpc(NetworkConnection connection, string json)
        {
            var snapshot = JsonUtility.FromJson<UpgradeSnapshot>(json);
            if (snapshot == null || snapshot.revision < Upgrades.revision) return;
            Upgrades = snapshot;
            UpgradesChanged?.Invoke();
        }
    }
}
