using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Connection;
using FishNet.Transporting;
using FishNet.Transporting.Tugboat;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace PirateSlop.Networking
{
    public sealed class SessionController : MonoBehaviour
    {
        public static SessionController Instance { get; private set; }
        public static bool MenuOpen => Instance != null && (!Instance.playing || Cursor.lockState != CursorLockMode.Locked);
        public SessionConfig Config;
        public NetworkObject ShipPrefab, PlayerPrefab;
        public Camera MenuCamera;
        public int MaxPlayers { get; private set; }
        public int ProtocolVersion { get; private set; }
        public bool Automated { get; private set; }
        public string SessionId { get; private set; }
        NetworkManager manager;
        Tugboat transport;
        readonly Dictionary<int, NetworkPlayer> players = new();
        readonly Dictionary<int, int> slots = new();
        int nextParticipant = 1, population;
        string address = "127.0.0.1:7777", status = "Создайте сессию или введите IPv4:порт", error = "";
        bool connecting, playing, dedicated, starting, hostRequested;
        float startedAt, quitAt;
        public NetworkPlayer GetPlayer(int connectionId) => players.TryGetValue(connectionId, out var p) ? p : null;
        void Awake() { Instance = this; Application.runInBackground = true; }
        void Start()
        {
            manager = GetComponent<NetworkManager>(); transport = GetComponent<Tugboat>();
            MaxPlayers = Config.MaxPlayers; ProtocolVersion = Config.ProtocolVersion; SessionId = Guid.NewGuid().ToString("N");
            manager.TimeManager.SetTickRate(Config.TickRate);
            manager.SceneManager.OnClientLoadedStartScenes += Loaded;
            manager.ServerManager.OnRemoteConnectionState += RemoteState;
            manager.ServerManager.OnServerConnectionState += ServerState;
            manager.ClientManager.OnClientConnectionState += ClientState;
            manager.ClientManager.RegisterBroadcast<PopulationMessage>(Population);
            var args = Environment.GetCommandLineArgs();
            dedicated = Has(args, "-server"); Automated = Has(args, "-autoclient");
            if (int.TryParse(Value(args, "-maxPlayers"), out var max)) MaxPlayers = Mathf.Clamp(max, 1, 128);
            if (int.TryParse(Value(args, "-protocol"), out var protocol)) ProtocolVersion = protocol;
            if (!string.IsNullOrEmpty(Value(args, "-sessionId"))) SessionId = Value(args, "-sessionId");
            if (float.TryParse(Value(args, "-duration"), out var duration)) quitAt = Time.realtimeSinceStartup + duration;
            string port = Value(args, "-port") ?? Config.Port.ToString();
            if (dedicated || Has(args, "-host")) Begin(true, "127.0.0.1:" + port);
            else if (Has(args, "-connect")) Begin(false, Value(args, "-connect"));
            else AdvancedPlayerController.SetCursor(false);
            if (dedicated || Automated) { Application.targetFrameRate = Config.TickRate; if (MenuCamera != null) MenuCamera.gameObject.SetActive(false); }
        }
        static bool Has(string[] args, string name) => Array.IndexOf(args, name) >= 0;
        static string Value(string[] args, string name) { int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i+1] : null; }
        public static bool ParseEndpoint(string text, out string host, out ushort port)
        {
            host = null; port = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            var parts = text.Trim().Split(':');
            if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out var ip) || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork || !ushort.TryParse(parts[1], out port) || port == 0) return false;
            host = ip.ToString(); return true;
        }
        public void Begin(bool host, string endpoint)
        {
            if (starting || connecting || playing || manager.ServerManager.Started) return;
            if (!ParseEndpoint(endpoint, out var ip, out var port)) { SetError("Введите IPv4:порт, например 192.168.1.10:7777"); return; }
            error = ""; address = endpoint; startedAt = Time.realtimeSinceStartup; connecting = true; starting = true; hostRequested = host;
            status = host ? "Создание сессии…" : "Подключение…";
            StartCoroutine(StartSession(host, ip, port));
        }
        IEnumerator StartSession(bool host, string ip, ushort port)
        {
            if (!SceneManager.GetSceneByName(Config.GameScene).isLoaded) yield return SceneManager.LoadSceneAsync(Config.GameScene, LoadSceneMode.Additive);
            if (!connecting) { starting = false; yield break; }
            SceneManager.SetActiveScene(SceneManager.GetSceneByName(Config.GameScene));
            transport.SetPort(port); transport.SetClientAddress(ip); transport.SetServerBindAddress("0.0.0.0", IPAddressType.IPv4);
            // Reserve a few transport slots so the application can return a meaningful full-session rejection.
            transport.SetMaximumClients(MaxPlayers + 8);
            starting = false;
            if (host) { if (!manager.ServerManager.StartConnection()) SetError("Не удалось запустить сервер: проверьте UDP-порт"); }
            else if (!manager.ClientManager.StartConnection()) SetError("Не удалось начать подключение");
        }
        void ServerState(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started)
            {
                Debug.Log($"SESSION_READY id={SessionId} port={transport.GetPort()} capacity={MaxPlayers}");
                if (dedicated) { connecting = false; status = "Сервер запущен"; }
                else if (hostRequested) manager.ClientManager.StartConnection();
            }
            else if (args.ConnectionState == LocalConnectionState.Stopped)
            {
                if (connecting && error == "") SetError("Сервер не запущен: порт занят или недоступен");
                players.Clear(); slots.Clear(); population = 0;
            }
        }
        void ClientState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState != LocalConnectionState.Stopped) return;
            bool unexpected = playing || connecting;
            playing = connecting = false;
            if (unexpected && error == "") SetError("Соединение закрыто: хост вышел или адрес недоступен");
            AdvancedPlayerController.SetCursor(false);
            if (MenuCamera != null && !Automated && !dedicated) MenuCamera.gameObject.SetActive(true);
            if (manager.ServerManager.Started && !dedicated) manager.ServerManager.StopConnection(true);
        }
        void Loaded(NetworkConnection conn, bool asServer)
        {
            if (!asServer || !conn.IsAuthenticated || players.ContainsKey(conn.ClientId)) return;
            int slot = 0; while (slots.ContainsValue(slot)) slot++;
            slots[conn.ClientId] = slot;
            int side = Mathf.CeilToInt(Mathf.Sqrt(MaxPlayers));
            Vector3 position = Config.ShipOrigin + new Vector3(slot % side * Config.SpawnSpacing, 0, slot / side * Config.SpawnSpacing);
            var ship = Instantiate(ShipPrefab, position, Quaternion.identity).GetComponent<NetworkShip>();
            int id = nextParticipant++; ship.ParticipantId.Value = id;
            manager.ServerManager.Spawn(ship.NetworkObject, conn);
            manager.SceneManager.AddOwnerToDefaultScene(ship.NetworkObject);
            var player = Instantiate(PlayerPrefab, ship.transform.TransformPoint(Config.PlayerLocalSpawn), Quaternion.identity).GetComponent<NetworkPlayer>();
            player.ParticipantId.Value = id; player.ShipObject.Value = ship.NetworkObject;
            players.Add(conn.ClientId, player);
            manager.ServerManager.Spawn(player.NetworkObject, conn);
            manager.SceneManager.AddOwnerToDefaultScene(player.NetworkObject);
            BroadcastPopulation();
            Debug.Log($"PLAYER_SPAWN participant={id} connection={conn.ClientId} slot={slot} position={player.transform.position}");
        }
        void RemoteState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState != RemoteConnectionState.Stopped) return;
            if (players.TryGetValue(conn.ClientId, out var leaving))
            {
                var ship = leaving == null ? null : leaving.Ship;
                if (ship != null)
                {
                    ship.Helm.ReleaseControl();
                    foreach (var other in players.Values) if (other != null && other != leaving && other.Passenger.Ship == ship.Body) other.ReturnHome();
                    if (ship.IsSpawned) manager.ServerManager.Despawn(ship.NetworkObject);
                }
                if (leaving != null && leaving.IsSpawned) manager.ServerManager.Despawn(leaving.NetworkObject);
                players.Remove(conn.ClientId); slots.Remove(conn.ClientId);
            }
            Debug.Log($"PLAYER_LEFT connection={conn.ClientId}"); BroadcastPopulation();
        }
        void BroadcastPopulation() { population = players.Count; manager.ServerManager.Broadcast(new PopulationMessage { Count = population, SessionId = SessionId }); }
        void Population(PopulationMessage message, Channel channel) { population = message.Count; SessionId = message.SessionId; }
        public void PlayerReady(NetworkPlayer player)
        {
            connecting = false; playing = true; status = "В сессии";
            if (MenuCamera != null) MenuCamera.gameObject.SetActive(false);
            Debug.Log($"CLIENT_READY participant={player.ParticipantId.Value} session={SessionId}");
        }
        public void SetError(string message) { error = message; status = message; Debug.LogWarning("SESSION_ERROR " + message); }
        public void Disconnect()
        {
            playing = connecting = false; hostRequested = false;
            manager.ClientManager.StopConnection(); if (manager.ServerManager.Started) manager.ServerManager.StopConnection(true);
            AdvancedPlayerController.SetCursor(false); status = "Отключено";
            if (MenuCamera != null && !Automated && !dedicated) MenuCamera.gameObject.SetActive(true);
        }
        void Update()
        {
            if (quitAt > 0 && Time.realtimeSinceStartup >= quitAt) { Disconnect(); Application.Quit(); }
            if (connecting && Time.realtimeSinceStartup - startedAt >= Config.ConnectTimeout) { var reason = error == "" ? "Тайм-аут подключения (15 с): проверьте IP, UDP-порт и Firewall" : error; Disconnect(); SetError(reason); }
        }
        void OnGUI()
        {
            if (dedicated || Automated || !MenuOpen) return;
            GUI.skin.label.wordWrap = true;
            GUILayout.BeginArea(new Rect((Screen.width-440)/2, (Screen.height-340)/2, 440, 340), "PirateSlop — онлайн", GUI.skin.window);
            GUILayout.Space(12); GUILayout.Label(status); GUILayout.Label($"Игроки: {population}/{MaxPlayers}");
            if (!playing && !connecting) { GUILayout.Label("IPv4:порт (UDP)"); address = GUILayout.TextField(address, 64); if (GUILayout.Button("Создать сессию", GUILayout.Height(35))) Begin(true, address); if (GUILayout.Button("Подключиться", GUILayout.Height(35))) Begin(false, address); }
            else { if (GUILayout.Button(connecting ? "Отменить" : "Отключиться", GUILayout.Height(35))) Disconnect(); if (playing && GUILayout.Button("Вернуться в игру", GUILayout.Height(35))) AdvancedPlayerController.SetCursor(true); }
            GUILayout.Label("Хост: публичный IPv4 и открытый UDP-порт.\nEscape — меню. E — штурвал своего корабля.");
            GUILayout.EndArea();
        }
    }
}
