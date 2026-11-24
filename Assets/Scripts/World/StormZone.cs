using UnityEngine;

namespace PirateSlop.World
{
    public sealed class StormZone : MonoBehaviour
    {
        float elapsed, receivedAt, duration = Networking.SessionController.DefaultStormDuration, startRadius;
        public const float FinalRadius = OceanSurface.CentralWhirlpoolRadius;
        public static StormZone Instance { get; private set; }
        Networking.NetworkPlayer localPlayer;
        public Networking.NetworkShip LocalShip => localPlayer != null ? localPlayer.Ship : null;
        public float DistanceInside(Vector3 point) => Radius - new Vector2(point.x, point.z).magnitude;
        void Awake() { Instance = this; }
        public bool Paused { get; private set; }
        public float Progress => Mathf.Clamp01((elapsed + (Paused ? 0 : Time.time - receivedAt)) / duration);
        public float Radius => Mathf.Lerp(startRadius, FinalRadius, Progress);

        public void Synchronize(float time, float length, float radius, bool paused = false)
        {
            Paused = paused;
            elapsed = time;
            receivedAt = Time.time;
            duration = Mathf.Max(1, length);
            startRadius = radius;
        }

        public Vector3 Center => Vector3.zero;
        public Vector3 TargetCenter => Vector3.zero;
        public float TargetRadius => FinalRadius;
        public float PhaseStartTime => receivedAt - elapsed;
        public float PhaseEndTime => PhaseStartTime + duration;

        void Start()
        {
            var prefab = Resources.Load<GameObject>("BRZoneVisual");
            if (prefab != null) Instantiate(prefab, transform);
            else Debug.LogError("BRZoneVisual prefab is missing.");
            
        }

        void Update()
        {
            if (localPlayer == null)
                foreach (var player in Networking.NetworkPlayer.Active)
                    if (player.IsOwner) { localPlayer = player; break; }
            if (OceanSurface.Instance != null)
            {
                OceanSurface.Instance.WaveScale = Mathf.Lerp(.06f, 0.08f, Progress);
            }
        }
        void OnGUI()
        {
            if (PirateSlop.GameVersionOverlay.InterfaceHidden) return;
            if (Event.current.type != EventType.Repaint) return;
            var camera = Camera.main;
            if (camera == null || !camera.enabled || Networking.SessionController.MenuOpen) return;
            Color old = GUI.color;
            GUI.color = Color.white;
            int remaining = Mathf.CeilToInt(duration * (1 - Progress));
            float distance = DistanceInside(localPlayer != null ? localPlayer.transform.position : camera.transform.position);
            string shipStatus = LocalShip != null ? Localization.T(DistanceInside(LocalShip.transform.position) > 0 ? "Корабль в зоне" : "КОРАБЛЬ В ШТОРМЕ") : "";
            if (Paused) shipStatus = Localization.T("ЗОНА НА ПАУЗЕ · урон отключён");
            GUI.color = distance <= 0 ? new Color(1, .55f, .4f) : new Color(.65f, 1, .94f);
            string timer = Localization.F("ШТОРМ  {0:00}:{1:00}   •   Радиус {2:0} м", remaining / 60, remaining % 60, Radius);
            string position = Localization.F(distance <= 0 ? "ВЫ В ШТОРМЕ • До зоны {0:0} м" : "В безопасной зоне • До шторма {0:0} м", Mathf.Abs(distance));
            string map = Localization.F("Карта: {0}", KeyBindings.Label(GameAction.Map));
            PirateHudStyle.Panel(new Rect(Screen.width - 404, 24, 380, 80), timer + "\n" + position + "\n" + (shipStatus.Length > 0 ? shipStatus + " • " : "") + map);
            GUI.color = old;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;


            if (OceanSurface.Instance != null) OceanSurface.Instance.WaveScale = .06f;
        }
    }
}




