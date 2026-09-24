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
        static Camera viewingCamera;
        public static bool ClearsFog(Camera camera) => IsViewing && camera == viewingCamera;
        public Material TrajectoryMaterial;
        [SerializeField] float viewpointLift = 1.25f;
        public float ViewpointLift => viewpointLift;
        NetworkPlayer player;
        ShipSpyglass station, aimed;
        Camera cameraView;
        bool engaged;
        bool portable, fogOverridden, savedFog;
        PlayerInventory inventory;
        NetworkEquipment equipment;
        bool markRequested;
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
            if (!engaged || camera != cameraView) return;
            savedFog = RenderSettings.fog;
            RenderSettings.fog = false;
            fogOverridden = true;
            RestoreVisibility();
            GetComponentsInChildren<Renderer>(true, renderers);
            if (station != null)
            {
                station.GetComponentsInChildren<Renderer>(true, stationRenderers);
                renderers.AddRange(stationRenderers);
            }
            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;
                visibility.Add((renderer, renderer.forceRenderingOff));
                renderer.forceRenderingOff = true;
            }
        }
        void EndCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera == cameraView) { RestoreVisibility(); RestoreFog(); }
        }
        void RestoreFog()
        {
            if (!fogOverridden) return;
            RenderSettings.fog = savedFog;
            fogOverridden = false;
        }
        void RestoreVisibility()
        {
            foreach (var state in visibility)
                if (state.renderer != null) state.renderer.forceRenderingOff = state.hidden;
            visibility.Clear();
        }
        void Awake() { player = GetComponent<NetworkPlayer>(); inventory = GetComponent<PlayerInventory>(); equipment = GetComponent<NetworkEquipment>(); }
        void Update()
        {
            if (!player.IsOwner) return;
            if (engaged && !portable && station == null) Exit();
            var keys = Keyboard.current;
            if (keys == null) return;
            if (engaged)
            {
                if (player.Motor.IsDead || player.Motor.IsSwimming || player.Motor.IsClimbing || Cursor.lockState != CursorLockMode.Locked ||
                    (portable ? equipment == null || !equipment.Active || inventory.ItemAt(inventory.SelectedSlot) != InventoryItem.Spyglass || Mouse.current == null || !Mouse.current.rightButton.isPressed : Vector3.Distance(transform.position, station.transform.position) > 3.5f) || keys.eKey.wasPressedThisFrame || keys.escapeKey.wasPressedThisFrame)
                { Exit(); return; }
                if (Mouse.current != null)
                {
                    if (!portable) markRequested |= Mouse.current.middleButton.wasPressedThisFrame;
                    var delta = Mouse.current.delta.ReadValue() * (.055f * zoom / 48f);
                    angles.x = Mathf.Clamp(angles.x - delta.y, -70f, 70f);
                    angles.y = Mathf.Repeat(angles.y + delta.x, 360f);
                    float wheel = Mouse.current.scroll.ReadValue().y;
                    if (Mathf.Abs(wheel) > .01f) zoom = Mathf.Clamp(zoom - Mathf.Sign(wheel) * 3f, portable ? 12f : 32f, portable ? 48f : 62f);
                }
                return;
            }
            aimed = null;
            if (!player.Motor.InputActive || player.Motor.IsSwimming || player.Motor.IsClimbing || player.Motor.LocomotionLocked || GetComponent<CannonHands>().HasHeldBall) return;
            cameraView = player.Motor.PlayerCamera;
            if (equipment != null && equipment.Active && inventory.ItemAt(inventory.SelectedSlot) == InventoryItem.Spyglass && !PlayerInventory.LootWindowOpen && !inventory.ControlFocused && Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            {
                player.Motor.SetThirdPerson(false);
                originalFov = cameraView.fieldOfView;
                var rotation = cameraView.transform.eulerAngles;
                angles = new Vector2(Mathf.DeltaAngle(0, rotation.x), rotation.y);
                zoom = 24f;
                portable = engaged = IsViewing = true;
                viewingCamera = cameraView;
                return;
            }
            if (Physics.Raycast(cameraView.transform.position, cameraView.transform.forward, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore))
                aimed = hit.collider.GetComponentInParent<ShipSpyglass>();
            if (aimed == null || aimed.GetComponentInParent<NetworkShip>() != player.Ship || !keys.eKey.wasPressedThisFrame) return;
            station = aimed; originalFov = cameraView.fieldOfView; angles = Vector2.zero; zoom = 48f;
            engaged = IsViewing = true; nextPreview = 0f;
            viewingCamera = cameraView;
        }
        void LateUpdate()
        {
            if (!player.IsOwner || !engaged) return;
            if (portable) cameraView.transform.rotation = Quaternion.Euler(angles.x, angles.y, 0f);
            else if (station != null) cameraView.transform.SetPositionAndRotation(station.Viewpoint.position + station.transform.up * viewpointLift, station.transform.rotation * Quaternion.Euler(angles.x, angles.y, 0f));
            cameraView.fieldOfView = Mathf.Lerp(cameraView.fieldOfView, zoom, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
            if (markRequested)
            {
                markRequested = false;
                player.MarkSpyglassTarget(cameraView.transform.forward);
            }
            if (!portable && player.Ship != null && Time.unscaledTime >= nextPreview) { nextPreview = Time.unscaledTime + .2f; Preview(); }
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
            RestoreFog();
            if (engaged && portable) player.Motor.SetLookAngles(angles.y, angles.x);
            if (cameraView != null && engaged) cameraView.fieldOfView = originalFov;
            station = null; engaged = IsViewing = markRequested = false;
            portable = false; viewingCamera = null;
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
            if (!engaged) { if (aimed != null) ContextPrompt.Offer("ПОДЗОРНАЯ ТРУБА · E — смотреть", 40); return; }
            if (mask == null)
            {
                const int resolution = 512;
                mask = new Texture2D(resolution,resolution,TextureFormat.RGBA32,false);
                mask.wrapMode = TextureWrapMode.Clamp;
                mask.filterMode = FilterMode.Bilinear;
                var pixels = new Color[resolution*resolution];
                for(int y=0;y<resolution;y++) for(int x=0;x<resolution;x++)
                {
                    float u = (x + .5f) / resolution, v = (y + .5f) / resolution;
                    float radius = new Vector2(u * 2 - 1, v * 2 - 1).magnitude;
                    float rim = Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.91f,.985f,radius));
                    float haze = .025f + .055f * Mathf.Pow(Mathf.Clamp01(radius), 3) + .025f * Mathf.PerlinNoise(u * 6f + 4f,v * 7f + 13f);
                    var glass = Color.Lerp(new Color(.68f,.74f,.68f), new Color(.008f,.015f,.02f), rim);
                    glass.a = Mathf.Lerp(haze, 1f, rim);
                    pixels[y*resolution+x] = glass;
                }
                mask.SetPixels(pixels); mask.Apply();
            }
            float size = Screen.height;
            var old=GUI.color; GUI.color=Color.black;
            GUI.DrawTexture(new Rect(0,0,(Screen.width-size)/2f,size),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect((Screen.width+size)/2f,0,Screen.width,size),Texture2D.whiteTexture);
            GUI.color=Color.white; GUI.DrawTexture(new Rect((Screen.width-size)/2f,0,size,size),mask);
            GUI.Label(new Rect(Screen.width/2f-5,Screen.height/2f-10,20,20),"+");
            ContextPrompt.Draw(portable ? "ПОДЗОРНАЯ ТРУБА · колесо — зум · отпустить ПКМ — выйти" : "ПОДЗОРНАЯ ТРУБА · Колесо — зум · Нажать колесо — метка на 2 мин · E / Esc — выйти"); GUI.color=old;
            PlayerHud.DrawCompass(cameraView);
            TargetMarkHud.Draw(player, cameraView);
        }
    }
}
