using System;
using Steamworks;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed class SteamParty : MonoBehaviour
    {
        public bool Available { get; private set; }
        public string Status { get; private set; } = "Steam не подключён";
        public CSteamID Lobby { get; private set; }
        public bool InLobby => Lobby.m_SteamID != 0;
        public bool IsLeader => InLobby && SteamMatchmaking.GetLobbyOwner(Lobby) == SteamUser.GetSteamID();
        public bool Busy { get; private set; }
        public bool MatchStarted => InLobby && SteamMatchmaking.GetLobbyData(Lobby, "state") == "playing";
        public bool Waiting => InLobby && SteamMatchmaking.GetLobbyData(Lobby, "state") == "waiting";
        readonly System.Collections.Generic.Dictionary<ulong, int> matchTeams = new();
        SessionController session;
        CSteamID leader;
        Callback<GameLobbyJoinRequested_t> invitation;
        Callback<LobbyDataUpdate_t> data;
        Callback<LobbyChatUpdate_t> members;
        CallResult<LobbyCreated_t> created;
        CallResult<LobbyEnter_t> entered;
        bool joiningMatch;
        float pendingAt;
        const string Game = "PirateSlop.Spacewar.v1";

        void Start()
        {
            session = GetComponent<SessionController>();
            if (Application.isBatchMode) return;
            try
            {
                Available = SteamAPI.Init();
                if (!Available) { Status = "Запустите Steam и перезапустите игру"; return; }
                if (SteamUtils.GetAppID().m_AppId != 480) { SteamAPI.Shutdown(); Available = false; Status = "Нужен тестовый AppID 480"; return; }
                SteamNetworkingUtils.InitRelayNetworkAccess();
                invitation = Callback<GameLobbyJoinRequested_t>.Create(m => Join(m.m_steamIDLobby));
                data = Callback<LobbyDataUpdate_t>.Create(m => { if (m.m_ulSteamIDLobby == Lobby.m_SteamID) CheckMatch(); });
                members = Callback<LobbyChatUpdate_t>.Create(m => { if (m.m_ulSteamIDLobby == Lobby.m_SteamID) CheckMatch(); });
                created = CallResult<LobbyCreated_t>.Create(Created);
                entered = CallResult<LobbyEnter_t>.Create(Entered);
                Status = "Steam • " + SteamFriends.GetPersonaName();
                var args = Environment.GetCommandLineArgs();
                for (int i = 0; i + 1 < args.Length; i++)
                    if (args[i] == "+connect_lobby" && ulong.TryParse(args[i + 1], out var id)) Join(new CSteamID(id));
            }
            catch (Exception ex) { Status = "Steam: " + ex.Message; Available = false; }
        }
        void Update()
        {
            if (!Available) return;
            SteamAPI.RunCallbacks();
            if (Busy && Time.unscaledTime - pendingAt > 20) { created.Cancel(); entered.Cancel(); Busy = false; Status = "Steam не ответил. Попробуйте снова."; }
        }
        public void Create()
        {
            if (!Available || Busy || InLobby || session.SessionBusy) return;
            Busy = true; pendingAt = Time.unscaledTime;
            created.Set(SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, session.MaxPlayers));
        }
        void Created(LobbyCreated_t result, bool failed)
        {
            Busy = false;
            if (failed || result.m_eResult != EResult.k_EResultOK) { Status = "Не удалось создать лобби: " + result.m_eResult; return; }
            Lobby = new CSteamID(result.m_ulSteamIDLobby); leader = SteamUser.GetSteamID();
            SteamMatchmaking.SetLobbyData(Lobby, "game", Game);
            SteamMatchmaking.SetLobbyData(Lobby, "protocol", session.ProtocolVersion.ToString());
            SteamMatchmaking.SetLobbyData(Lobby, "state", "waiting");
            SteamMatchmaking.SetLobbyData(Lobby, "leader", leader.ToString());
            SetTeam(1); Ready(false); Status = "Команда собирается";
        }
        public void Join(CSteamID id)
        {
            if (!Available || Busy || session.SessionBusy) { Status = "Сначала покиньте текущую сессию"; return; }
            Leave(); Busy = true; pendingAt = Time.unscaledTime;
            entered.Set(SteamMatchmaking.JoinLobby(id));
        }
        void Entered(LobbyEnter_t result, bool failed)
        {
            Busy = false;
            if (failed || result.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess) { Status = "Лобби недоступно или заполнено"; return; }
            Lobby = new CSteamID(result.m_ulSteamIDLobby);
            if (SteamMatchmaking.GetLobbyData(Lobby, "game") != Game || SteamMatchmaking.GetLobbyData(Lobby, "protocol") != session.ProtocolVersion.ToString())
            { Leave(); Status = "Другая игра или версия сборки"; return; }
            leader = SteamMatchmaking.GetLobbyOwner(Lobby);
            session.ShowSteamParty();
            SetTeam(1); Ready(false); joiningMatch = false; CheckMatch();
        }
        public int Count => InLobby ? SteamMatchmaking.GetNumLobbyMembers(Lobby) : 0;
        public CSteamID Member(int index) => SteamMatchmaking.GetLobbyMemberByIndex(Lobby, index);
        public string Name(CSteamID id) => SteamFriends.GetFriendPersonaName(id);
        public int Team(CSteamID id) => int.TryParse(SteamMatchmaking.GetLobbyMemberData(Lobby, id, "team"), out var value) ? Mathf.Clamp(value, 1, 4) : 1;
        public bool IsReady(CSteamID id) => SteamMatchmaking.GetLobbyMemberData(Lobby, id, "ready") == "1";
        public void SetTeam(int value) { if (Waiting) { SteamMatchmaking.SetLobbyMemberData(Lobby, "team", Mathf.Clamp(value, 1, 4).ToString()); Ready(false); } }
        public void Ready(bool value) { if (Waiting) SteamMatchmaking.SetLobbyMemberData(Lobby, "ready", value ? "1" : "0"); }
        public void Invite() { if (InLobby) SteamFriends.ActivateGameOverlayInviteDialog(Lobby); }
        public void Launch()
        {
            if (!IsLeader || !Waiting || session.SessionBusy) return;
            for (int i = 0; i < Count; i++) if (!IsReady(Member(i))) { Status = "Дождитесь готовности всех участников"; return; }
            SteamMatchmaking.SetLobbyJoinable(Lobby, false);
            matchTeams.Clear();
            for (int i = 0; i < Count; i++) matchTeams[Member(i).m_SteamID] = Team(Member(i));
            SteamMatchmaking.SetLobbyData(Lobby, "state", "loading");
            session.BeginSteam(true, leader.m_SteamID);
        }
        public void ServerReady()
        {
            if (!IsLeader) return;
            SteamMatchmaking.SetLobbyData(Lobby, "host", SteamUser.GetSteamID().ToString());
            SteamMatchmaking.SetLobbyData(Lobby, "state", "playing");
        }
        void CheckMatch()
        {
            if (!InLobby) return;
            if (SteamMatchmaking.GetLobbyOwner(Lobby) != leader) { session.Disconnect(); Leave(); Status = "Капитан вышел. Создайте новое лобби."; return; }
            if (!IsLeader && MatchStarted && !joiningMatch && !session.SessionBusy && ulong.TryParse(SteamMatchmaking.GetLobbyData(Lobby, "host"), out var host) && host == leader.m_SteamID)
            { joiningMatch = true; session.BeginSteam(false, host); }
        }
        public int AdmittedTeam(string address)
        {
            if (!InLobby || !ulong.TryParse(address, out var id)) return 0;
            for (int i = 0; i < Count; i++) if (Member(i).m_SteamID == id && matchTeams.TryGetValue(id, out var team)) return team;
            return 0;
        }
        public void Leave()
        {
            created?.Cancel(); entered?.Cancel(); Busy = false;
            if (Available && InLobby) SteamMatchmaking.LeaveLobby(Lobby);
            Lobby = default; joiningMatch = false; matchTeams.Clear();
        }
        void OnDestroy()
        {
            Leave(); invitation?.Dispose(); data?.Dispose(); members?.Dispose(); created?.Dispose(); entered?.Dispose();
            if (Available) SteamAPI.Shutdown();
        }
    }
}
