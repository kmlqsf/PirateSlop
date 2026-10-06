using System.Collections.Generic;
using FishNet.Object;
using FishNet.Connection;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkWeapon
    {
        public NetworkObject DeveloperTargetPrefab;
        readonly List<NetworkObject> developerObjects = new();
        float nextDeveloperCommand;
        float nextDeveloperSpeedCommand;
        public void DeveloperCommand(byte command, int count = 1) => DeveloperCommandServerRpc(command, count);
        [ServerRpc]
        void DeveloperCommandServerRpc(byte command, int count)
        {
            if (!DeveloperMenu.Available || (!IsOwner && !DeveloperMenu.AllowRemote)) { DeveloperResultTargetRpc(Owner, "Нужно разрешение хоста."); return; }
            if (command == 20)
            {
                if (Time.unscaledTime < nextDeveloperSpeedCommand) return;
                nextDeveloperSpeedCommand = Time.unscaledTime + .15f;
                var player = GetComponent<NetworkPlayer>();
                var deck = player != null && player.Passenger != null ? player.Passenger.Ship : null;
                var ship = deck != null ? deck.GetComponent<NetworkShip>() : player != null ? player.Ship : null;
                if (ship == null || !ship.IsSpawned || ship.IsSinking) { DeveloperResultTargetRpc(Owner, "Корабль не найден."); return; }
                ship.DeveloperSpeedMultiplier.Value = Mathf.Clamp(count, 100, 500) / 100f;
                return;
            }
            if (Time.time < nextDeveloperCommand) return;
            nextDeveloperCommand = Time.time + .3f;
            var session = SessionController.Instance;
            if (command == 22)
            {
                DeveloperResultTargetRpc(Owner, session != null ? session.GrantDeveloperUpgrade(GetComponent<NetworkPlayer>(), count) : "Сессия не готова.");
                return;
            }
            if (command == 14)
            {
                bool changed = session != null && session.ToggleStormPause();
                DeveloperResultTargetRpc(Owner, !changed ? "Зона ещё не запущена." : session.StormPaused ? "Зона остановлена. Сужение и урон на паузе." : "Зона продолжает сужаться. Урон включён.");
                return;
            }
            if (command == 16)
            {
                if (session != null) session.SetStormDuration(count);
                DeveloperResultTargetRpc(Owner, "Скорость зоны применена.");
                return;
            }
            if (command == 17 || command == 18)
            {
                var player = GetComponent<NetworkPlayer>();
                var ship = player != null ? player.Ship : null;
                if (ship == null)
                {
                    float minDist = float.MaxValue;
                    foreach (var s in NetworkShip.ActiveShips)
                    {
                        if (s == null) continue;
                        float d = Vector3.Distance(transform.position, s.transform.position);
                        if (d < minDist) { minDist = d; ship = s; }
                    }
                }
                if (ship != null)
                {
                    var sailSystem = ship.GetComponent<SailSystem>();
                    if (sailSystem != null)
                    {
                        float val = command == 17 ? 1f : 0f;
                        var tensions = sailSystem.CaptureTensions();
                        var owners = sailSystem.CaptureOwners();
                        for (int i = 0; i < tensions.Length; i++) { tensions[i] = val; owners[i] = 0; }
                        sailSystem.ApplyRopes(tensions, owners);
                        DeveloperResultTargetRpc(Owner, command == 17 ? "Паруса опущены (макс скорость)." : "Паруса подняты (мин скорость).");
                    }
                }
                else DeveloperResultTargetRpc(Owner, "Корабль не найден.");
                return;
            }
            if (command == 15)
            {
                var player = GetComponent<NetworkPlayer>();
                var ship = player != null ? player.Ship : null;
                if (ship == null)
                {
                    float minDist = float.MaxValue;
                    foreach (var s in NetworkShip.ActiveShips)
                    {
                        if (s == null) continue;
                        float d = Vector3.Distance(transform.position, s.transform.position);
                        if (d < minDist) { minDist = d; ship = s; }
                    }
                }
                if (ship != null)
                {
                    var manager = ship.GetComponent<KrakenEncounterManager>();
                    if (manager == null) manager = ship.gameObject.AddComponent<KrakenEncounterManager>();
                    manager.TriggerEncounter();
                    DeveloperResultTargetRpc(Owner, "Кракен вызван.");
                }
                else
                {
                    DeveloperResultTargetRpc(Owner, "Активный корабль не найден.");
                }
                return;
            }
            if (command == 19)
            {
                bool newState = !KrakenEncounterManager.AutoEncounterEnabled;
                KrakenEncounterManager.AutoEncounterEnabled = newState;
                SyncKrakenStateObserversRpc(newState);
                if (!newState)
                {
                    foreach (var s in NetworkShip.ActiveShips)
                    {
                        if (s != null && s.KrakenManager != null && s.KrakenManager.ActiveEncounter != null)
                            s.KrakenSink();
                    }
                }
                DeveloperResultTargetRpc(Owner, newState ? "Появление кракена включено." : "Появление кракена отключено.");
                return;
            }
            var health = GetComponent<CombatHealth>();
            if (command == 6) { health.Heal(health.MaxHealth); DeveloperResultTargetRpc(Owner, "Здоровье восстановлено."); return; }
            if (command == 12) { health.Damage(10f); DeveloperResultTargetRpc(Owner, "Нанесено 10 урона."); return; }
            if (command >= 32 && command < 64)
            {
                var item = (InventoryItem)(command - 32);
                int added = 0, requested = Mathf.Clamp(count, 1, 20);
                while (added < requested && AddItem(item)) added++;
                DeveloperResultTargetRpc(Owner, "Добавлено: " + added + " / " + requested + (added < requested ? " · Инвентарь заполнен или предмет недоступен." : "")); return;
            }
            if (command == 8) { DeveloperResultTargetRpc(Owner, AddItem(InventoryItem.Cannon) ? "Пушка добавлена." : "Инвентарь заполнен."); return; }
            if (command == 9)
            {
                foreach (var item in developerObjects) if (item != null && item.IsSpawned) ServerManager.Despawn(item);
                developerObjects.Clear(); DeveloperResultTargetRpc(Owner, "Тестовые объекты удалены."); return;
            }
            developerObjects.RemoveAll(o => o == null || !o.IsSpawned);
            if (developerObjects.Count >= 20) { DeveloperResultTargetRpc(Owner, "Удалите тестовые объекты: достигнут лимит 20."); return; }
            if (command == 21)
            {
                SpawnDeveloperChest();
                return;
            }
            if (command == 24)
            {
                SpawnDeveloperBowChest();
                return;
            }
            if (command == 23)
            {
                SpawnDeveloperLootEvent((SeaLootKind)count);
                return;
            }
            if (command == 0)
            {
                if (session == null) return;
                var spawned = session.SpawnDeveloperShip(transform.position, transform.eulerAngles.y);
                if (spawned != null) developerObjects.Add(spawned);
                DeveloperResultTargetRpc(Owner, spawned != null ? "Корабль создан рядом." : "Рядом нет свободного места на воде."); return;
            }
            var itemType = command >= 64 ? (InventoryItem)(command - 64) : command == 2 ? InventoryItem.Cannon : command == 3 ? InventoryItem.Pistol : command == 4 ? InventoryItem.Sabre : InventoryItem.Cannonball;
            if (command > 5 && command < 64 && command != 13) return;
            if (itemType < InventoryItem.Fish || (itemType > InventoryItem.BoomerangCannonball && itemType != InventoryItem.Pufferfish && itemType != InventoryItem.Swordfish) || itemType == InventoryItem.Mallet || itemType == InventoryItem.Plank) return;
            int prefabIndex = CannonAmmo.IsBall(itemType) ? (int)InventoryItem.Cannonball : (int)itemType;
            if (command != 1 && command != 13 && (DropPrefabs == null || prefabIndex >= DropPrefabs.Length || DropPrefabs[prefabIndex] == null))
            { DeveloperResultTargetRpc(Owner, "Префаб предмета не назначен."); return; }
            var prefab = command == 13 ? session?.PlayerPrefab : command == 1 ? DeveloperTargetPrefab : DropPrefabs[prefabIndex].NetworkObject;
            if (prefab == null) { DeveloperResultTargetRpc(Owner, "Префаб не назначен."); return; }
            Vector3 origin = transform.position + transform.forward * 3 + Vector3.up * 3;
            RaycastHit floor = default; float distance = 10;
            foreach (var hit in Physics.RaycastAll(origin, Vector3.down, distance, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) && hit.normal.y > .5f && hit.distance < distance) { floor = hit; distance = hit.distance; }
            if (floor.collider == null) { DeveloperResultTargetRpc(Owner, "Направьте взгляд на свободную палубу или землю."); return; }
            var bounds = prefab.GetComponent<Collider>().bounds;
            var box = prefab.GetComponent<BoxCollider>();
            float height = command == 13 ? 0f : command == 1 ? 1f : box != null ? box.size.y * .5f - box.center.y : .18f;
            Vector3 position = floor.point + Vector3.up * (height + .03f);
            if (command == 13 && Physics.CheckCapsule(position + Vector3.up * .35f, position + Vector3.up * 1.5f, .3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            { DeveloperResultTargetRpc(Owner, "Перед вами недостаточно места для манекена."); return; }
            var obj = Instantiate(prefab, position, Quaternion.identity);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(obj.gameObject, gameObject.scene);
            var support = floor.collider.GetComponentInParent<NetworkShip>();
            if (command == 13)
            {
                obj.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(transform.position - position, Vector3.up));
                var dummy = obj.GetComponent<NetworkPlayer>();
                dummy.IsTrainingDummy = true;
                dummy.TeamId.Value = int.MaxValue;
                dummy.Passenger.Attach(support != null ? support.Body : null);
                ServerManager.Spawn(obj);
                dummy.ParticipantId.Value = -(obj.ObjectId + 1);
                developerObjects.Add(obj);
                DeveloperResultTargetRpc(Owner, "Враг-манекен создан."); return;
            }
            var drop = obj.GetComponent<NetworkFish>();
            if (drop != null && CannonAmmo.IsBall(itemType)) drop.SetAmmoItem(itemType);
            if (drop != null) drop.Place(support != null ? support.NetworkObject : null, position, Quaternion.identity);
            var target = obj.GetComponent<DeveloperTarget>();
            if (target != null) target.Place(support, position);
            ServerManager.Spawn(obj); developerObjects.Add(obj);
            DeveloperResultTargetRpc(Owner, "Объект создан.");
        }
        void SpawnDeveloperBowChest()
        {
            var player = GetComponent<NetworkPlayer>();
            var deck = player != null && player.Passenger != null ? player.Passenger.Ship : null;
            var ship = deck != null ? deck.GetComponent<NetworkShip>() : player != null ? player.Ship : null;
            var session = SessionController.Instance;
            var catalog = session != null && session.Config != null ? session.Config.Loot : null;
            if (ship == null || !ship.IsSpawned || ship.IsSinking)
            { DeveloperResultTargetRpc(Owner, "Корабль игрока не найден."); return; }
            if (catalog == null || catalog.ChestPrefab == null)
            { DeveloperResultTargetRpc(Owner, "Каталог сундуков ещё не готов."); return; }
            if (!NetworkLootChest.TryFindBowDeckPoint(ship, catalog.ChestPrefab, out var point))
            { DeveloperResultTargetRpc(Owner, "На носовой палубе нет свободного места для сундука."); return; }
            var chest = Instantiate(catalog.ChestPrefab, point, ship.transform.rotation);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(chest.gameObject, ship.gameObject.scene);
            chest.Catalog = catalog;
            try
            {
                var random = new PirateSlop.World.MapRandom(unchecked((uint)Random.Range(1, int.MaxValue)));
                chest.Fill(ref random);
                chest.PlaceOnDeck(ship, point, ship.transform.rotation);
                ServerManager.Spawn(chest.NetworkObject);
                developerObjects.Add(chest.NetworkObject);
                DeveloperResultTargetRpc(Owner, "Сундук с обычным лутом создан на носу корабля.");
            }
            catch (System.InvalidOperationException error)
            {
                Destroy(chest.gameObject);
                DeveloperResultTargetRpc(Owner, "Не удалось наполнить сундук: " + error.Message);
            }
        }

        void SpawnDeveloperChest()
        {
            var session = SessionController.Instance;
            var catalog = session != null && session.Config != null ? session.Config.Loot : null;
            if (catalog == null || catalog.ChestPrefab == null)
            { DeveloperResultTargetRpc(Owner, "Префаб сундука не назначен."); return; }
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            var origin = transform.position + forward * 3f + Vector3.up * 3f;
            RaycastHit floor = default;
            float distance = 8f;
            foreach (var hit in Physics.RaycastAll(origin, Vector3.down, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform) || hit.normal.y < .7f || hit.point.y > transform.position.y + .5f || hit.distance >= distance) continue;
                floor = hit;
                distance = hit.distance;
            }
            if (floor.collider == null)
            { DeveloperResultTargetRpc(Owner, "Перед вами нет палубы или земли для сундука."); return; }
            var ship = floor.collider.GetComponentInParent<NetworkShip>();
            var rotation = ship != null ? ship.transform.rotation : Quaternion.LookRotation(forward, Vector3.up);
            var up = rotation * Vector3.up;
            var prefab = catalog.ChestPrefab;
            var box = prefab.GetComponent<BoxCollider>();
            var halfSize = box != null ? Vector3.Scale(box.size, prefab.transform.localScale) * .5f : new Vector3(.6f, .4f, .4f);
            var center = box != null ? Vector3.Scale(box.center, prefab.transform.localScale) : Vector3.up * .4f;
            var position = floor.point + up * (halfSize.y - center.y + .03f);
            foreach (var obstacle in Physics.OverlapBox(position + rotation * center, halfSize * .95f, rotation, ~0, QueryTriggerInteraction.Ignore))
            {
                if (obstacle == floor.collider) continue;
                DeveloperResultTargetRpc(Owner, "Перед вами недостаточно места для сундука.");
                return;
            }
            var chest = Instantiate(prefab, position, rotation);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(chest.gameObject, gameObject.scene);
            chest.Catalog = catalog;
            try
            {
                var random = new PirateSlop.World.MapRandom(unchecked((uint)Random.Range(1, int.MaxValue)));
                chest.Fill(ref random);
                if (ship != null) chest.PlaceOnDeck(ship, position);
                ServerManager.Spawn(chest.NetworkObject);
                developerObjects.Add(chest.NetworkObject);
                DeveloperResultTargetRpc(Owner, "Наполненный сундук создан перед вами.");
            }
            catch (System.InvalidOperationException error)
            {
                Destroy(chest.gameObject);
                DeveloperResultTargetRpc(Owner, "Не удалось наполнить сундук: " + error.Message);
            }
        }
        void SpawnDeveloperLootEvent(SeaLootKind kind)
        {
            if (kind < SeaLootKind.Capture || kind > SeaLootKind.Skull) return;
            var session = SessionController.Instance;
            var world = PirateSlop.World.ProceduralWorld.Instance;
            var catalog = session != null && session.Config != null ? session.Config.Loot : null;
            var player = GetComponent<NetworkPlayer>();
            var deck = player != null && player.Passenger != null ? player.Passenger.Ship : null;
            var ship = deck != null ? deck.GetComponent<NetworkShip>() : player != null ? player.Ship : null;
            if (ship == null || !ship.IsSpawned || ship.IsSinking)
            { DeveloperResultTargetRpc(Owner, "Корабль игрока не найден."); return; }
            if (world == null || !world.Ready || catalog == null || catalog.ChestPrefab == null)
            { DeveloperResultTargetRpc(Owner, "Мир или каталог лута ещё не готовы."); return; }
            var forward = Vector3.ProjectOnPlane(ship.transform.forward, Vector3.up).normalized;
            float clearance = kind == SeaLootKind.Capture ? catalog.CaptureRadius : kind == SeaLootKind.Skull && catalog.SkullEventPrefab != null ? catalog.SkullEventPrefab.PlatformRadius : 8f;
            float distance = ship.Motor.HullFootprint.y * .5f + clearance + 15f;
            float depth = kind == SeaLootKind.Sunken ? catalog.SunkenDepth + 2f : 3f;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                var point = ship.transform.position + forward * (distance + attempt * 20f);
                point.y = world.Layout.SeaLevel;
                if (kind == SeaLootKind.Raft && new Vector2(point.x, point.z).magnitude > session.SafeRadius() - 150f) continue;
                if (!world.CanSail(point, ship.transform.eulerAngles.y) || world.GroundHeight(point) > point.y - depth) continue;
                bool clear = true;
                foreach (var chest in NetworkLootChest.ServerChests)
                    if (chest != null && chest.IsSpawned && chest.Carrier == null && chest.Kind != SeaLootKind.None &&
                        Vector3.Distance(point, chest.EventPoint) < clearance + (chest.Kind == SeaLootKind.Capture ? catalog.CaptureRadius : 8f))
                    { clear = false; break; }
                if (kind == SeaLootKind.Capture)
                    for (int side = 0; clear && side < 8; side++)
                    {
                        float bearing = side * Mathf.PI / 4f;
                        var sample = point + new Vector3(Mathf.Cos(bearing), 0, Mathf.Sin(bearing)) * clearance;
                        clear = world.CanSail(sample, side * 45f);
                    }
                if (!clear) continue;
                foreach (var altar in NetworkSkullEvent.ServerEvents)
                    if (altar != null && Vector3.Distance(point, altar.transform.position) < clearance + altar.PlatformRadius) { clear = false; break; }
                if (!clear) continue;
                if (kind == SeaLootKind.Skull)
                {
                    var altar = NetworkSkullEvent.SpawnEvent(catalog, NetworkManager, point, world.gameObject.scene);
                    if (altar == null) { DeveloperResultTargetRpc(Owner, "Префаб черепа не подключён к каталогу лута."); return; }
                    developerObjects.Add(altar.NetworkObject);
                    DeveloperResultTargetRpc(Owner, "Огненный череп создан перед кораблём.");
                    return;
                }
                var spawned = Instantiate(catalog.ChestPrefab, point, Quaternion.identity);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(spawned.gameObject, world.gameObject.scene);
                spawned.Catalog = catalog;
                try
                {
                    var random = new PirateSlop.World.MapRandom(unchecked((uint)Random.Range(1, int.MaxValue)));
                    spawned.Fill(ref random);
                    spawned.ConfigureOcean(kind, point);
                    ServerManager.Spawn(spawned.NetworkObject);
                    developerObjects.Add(spawned.NetworkObject);
                    DeveloperResultTargetRpc(Owner, "Лутовый ивент создан перед кораблём.");
                }
                catch (System.InvalidOperationException error)
                {
                    Destroy(spawned.gameObject);
                    DeveloperResultTargetRpc(Owner, "Не удалось наполнить сундук: " + error.Message);
                }
                return;
            }
            DeveloperResultTargetRpc(Owner, "Перед кораблём нет свободной воды для этого ивента. Переместите корабль.");
        }
        [TargetRpc] void DeveloperResultTargetRpc(NetworkConnection connection, string message) => GetComponent<DeveloperMenu>().Report(message);
        [ObserversRpc(BufferLast = true, RunLocally = true)] void SyncKrakenStateObserversRpc(bool enabled) => KrakenEncounterManager.AutoEncounterEnabled = enabled;
    }

}
