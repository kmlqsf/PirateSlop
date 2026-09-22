using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkPlayer
    {
        float nextBotDebugRequest;
        float nextBotActionRequest;

        public string AssignHelmAction(bool cancel)
        {
            if (!IsServerInitialized || !IsBot.Value || botActions == null) return "Бот недоступен";
            botManualTask = true;
            BotTaskKey = -2;
            if (cancel) { botActions.Cancel("Отменено из F10"); botManualTask = false; nextBotTask = Time.time + 15f; return "Задача отменена"; }
            BotAssignment = "Ручная команда F10";
            BotCandidates = "Автоматическое назначение приостановлено";
            botActions.Assign(new BotStationAction(this, SessionController.Instance.Config.BotMotion,
                new BotHelmStation(Ship != null ? Ship.Helm : null), "Явная команда F10"), out var reason);
            return reason;
        }

        public void RequestBotHelmAction(int number, bool cancel)
        {
            if (IsOwner && IsClientInitialized) BotHelmActionServerRpc(number, cancel);
        }

        bool BotDebugAllowed => DeveloperMenu.Available && SessionController.Instance != null &&
            (IsOwner || DeveloperMenu.AllowRemote || SessionController.Instance.RemoteBotDiagnosticsAllowed);

        [ServerRpc]
        void BotHelmActionServerRpc(int number, bool cancel)
        {
            if (Owner == null || !Owner.IsActive || Time.unscaledTime < nextBotActionRequest) return;
            nextBotActionRequest = Time.unscaledTime + .5f;
            BotActionResultTargetRpc(Owner, BotDebugAllowed ? SessionController.Instance.AssignBotHelmAction(number, cancel) : "Доступ запрещён");
        }

        [TargetRpc]
        void BotActionResultTargetRpc(NetworkConnection recipient, string message) => GetComponent<BotDebugPanel>()?.ReportAction(message);

        public void RequestBotDebug(int number, int revision)
        {
            if (IsOwner && IsClientInitialized) BotDebugServerRpc(number, revision);
        }

        [ServerRpc]
        void BotDebugServerRpc(int number, int revision)
        {
            if (Time.unscaledTime < nextBotDebugRequest || Owner == null || !Owner.IsActive) return;
            nextBotDebugRequest = Time.unscaledTime + .45f;
            var session = SessionController.Instance;
            bool allowed = BotDebugAllowed;
            if (!allowed)
            {
                BotDebugTargetRpc(Owner, "Доступ запрещён. Нужно разрешение хоста на отладку.", System.Array.Empty<BotDebugRow>(), default);
                return;
            }
            BotDebugTargetRpc(Owner, "", session.GetBotDebugRows(), session.GetBotDebugSnapshot(number, revision));
        }

        [TargetRpc]
        void BotDebugTargetRpc(NetworkConnection recipient, string error, BotDebugRow[] rows, BotDebugSnapshot snapshot)
        {
            GetComponent<BotDebugPanel>()?.Receive(error, rows, snapshot);
        }
    }
}
