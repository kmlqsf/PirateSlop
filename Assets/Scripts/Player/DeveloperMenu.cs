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
        int lootEvent;
        bool lootEventListOpen;
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
                if (IsOpen && !shipSpeedPending) shipSpeedPercent = SpeedShip != null ? SpeedShip.DeveloperSpeedMultiplier.Value * 100f : 100f;
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
            GUILayout.BeginArea(new Rect(20, 30, Mathf.Min(480, Screen.width - 40), Mathf.Min(760, Screen.height - 60)), "Инструменты разработчика · F8", GUI.skin.window);
            GUILayout.Space(25);
            scroll = GUILayout.BeginScrollView(scroll);
            if (network.IsServerInitialized) AllowRemote = GUILayout.Toggle(AllowRemote, "Разрешить команды другим игрокам");
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
            GUILayout.Label("Персонаж и испытания");
            Button(SessionController.Instance != null && SessionController.Instance.StormPaused ? "Продолжить зону" : "Остановить зону (сужение и урон)", 14);
            Button("Восстановить здоровье", 6);
            Button("Нанести себе 10 урона", 12);
            Button("Создать корабль рядом", 0);
            Button("Создать сундук с лутом на носу корабля", 21);
            Button("Создать тренировочную мишень", 1);
            Button("Создать врага-манекена перед собой", 13);
            GUILayout.Label("Управление (Зона и Корабль)");
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Скорость корабля: {shipSpeedPercent:0}%", GUILayout.Width(180));
            float speedPercent = Mathf.Round(GUILayout.HorizontalSlider(shipSpeedPercent, 100f, 500f));
            if (!Mathf.Approximately(speedPercent, shipSpeedPercent)) { shipSpeedPercent = speedPercent; shipSpeedPending = true; }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Скорость зоны: {zoneSpeedMultiplier:0.0}x", GUILayout.Width(150));
            zoneSpeedMultiplier = GUILayout.HorizontalSlider(zoneSpeedMultiplier, 0.1f, 100f);
            if (GUILayout.Button("Применить", GUILayout.Width(80))) network.DeveloperCommand(16, Mathf.RoundToInt(600f / zoneSpeedMultiplier));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Паруса на макс.", GUILayout.Height(29))) network.DeveloperCommand(17);
            if (GUILayout.Button("Паруса на мин.", GUILayout.Height(29))) network.DeveloperCommand(18);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Морской туман: {fogMultiplier * 100f:0}%", GUILayout.Width(180));
            float newFog = GUILayout.HorizontalSlider(fogMultiplier, 0f, 3f);
            if (!Mathf.Approximately(newFog, fogMultiplier))
                SeaMistRendererFeature.SetDensityMultiplier(newFog);
            GUILayout.EndHorizontal();
            ShowLootEventLabels = GUILayout.Toggle(ShowLootEventLabels, "Показывать надписи всех лутовых ивентов");
            GUILayout.Space(8);
            GUILayout.Label("Лутовый ивент перед кораблём");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(lootEventNames[lootEvent] + " ▼", GUILayout.Height(29))) lootEventListOpen = !lootEventListOpen;
            if (GUILayout.Button("Спаун", GUILayout.Width(80), GUILayout.Height(29)))
            {
                lootEventListOpen = false;
                network.DeveloperCommand(19, lootEvent + 1);
            }
            GUILayout.EndHorizontal();
            if (lootEventListOpen)
            {
                for (int i = 0; i < lootEventNames.Length; i++)
                    if (GUILayout.Button(lootEventNames[i], GUILayout.Height(27))) { lootEvent = i; lootEventListOpen = false; }
            }
            
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
            if (GUILayout.Button("Trigger Kraken", GUILayout.Height(29)))
            {
                if (network != null) network.DeveloperCommand(15);
                else
                {
                    var ship = FindFirstObjectByType<ShipController>();
                    ship?.GetComponent<KrakenEncounterManager>()?.TriggerEncounter();
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(8);
            GUILayout.Label("Количество при выдаче в инвентарь");
            quantity = GUILayout.Toolbar(quantity, new[] { "1", "5", "20" });
            for (int i = 0; i < items.Length; i++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(names[i], GUILayout.Width(175));
                if (GUILayout.Button("Выдать", GUILayout.Height(27))) network.DeveloperCommand((byte)(32 + (int)items[i]), quantities[quantity]);
                if (GUILayout.Button("На землю", GUILayout.Height(27))) network.DeveloperCommand((byte)(64 + (int)items[i]));
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(8);
            Button("Удалить созданные тестовые объекты", 9);
            GUILayout.Label(message);
            if (GUILayout.Button("Закрыть")) { IsOpen = false; AdvancedPlayerController.SetCursor(true); }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
        void Button(string label, byte command)
        {
            if (GUILayout.Button(label, GUILayout.Height(29))) network.DeveloperCommand(command);
        }
        public void Report(string value) => message = value;
    }
}
