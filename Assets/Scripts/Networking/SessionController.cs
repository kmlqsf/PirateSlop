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
using PirateSlop.World;
using System.Linq;
namespace PirateSlop.Networking
{
    public sealed partial class SessionController : MonoBehaviour
    {
        public static SessionController Instance { get; private set; }
        public static bool MenuOpen => Instance != null && (!Instance.playing || (Cursor.lockState != CursorLockMode.Locked && !PlayerInventory.LootWindowOpen));
        public SessionConfig Config;
        public NetworkObject ShipPrefab, PlayerPrefab;
        public Camera MenuCamera;
        public int MaxPlayers { get; private set; }
        public int ProtocolVersion { get; private set; }
        public bool Automated { get; private set; }
        public string SessionId { get; private set; }
        NetworkManager manager;
        Tugboat transport;
        SteamParty party;
        bool steamSession;
        ulong steamHost;
        public void ShowSteamParty() { menuPage = 0; AdvancedPlayerController.SetCursor(false); }
        public bool SessionBusy => connecting || playing || starting || (manager != null && manager.ServerManager.Started);
        public int SteamTeam(NetworkConnection conn) => !steamSession ? 0 : party.AdmittedTeam(manager.TransportManager.Transport.GetConnectionAddress(conn.ClientId));
        public bool AcceptSteamConnection(NetworkConnection conn) => !steamSession || SteamTeam(conn) > 0;
        public void BeginSteam(bool host, ulong steamId, bool environmentTest = false)
        {
            if (SessionBusy || party == null || !party.Available) return;
            steamSession = true; steamHost = steamId;
            Begin(host, "127.0.0.1:" + Config.Port, environmentTest);
        }
        readonly Dictionary<int, NetworkPlayer> players = new();
        public IEnumerable<NetworkPlayer> AllPlayers => players.Values;
        readonly Dictionary<int, int> slots = new();
        int nextParticipant = 1, population;
        string address = "127.0.0.1:7777", status = "Создайте сессию или введите IPv4:порт", error = "";
        bool connecting, playing, dedicated, starting, hostRequested;
        float startedAt, quitAt;
        readonly Dictionary<int, float> awaitingWorld = new();
        string worldJson, worldChecksum;
        Coroutine worldLoading;
        string seedInput = "";
        public NetworkPlayer GetPlayer(int connectionId) => players.TryGetValue(connectionId, out var p) ? p : null;
        void Awake() { Instance = this; Application.runInBackground = true; }
        void Start()
        {
            var sc = GameObject.Find("SettingsCanvas");
            if (sc != null) Destroy(sc);
            manager = GetComponent<NetworkManager>(); transport = GetComponent<Tugboat>();
            party = GetComponent<SteamParty>();
            MaxPlayers = Config.MaxPlayers; ProtocolVersion = Config.ProtocolVersion; SessionId = Guid.NewGuid().ToString("N");
            manager.TimeManager.SetTickRate(Config.TickRate);
            manager.TimeManager.OnPostTick += ResolveShipCollisions;
            manager.SceneManager.OnClientLoadedStartScenes += Loaded;
            manager.ServerManager.OnRemoteConnectionState += RemoteState;
            manager.ServerManager.OnServerConnectionState += ServerState;
            manager.ClientManager.OnClientConnectionState += ClientState;
            manager.ClientManager.RegisterBroadcast<PopulationMessage>(Population);
            manager.ClientManager.RegisterBroadcast<WorldManifestMessage>(WorldManifest);
            manager.ClientManager.RegisterBroadcast<StormMessage>(ReceiveStorm);
            manager.ServerManager.RegisterBroadcast<WorldReadyMessage>(WorldReady);
            var args = Environment.GetCommandLineArgs();
            dedicated = Has(args, "-server"); Automated = Has(args, "-autoclient");
            fillWithBots = Has(args, "-bots");
            RemoteBotDiagnosticsAllowed = Has(args, "-botdebugremote");
            if (int.TryParse(Value(args, "-maxPlayers"), out var max)) MaxPlayers = Mathf.Clamp(max, 1, 128);
            MaxPlayers = Mathf.Max(CrewSize, MaxPlayers / CrewSize * CrewSize);
            if (int.TryParse(Value(args, "-protocol"), out var protocol)) ProtocolVersion = protocol;
            if (!string.IsNullOrEmpty(Value(args, "-sessionId"))) SessionId = Value(args, "-sessionId");
            if (float.TryParse(Value(args, "-duration"), out var duration)) quitAt = Time.realtimeSinceStartup + duration;
            string port = Value(args, "-port") ?? Config.Port.ToString();
            seedInput = Value(args, "-seed") ?? "";
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
        bool environmentTestRequested;
        bool EnvironmentTestActive => EnvironmentTestGallery.IsTest(ProceduralWorld.Instance != null ? ProceduralWorld.Instance.Layout : null);
        public void Begin(bool host, string endpoint, bool environmentTest = false)
        {
            if (starting || connecting || playing || manager.ServerManager.Started) return;
            if (host && !environmentTest && !string.IsNullOrWhiteSpace(seedInput) && !int.TryParse(seedInput, out _)) { SetError("Seed должен быть целым числом."); return; }
            if (!ParseEndpoint(endpoint, out var ip, out var port)) { SetError("Введите IPv4:порт, например 192.168.1.10:7777"); return; }
            environmentTestRequested = host && environmentTest;
            observerSession = !environmentTestRequested && host && fillWithBots && observerSelected && !dedicated && !Automated;
            error = ""; address = endpoint; startedAt = Time.realtimeSinceStartup; connecting = true; starting = true; hostRequested = host;
            status = host ? "Создание сессии…" : "Подключение…";
            botRosterPolicy = host ? new InitialFillBotRosterPolicy(fillWithBots && !environmentTestRequested) : null;
            StartCoroutine(StartSession(host, ip, port));
        }
        IEnumerator StartSession(bool host, string ip, ushort port)
        {
            if (!SceneManager.GetSceneByName(Config.GameScene).isLoaded) yield return SceneManager.LoadSceneAsync(Config.GameScene, LoadSceneMode.Additive);
            if (!connecting) { starting = false; yield break; }
            SceneManager.SetActiveScene(SceneManager.GetSceneByName(Config.GameScene));
            var world = ProceduralWorld.Instance;
            if (world == null) { starting = false; connecting = false; SetError("В NetworkOcean отсутствует ProceduralWorld."); yield break; }
            stormRunning = stormPaused = false;
            if (storm != null) Destroy(storm.gameObject);
            if (OceanSurface.Instance != null) OceanSurface.Instance.WaveScale = .06f;
            if (host)
            {
                WorldLayout layout = null;
                try
                {
                    int seed = int.TryParse(seedInput, out var requestedSeed) ? requestedSeed : world.Profile.RandomSeed ? BitConverter.ToInt32(Guid.NewGuid().ToByteArray(), 0) : world.Profile.Seed;
                    float seaLevel = OceanSurface.Instance != null ? OceanSurface.Instance.SeaLevel : 0;
                    layout = environmentTestRequested ? EnvironmentTestGallery.CreateLayout(world.Profile, seaLevel) : WorldGenerator.Generate(world.Profile, seed, MaxPlayers, seaLevel);
                }
                catch (Exception ex) { SetError("Генерация карты: " + ex.Message); }
                if (layout == null) { starting = connecting = false; yield break; }
                yield return GenerateWorld(world, layout);
                if (!world.Ready || !connecting) { starting = connecting = false; yield break; }
                worldJson = layout.ToJson(); worldChecksum = world.Checksum;
            }
            else world.Clear();
            if (!connecting) { starting = false; yield break; }
            startedAt = Time.realtimeSinceStartup;
            transport.SetPort(port);
            // 0.0.0.0 is a server bind address, not a routable client endpoint.
            // The host's local client must connect through loopback; remote clients
            // continue to use the address entered in the menu.
            transport.SetClientAddress(host ? "127.0.0.1" : ip);
            transport.SetServerBindAddress("0.0.0.0", IPAddressType.IPv4);
            // Reserve a few transport slots so the application can return a meaningful full-session rejection.
            transport.SetMaximumClients(MaxPlayers + 8);
            var multipass = manager.TransportManager.Transport as FishNet.Transporting.Multipass.Multipass;
            if (multipass != null)
            {
                multipass.SetClientTransport(steamSession ? 1 : 0);
                if (steamSession)
                {
                    multipass.GetTransport(1).SetClientAddress(steamHost.ToString());
                    multipass.GetTransport(1).SetMaximumClients(MaxPlayers + 8);
                }
            }
            starting = false;
            if (host) { if (!(multipass != null ? multipass.StartConnection(true, steamSession ? 1 : 0) : manager.ServerManager.StartConnection())) { connecting = false; SetError("Не удалось запустить сервер"); if (steamSession) party.Leave(); } }
            else if (!manager.ClientManager.StartConnection()) SetError("Не удалось начать подключение");
        }
        void ServerState(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started)
            {
                Debug.Log($"SESSION_READY id={SessionId} port={transport.GetPort()} capacity={MaxPlayers}");
                if (!EnvironmentTestActive) SeaLootSpawner.Spawn(ProceduralWorld.Instance, manager, Config.Loot);
                InitializeBotRoster();
                if (steamSession) party.ServerReady();
                if (dedicated) { connecting = false; status = "Сервер запущен"; }
                else if (hostRequested) manager.ClientManager.StartConnection();
            }
            else if (args.ConnectionState == LocalConnectionState.Stopped)
            {
                if (connecting && error == "") SetError("Сервер не запущен: порт занят или недоступен");
                players.Clear(); slots.Clear(); awaitingWorld.Clear(); population = 0;
                ResetRoster();
            }
        }
        void ClientState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState != LocalConnectionState.Stopped) return;
            StopObserver();
            bool unexpected = playing || connecting;
            playing = connecting = false;
            if (steamSession && unexpected) { party?.LeaveSession(); steamSession = false; }
            if (unexpected && error == "") SetError("Соединение закрыто: хост вышел или адрес недоступен");
            AdvancedPlayerController.SetCursor(false);
            if (MenuCamera != null && !Automated && !dedicated) MenuCamera.gameObject.SetActive(true);
            if (manager.ServerManager.Started && !dedicated) manager.ServerManager.StopConnection(true);
        }
        void Loaded(NetworkConnection conn, bool asServer)
        {
            if (!asServer || !conn.IsAuthenticated || players.ContainsKey(conn.ClientId)) return;
            if (string.IsNullOrEmpty(worldJson) || ProceduralWorld.Instance == null || !ProceduralWorld.Instance.Ready) { conn.Disconnect(true); return; }
            awaitingWorld[conn.ClientId] = Time.realtimeSinceStartup;
            manager.ServerManager.Broadcast(conn, new WorldManifestMessage { Json = worldJson, Checksum = worldChecksum });
        }
        IEnumerator GenerateWorld(ProceduralWorld world, WorldLayout layout)
        {
            status = "Генерация карты…";
            var builder = world.Build(layout);
            while (connecting || manager.ServerManager.Started)
            {
                bool more;
                try { more = builder.MoveNext(); }
                catch (Exception ex) { world.Clear(); SetError("Карта: " + ex.Message); break; }
                if (!more) break;
                startedAt = Time.realtimeSinceStartup;
                yield return builder.Current;
            }
            (builder as IDisposable)?.Dispose();
            if (world.Ready && !EnvironmentTestActive) ShipComparison.Spawn(world, Config);
        }
        void WorldManifest(WorldManifestMessage message, Channel channel)
        {
            if (worldLoading != null || playing) return;
            WorldLayout layout;
            try { layout = WorldLayout.FromJson(message.Json); }
            catch (Exception ex) { Disconnect(); SetError("Карта: " + ex.Message); return; }
            worldLoading = StartCoroutine(ReceiveWorld(layout, message.Checksum));
        }
        IEnumerator ReceiveWorld(WorldLayout layout, string checksum)
        {
            yield return null;
            var world = ProceduralWorld.Instance;
            if (!manager.ServerManager.Started) yield return GenerateWorld(world, layout);
            if (world == null || !world.Ready || world.Checksum != checksum)
            {
                worldLoading = null; Disconnect(); SetError("Карта не совпадает с сервером. Обновите обе игры."); yield break;
            }
            manager.ClientManager.Broadcast(new WorldReadyMessage { Checksum = checksum });
            startedAt = Time.realtimeSinceStartup; worldLoading = null;
        }
        void WorldReady(NetworkConnection conn, WorldReadyMessage message, Channel channel)
        {
            if (!conn.IsAuthenticated || !awaitingWorld.Remove(conn.ClientId) || players.ContainsKey(conn.ClientId)) return;
            if (message.Checksum != worldChecksum) { conn.Disconnect(true); return; }
            if (observerSession && manager.ClientManager.Connection != null && conn.ClientId == manager.ClientManager.Connection.ClientId)
            { StartObserver(); return; }
            SpawnPlayer(conn);
        }
        void SpawnPlayer(NetworkConnection conn)
        {
            int crew = SteamTeam(conn);
            if (steamSession && crew <= 0) { conn.Disconnect(true); return; }
            int team = HumanTeam(crew);
            if (team <= 0) { conn.Disconnect(true); return; }
            var replacement = FindReplacementBot(team);
            int teamCount = players.Values.Count(p => p != null && p.TeamId.Value == team);
            if (teamCount >= CrewSize && (replacement == null || replacement.TeamId.Value != team)) { conn.Disconnect(true); return; }
            if (players.Count >= MaxPlayers && replacement == null) { conn.Disconnect(true); return; }
            var crewmate = players.Values.FirstOrDefault(p => p != null && p.TeamId.Value == team && p.Ship != null);
            NetworkShip ship;
            int slot;
            if (crewmate != null)
            {
                ship = crewmate.Ship;
                slot = slots[players.First(p => p.Value == crewmate).Key];
            }
            else
            {
                if (!TrySpawnShip(team, Config.ClusteredTestSpawns, out ship, out slot)) { conn.Disconnect(true); return; }
            }
            slots[conn.ClientId] = slot;
            int id = nextParticipant++;
            var player = Instantiate(PlayerPrefab, CrewSpawn(ship, replacement), ship.transform.rotation).GetComponent<NetworkPlayer>();
            player.ParticipantId.Value = id; player.ShipObject.Value = ship.NetworkObject;
            player.HomeShipId.Value = ship.ParticipantId.Value;
            player.TeamId.Value = team;
            players.Add(conn.ClientId, player);
            manager.ServerManager.Spawn(player.NetworkObject, conn);
            if (replacement != null)
            {
                replacement.GetComponent<NetworkWeapon>().TransferInventoryTo(player.GetComponent<NetworkWeapon>());
                RetireReplacedBot(replacement);
            }
            manager.SceneManager.AddOwnerToDefaultScene(player.NetworkObject);
            if (!EnvironmentTestActive)
            {
                if (!stormRunning) StartStorm();
                manager.ServerManager.Broadcast(conn, CurrentStorm());
            }
            BroadcastPopulation();
            Debug.Log($"PLAYER_SPAWN participant={id} connection={conn.ClientId} slot={slot} position={player.transform.position}");
        }
        bool FindNearbySpawn(ref Vector3 position, ref float yaw)
        {
            var anchor = players.Values.FirstOrDefault(p => p != null && !p.IsBot.Value && p.Ship != null);
            if (anchor == null) return true;
            yaw = anchor.Ship.transform.eulerAngles.y;
            float spacing = Mathf.Max(52f, Config.SpawnSpacing);
            for (int ring = 1; ring <= 8; ring++)
                for (int i = 0; i < ring * 8; i++)
                {
                    float angle = i * Mathf.PI * 2f / (ring * 8);
                    Vector3 candidate = anchor.Ship.transform.position + Quaternion.Euler(0, yaw, 0) * new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (spacing * ring);
                    if (!ProceduralWorld.Instance.CanSail(candidate, yaw)) continue;
                    if (players.Values.Any(p => p != null && p.Ship != null && Vector3.Distance(p.Ship.transform.position, candidate) < spacing - .1f)) continue;
                    position = candidate; return true;
                }
            return false;
        }
        void RemoteState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState != RemoteConnectionState.Stopped) return;
            awaitingWorld.Remove(conn.ClientId);
            if (players.TryGetValue(conn.ClientId, out var leaving))
            {
                var ship = leaving == null ? null : leaving.Ship;
                if (ship != null && !players.Values.Any(p => p != null && p != leaving && p.Ship == ship))
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
        static readonly HashSet<int> teamPopulationSet = new();
        static readonly List<int> expiredAwaitingKeys = new();
        void BroadcastPopulation()
        {
            population = players.Count;
            int bots = 0;
            teamPopulationSet.Clear();
            foreach (var p in players.Values)
            {
                if (p == null) continue;
                if (p.IsBot.Value) bots++;
                if (!TeamEliminated(p.TeamId.Value)) teamPopulationSet.Add(p.TeamId.Value);
            }
            botPopulation = bots;
            teamPopulation = teamPopulationSet.Count;
            manager.ServerManager.Broadcast(new PopulationMessage { Count = population, Bots = botPopulation, Teams = teamPopulation, SessionId = SessionId });
        }
        void Population(PopulationMessage message, Channel channel) { population = message.Count; botPopulation = message.Bots; teamPopulation = message.Teams; SessionId = message.SessionId; }
        public void PlayerReady(NetworkPlayer player)
        {
            connecting = false; playing = true; status = "В сессии";
            if (MenuCamera != null) MenuCamera.gameObject.SetActive(false);
            Debug.Log($"CLIENT_READY participant={player.ParticipantId.Value} session={SessionId}");
        }
        public void SetError(string message) { error = message; status = message; Debug.LogWarning("SESSION_ERROR " + message); }
        public void Disconnect()
        {
            StopObserver();
            playing = connecting = false; hostRequested = false;
            if (worldLoading != null) { StopCoroutine(worldLoading); worldLoading = null; }
            manager.ClientManager.StopConnection(); if (manager.ServerManager.Started) manager.ServerManager.StopConnection(true);
            if (steamSession) party?.LeaveSession();
            steamSession = false;
            AdvancedPlayerController.SetCursor(false); status = "Отключено";
            if (MenuCamera != null && !Automated && !dedicated) MenuCamera.gameObject.SetActive(true);
        }
        void Update()
        {
            if (steamSession && !SessionBusy && !string.IsNullOrEmpty(error)) { party?.LeaveSession(); steamSession = false; }
            TickStorm();
            TickCrewElimination();
            TickBotDiagnostics();
            TickBotTasks();
            if (manager != null && manager.ServerManager.Started) BotPaths.Tick(Config.BotMotion.PathNodesPerFrame, Config.BotMotion.PathMillisecondsPerFrame);
            if (manager != null && manager.ServerManager.Started && awaitingWorld.Count > 0)
            {
                expiredAwaitingKeys.Clear();
                float now = Time.realtimeSinceStartup;
                foreach (var pair in awaitingWorld)
                    if (now - pair.Value > 120f) expiredAwaitingKeys.Add(pair.Key);
                for (int i = 0; i < expiredAwaitingKeys.Count; i++)
                {
                    int id = expiredAwaitingKeys[i];
                    awaitingWorld.Remove(id);
                    if (manager.ServerManager.Clients.TryGetValue(id, out var conn)) conn.Disconnect(true);
                }
            }
            if (quitAt > 0 && Time.realtimeSinceStartup >= quitAt) { Disconnect(); Application.Quit(); }
            if (connecting && Time.realtimeSinceStartup - startedAt >= Config.ConnectTimeout) { var reason = error == "" ? "Тайм-аут подключения (15 с): проверьте IP, UDP-порт и Firewall" : error; Disconnect(); SetError(reason); }
        }
        void OnDestroy()
        {
            StopObserver();
            ReleaseMenu();
            if (manager != null) manager.TimeManager.OnPostTick -= ResolveShipCollisions;
        }
        public NetworkObject SpawnDeveloperShip(Vector3 origin, float yaw)
        {
            if (!DeveloperMenu.Available || manager == null || !manager.IsServerStarted) return null;
            var ships = FindObjectsByType<NetworkShip>(FindObjectsSortMode.None);
            for (int i = 0; i < 24; i++)
            {
                Vector3 point = origin + Quaternion.Euler(0, i * 45, 0) * Vector3.forward * (38 + i / 8 * 15);
                point.y = OceanSurface.Instance != null ? OceanSurface.Instance.SeaLevel : 0;
                if (ships.Any(s => Vector3.Distance(s.transform.position, point) < 35)) continue;
                if (ProceduralWorld.Instance != null && !ProceduralWorld.Instance.CanSail(point, yaw)) continue;
                var ship = Instantiate(ShipPrefab, point, Quaternion.Euler(0, yaw, 0));
                ship.GetComponent<NetworkShip>().ParticipantId.Value = nextParticipant++;
                SceneManager.MoveGameObjectToScene(ship.gameObject, SceneManager.GetSceneByName(Config.GameScene));
                manager.ServerManager.Spawn(ship);
                return ship;
            }
            return null;
        }
        void ResolveShipCollisions()
        {
            if (manager == null || !manager.IsServerStarted) return;
            var ships = NetworkShip.ActiveShips;
            for (int i = 0; i < ships.Count; i++)
            for (int j = i + 1; j < ships.Count; j++)
            {
                var a = ships[i]; var b = ships[j];
                if (a == null || b == null || a.IsSinking || b.IsSinking || !a.IsServerInitialized || !b.IsServerInitialized) continue;
                Vector2 delta = new(b.transform.position.x-a.transform.position.x,b.transform.position.z-a.transform.position.z);
                float radius = a.HullHalfExtents.magnitude+b.HullHalfExtents.magnitude;
                if (delta.sqrMagnitude >= radius*radius) continue;
                float ay = a.transform.eulerAngles.y*Mathf.Deg2Rad, by = b.transform.eulerAngles.y*Mathf.Deg2Rad;
                Vector2 ar = new(Mathf.Cos(ay),-Mathf.Sin(ay)), af = new(Mathf.Sin(ay),Mathf.Cos(ay));
                Vector2 br = new(Mathf.Cos(by),-Mathf.Sin(by)), bf = new(Mathf.Sin(by),Mathf.Cos(by));
                float depth = float.PositiveInfinity;
                Vector2 normal = Vector2.zero;
                if (!HullAxis(ar,delta,ar,af,a.HullHalfExtents,br,bf,b.HullHalfExtents,ref depth,ref normal) ||
                    !HullAxis(af,delta,ar,af,a.HullHalfExtents,br,bf,b.HullHalfExtents,ref depth,ref normal) ||
                    !HullAxis(br,delta,ar,af,a.HullHalfExtents,br,bf,b.HullHalfExtents,ref depth,ref normal) ||
                    !HullAxis(bf,delta,ar,af,a.HullHalfExtents,br,bf,b.HullHalfExtents,ref depth,ref normal)) continue;
                Vector3 correction = new Vector3(normal.x,0,normal.y)*(depth+.002f);
                Vector2 tangent = new(-normal.y, normal.x);
                float an = Mathf.Abs(Vector2.Dot(ar, normal)) * a.HullHalfExtents.x + Mathf.Abs(Vector2.Dot(af, normal)) * a.HullHalfExtents.y;
                float bn = Mathf.Abs(Vector2.Dot(br, normal)) * b.HullHalfExtents.x + Mathf.Abs(Vector2.Dot(bf, normal)) * b.HullHalfExtents.y;
                float at = Mathf.Abs(Vector2.Dot(ar, tangent)) * a.HullHalfExtents.x + Mathf.Abs(Vector2.Dot(af, tangent)) * a.HullHalfExtents.y;
                float bt = Mathf.Abs(Vector2.Dot(br, tangent)) * b.HullHalfExtents.x + Mathf.Abs(Vector2.Dot(bf, tangent)) * b.HullHalfExtents.y;
                float offset = Vector2.Dot(delta, tangent);
                float along = (Mathf.Max(-at, offset - bt) + Mathf.Min(at, offset + bt)) * .5f;
                Vector2 contact = normal * ((an + Vector2.Dot(delta, normal) - bn) * .5f) + tangent * along;
                Vector3 point = a.transform.position + new Vector3(contact.x, 0f, contact.y);
                var ocean = OceanSurface.Instance;
                if (ocean != null) point.y = ocean.Height(point) + .15f;
                float impactSpeed = Mathf.Abs(Vector3.Dot(a.Motor.CannonPointVelocity(point) - b.Motor.CannonPointVelocity(point), new Vector3(normal.x, 0f, normal.y)));
                float impactStrength = Mathf.Clamp01(impactSpeed / Mathf.Max(1f, Mathf.Max(a.Motor.MaxSpeed, b.Motor.MaxSpeed)));
                if (a.Motor.IsFrozen && b.Motor.IsFrozen) continue;
                if (a.Motor.IsFrozen) b.Motor.ResolveCollision(correction, point, true, impactStrength);
                else if (b.Motor.IsFrozen) a.Motor.ResolveCollision(-correction, point, true, impactStrength);
                else { a.Motor.ResolveCollision(-correction*.5f, point, true, impactStrength); b.Motor.ResolveCollision(correction*.5f, point, false, impactStrength); }
            }
        }
        static bool HullAxis(Vector2 axis,Vector2 delta,Vector2 ar,Vector2 af,Vector2 ah,Vector2 br,Vector2 bf,Vector2 bh,ref float depth,ref Vector2 normal)
        {
            float distance = Vector2.Dot(delta,axis);
            float overlap = Mathf.Abs(Vector2.Dot(ar,axis))*ah.x+Mathf.Abs(Vector2.Dot(af,axis))*ah.y+
                Mathf.Abs(Vector2.Dot(br,axis))*bh.x+Mathf.Abs(Vector2.Dot(bf,axis))*bh.y-Mathf.Abs(distance);
            if (overlap <= 0f) return false;
            if (overlap < depth) { depth=overlap; normal=distance<0f ? -axis : axis; }
            return true;
        }
        void OnGUI() => DrawSessionMenu();
    }
}

