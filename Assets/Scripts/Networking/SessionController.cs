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
        readonly Dictionary<int, NetworkPlayer> players = new();
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
            manager = GetComponent<NetworkManager>(); transport = GetComponent<Tugboat>();
            MaxPlayers = Config.MaxPlayers; ProtocolVersion = Config.ProtocolVersion; SessionId = Guid.NewGuid().ToString("N");
            manager.TimeManager.SetTickRate(Config.TickRate);
            manager.TimeManager.OnPostTick += ResolveShipCollisions;
            manager.SceneManager.OnClientLoadedStartScenes += Loaded;
            manager.ServerManager.OnRemoteConnectionState += RemoteState;
            manager.ServerManager.OnServerConnectionState += ServerState;
            manager.ClientManager.OnClientConnectionState += ClientState;
            manager.ClientManager.RegisterBroadcast<PopulationMessage>(Population);
            manager.ClientManager.RegisterBroadcast<WorldManifestMessage>(WorldManifest);
            manager.ServerManager.RegisterBroadcast<WorldReadyMessage>(WorldReady);
            var args = Environment.GetCommandLineArgs();
            dedicated = Has(args, "-server"); Automated = Has(args, "-autoclient");
            if (int.TryParse(Value(args, "-maxPlayers"), out var max)) MaxPlayers = Mathf.Clamp(max, 1, 128);
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
        public void Begin(bool host, string endpoint)
        {
            if (starting || connecting || playing || manager.ServerManager.Started) return;
            if (host && !string.IsNullOrWhiteSpace(seedInput) && !int.TryParse(seedInput, out _)) { SetError("Seed должен быть целым числом."); return; }
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
            var world = ProceduralWorld.Instance;
            if (world == null) { starting = false; connecting = false; SetError("В NetworkOcean отсутствует ProceduralWorld."); yield break; }
            if (host)
            {
                WorldLayout layout = null;
                try
                {
                    int seed = int.TryParse(seedInput, out var requestedSeed) ? requestedSeed : world.Profile.RandomSeed ? BitConverter.ToInt32(Guid.NewGuid().ToByteArray(), 0) : world.Profile.Seed;
                    layout = WorldGenerator.Generate(world.Profile, seed, MaxPlayers, OceanSurface.Instance != null ? OceanSurface.Instance.SeaLevel : 0);
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
            starting = false;
            if (host) { if (!manager.ServerManager.StartConnection()) SetError("Не удалось запустить сервер: проверьте UDP-порт"); }
            else if (!manager.ClientManager.StartConnection()) SetError("Не удалось начать подключение");
        }
        void ServerState(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started)
            {
                Debug.Log($"SESSION_READY id={SessionId} port={transport.GetPort()} capacity={MaxPlayers}");
                IslandLootSpawner.Spawn(ProceduralWorld.Instance, manager, Config.Loot);
                if (dedicated) { connecting = false; status = "Сервер запущен"; }
                else if (hostRequested) manager.ClientManager.StartConnection();
            }
            else if (args.ConnectionState == LocalConnectionState.Stopped)
            {
                if (connecting && error == "") SetError("Сервер не запущен: порт занят или недоступен");
                players.Clear(); slots.Clear(); awaitingWorld.Clear(); population = 0;
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
            SpawnPlayer(conn);
        }
        void SpawnPlayer(NetworkConnection conn)
        {
            var spawns = ProceduralWorld.Instance.Points("ship_spawn").ToArray();
            int slot = Array.FindIndex(spawns, candidate => !slots.ContainsValue(Array.IndexOf(spawns, candidate)) && ProceduralWorld.Instance.CanSail(candidate.Position, candidate.Yaw) && !players.Values.Any(p => p != null && p.Ship != null && Vector3.Distance(p.Ship.transform.position, candidate.Position) < 40));
            if (slot < 0) { conn.Disconnect(true); return; }
            var spawn = spawns[slot];
            Vector3 position = spawn.Position;
            float yaw = spawn.Yaw;
            if (Config.ClusteredTestSpawns && !FindNearbySpawn(ref position, ref yaw)) { conn.Disconnect(true); return; }
            slots[conn.ClientId] = slot;
            var ship = Instantiate(ShipPrefab, position, Quaternion.Euler(0, yaw, 0)).GetComponent<NetworkShip>();
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
        bool FindNearbySpawn(ref Vector3 position, ref float yaw)
        {
            var anchor = players.Values.FirstOrDefault(p => p != null && p.Ship != null);
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
            if (worldLoading != null) { StopCoroutine(worldLoading); worldLoading = null; }
            manager.ClientManager.StopConnection(); if (manager.ServerManager.Started) manager.ServerManager.StopConnection(true);
            AdvancedPlayerController.SetCursor(false); status = "Отключено";
            if (MenuCamera != null && !Automated && !dedicated) MenuCamera.gameObject.SetActive(true);
        }
        void Update()
        {
            if (manager != null && manager.ServerManager.Started)
                foreach (var id in awaitingWorld.Where(p => Time.realtimeSinceStartup - p.Value > 120).Select(p => p.Key).ToArray())
                { awaitingWorld.Remove(id); if (manager.ServerManager.Clients.TryGetValue(id, out var conn)) conn.Disconnect(true); }
            if (quitAt > 0 && Time.realtimeSinceStartup >= quitAt) { Disconnect(); Application.Quit(); }
            if (connecting && Time.realtimeSinceStartup - startedAt >= Config.ConnectTimeout) { var reason = error == "" ? "Тайм-аут подключения (15 с): проверьте IP, UDP-порт и Firewall" : error; Disconnect(); SetError(reason); }
        }
        void OnDestroy()
        {
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
            var ships = new List<NetworkShip>();
            foreach (var ship in FindObjectsByType<NetworkShip>(FindObjectsSortMode.None)) if (ship.IsSpawned) ships.Add(ship);
            for (int i = 0; i < ships.Count; i++)
            for (int j = i + 1; j < ships.Count; j++)
            {
                var a = ships[i]; var b = ships[j];
                Vector3 correction = Vector3.zero;
                foreach (var ca in a.GetComponentsInChildren<Collider>())
                foreach (var cb in b.GetComponentsInChildren<Collider>())
                {
                    if (!ca.enabled || !cb.enabled || ca.isTrigger || cb.isTrigger || ca.attachedRigidbody != a.Body || cb.attachedRigidbody != b.Body) continue;
                    if (!ca.bounds.Intersects(cb.bounds)) continue;
                    if (!Physics.ComputePenetration(ca,ca.transform.position,ca.transform.rotation,cb,cb.transform.position,cb.transform.rotation,out var normal,out var depth)) continue;
                    normal.y = 0;
                    float horizontal = normal.magnitude;
                    if (horizontal < .1f) continue;
                    Vector3 candidate = normal / horizontal * (depth / horizontal + .002f);
                    if (candidate.sqrMagnitude > correction.sqrMagnitude) correction = candidate;
                }
                if (correction.sqrMagnitude > 0)
                { a.Motor.ResolveCollision(correction * .5f); b.Motor.ResolveCollision(-correction * .5f); }
            }
        }
        void OnGUI() => DrawSessionMenu();
    }
}

