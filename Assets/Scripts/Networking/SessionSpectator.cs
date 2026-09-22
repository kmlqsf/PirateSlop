using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class SessionController
    {
        bool chooseObserver, observerSelected, observerSession;
        GameObject observerCamera;
        public bool ObserverActive => observerSession && playing && manager != null && manager.ServerManager.Started;

        public bool IsSpectatorConnection(FishNet.Connection.NetworkConnection connection)
        {
            if (!observerSession || manager == null || !manager.ServerManager.Started || connection == null || !connection.IsAuthenticated) return false;
            var local = manager.ClientManager.Connection;
            return local != null && local.IsActive && connection.ClientId == local.ClientId;
        }

        void StartObserver()
        {
            connecting = false; playing = true; status = "Наблюдение за ботами";
            observerCamera = new GameObject("BotSpectatorCamera");
            var camera = observerCamera.AddComponent<Camera>();
            var gameplayCamera = PlayerPrefab != null ? PlayerPrefab.GetComponentInChildren<Camera>(true) : null;
            if (gameplayCamera != null) camera.CopyFrom(gameplayCamera);
            else if (MenuCamera != null) camera.CopyFrom(MenuCamera);
            if (MenuCamera != null) MenuCamera.gameObject.SetActive(false);
            camera.cullingMask = ~LayerMask.GetMask("UI");
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.orthographic = false;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 6000f;
            camera.fieldOfView = 75f;
            camera.targetTexture = null;
            camera.rect = new Rect(0, 0, 1, 1);
            camera.enabled = true;
            observerCamera.tag = "MainCamera";
            observerCamera.AddComponent<AudioListener>();
            var position = new Vector3(0, 30, 0);
            float yaw = 0;
            foreach (var ship in NetworkShip.ActiveShips)
                if (ship != null) { position = ship.transform.position + Vector3.up * 20f - ship.transform.forward * 35f; yaw = ship.transform.eulerAngles.y; break; }
            observerCamera.transform.SetPositionAndRotation(position, Quaternion.Euler(20f, yaw, 0));
            observerCamera.AddComponent<BotSpectatorCamera>();
            observerCamera.AddComponent<BotDebugPanel>();
            AdvancedPlayerController.SetCursor(true);
        }

        void StopObserver()
        {
            if (observerCamera != null) { observerCamera.SetActive(false); Destroy(observerCamera); }
            observerCamera = null;
            observerSession = false;
        }
    }
}
