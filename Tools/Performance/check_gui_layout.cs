var root = UnityEditor.PrefabUtility.LoadPrefabContents("Assets/Prefabs/Networking/NetworkPlayer.prefab");
var setting = typeof(PirateSlop.PlayerGuiBudget).GetProperty("Enabled");
bool previous = PirateSlop.PlayerGuiBudget.Enabled;
try
{
    if (root.GetComponent<PirateSlop.ContextPrompt>() == null) root.AddComponent<PirateSlop.ContextPrompt>();
    if (root.GetComponent<PirateSlop.SwimPresentation>() == null) root.AddComponent<PirateSlop.SwimPresentation>();
    if (root.GetComponent<PirateSlop.PlayerFreezeScreen>() == null) root.AddComponent<PirateSlop.PlayerFreezeScreen>();
    var behaviours = root.GetComponents<UnityEngine.MonoBehaviour>().Where(b => b != null).ToArray();
    foreach (var behaviour in behaviours) behaviour.useGUILayout = true;
    setting.SetValue(null, false);
    PirateSlop.PlayerGuiBudget.Configure(root);
    if (behaviours.Any(b => !b.useGUILayout)) throw new System.Exception("Legacy mode changed GUI layout flags");
    setting.SetValue(null, true);
    PirateSlop.PlayerGuiBudget.Configure(root);
    var changed = behaviours.Where(b => !b.useGUILayout).Select(b => b.GetType().Name).OrderBy(n => n).ToArray();
    if (changed.Length < 20) throw new System.Exception("Expected audited HUD components were not configured");
    if (behaviours.Any(b => (b is PirateSlop.DeveloperMenu || b.GetType().Name == "BotDebugPanel") && !b.useGUILayout))
        throw new System.Exception("GUILayout developer UI was disabled");
    var report = Newtonsoft.Json.JsonConvert.SerializeObject(new { kind = "native-player-gui-layout-check", legacyUntouched = true,
        developerUiUntouched = true, fixedRectComponents = changed, gamePlayTest = false }, Newtonsoft.Json.Formatting.Indented);
    System.IO.File.WriteAllText("Docs/Performance/Reports/20261016-gui-layout-check.json", report);
    return report;
}
finally
{
    setting.SetValue(null, previous);
    UnityEditor.PrefabUtility.UnloadPrefabContents(root);
}
