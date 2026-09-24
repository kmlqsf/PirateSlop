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
        NetworkWeapon network;
        string message = "";
        Vector2 scroll;
        int quantity;
        float zoneSpeedMultiplier = 1f;
        float fogMultiplier => SeaMistRendererFeature.DensityMultiplier;
        float originalFogDensity = -1f;
        bool originalFogEnabled;
        static readonly int[] quantities = { 1, 5, 20 };
        static readonly InventoryItem[] items = { InventoryItem.Cannon, InventoryItem.Pistol, InventoryItem.Sabre, InventoryItem.Rod, InventoryItem.Fish, InventoryItem.Swordfish, InventoryItem.Pufferfish, InventoryItem.Cannonball, InventoryItem.FireCannonball, InventoryItem.IceCannonball, InventoryItem.PushCannonball, InventoryItem.BoomerangCannonball };
        static readonly string[] names = { "Пушка", "Пистолет", "Сабля", "Удочка", "Рыба", "Рыба-меч", "Рыба-фугу", "Обычное ядро", "Огненное ядро", "Ледяное ядро", "Отталкивающее ядро", "Бумеранг" };
        void Awake() => network = GetComponent<NetworkWeapon>();
        void Update()
        {
            if (!Available || BotDebugPanel.ConsumedInput || network == null || !network.IsOwner) return;
            if (Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame)
            { IsOpen = !IsOpen; AdvancedPlayerController.SetCursor(!IsOpen); }
        }
        void OnDisable() { if (network != null && network.IsOwner) IsOpen = false; }
        void OnGUI()
        {
            if (!Available || !IsOpen || network == null || !network.IsOwner) return;
            GUILayout.BeginArea(new Rect(20, 30, Mathf.Min(480, Screen.width - 40), Mathf.Min(760, Screen.height - 60)), "Инструменты разработчика · F8", GUI.skin.window);
            GUILayout.Space(25);
            scroll = GUILayout.BeginScrollView(scroll);
            if (network.IsServerInitialized) AllowRemote = GUILayout.Toggle(AllowRemote, "Разрешить команды другим игрокам");
            GUILayout.Label("Персонаж и испытания");
            Button(SessionController.Instance != null && SessionController.Instance.StormPaused ? "Продолжить зону" : "Остановить зону (сужение и урон)", 14);
            Button("Восстановить здоровье", 6);
            Button("Нанести себе 10 урона", 12);
            Button("Создать корабль рядом", 0);
            Button("Создать тренировочную мишень", 1);
            Button("Создать врага-манекена перед собой", 13);
            GUILayout.Label("Управление (Зона и Корабль)");
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
            GUILayout.Label($"Морской туман: {fogMultiplier:0.0}x", GUILayout.Width(180));
            float newFog = GUILayout.HorizontalSlider(fogMultiplier, 0f, 3f);
            if (!Mathf.Approximately(newFog, fogMultiplier))
            {
                if (originalFogDensity < 0f)
                {
                    originalFogDensity = RenderSettings.fogDensity;
                    originalFogEnabled = RenderSettings.fog;
                }
                SeaMistRendererFeature.DensityMultiplier = newFog;
                RenderSettings.fogDensity = originalFogDensity * fogMultiplier;
                RenderSettings.fog = originalFogEnabled && fogMultiplier > 0.01f;
            }
            GUILayout.EndHorizontal();
            
            if (GUILayout.Button("Trigger Kraken", GUILayout.Height(29)))
            {
                if (network != null) network.DeveloperCommand(15);
                else
                {
                    var ship = FindFirstObjectByType<ShipController>();
                    ship?.GetComponent<KrakenEncounterManager>()?.TriggerEncounter();
                }
            }
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
