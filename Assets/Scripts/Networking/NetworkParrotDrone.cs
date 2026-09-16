using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace PirateSlop.Networking
{
    public sealed class NetworkParrotDrone : NetworkBehaviour
    {
        public const float FlightSeconds = 6f;
        [SerializeField] float speed = 18f;
        readonly SyncVar<Vector3> flightPosition = new();
        readonly SyncVar<Quaternion> flightRotation = new(Quaternion.identity);
        readonly SyncVar<NetworkObject> pilot = new();
        readonly SyncVar<float> remaining = new(FlightSeconds);
        NetworkPlayer shooter;
        int team;
        NetworkShip homeShip;
        float launched, nextInput, yaw, pitch;
        bool exploded;
        Camera flightCamera;
        Renderer[] flightRenderers;
        AudioListener playerListener;
        bool cameraWasEnabled, listenerWasEnabled, viewing;
        Vector3 steering;
        readonly System.Collections.Generic.Dictionary<Transform, Quaternion> wings = new();
        public void Launch(NetworkPlayer owner)
        {
            shooter = owner; team = owner.TeamId.Value; homeShip = owner.Ship;
            pilot.Value = owner.NetworkObject;
            owner.Motor.ActiveParrot = this;
            launched = Time.time; steering = transform.forward;
            flightPosition.Value = transform.position; flightRotation.Value = transform.rotation;
        }
        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += BeforeCamera;
            RenderPipelineManager.endCameraRendering += AfterCamera;
        }
        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeforeCamera;
            RenderPipelineManager.endCameraRendering -= AfterCamera;
            EndView();
        }
        void BeforeCamera(ScriptableRenderContext context, Camera camera)
        {
            if (flightRenderers == null) flightRenderers = GetComponentsInChildren<Renderer>();
            foreach (var renderer in flightRenderers) if (renderer != null) renderer.forceRenderingOff = camera == flightCamera;
        }
        void AfterCamera(ScriptableRenderContext context, Camera camera)
        {
            if (flightRenderers != null) foreach (var renderer in flightRenderers) if (renderer != null) renderer.forceRenderingOff = false;
        }
        public override void OnStartNetwork()
        {
            foreach (var child in GetComponentsInChildren<Transform>())
                if (child.name == "WingLeft" || child.name == "WingRight") wings[child] = child.localRotation;
        }
        bool Enemy(NetworkPlayer player)
        {
            return player != null && player != shooter && player.IsSpawned && !player.Motor.IsDead &&
                !(team > 0 && player.TeamId.Value == team) && !(homeShip != null && player.Ship == homeShip);
        }
        void Update()
        {
            if (!IsSpawned) return;
            if (shooter == null && pilot.Value != null) shooter = pilot.Value.GetComponent<NetworkPlayer>();
            if (shooter != null && shooter.IsOwner && !shooter.Motor.IsDead)
            {
                if (flightCamera == null) BeginView();
                if (!SessionController.MenuOpen && !DeveloperMenu.IsOpen && Cursor.lockState == CursorLockMode.Locked && Mouse.current != null)
                {
                    Vector2 delta = Mouse.current.delta.ReadValue() * shooter.Motor.LookSensitivity;
                    yaw = Mathf.Repeat(yaw + delta.x, 360f); pitch = Mathf.Clamp(pitch - delta.y, -89f, 89f);
                }
                if (Time.unscaledTime >= nextInput)
                {
                    nextInput = Time.unscaledTime + 1f / 30f;
                    SteerServerRpc(Quaternion.Euler(pitch, yaw, 0) * Vector3.forward);
                }
            }
            else if (flightCamera != null) EndView();
            if (IsServerInitialized && !exploded) Fly(Time.deltaTime);
            else if (!IsServerInitialized)
            {
                transform.position = Vector3.Lerp(transform.position, flightPosition.Value, 1 - Mathf.Exp(-25 * Time.deltaTime));
                transform.rotation = Quaternion.Slerp(transform.rotation, flightRotation.Value, 1 - Mathf.Exp(-20 * Time.deltaTime));
            }
            foreach (var wing in wings) if (wing.Key != null) wing.Key.localRotation = wing.Value * Quaternion.Euler(0, Mathf.Sin(Time.time * 20) * 65 * (wing.Key.name == "WingLeft" ? 1 : -1), 0);
        }
        void BeginView()
        {
            var motor = shooter.Motor;
            motor.ActiveParrot = this;
            yaw = transform.eulerAngles.y; pitch = Mathf.DeltaAngle(0, transform.eulerAngles.x);
            var source = motor.PlayerCamera;
            flightCamera = new GameObject("ParrotFlightCamera").AddComponent<Camera>();
            flightCamera.CopyFrom(source);
            flightCamera.nearClipPlane = .04f;
            flightCamera.fieldOfView = 80f;
            viewing = true;
            cameraWasEnabled = source.enabled; source.enabled = false;
            playerListener = source.GetComponent<AudioListener>();
            if (playerListener != null) { listenerWasEnabled = playerListener.enabled; playerListener.enabled = false; }
            flightCamera.gameObject.AddComponent<AudioListener>();
            flightCamera.enabled = true;
        }
        void LateUpdate()
        {
            if (flightCamera != null) flightCamera.transform.SetPositionAndRotation(transform.position + Quaternion.Euler(pitch, yaw, 0) * new Vector3(0, .12f, .32f), Quaternion.Euler(pitch, yaw, 0));
        }
        void EndView()
        {
            if (flightCamera != null) { flightCamera.enabled = false; flightCamera.GetComponent<AudioListener>().enabled = false; Destroy(flightCamera.gameObject); flightCamera = null; }
            if (shooter != null)
            {
                if (shooter.Motor.ActiveParrot == this) shooter.Motor.ActiveParrot = null;
                if (viewing && shooter.IsOwner && shooter.Motor.PlayerCamera != null) shooter.Motor.PlayerCamera.enabled = cameraWasEnabled;
            }
            if (viewing && playerListener != null) playerListener.enabled = listenerWasEnabled;
            viewing = false;
        }
        public override void OnStopNetwork() { EndView(); base.OnStopNetwork(); }
        void OnDestroy() { EndView(); }
        [ServerRpc(RequireOwnership = false)]
        void SteerServerRpc(Vector3 forward, FishNet.Connection.NetworkConnection sender = null)
        {
            if (shooter == null || sender != shooter.Owner || exploded || !float.IsFinite(forward.sqrMagnitude) || forward.sqrMagnitude < .5f) return;
            steering = forward.normalized;
        }
        bool Ignored(Collider hit) => hit.transform.IsChildOf(transform) || (shooter != null && Time.time - launched < .2f && hit.transform.IsChildOf(shooter.transform));
        void Fly(float dt)
        {
            remaining.Value = Mathf.Max(0, FlightSeconds - (Time.time - launched));
            if (remaining.Value <= 0 || shooter == null || !shooter.IsSpawned || shooter.Motor.IsDead || shooter.Owner == null || !shooter.Owner.IsActive) { Explode(); return; }
            foreach (var overlap in Physics.OverlapSphere(transform.position, .18f, ~0, QueryTriggerInteraction.Ignore))
                if (!Ignored(overlap)) { Explode(); return; }
            transform.rotation = Quaternion.LookRotation(steering);
            Vector3 step = steering * speed * dt;
            float nearest = step.magnitude;
            Collider obstacle = null;
            foreach (var hit in Physics.SphereCastAll(transform.position, .18f, steering, nearest, ~0, QueryTriggerInteraction.Ignore))
            {
                if (Ignored(hit.collider)) continue;
                if (hit.distance <= nearest) { nearest = hit.distance; obstacle = hit.collider; }
            }
            transform.position += steering * nearest;
            if (obstacle != null || (OceanSurface.Instance != null && transform.position.y <= OceanSurface.Instance.Height(transform.position))) { Explode(); return; }
            flightPosition.Value = transform.position; flightRotation.Value = transform.rotation;
        }
        void Explode()
        {
            if (exploded) return;
            exploded = true;
            var damaged = new System.Collections.Generic.HashSet<CombatHealth>();
            foreach (var collider in Physics.OverlapSphere(transform.position, 3, ~0, QueryTriggerInteraction.Ignore))
            {
                var player = collider.GetComponentInParent<NetworkPlayer>();
                if (!Enemy(player)) continue;
                var health = player.GetComponent<CombatHealth>();
                if (health == null || !damaged.Add(health)) continue;
                Vector3 center = player.transform.position + Vector3.up;
                if (FirearmTrace.Cast(gameObject, transform.position, center, out var hit) && hit.collider.GetComponentInParent<NetworkPlayer>() != player) continue;
                health.Damage(50, shooter != null ? shooter.gameObject : null);
            }
            ExplosionObserversRpc(transform.position);
            if (shooter != null && shooter.Motor.ActiveParrot == this) shooter.Motor.ActiveParrot = null;
            ServerManager.Despawn(NetworkObject);
        }
        [ObserversRpc(RunLocally = true)]
        void ExplosionObserversRpc(Vector3 point)
        {
            EndView();
            CombatVfx.Fire(point, Vector3.up, true); CombatVfx.Impact(point, Vector3.up, true);
            GameAudio.Play(SoundCue.Cannon, point);
        }
        void OnGUI()
        {
            if (flightCamera == null || SessionController.MenuOpen) return;
            float value = Mathf.Max(0, remaining.Value);
            var rect = new Rect(Screen.width * .5f - 170, 100, 340, 84);
            PirateHudStyle.Panel(rect);
            PirateHudStyle.Label(new Rect(rect.x, rect.y, rect.width, 28), "ПОПУГАЙ · " + value.ToString("0.0") + " с", PirateHudStyle.Gold, true);
            PirateHudStyle.Bar(new Rect(rect.x + 20, rect.y + 34, rect.width - 40, 12), value / FlightSeconds, value <= 2 ? new Color(.9f, .3f, .15f) : PirateHudStyle.Gold);
            PirateHudStyle.Label(new Rect(rect.x, rect.y + 50, rect.width, 25), "Мышь — направление полёта", PirateHudStyle.Paper);
        }
    }
}
