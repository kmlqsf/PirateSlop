using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using PirateSlop.Networking;

namespace PirateSlop
{
    [DefaultExecutionOrder(200)]
    public sealed class ShipSpyglassView : MonoBehaviour
    {
        public static bool IsViewing { get; private set; }
        public Material TrajectoryMaterial;
        [SerializeField] float viewpointLift = 1.25f;
        NetworkPlayer player;
        ShipSpyglass station, aimed;
        Camera cameraView;
        bool engaged;
        float originalFov, zoom = 48f, nextPreview;
        Vector2 angles;
        readonly List<LineRenderer> lines = new();
        Texture2D mask;
        readonly List<Renderer> renderers = new();
        readonly List<Renderer> stationRenderers = new();
        readonly List<(Renderer renderer, bool hidden)> visibility = new();
        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += BeginCamera;
            RenderPipelineManager.endCameraRendering += EndCamera;
        }
        void BeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (!engaged || station == null || camera != cameraView) return;
            RestoreVisibility();
            GetComponentsInChildren<Renderer>(true, renderers);
            station.GetComponentsInChildren<Renderer>(true, stationRenderers);
            renderers.AddRange(stationRenderers);
            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;
                visibility.Add((renderer, renderer.forceRenderingOff));
                renderer.forceRenderingOff = true;
            }
        }
        void EndCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera == cameraView) RestoreVisibility();
        }
        void RestoreVisibility()
        {
            foreach (var state in visibility)
                if (state.renderer != null) state.renderer.forceRenderingOff = state.hidden;
            visibility.Clear();
        }
        void Awake() { player = GetComponent<NetworkPlayer>(); }
        void Update()
        {
            if (!player.IsOwner) return;
            if (engaged && station == null) Exit();
            var keys = Keyboard.current;
            if (keys == null) return;
            if (station != null)
            {
                if (player.Motor.IsDead || player.Motor.IsSwimming || player.Motor.IsClimbing || Cursor.lockState != CursorLockMode.Locked ||
                    Vector3.Distance(transform.position, station.transform.position) > 3.5f || keys.eKey.wasPressedThisFrame || keys.escapeKey.wasPressedThisFrame)
                { Exit(); return; }
                if (Mouse.current != null)
                {
                    var delta = Mouse.current.delta.ReadValue() * (.055f * zoom / 48f);
                    angles.x = Mathf.Clamp(angles.x - delta.y, -70f, 70f);
                    angles.y = Mathf.Repeat(angles.y + delta.x, 360f);
                    float wheel = Mouse.current.scroll.ReadValue().y;
                    if (Mathf.Abs(wheel) > .01f) zoom = Mathf.Clamp(zoom - Mathf.Sign(wheel) * 3f, 32f, 62f);
                }
                return;
            }
            aimed = null;
            if (!player.Motor.InputActive || player.Motor.IsSwimming || player.Motor.IsClimbing || player.Motor.LocomotionLocked || GetComponent<CannonHands>().HasHeldBall) return;
            cameraView = player.Motor.PlayerCamera;
            if (Physics.Raycast(cameraView.transform.position, cameraView.transform.forward, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore))
                aimed = hit.collider.GetComponentInParent<ShipSpyglass>();
            if (aimed == null || aimed.GetComponentInParent<NetworkShip>() != player.Ship || !keys.eKey.wasPressedThisFrame) return;
            station = aimed; originalFov = cameraView.fieldOfView; angles = Vector2.zero; zoom = 48f;
            engaged = IsViewing = true; nextPreview = 0f;
        }
        void LateUpdate()
        {
            if (!player.IsOwner || station == null) return;
            cameraView.transform.SetPositionAndRotation(station.Viewpoint.position + station.transform.up * viewpointLift, station.transform.rotation * Quaternion.Euler(angles.x, angles.y, 0f));
            cameraView.fieldOfView = Mathf.Lerp(cameraView.fieldOfView, zoom, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
            if (Time.unscaledTime >= nextPreview) { nextPreview = Time.unscaledTime + .2f; Preview(); }
        }
        void Preview()
        {
            var cannons = player.Ship.GetComponentsInChildren<SimpleCannon>();
            while (lines.Count < cannons.Length)
            {
                var line = new GameObject("CannonTrajectory").AddComponent<LineRenderer>();
                line.sharedMaterial = TrajectoryMaterial; line.widthMultiplier = .16f; line.numCapVertices = 3;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lines.Add(line);
            }
            for (int i = 0; i < lines.Count; i++)
            {
                lines[i].gameObject.SetActive(i < cannons.Length);
                if (i >= cannons.Length) continue;
                var cannon = cannons[i]; var points = new List<Vector3>();
                Vector3 point = cannon.ShotPosition, velocity = cannon.ShotVelocity;
                var ammo = cannon.IsLoaded ? cannon.LoadedAmmo : InventoryItem.Cannonball;
                bool boomerang = ammo == InventoryItem.BoomerangCannonball;
                Vector3 launch = point, initialVelocity = velocity;
                const float dt = .04f;
                for (int step = 0; step < 250; step++)
                {
                    points.Add(point);
                    float age = (step + 1) * dt;
                    Vector3 nextVelocity = CannonShotDamage.StepVelocity(velocity, dt);
                    Vector3 next = point + (velocity + nextVelocity) * (.5f * dt);
                    if (boomerang && age <= 2f) next = launch + initialVelocity * age;
                    if (boomerang && age > 2f)
                    {
                        float t = Mathf.Clamp01((age - 2f) / 3f), u = 1f - t;
                        Vector3 start = launch + initialVelocity * 2f;
                        Vector3 right = Vector3.Cross(Vector3.up, initialVelocity.normalized).normalized;
                        next = start * (u*u*u) + (start + initialVelocity.normalized*8f + Vector3.up*8f) * (3f*u*u*t) +
                            (launch - right*16f + Vector3.up*8f) * (3f*u*t*t) + launch*(t*t*t);
                    }
                    bool stop = false;
                    foreach (var hit in Physics.SphereCastAll(point, .24f, (next-point).normalized, (next-point).magnitude, ~0, QueryTriggerInteraction.Ignore))
                        if (!hit.transform.IsChildOf(player.Ship.transform) && !hit.transform.IsChildOf(transform)) { next = hit.point; stop = true; break; }
                    var ocean = OceanSurface.Instance;
                    if (ocean != null && next.y <= ocean.Height(next)) { next.y = ocean.Height(next); stop = true; }
                    point = next; velocity = nextVelocity;
                    if (stop || (boomerang && age >= 5f)) { points.Add(point); break; }
                }
                lines[i].startColor = lines[i].endColor = cannon.IsLoaded ? new Color(1f,.76f,.25f) : new Color(.45f,.8f,.85f,.65f);
                lines[i].positionCount = points.Count; lines[i].SetPositions(points.ToArray());
            }
        }
        void Exit()
        {
            RestoreVisibility();
            if (cameraView != null && engaged) cameraView.fieldOfView = originalFov;
            station = null; engaged = IsViewing = false;
            foreach (var line in lines) if (line != null) line.gameObject.SetActive(false);
        }
        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
            RestoreVisibility();
            if (engaged) Exit();
        }
        void OnDestroy() { foreach (var line in lines) if (line != null) Destroy(line.gameObject); if (mask != null) Destroy(mask); }
        void OnGUI()
        {
            if (!player.IsOwner) return;
            if (station == null) { if (aimed != null) PirateHudStyle.Panel(new Rect(Screen.width/2f-160,Screen.height-170,320,30), "E — смотреть в подзорную трубу"); return; }
            if (mask == null)
            {
                mask = new Texture2D(128,128,TextureFormat.RGBA32,false); mask.wrapMode = TextureWrapMode.Clamp;
                var pixels = new Color[128*128];
                for(int y=0;y<128;y++) for(int x=0;x<128;x++) { float radius = new Vector2((x-63.5f)/63.5f,(y-63.5f)/63.5f).magnitude; pixels[y*128+x]=new Color(.008f,.015f,.02f,Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.89f,.98f,radius))); }
                mask.SetPixels(pixels); mask.Apply();
            }
            float size = Screen.height;
            var old=GUI.color; GUI.color=Color.black;
            GUI.DrawTexture(new Rect(0,0,(Screen.width-size)/2f,size),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect((Screen.width+size)/2f,0,Screen.width,size),Texture2D.whiteTexture);
            GUI.color=Color.white; GUI.DrawTexture(new Rect((Screen.width-size)/2f,0,size,size),mask);
            GUI.Label(new Rect(Screen.width/2f-5,Screen.height/2f-10,20,20),"+");
            PirateHudStyle.Panel(new Rect(Screen.width/2f-285,Screen.height-65,570,35),"Колесо — зум · E / Esc — выйти · Золото: заряжена · Голубой: пуста"); GUI.color=old;
            PlayerHud.DrawCompass(cameraView);
        }
    }
}
