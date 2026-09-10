using System.Linq;
using PirateSlop.Networking;
using UnityEditor;
using UnityEngine;

namespace PirateSlop.Editor
{
    public sealed class GameplayDiagnostics : EditorWindow
    {
        Vector2 scroll;
        string report = "Start a host session to inspect server decisions.";
        double nextRefresh;
        [MenuItem("PirateSlop/Diagnostics/Bots and Cannons")]
        public static void Open() => GetWindow<GameplayDiagnostics>("Bots and Cannons");
        void OnInspectorUpdate()
        {
            if (EditorApplication.timeSinceStartup < nextRefresh) return;
            nextRefresh = EditorApplication.timeSinceStartup + .5;
            report = Capture(); Repaint();
        }
        public static string Capture()
        {
            if (!Application.isPlaying) return "Start a host session to inspect server decisions.";
            var text = new System.Text.StringBuilder();
            var players = Object.FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
            foreach (var ship in NetworkShip.ActiveShips)
            {
                if (ship == null) continue;
                text.AppendLine($"SHIP {ship.ParticipantId.Value} / TEAM {ship.TeamId.Value}: {ship.Motor.Speed:F2} m/s, sails {ship.GetComponent<SailSystem>().DeployPercentage:P0}, rudder {ship.Helm.CurrentRudderNormalized:F2}");
                text.AppendLine($"  Authority: {ship.IsServerInitialized}; sinking: {ship.IsSinking}; frozen: {ship.Motor.IsFrozen}");
                foreach (var player in players.Where(p => p.Ship == ship))
                    text.AppendLine($"  {(player.IsBot.Value ? "BOT" : "PLAYER")} {player.ParticipantId.Value}: {player.BotStatus}; helm range: {ship.Helm.InRange(player.Motor)}; dead: {player.Motor.IsDead}; swimming: {player.Motor.IsSwimming}");
                foreach (var gun in ship.GetComponentsInChildren<SimpleCannon>())
                    text.AppendLine($"  GUN {gun.Index}: {gun.Readiness}; operator: {(gun.Operator != null ? gun.Operator.name : "none")}; ammo: {gun.LoadedAmmo}");
                text.AppendLine();
            }
            return text.ToString();
        }
        void OnGUI()
        {
            EditorGUILayout.HelpBox("Read-only. Bot decisions are available on the host/server. Refreshes twice per second.", MessageType.Info);
            if (GUILayout.Button("Copy report")) EditorGUIUtility.systemCopyBuffer = report;
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }
    }
}
