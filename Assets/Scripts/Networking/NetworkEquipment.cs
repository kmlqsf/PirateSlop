using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace PirateSlop.Networking
{
    [DefaultExecutionOrder(45)]
    public sealed class NetworkEquipment : NetworkBehaviour
    {
        public GameObject[] Models;
        public NetworkParrotDrone ParrotPrefab;
        readonly SyncVar<float> waterRunRemaining = new();
        float waterRunUntil;
        public bool WaterRunning => IsSpawned && waterRunRemaining.Value > 0 && !motor.IsDead;
        public bool Scoped => IsOwner && Active && Item == InventoryItem.Musket && sentAim && action.Value == 0;
        float scopeFov = 24;
        Texture2D scopeMask;
        readonly SyncVar<bool> aiming = new();
        readonly SyncVar<byte> action = new();
        readonly SyncVar<int> rounds = new(1);
        readonly SyncVar<Vector3> direction = new(Vector3.forward);
        readonly int[] ammunition = { 1, 1, 1, 1, 1, 1 };
        PlayerInventory inventory;
        AdvancedPlayerController motor;
        NetworkWeapon network;
        CannonHands hands;
        Transform view, world;
        Renderer[] viewRenderers, worldRenderers;
        readonly System.Collections.Generic.Dictionary<Transform, Quaternion> wings = new();
        InventoryItem shown = InventoryItem.None;
        int actionSlot = -1, previousSlot = -1;
        float until, nextShot, nextAim, animationAt, recoilAt = -10, baseFov, aimBlend;
        byte visualAction;
        bool sentAim;
        public InventoryItem Item => inventory.EquipmentAt(inventory.SelectedSlot);
        public bool Firearm => Item == InventoryItem.Musket || Item == InventoryItem.DoubleBarrel;
        public bool Active => IsSpawned && Item >= InventoryItem.Wine && !motor.IsDead && !motor.IsSwimming && !motor.IsClimbing && !motor.LocomotionLocked && !hands.HasHeldBall && !inventory.HandsOccupied;
        public Transform View => view;
        public Transform World => world;
        public Vector3 SupportOffset
        {
            get
            {
                if (!Firearm) return new Vector3(-.13f, -.06f, .03f);
                if (action.Value != 1) return new Vector3(0, -.04f, .4f);
                float phase = Mathf.Clamp01((Time.time-animationAt)/(Item == InventoryItem.DoubleBarrel ? 3.6f : 3.2f));
                float reach = Mathf.Sin(phase*Mathf.PI);
                return new Vector3(-.045f,-.04f+reach*.13f,.15f+reach*.5f);
            }
        }
        public Vector3 GripOffset => Firearm ? new Vector3(0, -.03f, 0) : new Vector3(.03f, .06f, 0);
        public void ResetSlot(int slot, InventoryItem item) { if(IsServerInitialized) ammunition[slot] = item == InventoryItem.DoubleBarrel ? 2 : 1; }
        void Awake()
        {
            inventory = GetComponent<PlayerInventory>(); motor = GetComponent<AdvancedPlayerController>();
            network = GetComponent<NetworkWeapon>(); hands = GetComponent<CannonHands>();
        }
        void OnEnable() { RenderPipelineManager.beginCameraRendering += BeforeCamera; RenderPipelineManager.endCameraRendering += AfterCamera; }
        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeforeCamera; RenderPipelineManager.endCameraRendering -= AfterCamera;
            if (baseFov > 0 && motor != null && motor.PlayerCamera != null) motor.PlayerCamera.fieldOfView = baseFov;
        }
        bool CanUse => Active && (inventory.Fishing == null || (!inventory.Fishing.IsFishing && !inventory.Fishing.IsEating));
        void Update()
        {
            if (!IsSpawned) return;
            if (IsServerInitialized)
            {
                if (motor.IsDead) waterRunUntil = 0;
                waterRunRemaining.Value = Mathf.Ceil(Mathf.Max(0,waterRunUntil-Time.time));
                previousSlot = inventory.SelectedSlot; rounds.Value = ammunition[previousSlot];
                if (!CanUse || (action.Value != 0 && actionSlot != inventory.SelectedSlot)) { action.Value = 0; aiming.Value = false; }
                if (action.Value != 0 && Time.time >= until)
                {
                    if (action.Value == 1) { ammunition[actionSlot] = Item == InventoryItem.DoubleBarrel ? 2 : 1; rounds.Value = ammunition[actionSlot]; }
                    if (action.Value == 2 && network.ConsumeEquipment(actionSlot, InventoryItem.Wine)) { waterRunUntil = Time.time+60; waterRunRemaining.Value = 60; }
                    action.Value = 0;
                }
            }
            if (!IsOwner) return;
            var mouse = Mouse.current; var keyboard = Keyboard.current;
            bool can = CanUse && motor.InputActive && !PlayerInventory.LootWindowOpen;
            bool aim = can && Firearm && action.Value == 0 && mouse != null && mouse.rightButton.isPressed;
            if (aim && motor.IsThirdPerson) motor.SetThirdPerson(false);
            if (aim && Item == InventoryItem.Musket && mouse != null)
                scopeFov = Mathf.Clamp(scopeFov-mouse.scroll.ReadValue().y*.04f,12,38);
            if (Time.time >= nextAim && (aim || sentAim != aim))
            {
                nextAim = Time.time + .1f; sentAim = aim;
                AimServerRpc(aim, motor.PlayerCamera.transform.forward);
            }
            if (!can || mouse == null) { if (action.Value == 2) CancelServerRpc(); return; }
            Vector3 eyeOffset = motor.PlayerCamera.transform.position-transform.position;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame && Firearm) UseServerRpc(1, motor.PlayerCamera.transform.forward, eyeOffset);
            if (mouse.leftButton.wasPressedThisFrame && !inventory.InteractionUsed && !hands.CanPickUpBall()) UseServerRpc(0, motor.PlayerCamera.transform.forward, eyeOffset);
            if (mouse.leftButton.wasReleasedThisFrame && Item == InventoryItem.Wine) CancelServerRpc();
        }
        [ServerRpc]
        void AimServerRpc(bool value, Vector3 forward)
        {
            if (!float.IsFinite(forward.sqrMagnitude) || forward.sqrMagnitude < .5f) return;
            aiming.Value = value && CanUse && Firearm && action.Value == 0;
            direction.Value = forward.normalized;
        }
        [ServerRpc]
        void CancelServerRpc() { if (action.Value == 2) action.Value = 0; }
        [ServerRpc]
        void UseServerRpc(byte request, Vector3 forward, Vector3 eyeOffset)
        {
            if (!CanUse || action.Value != 0 || Time.time < nextShot || !float.IsFinite(forward.sqrMagnitude) || forward.sqrMagnitude < .5f) return;
            if (Item == InventoryItem.GrapplingHook) { if (request == 0) network.ThrowGrapple(forward); nextShot = Time.time + .6f; return; }
            int slot = inventory.SelectedSlot;
            direction.Value = forward.normalized;
            if (Item == InventoryItem.Wine) { BeginAction(2, 2.2f, slot); return; }
            if (Item == InventoryItem.BombParrot)
            {
                if (ParrotPrefab == null || !network.ConsumeEquipment(slot, InventoryItem.BombParrot)) return;
                var drone = Instantiate(ParrotPrefab,transform.position+Vector3.up*1.3f+transform.forward*.55f,Quaternion.LookRotation(forward));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(drone.gameObject,gameObject.scene);
                drone.Launch(GetComponent<NetworkPlayer>());
                ServerManager.Spawn(drone.NetworkObject);
                nextShot = Time.time+.6f; return;
            }
            if (!Firearm) return;
            int capacity = Item == InventoryItem.DoubleBarrel ? 2 : 1;
            if (request == 1)
            {
                if (ammunition[slot] < capacity) BeginAction(1, capacity == 2 ? 3.6f : 3.2f, slot);
                return;
            }
            if (request != 0) return;
            if (ammunition[slot] < capacity) { EmptyTargetRpc(Owner); return; }
            ammunition[slot] -= capacity; rounds.Value = ammunition[slot]; nextShot = Time.time + .55f;
            Vector3 eye = transform.position + Vector3.up * (motor.IsCrouched ? .8f : 1.6f);
            if(float.IsFinite(eyeOffset.sqrMagnitude) && eyeOffset.sqrMagnitude < 4)
            {
                Vector3 cameraEye = transform.position+eyeOffset;
                if(!FirearmTrace.Cast(gameObject,eye,cameraEye,out var block)) eye=cameraEye;
            }
            Vector3 muzzle = eye + forward.normalized * .65f;
            int pellets = Item == InventoryItem.DoubleBarrel ? 14 : 1;
            var settings = new FirearmSettings { Range = pellets == 1 ? 240 : 65, NearDamage = pellets == 1 ? 70 : 5, FarDamage = pellets == 1 ? 45 : 1.5f, NearHeadDamage = pellets == 1 ? 100 : 5, FarHeadDamage = pellets == 1 ? 70 : 1.5f, FalloffStart = pellets == 1 ? 70 : 10, FalloffEnd = pellets == 1 ? 200 : 40 };
            var shots = new FirearmShot[pellets];
            var damage = new System.Collections.Generic.Dictionary<CombatHealth,float>();
            for (int i = 0; i < pellets; i++)
            {
                Vector2 spread = pellets == 1 ? Vector2.zero : Random.insideUnitCircle * (aiming.Value ? 2.5f : 5f);
                Vector3 ray = Quaternion.LookRotation(forward) * Quaternion.Euler(spread.y, spread.x, 0) * Vector3.forward;
                Vector3 barrel = muzzle + (pellets == 1 ? Vector3.zero : Quaternion.LookRotation(forward) * Vector3.right * (i < 7 ? -.049f : .049f));
                shots[i] = FirearmTrace.Resolve(gameObject, eye, barrel, ray, settings.Range, out var hit);
                if (hit.collider == null) continue;
                var health = hit.collider.GetComponentInParent<CombatHealth>();
                if (health != null)
                {
                    if(pellets == 1) health.ReceiveFirearmHit(Vector3.Distance(eye, hit.point), hit.point, gameObject, settings);
                    else
                    {
                        damage.TryGetValue(health,out float total);
                        damage[health] = total + Mathf.Lerp(settings.NearDamage,settings.FarDamage,Mathf.InverseLerp(settings.FalloffStart,settings.FalloffEnd,Vector3.Distance(eye,hit.point)));
                    }
                }
                else foreach (var component in hit.collider.GetComponentsInParent<MonoBehaviour>())
                    if (component is IWeaponTarget target) { target.ReceiveWeaponHit(settings.NearDamage, gameObject); break; }
            }
            foreach(var hit in damage) hit.Key.Damage(Mathf.Min(70,hit.Value),gameObject);
            if(pellets > 1) motor.ApplyKnockback(-transform.forward*1.5f+Vector3.up*.8f);
            ShotObserversRpc(shots);
        }
        void BeginAction(byte value, float duration, int slot) { actionSlot = slot; until = Time.time + duration; action.Value = value; aiming.Value = false; }
        [TargetRpc] void EmptyTargetRpc(FishNet.Connection.NetworkConnection connection) => GameAudio.Play(SoundCue.DryFire, transform.position);
        [ObserversRpc(RunLocally = true)]
        void ShotObserversRpc(FirearmShot[] shots)
        {
            recoilAt = Time.time;
            Vector3 start = shots[0].Start;
            bool shotgun=shots.Length==14;
            GameAudio.Play(shotgun ? SoundCue.DoubleBarrel : SoundCue.Musket, start);
            FirearmVfx.Fire(start,(shots[0].End-start).normalized,shotgun ? 1.25f : 1.15f);
            if(shotgun) FirearmVfx.Fire(shots[7].Start,(shots[7].End-shots[7].Start).normalized,.85f);
            for(int i=0;i<shots.Length;i++)
            {
                var shot=shots[i];
                new GameObject("EquipmentTracer").AddComponent<PistolBullet>().Initialize(shot.Start,shot,GetComponent<PirateWeapon>().EffectMaterial,shotgun ? .018f : .045f,!shotgun || i%4==0);
            }
        }
        void CreateVisuals()
        {
            wings.Clear();
            if (view != null) Destroy(view.gameObject);
            if (world != null) Destroy(world.gameObject);
            shown = Item;
            if (shown < InventoryItem.Wine || Models == null || (int)shown - 13 >= Models.Length) return;
            var model = Models[(int)shown - 13];
            if (model == null) return;
            view = new GameObject("EquipmentView").transform; view.SetParent(motor.PlayerCamera.transform, false);
            world = new GameObject("EquipmentWorld").transform; world.SetParent(transform, false);
            foreach (var root in new[] { view, world })
            {
                var visual = Instantiate(model, root).transform;
                foreach (var collider in visual.GetComponentsInChildren<Collider>()) Destroy(collider);
                if (Firearm) { visual.localRotation = Quaternion.Euler(0, 90, 0); visual.localPosition = new Vector3(0, -.04f, .22f); }
                else { visual.localRotation = Quaternion.Euler(0, 180, 0); visual.localPosition = new Vector3(0, Item == InventoryItem.BombParrot ? -.16f : -.09f, 0); visual.localScale *= Item == InventoryItem.BombParrot ? .65f : 1f; }
            }
            viewRenderers = view.GetComponentsInChildren<Renderer>(); worldRenderers = world.GetComponentsInChildren<Renderer>();
            foreach(var root in new[]{view,world}) foreach(var bone in root.GetComponentsInChildren<Transform>())
                if(bone.name == "WingLeft" || bone.name == "WingRight") wings[bone] = bone.localRotation;
        }
        void LateUpdate()
        {
            if (IsOwner && !Active && baseFov > 0) motor.PlayerCamera.fieldOfView = baseFov;
            if (Item != shown) CreateVisuals();
            if (visualAction != action.Value)
            {
                byte previous = visualAction;
                visualAction = action.Value; animationAt = Time.time;
                if (visualAction == 1) GameAudio.Play(SoundCue.Reload, transform.position);
                else if (visualAction == 2) GameAudio.Play(SoundCue.Place, transform.position,.5f);
                else if (previous == 1 && rounds.Value > 0) GameAudio.Play(SoundCue.ReloadReady, transform.position);
            }
            if (view == null || world == null) return;
            view.gameObject.SetActive(Active); world.gameObject.SetActive(Active);
            aimBlend = Mathf.MoveTowards(aimBlend, Active && (IsOwner ? sentAim : aiming.Value) ? 1 : 0, Time.deltaTime * 7);
            if (IsOwner)
            {
                if (baseFov <= 0) baseFov = motor.PlayerCamera.fieldOfView;
                motor.PlayerCamera.fieldOfView = Mathf.Lerp(baseFov, Item == InventoryItem.Musket ? scopeFov : 55, aimBlend);
            }
            float t = Time.time - animationAt;
            float recoil = Mathf.Exp(-(Time.time - recoilAt) * 15);
            Vector3 position = Vector3.Lerp(new Vector3(.22f,-.25f,.45f), new Vector3(0,Item == InventoryItem.Musket ? -.205f : -.1535f,.3f), aimBlend);
            Vector3 rotation = new Vector3(-recoil * (Item == InventoryItem.DoubleBarrel ? 20 : 15), recoil*1.5f, -recoil*2);
            position.z -= recoil * .13f;
            if (visualAction == 1)
            {
                float amount = Mathf.Sin(Mathf.Clamp01(t / (Item == InventoryItem.DoubleBarrel ? 3.6f : 3.2f)) * Mathf.PI);
                rotation += new Vector3(-35, -15, 25) * amount;
                position += new Vector3(.04f,-.13f,-.1f) * amount;
            }
            if (visualAction == 2)
            {
                float amount = Mathf.SmoothStep(0,1,Mathf.Min(t / .45f, (2.2f-t) / .35f));
                position = Vector3.Lerp(position,new Vector3(.07f,.18f,.34f),amount);
                rotation = new Vector3(-115,0,-12)*amount;
            }
            view.localPosition = position; view.localRotation = Quaternion.Euler(rotation);
            float pitch = Mathf.Asin(Mathf.Clamp(-direction.Value.y,-1,1))*Mathf.Rad2Deg;
            world.localPosition = new Vector3(position.x,1.4f+position.y,position.z);
            world.localRotation = Quaternion.Euler(rotation + new Vector3(aiming.Value ? pitch : 0,0,0));
            foreach(var wing in wings)
                wing.Key.localRotation = wing.Value * Quaternion.Euler(0,(wing.Key.name == "WingLeft" ? 1 : -1) * Mathf.Sin(Time.time*(visualAction==3 ? 18 : 3)) * (visualAction==3 ? 60 : 5),0);
        }
        void BeforeCamera(ScriptableRenderContext context, Camera camera)
        {
            bool first = IsOwner && camera == motor.PlayerCamera && !motor.IsThirdPerson;
            if(viewRenderers!=null) foreach(var r in viewRenderers) if(r!=null) r.forceRenderingOff=!Active || !first || Scoped;
            if(worldRenderers!=null) foreach(var r in worldRenderers) if(r!=null) r.forceRenderingOff=!Active || first;
        }
        void AfterCamera(ScriptableRenderContext context, Camera camera) { if(viewRenderers!=null) foreach(var r in viewRenderers) if(r!=null) r.forceRenderingOff=true; }
        void OnGUI()
        {
            if(IsOwner && WaterRunning) PirateHudStyle.Label(new Rect(28,140,270,30),"Хождение по воде: "+waterRunRemaining.Value+" с",PirateHudStyle.Paper);
            if (!IsOwner || !Active || !motor.InputActive) return;
            if(Scoped) DrawScope();
            string hint = action.Value == 1 ? "Перезарядка…" : action.Value == 2 ? "Пьём…" : Firearm ? rounds.Value+" / ∞   ЛКМ — огонь • ПКМ — прицел • R — зарядить" : Item == InventoryItem.GrapplingHook ? "ЛКМ — забросить крюк (35 м) · W/S — подъём / спуск" : Item == InventoryItem.Wine ? "Удерживать ЛКМ — бег по воде на 60 с" : "ЛКМ — выпустить попугая • цель до 100 м • 50 урона";
            PirateHudStyle.Label(new Rect(Screen.width/2f-300,Screen.height-175,600,32),hint,PirateHudStyle.Paper);
        }
        void DrawScope()
        {
            if(scopeMask==null)
            {
                scopeMask=new Texture2D(256,256,TextureFormat.RGBA32,false);
                for(int y=0;y<256;y++) for(int x=0;x<256;x++)
                { float radius=Vector2.Distance(new Vector2(x+.5f,y+.5f),new Vector2(128,128))/128; scopeMask.SetPixel(x,y,new Color(0,0,0,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.94f,1,radius)))); }
                scopeMask.Apply();
            }
            int depth=GUI.depth; GUI.depth=-50;
            Color color=GUI.color; GUI.color=Color.black;
            float size=Mathf.Min(Screen.width,Screen.height)*.95f,x0=(Screen.width-size)*.5f,y0=(Screen.height-size)*.5f;
            GUI.DrawTexture(new Rect(0,0,Screen.width,y0),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0,y0+size,Screen.width,Screen.height),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0,y0,x0,size),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x0+size,y0,Screen.width,size),Texture2D.whiteTexture);
            GUI.color=Color.white; GUI.DrawTexture(new Rect(x0,y0,size,size),scopeMask);
            GUI.color=Color.black;
            GUI.DrawTexture(new Rect(Screen.width*.5f-1,y0+size*.1f,2,size*.8f),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x0+size*.1f,Screen.height*.5f-1,size*.8f,2),Texture2D.whiteTexture);
            GUI.color=color; GUI.depth=depth;
        }
        void OnDestroy() { if(scopeMask!=null) Destroy(scopeMask); if(view!=null) Destroy(view.gameObject); if(world!=null) Destroy(world.gameObject); }
    }
}
