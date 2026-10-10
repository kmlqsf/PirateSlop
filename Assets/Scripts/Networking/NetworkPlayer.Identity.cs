using System.Globalization;
using System.Text;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkPlayer
    {
        public readonly SyncVar<string> SteamName = new();
        public string DisplayName => IsBot.Value ? "БОТ " + BotNumber : string.IsNullOrEmpty(SteamName.Value) ? "Пират " + ParticipantId.Value : SteamName.Value;
        float nextIdentityCheck, nextIdentityRequest;
        void TickIdentity()
        {
            if (!IsOwner || !IsClientInitialized || IsBot.Value || Time.unscaledTime < nextIdentityCheck) return;
            nextIdentityCheck = Time.unscaledTime + 5f;
            string name = CleanDisplayName(SessionController.Instance?.LocalSteamName);
            if (!string.IsNullOrEmpty(name) && SteamName.Value != name) SubmitSteamName(name);
        }
        [ServerRpc]
        void SubmitSteamName(string requested)
        {
            if (IsBot.Value || Time.unscaledTime < nextIdentityRequest) return;
            nextIdentityRequest = Time.unscaledTime + 2f;
            var session = SessionController.Instance;
            if (session != null && session.ResolveSteamName(Owner, out var authoritative)) requested = authoritative;
            string name = CleanDisplayName(requested);
            if (!string.IsNullOrEmpty(name)) SteamName.Value = name;
        }
        public static string CleanDisplayName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            var clean = new StringBuilder(96);
            for (int i = 0; i < value.Length && clean.Length < 96; i++)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(value, i);
                if (category == UnicodeCategory.Control || category == UnicodeCategory.LineSeparator || category == UnicodeCategory.ParagraphSeparator || category == UnicodeCategory.Format && value[i] != '\u200d') continue;
                if (char.IsHighSurrogate(value[i]))
                {
                    if (i + 1 < value.Length && char.IsLowSurrogate(value[i+1])) { clean.Append(value[i]); clean.Append(value[++i]); }
                }
                else if (!char.IsLowSurrogate(value[i])) clean.Append(value[i]);
            }
            string result = clean.ToString().Trim();
            int[] elements = StringInfo.ParseCombiningCharacters(result);
            return elements.Length > 32 ? result.Substring(0, elements[32]) : result;
        }
    }
}
