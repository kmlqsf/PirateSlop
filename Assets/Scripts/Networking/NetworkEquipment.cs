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
        FirearmHandling handling;
        int localSequence, predictedSequence=-1;
        float nextLocalFire;
        bool pendingShot;
        readonly FirearmPrediction prediction=new();
        public bool IsBusy => action.Value!=0;
        public float ScopeFov => scopeFov;
        public int LoadedRounds => pendingShot?0:rounds.Value;
        public bool IsReloading => action.Value==1;
        public float ShotAge => Time.time - recoilAt;
        public bool AnimationAiming => IsOwner ? sentAim : aiming.Value;
        public Vector3 AnimationDirection => direction.Value;
        float ReloadSeconds => Firearm && handling!=null && handling.Definition!=null ? handling.Definition.Ballistics.ReloadDuration : 3.2f;
        CannonHands hands;
        Transform view, world;
        Renderer[] viewRenderers, worldRenderers;
        readonly System.Collections.Generic.Dictionary<Transform, Quaternion> wings = new();
        InventoryItem shown = InventoryItem.None;
        int actionSlot = -1, previousSlot = -1;
        float until, nextShot, nextAim, animationAt, recoilAt = -10, aimBlend;
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
                float phase = Mathf.Clamp01((Time.time-animationAt)/ReloadSeconds);
                float reach = Mathf.Sin(phase*Mathf.PI);
                return new Vector3(-.045f,-.04f+reach*.13f,.15f+reach*.5f);
            }
        }
        public Vector3 GripOffset => Firearm ? new Vector3(0, -.03f, 0) : new Vector3(.03f, .06f, 0);
        int CapacityFor(InventoryItem item) => item==InventoryItem.DoubleBarrel ? handling.Shotgun.Capacity : item==InventoryItem.Musket ? handling.Musket.Capacity : 1;
        public void ResetSlot(int slot, InventoryItem item) { if(IsServerInitialized) ammunition[slot] = CapacityFor(item); }
        void Awake()
        {
            inventory = GetComponent<PlayerInventory>(); motor = GetComponent<AdvancedPlayerController>();
            network = GetComponent<NetworkWeapon>(); hands = GetComponent<CannonHands>();
            handling=GetComponent<FirearmHandling>();
        }
        void OnEnable() { RenderPipelineManager.beginCameraRendering += BeforeCamera; RenderPipelineManager.endCameraRendering += AfterCamera; }
        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeforeCamera; RenderPipelineManager.endCameraRendering -= AfterCamera;
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
                    if (action.Value == 1) { ammunition[actionSlot] = CapacityFor(Item); rounds.Value = ammunition[actionSlot]; }
                    if (action.Value == 2 && network.ConsumeEquipment(actionSlot, InventoryItem.Wine)) { waterRunUntil = Time.time+60; waterRunRemaining.Value = 60; }
                    action.Value = 0;
                }
            }
            if (!IsOwner) return;
            var mouse = Mouse.current; var keyboard = Keyboard.current;
            if (Item == InventoryItem.GrapplingHook && mouse != null && mouse.leftButton.wasReleasedThisFrame) network.ReleaseGrapple();
            bool can = CanUse && motor.InputActive && !PlayerInventory.LootWindowOpen;
            bool aim = can && Firearm && action.Value == 0 && handling!=null && handling.Aiming;
            if (aim && motor.IsThirdPerson) motor.SetThirdPerson(false);
            if (aim && Item == InventoryItem.Musket && mouse != null)
                scopeFov = Mathf.Clamp(scopeFov-mouse.scroll.ReadValue().y*.04f,12,38);
            bool changedAim=sentAim!=aim;sentAim=aim;
            if (changedAim || (Time.time >= nextAim && aim))
            {
                nextAim = Time.time + .1f; sentAim = aim;
                AimServerRpc(aim, motor.AimDirection);
            }
            if (!can || mouse == null) { if (action.Value == 2) CancelServerRpc(); return; }
            Vector3 eyeOffset = motor.PlayerCamera.transform.position-transform.position;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame && Firearm) UseServerRpc(1, motor.AimDirection, eyeOffset,++localSequence,aim);
            if (mouse.leftButton.wasPressedThisFrame && !pendingShot && !inventory.InteractionUsed && !hands.CanPickUpBall() && Time.time>=nextLocalFire && (!Firearm || handling.Ready))
            {
                int sequence=++localSequence;
                if (Item == InventoryItem.GrapplingHook) network.BeginLocalGrapple();
                Vector3 forward=motor.AimDirection;
                if(Firearm)
                {
                    var definition=handling.Definition;
                    if(rounds.Value<definition.Capacity) { GameAudio.Play(SoundCue.DryFire,transform.position);nextLocalFire=Time.time+.2f;return; }
                    if(action.Value!=0) return;
                    nextLocalFire=Time.time+definition.Ballistics.ShotInterval;
                    if(!IsServerInitialized)
                    {
                        pendingShot=true;predictedSequence=sequence;recoilAt=Time.time;
                        var muzzle=MuzzleForView();handling.Fire(muzzle,forward);
                        Vector3 eye=motor.PlayerCamera.transform.position;
                        prediction.Fire(gameObject,definition,eye,muzzle!=null?muzzle.position:definition.MuzzlePoint(eye,forward,aim),forward,aim,sequence,GetComponent<PirateWeapon>().EffectMaterial);
                    }
                }
                UseServerRpc(0,forward,eyeOffset,sequence,aim);
            }
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
        void UseServerRpc(byte request, Vector3 forward, Vector3 eyeOffset,int sequence,bool aimed)
        {
            try { UseAuthority(request,forward,eyeOffset,sequence,aimed); }
            finally { if(request==0) ShotAcknowledgedTargetRpc(Owner); }
        }
        [TargetRpc]
        void ShotAcknowledgedTargetRpc(FishNet.Connection.NetworkConnection connection) { pendingShot=false; }
        void UseAuthority(byte request, Vector3 forward, Vector3 eyeOffset,int sequence,bool aimed)
        {
            if (!CanUse || action.Value != 0 || !float.IsFinite(forward.sqrMagnitude) || forward.sqrMagnitude < .5f) return;
            if (Item == InventoryItem.GrapplingHook) { if (request == 0) network.ThrowGrapple(forward, eyeOffset); return; }
            if (Time.time < nextShot) return;
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
            var definition=handling!=null?handling.Definition:null;
            if(definition==null) return;
            int capacity = definition.Capacity;
            if (request == 1)
            {
                if (ammunition[slot] < capacity) BeginAction(1, definition.Ballistics.ReloadDuration, slot);
                return;
            }
            if (request != 0) return;
            if (ammunition[slot] < capacity) { EmptyTargetRpc(Owner); return; }
            aiming.Value=aimed;
            ammunition[slot] -= capacity; rounds.Value = ammunition[slot]; nextShot = Time.time + definition.Ballistics.ShotInterval;
            Vector3 eye = transform.position + Vector3.up * (motor.IsCrouched ? .8f : 1.6f);
            if(float.IsFinite(eyeOffset.sqrMagnitude) && eyeOffset.sqrMagnitude < 4)
            {
                Vector3 cameraEye = transform.position+eyeOffset;
                if(!FirearmTrace.Cast(gameObject,eye,cameraEye,out var block)) eye=cameraEye;
            }
            Vector3 muzzle = definition.MuzzlePoint(eye,forward,aimed);
            var shots=FirearmCombat.Resolve(gameObject,definition,eye,muzzle,forward,aimed,sequence,true);
            if(definition.ShooterKnockback>0) motor.ApplyKnockback(-Vector3.ProjectOnPlane(forward,Vector3.up).normalized*definition.ShooterKnockback+Vector3.up*.8f);
            ShotObserversRpc(shots,sequence,Item);
        }
        void BeginAction(byte value, float duration, int slot) { actionSlot = slot; until = Time.time + duration; action.Value = value; aiming.Value = false; }
        [TargetRpc] void EmptyTargetRpc(FishNet.Connection.NetworkConnection connection) => GameAudio.Play(SoundCue.DryFire, transform.position);
        Transform MuzzleForView()
        {
            var root=IsOwner && !motor.IsThirdPerson ? view : world;
            if(root==null) return null;
            return root.Find("Muzzle");
        }
        [ObserversRpc(RunLocally = true)]
        void ShotObserversRpc(FirearmShot[] shots,int sequence,InventoryItem item)
        {
            if(shots==null || shots.Length==0) return;
            var definition=item==InventoryItem.DoubleBarrel ? handling.Shotgun : handling.Musket;
            bool predicted=IsOwner && sequence==predictedSequence;
            if(predicted) { prediction.Confirm(shots);predictedSequence=-1;return; }
            var muzzle=MuzzleForView();
            Vector3 start=muzzle!=null && Item==item ? muzzle.position : shots[0].Start;
            if(FirearmTrace.Cast(gameObject,shots[0].Start,start,out var obstruction)) start=shots[0].Start;
            recoilAt=Time.time;
            if(Item==item) handling.Fire(muzzle,(shots[0].End-shots[0].Start).normalized);
            else { GameAudio.Firearm(definition,start);FirearmVfx.Fire(start,(shots[0].End-shots[0].Start).normalized,definition.FlashPower); }
            for(int i=0;i<shots.Length;i++)
            {
                var shot=shots[i];
                Vector3 origin=start+(shots.Length>1 ? transform.right*(i<shots.Length/2 ? -.049f:.049f):Vector3.zero);
                PistolBullet.Spawn(origin,shot,GetComponent<PirateWeapon>().EffectMaterial,definition.TracerWidth,shots.Length==1 || i%4==0,definition.TracerSpeed);
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
                if(Firearm)
                {
                    var socket=new GameObject("Muzzle").transform;socket.SetParent(root,false);
                    socket.localPosition=handling.Definition.MuzzleOffset;
                }
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
            if (Item != shown) CreateVisuals();
            if (visualAction != action.Value)
            {
                byte previous = visualAction;
                visualAction = action.Value; animationAt = Time.time;
                if (visualAction == 1) GameAudio.Play(SoundCue.Reload, transform.position);
                else if (visualAction == 2) GameAudio.Play(SoundCue.BottleOpen, transform.position);
                else if (previous == 2) GameAudio.Play(SoundCue.BottleClose, transform.position);
                else if (previous == 1 && rounds.Value > 0) GameAudio.Play(SoundCue.ReloadReady, transform.position);
            }
            if (view == null || world == null) return;
            view.gameObject.SetActive(Active); world.gameObject.SetActive(Active);
            aimBlend = Mathf.MoveTowards(aimBlend, Active && (IsOwner ? sentAim : aiming.Value) ? 1 : 0, Time.deltaTime * 7);
            float t = Time.time - animationAt;
            float recoil = Mathf.Exp(-(Time.time - recoilAt) * 15);
            var definition=Firearm && handling!=null?handling.Definition:null;
            Vector3 position = definition!=null ? Vector3.Lerp(definition.HipPosition,definition.AimPosition,IsOwner?handling.AimBlend:aimBlend) : new Vector3(.22f,-.25f,.45f);
            Vector3 rotation = new Vector3(-recoil * (Item == InventoryItem.DoubleBarrel ? 20 : 15), recoil*1.5f, -recoil*2);
            if(!IsOwner) position.z -= recoil * .13f;
            if(IsOwner && definition!=null) { rotation=handling.PoseRotation;position+=handling.PosePosition; }
            if (visualAction == 1)
            {
                float amount = Mathf.Sin(Mathf.Clamp01(t / ReloadSeconds) * Mathf.PI);
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
            if(Firearm) return;
            string hint = action.Value == 2 ? "Пьём…" : Item == InventoryItem.GrapplingHook ? "Удерживать ЛКМ — крюк (35 м) · Отпустить ЛКМ — разорвать зацеп" : Item == InventoryItem.Wine ? "Удерживать ЛКМ — бег по воде на 60 с" : "ЛКМ — выпустить попугая • цель до 100 м • 50 урона";
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

