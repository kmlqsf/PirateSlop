using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using PirateSlop.Networking;

namespace PirateSlop
{
    [DefaultExecutionOrder(-100)]
    public sealed class DeveloperMenu : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }
        public static bool Available => Application.isEditor || Debug.isDebugBuild;
        public static bool AllowRemote;
        public static bool ShowLootEventLabels { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetLootLabels() => ShowLootEventLabels = false;
        NetworkWeapon network;
        string message = "";
        Vector2 scroll;
        int quantity;
        int tab, item, upgrade;
        int openList = -1;
        string search = "";
        Vector2 listScroll;
        UpgradeCard[] upgrades = Array.Empty<UpgradeCard>();
        string[] upgradeNames = Array.Empty<string>();
        bool catalogLoaded;
        GUIStyle descriptionStyle;
        static readonly string[] tabs = { "Игрок", "Лут", "Корабль", "Мир" };
        static readonly string[] quantityNames = { "1", "5", "20" };
        int lootEvent;
        static readonly string[] lootEventNames = { "Зона захвата", "Плот с закрытым сундуком", "Подводный тайник", "Сундук со стаей акул", "Огненный череп" };
        float zoneSpeedMultiplier = 1f;
        float shipSpeedPercent = 100f, nextShipSpeedSend;
        bool shipSpeedPending;
        NetworkShip SpeedShip
        {
            get
            {
                var player = GetComponent<NetworkPlayer>();
                var deck = player != null && player.Passenger != null ? player.Passenger.Ship : null;
                return deck != null ? deck.GetComponent<NetworkShip>() : player != null ? player.Ship : null;
            }
        }
        float fogMultiplier => SeaMistRendererFeature.DensityMultiplier;
        static readonly int[] quantities = { 1, 5, 20 };
        static readonly InventoryItem[] items = { InventoryItem.Cannon, InventoryItem.Pistol, InventoryItem.Sabre, InventoryItem.Rod, InventoryItem.Fish, InventoryItem.Swordfish, InventoryItem.Pufferfish, InventoryItem.Cannonball, InventoryItem.FireCannonball, InventoryItem.IceCannonball, InventoryItem.PushCannonball, InventoryItem.BoomerangCannonball };
        static readonly string[] names = { "Пушка", "Пистолет", "Сабля", "Удочка", "Рыба", "Рыба-меч", "Рыба-фугу", "Обычное ядро", "Огненное ядро", "Ледяное ядро", "Отталкивающее ядро", "Бумеранг" };
        void Awake() => network = GetComponent<NetworkWeapon>();
        void Update()
        {
            if (!Available || BotDebugPanel.ConsumedInput || network == null || !network.IsOwner) return;
            if (Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame)
            {
                IsOpen = !IsOpen;
                if (IsOpen)
                {
                    if (!shipSpeedPending) shipSpeedPercent = SpeedShip != null ? SpeedShip.DeveloperSpeedMultiplier.Value * 100f : 100f;
                    if (!catalogLoaded)
                    {
                        catalogLoaded = true;
                        var catalog = RoguelikeCatalog.Load();
                        if (catalog != null)
                        {
                            upgrades = catalog.Cards.OrderBy(card => card.id, StringComparer.Ordinal).ToArray();
                            upgradeNames = upgrades.Select(card => card.name + " · " + RarityName(card.Rarity)).ToArray();
                        }
                    }
                }
                AdvancedPlayerController.SetCursor(!IsOpen);
            }
            if (shipSpeedPending && Time.unscaledTime >= nextShipSpeedSend)
            {
                network.DeveloperCommand(20, Mathf.RoundToInt(shipSpeedPercent));
                shipSpeedPending = false;
                nextShipSpeedSend = Time.unscaledTime + .25f;
            }
        }
        void OnDisable() { if (network != null && network.IsOwner) IsOpen = false; }
        void OnGUI()
        {
            if (!Available || !IsOpen || network == null || !network.IsOwner) return;
            descriptionStyle ??= new GUIStyle(GUI.skin.box) { wordWrap = true, alignment = TextAnchor.MiddleLeft };
            GUILayout.BeginArea(new Rect(20, 30, Mathf.Min(480, Screen.width - 40), Mathf.Min(760, Screen.height - 60)), "Инструменты разработчика · F8", GUI.skin.window);
            GUILayout.Space(25);
            if (network.IsServerInitialized) AllowRemote = GUILayout.Toggle(AllowRemote, "Разрешить команды другим игрокам");
            int nextTab = GUILayout.Toolbar(tab, tabs, GUILayout.Height(30));
            if (nextTab != tab) { tab = nextTab; scroll = Vector2.zero; openList = -1; }
            GUILayout.Space(8);
            scroll = GUILayout.BeginScrollView(scroll);
            if (tab == 3)
            {
                var testSky = World.TestSkyDayNight.Active;
                if (testSky != null)
                {
                    GUILayout.Label("Небо: день → закат → лунная ночь");
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("День", GUILayout.Width(45));
                    testSky.TargetBlend = GUILayout.HorizontalSlider(testSky.TargetBlend, 0f, 1f);
                    GUILayout.Label("Ночь", GUILayout.Width(45));
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Солнечный день", GUILayout.Height(29))) testSky.TargetBlend = 0f;
                    if (GUILayout.Button("Лунная ночь", GUILayout.Height(29))) testSky.TargetBlend = 1f;
                    GUILayout.EndHorizontal();
                    GUILayout.Space(8);
                }

                var ocean = OceanSurface.Instance != null ? OceanSurface.Instance.HeightSource as BoatAttackOcean : null;
                if (ocean != null)
                {
                    GUILayout.Label("Океан");
                    bool canAdjustOcean = SessionController.Instance != null && SessionController.Instance.CanAdjustTestOcean;
                    GUI.enabled = canAdjustOcean;
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"Сила волн: {ocean.WaveStrength * 100f:0}%", GUILayout.Width(180));
                    float strength = GUILayout.HorizontalSlider(ocean.WaveStrength, 0f, 2f);
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"Крутизна волн: {ocean.WaveSteepness * 100f:0}%", GUILayout.Width(180));
                    float steepness = GUILayout.HorizontalSlider(ocean.WaveSteepness, 0f, 1f);
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"Скорость волн: {ocean.WaveSpeed:0.00}x", GUILayout.Width(180));
                    float waveSpeed = GUILayout.HorizontalSlider(ocean.WaveSpeed, .25f, 2f);
                    GUILayout.EndHorizontal();
                    if (canAdjustOcean) SessionController.Instance.SetTestOcean(strength, steepness, waveSpeed);
                    if (GUILayout.Button("Вернуть настройки океана", GUILayout.Height(29)))
                        SessionController.Instance.SetTestOcean(1f, 1f, 1f);
                    GUI.enabled = true;
                    if (!canAdjustOcean) GUILayout.Label("Настройки волн меняет хост");
                    var foam = WaterShipFoam.Instance;
                    if (foam != null)
                    {
                        GUILayout.Label("Взаимодействие с кораблём");
                        GUILayout.BeginHorizontal();
                        GUILayout.Label($"Носовая волна: {foam.BowWaveStrength * 100f:0}%", GUILayout.Width(180));
                        foam.BowWaveStrength = GUILayout.HorizontalSlider(foam.BowWaveStrength, 0f, 2f);
                        GUILayout.EndHorizontal();
                        GUILayout.BeginHorizontal();
                        GUILayout.Label($"Высота носовой волны: {foam.BowWaveHeight * 100f:0} см", GUILayout.Width(230));
                        foam.BowWaveHeight = GUILayout.HorizontalSlider(foam.BowWaveHeight, 0f, .22f);
                        GUILayout.EndHorizontal();
                        GUILayout.BeginHorizontal();
                        GUILayout.Label($"Брызги от ударов: {foam.SprayStrength * 100f:0}%", GUILayout.Width(180));
                        foam.SprayStrength = GUILayout.HorizontalSlider(foam.SprayStrength, 0f, 2f);
                        GUILayout.EndHorizontal();
                        if (GUILayout.Button("Вернуть настройки пены и брызг", GUILayout.Height(29))) { foam.BowWaveStrength = foam.SprayStrength = 1f; foam.BowWaveHeight = .22f; }
                    }
                    GUILayout.Space(8);
                }
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Морской туман: {fogMultiplier * 100f:0}%", GUILayout.Width(180));
                float newFog = GUILayout.HorizontalSlider(fogMultiplier, 0f, 3f);
                if (!Mathf.Approximately(newFog, fogMultiplier)) SeaMistRendererFeature.SetDensityMultiplier(newFog);
                GUILayout.EndHorizontal();
                GUILayout.Space(8);
                GUILayout.Label("Штормовая зона");
                Button(SessionController.Instance != null && SessionController.Instance.StormPaused ? "Продолжить зону" : "Остановить зону (сужение и урон)", 14);
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Скорость зоны: {zoneSpeedMultiplier:0.0}x", GUILayout.Width(150));
                zoneSpeedMultiplier = GUILayout.HorizontalSlider(zoneSpeedMultiplier, 0.1f, 100f);
                if (GUILayout.Button("Применить", GUILayout.Width(80))) network.DeveloperCommand(16, Mathf.RoundToInt(600f / zoneSpeedMultiplier));
                GUILayout.EndHorizontal();
            }
            if (tab == 0)
            {
                GUILayout.Label("Здоровье");
                GUILayout.BeginHorizontal();
                Button("Восстановить здоровье", 6);
                Button("Нанести себе 10 урона", 12);
                GUILayout.EndHorizontal();
                GUILayout.Space(8);
                GUILayout.Label("Испытания");
                Button("Создать тренировочную мишень", 1);
                Button("Создать врага-манекена перед собой", 13);
            }
            if (tab == 2)
            {
                Button("Создать корабль рядом", 0);
                GUILayout.Label("Движение корабля");
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Скорость корабля: {shipSpeedPercent:0}%", GUILayout.Width(180));
                float speedPercent = Mathf.Round(GUILayout.HorizontalSlider(shipSpeedPercent, 100f, 500f));
                if (!Mathf.Approximately(speedPercent, shipSpeedPercent)) { shipSpeedPercent = speedPercent; shipSpeedPending = true; }
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Паруса на макс.", GUILayout.Height(29))) network.DeveloperCommand(17);
                if (GUILayout.Button("Паруса на мин.", GUILayout.Height(29))) network.DeveloperCommand(18);
                GUILayout.EndHorizontal();
            }
            if (tab == 1)
            {
                GUILayout.Label("Предметы");
                Dropdown(0, names, ref item);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Количество", GUILayout.Width(100));
                quantity = GUILayout.Toolbar(quantity, quantityNames);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Выдать себе", GUILayout.Height(29))) network.DeveloperCommand((byte)(32 + (int)items[item]), quantities[quantity]);
                if (GUILayout.Button("На землю · 1 шт.", GUILayout.Height(29))) network.DeveloperCommand((byte)(64 + (int)items[item]));
                GUILayout.EndHorizontal();
                GUILayout.Space(10);
                GUILayout.Label("Улучшения");
                if (upgrades.Length > 0)
                {
                    Dropdown(1, upgradeNames, ref upgrade);
                    GUILayout.Label(upgrades[upgrade].description, descriptionStyle);
                    var player = GetComponent<NetworkPlayer>();
                    bool owned = player != null && player.Upgrades.owned.Any(card => card.id == upgrades[upgrade].id);
                    GUI.enabled = !owned;
                    if (GUILayout.Button(owned ? "Уже получено" : "Забрать улучшение", GUILayout.Height(29))) network.DeveloperCommand(22, upgrade);
                    GUI.enabled = true;
                }
                else GUILayout.Label("Каталог улучшений недоступен.");
                GUILayout.Space(10);
                Button("Создать наполненный сундук перед собой", 21);
                Button("Создать сундук на носу корабля", 24);
                GUILayout.Space(10);
                ShowLootEventLabels = GUILayout.Toggle(ShowLootEventLabels, "Показывать надписи всех лутовых ивентов");
                GUILayout.Space(8);
                GUILayout.Label("Лутовый ивент перед кораблём");
                Dropdown(2, lootEventNames, ref lootEvent);
                if (GUILayout.Button("Создать ивент", GUILayout.Height(29)))
                {
                    openList = -1;
                    network.DeveloperCommand(23, lootEvent + 1);
                }
            }
            if (tab == 3)
            {
            
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(KrakenEncounterManager.AutoEncounterEnabled ? "Отключить кракена" : "Включить кракена", GUILayout.Height(29)))
                {
                    if (network != null) network.DeveloperCommand(19);
                    else
                    {
                        KrakenEncounterManager.AutoEncounterEnabled = !KrakenEncounterManager.AutoEncounterEnabled;
                        if (!KrakenEncounterManager.AutoEncounterEnabled)
                        {
                            var ship = FindFirstObjectByType<ShipController>();
                            var mgr = ship != null ? ship.GetComponent<KrakenEncounterManager>() : null;
                            if (mgr != null && mgr.ActiveEncounter != null) mgr.ActiveEncounter.TriggerSinkLocal();
                        }
                    }
                }
                if (GUILayout.Button("Вызвать кракена", GUILayout.Height(29)))
                {
                    if (network != null) network.DeveloperCommand(15);
                    else
                    {
                        var ship = FindFirstObjectByType<ShipController>();
                        ship?.GetComponent<KrakenEncounterManager>()?.TriggerEncounter();
                    }
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.Space(8);
            Button("Удалить созданные тестовые объекты", 9);
            GUILayout.Label(message);
            if (GUILayout.Button("Закрыть")) { IsOpen = false; AdvancedPlayerController.SetCursor(true); }
            GUILayout.EndArea();
        }
        void Dropdown(int id, string[] options, ref int selected)
        {
            if (GUILayout.Button(options[selected] + (openList == id ? " ▲" : " ▼"), GUILayout.Height(29)))
            {
                openList = openList == id ? -1 : id;
                search = "";
                listScroll = Vector2.zero;
            }
            if (openList != id) return;
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Поиск", GUILayout.Width(50));
            string nextSearch = GUILayout.TextField(search);
            if (nextSearch != search) { search = nextSearch; listScroll = Vector2.zero; }
            if (GUILayout.Button("×", GUILayout.Width(28))) { search = ""; listScroll = Vector2.zero; }
            GUILayout.EndHorizontal();
            listScroll = GUILayout.BeginScrollView(listScroll, GUILayout.Height(170));
            bool found = false;
            for (int i = 0; i < options.Length; i++)
            {
                if (options[i].IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                found = true;
                if (GUILayout.Button(options[i], GUILayout.Height(27))) { selected = i; openList = -1; }
            }
            if (!found) GUILayout.Label("Ничего не найдено");
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }
        static string RarityName(UpgradeRarity rarity) => rarity switch
        {
            UpgradeRarity.Common => "Обычное",
            UpgradeRarity.Rare => "Редкое",
            UpgradeRarity.Epic => "Эпическое",
            _ => "Легендарное"
        };
        void Button(string label, byte command)
        {
            if (GUILayout.Button(label, GUILayout.Height(29))) network.DeveloperCommand(command);
        }
        public void Report(string value) => message = value;
    }
}
