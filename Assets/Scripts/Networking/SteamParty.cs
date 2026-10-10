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
        public CSteamID MatchLobby { get; private set; }
        public bool MatchStarted => MatchLobby.m_SteamID != 0 && SteamMatchmaking.GetLobbyData(MatchLobby, "state") == "playing";
        public bool Waiting => InLobby && SteamMatchmaking.GetLobbyData(Lobby, "state") == "waiting";
        public const int TeamCount = 10;
        public const int LobbyCapacity = TeamCount * SessionController.CrewSize;
        string TeamMode => "selected";
        readonly System.Collections.Generic.Dictionary<ulong, int> matchTeams = new();
        readonly System.Collections.Generic.Dictionary<string, int> crewTeams = new();
        CallResult<LobbyCreated_t> matchCreated;
        CallResult<LobbyEnter_t> matchEntered;
        float nextCheck;
        ulong ignoredMatch;
        SessionController session;
        CSteamID leader;
        Callback<GameLobbyJoinRequested_t> invitation;
        Callback<GameRichPresenceJoinRequested_t> presenceInvitation;
        bool inviteVisible;
        Vector2 friendScroll;
        readonly System.Collections.Generic.Dictionary<ulong, float> invitedAt = new();
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
                presenceInvitation = Callback<GameRichPresenceJoinRequested_t>.Create(m => JoinConnection(m.m_rgchConnect));
                data = Callback<LobbyDataUpdate_t>.Create(m => { if (m.m_ulSteamIDLobby == Lobby.m_SteamID) CheckMatch(); });
                members = Callback<LobbyChatUpdate_t>.Create(m => { if (m.m_ulSteamIDLobby == Lobby.m_SteamID) CheckMatch(); });
                created = CallResult<LobbyCreated_t>.Create(Created);
                entered = CallResult<LobbyEnter_t>.Create(Entered);
                matchCreated = CallResult<LobbyCreated_t>.Create(MatchCreated);
                matchEntered = CallResult<LobbyEnter_t>.Create(MatchEntered);
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
            if (Time.unscaledTime >= nextCheck) { nextCheck = Time.unscaledTime + .5f; CheckMatch(); }
            if (Busy && Time.unscaledTime - pendingAt > 30) { created.Cancel(); entered.Cancel(); LeaveSession(); Status = "Steam не ответил. Попробуйте снова."; }
        }
        public void Create()
        {
            if (!Available || Busy || InLobby || session.SessionBusy) return;
            Busy = true; pendingAt = Time.unscaledTime;
            created.Set(SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, LobbyCapacity));
        }
        void Created(LobbyCreated_t result, bool failed)
        {
            Busy = false;
            if (failed || result.m_eResult != EResult.k_EResultOK) { Status = "Не удалось создать лобби: " + result.m_eResult; return; }
            Lobby = new CSteamID(result.m_ulSteamIDLobby); leader = SteamUser.GetSteamID();
            SteamMatchmaking.SetLobbyData(Lobby, "game", Game);
            SteamMatchmaking.SetLobbyData(Lobby, "kind", "party");
            SteamMatchmaking.SetLobbyData(Lobby, "protocol", session.ProtocolVersion.ToString());
            SteamMatchmaking.SetLobbyData(Lobby, "state", "waiting");
            SteamMatchmaking.SetLobbyData(Lobby, "team_mode", TeamMode);
            SteamMatchmaking.SetLobbyData(Lobby, "leader", leader.ToString());
            SteamMatchmaking.SetLobbyJoinable(Lobby, true);
            SteamMatchmaking.SetLobbyData(Lobby, "roster_revision", "0");
            ResolveTeams(); session.ShowSteamParty();
            Ready(false); PublishPresence(); Status = "Лобби открыто — выберите экипаж";
        }
        public void Join(CSteamID id)
        {
            if (!Available || Busy || session.SessionBusy) { Status = "Сначала покиньте текущую сессию"; return; }
            if (id == Lobby) return;
            Leave(); Busy = true; pendingAt = Time.unscaledTime;
            entered.Set(SteamMatchmaking.JoinLobby(id));
        }
        void Entered(LobbyEnter_t result, bool failed)
        {
            Busy = false;
            if (failed || result.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess) { Status = "Лобби недоступно или заполнено"; return; }
            Lobby = new CSteamID(result.m_ulSteamIDLobby);
            if (SteamMatchmaking.GetLobbyData(Lobby, "kind") != "party" || SteamMatchmaking.GetLobbyData(Lobby, "game") != Game || SteamMatchmaking.GetLobbyData(Lobby, "protocol") != session.ProtocolVersion.ToString())
            { Leave(); Status = "Другая игра или версия сборки"; return; }
            leader = SteamMatchmaking.GetLobbyOwner(Lobby);
            session.ShowSteamParty();
            Ready(false); joiningMatch = false; CheckMatch();
        }
        public int Count => InLobby ? SteamMatchmaking.GetNumLobbyMembers(Lobby) : 0;
        public CSteamID Member(int index) => SteamMatchmaking.GetLobbyMemberByIndex(Lobby, index);
        public string Name(CSteamID id) => SteamFriends.GetFriendPersonaName(id);
        public int Team(CSteamID id) => InLobby && int.TryParse(SteamMatchmaking.GetLobbyData(Lobby, "slot_" + id), out var team) && team >= 1 && team <= TeamCount ? team : 0;
        public int TeamMembers(int team)
        {
            int count = 0;
            for (int i = 0; i < Count; i++) if (Team(Member(i)) == team) count++;
            return count;
        }
        public bool TeamPending(CSteamID id)
        {
            string request = SteamMatchmaking.GetLobbyMemberData(Lobby, id, "team_request");
            return Team(id) == 0 || (!string.IsNullOrEmpty(request) && request != SteamMatchmaking.GetLobbyData(Lobby, "ack_" + id));
        }
        string RosterRevision => SteamMatchmaking.GetLobbyData(Lobby, "roster_revision");
        public bool IsReady(CSteamID id) => Team(id) > 0 && !TeamPending(id) && SteamMatchmaking.GetLobbyMemberData(Lobby, id, "ready") == "1" && SteamMatchmaking.GetLobbyMemberData(Lobby, id, "ready_revision") == RosterRevision;
        public void Ready(bool value)
        {
            if (!Waiting || (value && (Busy || session.SessionBusy || TeamPending(SteamUser.GetSteamID())))) return;
            SteamMatchmaking.SetLobbyMemberData(Lobby, "ready_revision", RosterRevision);
            SteamMatchmaking.SetLobbyMemberData(Lobby, "ready", value ? "1" : "0");
        }
        public void SelectTeam(int team)
        {
            if (!Waiting || Busy || session.SessionBusy || team < 1 || team > TeamCount || TeamPending(SteamUser.GetSteamID()) || Team(SteamUser.GetSteamID()) == team) return;
            if (TeamMembers(team) >= SessionController.CrewSize) { Status = "Этот экипаж уже заполнен"; return; }
            Ready(false);
            SteamMatchmaking.SetLobbyMemberData(Lobby, "team_request", team + ":" + Guid.NewGuid().ToString("N"));
            Status = "Переход в экипаж " + team + "…";
            ResolveTeams();
        }
        void ResolveTeams()
        {
            if (!IsLeader || !Waiting || Busy || session.SessionBusy) return;
            int count = Count;
            var occupants = new int[TeamCount + 1];
            var assignments = new int[count];
            bool changed = false;
            for (int i = 0; i < count; i++)
            {
                int team = Team(Member(i));
                if (team > 0 && occupants[team] < SessionController.CrewSize) { assignments[i] = team; occupants[team]++; }
            }
            for (int i = 0; i < count; i++)
            {
                var member = Member(i);
                int team = assignments[i];
                string request = SteamMatchmaking.GetLobbyMemberData(Lobby, member, "team_request");
                bool requested = !string.IsNullOrEmpty(request) && request != SteamMatchmaking.GetLobbyData(Lobby, "ack_" + member);
                string rejection = "";
                if (requested)
                {
                    if (int.TryParse(request.Split(':')[0], out int target) && target >= 1 && target <= TeamCount && (target == team || occupants[target] < SessionController.CrewSize))
                    {
                        if (team != target) { if (team > 0) occupants[team]--; occupants[target]++; team = target; }
                    }
                    else rejection = "Экипаж заполнен — выберите другой";
                }
                if (team == 0)
                    for (int candidate = 1; candidate <= TeamCount; candidate++)
                        if (occupants[candidate] < SessionController.CrewSize) { team = candidate; occupants[team]++; break; }
                if (Team(member) != team)
                {
                    SteamMatchmaking.SetLobbyData(Lobby, "slot_" + member, team.ToString());
                    changed = true;
                }
                if (requested)
                {
                    SteamMatchmaking.SetLobbyData(Lobby, "error_" + member, rejection);
                    SteamMatchmaking.SetLobbyData(Lobby, "ack_" + member, request);
                }
            }
            string roster = "";
            for (int i = 0; i < count; i++) roster += Member(i) + ":" + Team(Member(i)) + ";";
            if (changed || SteamMatchmaking.GetLobbyData(Lobby, "roster") != roster)
            {
                int.TryParse(RosterRevision, out int revision);
                SteamMatchmaking.SetLobbyData(Lobby, "roster", roster);
                SteamMatchmaking.SetLobbyData(Lobby, "roster_revision", (revision + 1).ToString());
            }
        }
        string lastTeamAck;
        void UpdateTeamStatus()
        {
            var me = SteamUser.GetSteamID();
            string ack = SteamMatchmaking.GetLobbyData(Lobby, "ack_" + me);
            if (ack == lastTeamAck) return;
            lastTeamAck = ack;
            string rejection = SteamMatchmaking.GetLobbyData(Lobby, "error_" + me);
            Status = string.IsNullOrEmpty(rejection) ? "Вы в экипаже " + Team(me) : rejection;
        }
        void PublishMembership()
        {
            SteamMatchmaking.SetLobbyMemberData(MatchLobby, "team_mode", TeamMode);
            SteamMatchmaking.SetLobbyMemberData(MatchLobby, "party", Lobby.ToString());
            SteamMatchmaking.SetLobbyMemberData(MatchLobby, "selected_team", Team(SteamUser.GetSteamID()).ToString());
        }
        void JoinConnection(string connection)
        {
            var args = (connection ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (args.Length == 2 && args[0] == "+connect_lobby" && ulong.TryParse(args[1], out var id)) Join(new CSteamID(id));
        }
        void PublishPresence()
        {
            SteamFriends.SetRichPresence("connect", Waiting ? "+connect_lobby " + Lobby.m_SteamID : null);
            SteamFriends.SetRichPresence("steam_player_group", InLobby ? Lobby.m_SteamID.ToString() : null);
            SteamFriends.SetRichPresence("steam_player_group_size", InLobby ? Count.ToString() : null);
        }
        public void Invite()
        {
            if (!Available || !Waiting) return;
            inviteVisible = true;
            AdvancedPlayerController.SetCursor(false);
        }
        void OnGUI()
        {
            if (!inviteVisible || !Available || !Waiting) return;
            int oldDepth = GUI.depth;
            GUI.depth = -100;
            float width = Mathf.Min(440, Screen.width - 24), height = Mathf.Min(460, Screen.height - 24);
            GUI.ModalWindow(734921, new Rect((Screen.width - width) * .5f, (Screen.height - height) * .5f, width, height), DrawInvites, "Пригласить друзей");
            GUI.depth = oldDepth;
        }
        void DrawInvites(int id)
        {
            GUILayout.Space(8);
            if (SteamUtils.IsOverlayEnabled())
            {
                if (GUILayout.Button("Открыть приглашения Steam", GUILayout.Height(30))) SteamFriends.ActivateGameOverlayInviteDialog(Lobby);
            }
            else GUILayout.Label("Оверлей недоступен — пригласите друга из списка.");
            friendScroll = GUILayout.BeginScrollView(friendScroll);
            int count = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);
            if (count == 0) GUILayout.Label("В списке Steam пока нет друзей.");
            for (int i = 0; i < count; i++)
            {
                var friend = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);
                bool present = false;
                for (int j = 0; j < Count; j++) if (Member(j) == friend) { present = true; break; }
                bool sent = invitedAt.TryGetValue(friend.m_SteamID, out var at) && Time.unscaledTime - at < 10;
                GUILayout.BeginHorizontal();
                GUILayout.Label(Name(friend), GUILayout.MinWidth(140));
                bool enabled = GUI.enabled;
                GUI.enabled = enabled && !present && !sent && Count < LobbyCapacity;
                if (GUILayout.Button(present ? "В лобби" : sent ? "Отправлено" : "Пригласить", GUILayout.Width(115)))
                {
                    bool success = SteamMatchmaking.InviteUserToLobby(Lobby, friend);
                    if (success) invitedAt[friend.m_SteamID] = Time.unscaledTime;
                    Status = success ? "Приглашение отправлено: " + Name(friend) : "Не удалось отправить приглашение";
                }
                GUI.enabled = enabled;
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.Label(Status);
            if (GUILayout.Button("Закрыть", GUILayout.Height(30))) inviteVisible = false;
        }
        bool launchEnvironmentTest;
        public void Launch(bool environmentTest = false)
        {
            if (!CanLaunch()) return;
            launchEnvironmentTest = environmentTest;
            LockRoster();
            Busy = true; pendingAt = Time.unscaledTime;
            matchCreated.Set(SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, Mathf.Min(LobbyCapacity, session.MaxPlayers)));
        }
        bool CanLaunch()
        {
            if (!IsLeader || !Waiting || Busy || session.SessionBusy) return false;
            ResolveTeams();
            if (Count > session.MaxPlayers) { Status = "В сессии недостаточно мест"; return false; }
            int occupied = 0;
            for (int team = 1; team <= TeamCount; team++) if (TeamMembers(team) > 0) occupied++;
            if (occupied > session.MaxPlayers / SessionController.CrewSize) { Status = "В сессии недостаточно кораблей"; return false; }
            for (int i = 0; i < Count; i++) if (TeamPending(Member(i))) { Status = "Дождитесь распределения экипажей"; return false; }
            for (int i = 0; i < Count; i++) if (!IsReady(Member(i))) { Status = "Дождитесь готовности всех участников"; return false; }
            return true;
        }
        void MatchCreated(LobbyCreated_t result, bool failed)
        {
            Busy = false;
            if (failed || result.m_eResult != EResult.k_EResultOK) { LeaveSession(); Status = "Не удалось создать сессию"; return; }
            MatchLobby = new CSteamID(result.m_ulSteamIDLobby);
            SteamMatchmaking.SetLobbyData(MatchLobby, "game", Game);
            SteamMatchmaking.SetLobbyData(MatchLobby, "kind", "session");
            SteamMatchmaking.SetLobbyData(MatchLobby, "protocol", session.ProtocolVersion.ToString());
            SteamMatchmaking.SetLobbyData(MatchLobby, "host", SteamUser.GetSteamID().ToString());
            SteamMatchmaking.SetLobbyData(MatchLobby, "state", "loading");
            if (launchEnvironmentTest) SteamMatchmaking.SetLobbyData(MatchLobby, "environment_test", "1");
            PublishMembership();
            PublishSession();
            joiningMatch = true;
            UpdateAdmissions();
            session.BeginSteam(true, SteamUser.GetSteamID().m_SteamID, launchEnvironmentTest);
        }
        public void JoinSession(ulong id)
        {
            if (!CanLaunch() || id == 0) return;
            ignoredMatch = 0;
            LockRoster();
            EnterSession(id);
        }
        void LockRoster()
        {
            SteamMatchmaking.SetLobbyJoinable(Lobby, false);
            SteamMatchmaking.SetLobbyData(Lobby, "state", "starting");
            inviteVisible = false;
            PublishPresence();
        }
        void EnterSession(ulong id)
        {
            if (Busy || MatchLobby.m_SteamID != 0 || session.SessionBusy) return;
            Busy = true; pendingAt = Time.unscaledTime;
            matchEntered.Set(SteamMatchmaking.JoinLobby(new CSteamID(id)));
        }
        void MatchEntered(LobbyEnter_t result, bool failed)
        {
            Busy = false;
            if (failed || result.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            { LeaveSession(); ignoredMatch = result.m_ulSteamIDLobby; Status = "Сессия недоступна или заполнена"; return; }
            MatchLobby = new CSteamID(result.m_ulSteamIDLobby);
            if (SteamMatchmaking.GetLobbyData(MatchLobby, "game") != Game || SteamMatchmaking.GetLobbyData(MatchLobby, "kind") != "session" || SteamMatchmaking.GetLobbyData(MatchLobby, "protocol") != session.ProtocolVersion.ToString())
            { LeaveSession(); Status = "Другая игра или версия сессии"; return; }
            if (IsLeader && SteamMatchmaking.GetNumLobbyMembers(MatchLobby) + Count - 1 > SteamMatchmaking.GetLobbyMemberLimit(MatchLobby))
            { LeaveSession(); Status = "В сессии недостаточно мест для всей команды"; return; }
            PublishMembership();
            if (IsLeader) PublishSession();
            Busy = true; pendingAt = Time.unscaledTime;
            CheckMatch();
        }
        void PublishSession()
        {
            SteamMatchmaking.SetLobbyJoinable(Lobby, false);
            SteamMatchmaking.SetLobbyData(Lobby, "session", MatchLobby.ToString());
            SteamMatchmaking.SetLobbyData(Lobby, "state", "loading");
            inviteVisible = false; PublishPresence();
        }
        public void ServerReady()
        {
            if (MatchLobby.m_SteamID == 0 || SteamMatchmaking.GetLobbyOwner(MatchLobby) != SteamUser.GetSteamID()) return;
            SteamMatchmaking.SetLobbyData(MatchLobby, "state", "playing");
        }
        void CheckMatch()
        {
            if (!InLobby) return;
            PublishPresence();
            var owner = SteamMatchmaking.GetLobbyOwner(Lobby);
            bool ownerChanged = owner != leader;
            leader = owner;
            if (ownerChanged && IsLeader && MatchLobby.m_SteamID == 0 && SteamMatchmaking.GetLobbyData(Lobby, "state") == "starting")
            {
                SteamMatchmaking.SetLobbyData(Lobby, "state", "waiting");
                SteamMatchmaking.SetLobbyJoinable(Lobby, true);
                Status = "Хост вышел — вы управляете лобби";
            }
            ResolveTeams(); UpdateTeamStatus();
            ulong.TryParse(SteamMatchmaking.GetLobbyData(Lobby, "session"), out var requested);
            if (requested == 0) ignoredMatch = 0;
            if (MatchLobby.m_SteamID == 0)
            {
                if (requested != 0 && requested != ignoredMatch) EnterSession(requested);
                return;
            }
            if (!ulong.TryParse(SteamMatchmaking.GetLobbyData(MatchLobby, "host"), out var host) || SteamMatchmaking.GetLobbyOwner(MatchLobby).m_SteamID != host)
            { session.Disconnect(); LeaveSession(); Status = "Хост сессии вышел"; return; }
            if (host == SteamUser.GetSteamID().m_SteamID) UpdateAdmissions();
            if (!joiningMatch && MatchStarted && !session.SessionBusy && SteamMatchmaking.GetLobbyData(MatchLobby, "crew_" + SteamUser.GetSteamID()) == Lobby.ToString() && int.TryParse(SteamMatchmaking.GetLobbyData(MatchLobby, "team_" + SteamUser.GetSteamID()), out var team) && team > 0)
            { Busy = false; joiningMatch = true; session.BeginSteam(false, host); }
        }
        void UpdateAdmissions()
        {
            for (int i = 0; i < SteamMatchmaking.GetNumLobbyMembers(MatchLobby); i++)
            {
                var member = SteamMatchmaking.GetLobbyMemberByIndex(MatchLobby, i);
                if (!ulong.TryParse(SteamMatchmaking.GetLobbyMemberData(MatchLobby, member, "party"), out var crew) || crew == 0) continue;
                string mode = SteamMatchmaking.GetLobbyMemberData(MatchLobby, member, "team_mode");
                if (mode != "selected") continue;
                if (!int.TryParse(SteamMatchmaking.GetLobbyMemberData(MatchLobby, member, "selected_team"), out int selected) || selected < 1 || selected > TeamCount) continue;
                if (crew == Lobby.m_SteamID && selected != Team(member)) continue;
                if (matchTeams.ContainsKey(member.m_SteamID) && SteamMatchmaking.GetLobbyData(MatchLobby, "crew_" + member) == crew.ToString()) continue;
                string group = crew + ":" + selected;
                if (!crewTeams.TryGetValue(group, out var team))
                {
                    if (crewTeams.Count >= Mathf.Min(TeamCount, session.MaxPlayers / SessionController.CrewSize)) continue;
                    team = crewTeams.Count + 1; crewTeams.Add(group, team);
                }
                int admitted = 0;
                foreach (var assigned in matchTeams) if (assigned.Key != member.m_SteamID && assigned.Value == team) admitted++;
                if (admitted >= SessionController.CrewSize) continue;
                matchTeams[member.m_SteamID] = team;
                SteamMatchmaking.SetLobbyData(MatchLobby, "team_" + member, team.ToString());
                SteamMatchmaking.SetLobbyData(MatchLobby, "crew_" + member, crew.ToString());
            }
        }
        public int AdmittedTeam(string address)
        {
            if (MatchLobby.m_SteamID == 0 || !ulong.TryParse(address, out var id)) return 0;
            UpdateAdmissions();
            for (int i = 0; i < SteamMatchmaking.GetNumLobbyMembers(MatchLobby); i++)
                if (SteamMatchmaking.GetLobbyMemberByIndex(MatchLobby, i).m_SteamID == id && matchTeams.TryGetValue(id, out var team)) return team;
            return 0;
        }
        public void LeaveSession()
        {
            launchEnvironmentTest = false;
            matchCreated?.Cancel(); matchEntered?.Cancel(); Busy = false;
            if (Available && MatchLobby.m_SteamID != 0)
            {
                ignoredMatch = MatchLobby.m_SteamID;
                SteamMatchmaking.LeaveLobby(MatchLobby);
            }
            MatchLobby = default; joiningMatch = false; matchTeams.Clear(); crewTeams.Clear();
            if (Available && IsLeader)
            {
                SteamMatchmaking.SetLobbyData(Lobby, "session", "");
                SteamMatchmaking.SetLobbyData(Lobby, "state", "waiting");
                SteamMatchmaking.SetLobbyJoinable(Lobby, true);
            }
            if (Available) { Ready(false); PublishPresence(); }
        }
        public void Leave()
        {
            LeaveSession();
            created?.Cancel(); entered?.Cancel(); Busy = false;
            if (Available && InLobby) SteamMatchmaking.LeaveLobby(Lobby);
            Lobby = default; ignoredMatch = 0; lastTeamAck = null;
            inviteVisible = false; invitedAt.Clear();
            if (Available) PublishPresence();
        }
        void OnDestroy()
        {
            Leave(); invitation?.Dispose(); presenceInvitation?.Dispose(); data?.Dispose(); members?.Dispose(); created?.Dispose(); entered?.Dispose(); matchCreated?.Dispose(); matchEntered?.Dispose();
            if (Available) SteamAPI.Shutdown();
        }
    }
}
