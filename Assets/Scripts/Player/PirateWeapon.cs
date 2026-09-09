using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using PirateSlop.Networking;

namespace PirateSlop
{
    public interface IWeaponTarget { void ReceiveWeaponHit(float damage, GameObject attacker); }

    [DefaultExecutionOrder(30)]
    public sealed partial class PirateWeapon : MonoBehaviour
    {
        public Transform WorldPivot, ViewPivot, WorldMuzzle, ViewMuzzle;
        public Material EffectMaterial;
        public FirearmSettings Firearm = new();
        bool predictedShot;
        float nextLocalShot;
        AdvancedPlayerController motor;
        CannonHands hands;
        NetworkWeapon network;
        PlayerInventory inventory;
        FirearmHandling handling;
        int shotSequence;
        readonly FirearmPrediction prediction=new();
        DirectShipControls controls;
        bool Equipped => (motor == null || !(motor.IsSwimming || motor.IsClimbing || motor.IsDead)) && (inventory == null || inventory.PistolSelected || inventory.SabreSelected) && (controls == null || !controls.IsDragging);
        bool loaded = true, reloading;
        float reloadUntil, nextAttack, recoil, stab, lift;
        Quaternion worldRest, viewRest;
        Vector3 worldPosition, viewPosition;
        Renderer[] viewRenderers;
        Renderer[] worldRenderers;
        public bool Loaded => loaded && !predictedShot;
        public bool Reloading => reloading;
        bool Networked => network != null && (network.IsClientInitialized || network.IsServerInitialized);
        void Awake()
        {
            motor = GetComponent<AdvancedPlayerController>(); hands = GetComponent<CannonHands>(); network = GetComponent<NetworkWeapon>();
            inventory = GetComponent<PlayerInventory>();
            handling=GetComponent<FirearmHandling>();
            if(handling!=null && handling.Pistol!=null) Firearm=handling.Pistol.Ballistics;
            controls = GetComponent<DirectShipControls>();
            worldRest = WorldPivot.localRotation; viewRest = ViewPivot.localRotation;
            worldPosition = WorldPivot.localPosition; viewPosition = ViewPivot.localPosition;
            viewRenderers = ViewPivot.GetComponentsInChildren<Renderer>(true);
            worldRenderers = WorldPivot.GetComponentsInChildren<Renderer>(true);
            SetupSeparateWeapons();
        }
        void OnEnable() { RenderPipelineManager.beginCameraRendering += BeforeCamera; RenderPipelineManager.endCameraRendering += AfterCamera; }
        void OnDisable() { RenderPipelineManager.beginCameraRendering -= BeforeCamera; RenderPipelineManager.endCameraRendering -= AfterCamera; }
        void BeforeCamera(ScriptableRenderContext context, Camera camera)
        {
            if (viewRenderers == null) return;
            bool show = Equipped && camera == motor.PlayerCamera && motor.InputActive && !motor.IsThirdPerson && !motor.LocomotionLocked && (hands == null || !hands.HasHeldBall);
            foreach(var r in viewRenderers) if(r != null) r.forceRenderingOff = !show;
            bool hideWorld = !Equipped || (camera == motor.PlayerCamera && !motor.IsThirdPerson);
            foreach(var r in worldRenderers) if(r != null) r.forceRenderingOff = hideWorld;
            ShowSeparateWeapons(camera, show, hideWorld);
        }
        void AfterCamera(ScriptableRenderContext context, Camera camera)
        {
            if(viewRenderers != null) foreach(var r in viewRenderers) if(r != null) r.forceRenderingOff = true;
            if(worldRenderers != null) foreach(var r in worldRenderers) if(r != null) r.forceRenderingOff = !Equipped;
            HideSeparateView();
        }
        void Update()
        {
            if (!Networked) TickAuthority();
            if (controls != null && controls.BlocksPrimary) return;
            if (!Equipped || !motor.InputActive || motor.LocomotionLocked || (hands != null && hands.HasHeldBall)) return;
            var mouse = Mouse.current; var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame) Request(1);
            if (mouse == null) return;
            if (mouse.leftButton.wasPressedThisFrame && !inventory.InteractionUsed && (hands == null || !hands.CanPickUpBall()) && (SabreEquipped || handling==null || handling.Ready)) Request(SabreEquipped ? (byte)2 : (byte)0);
        }
        void Request(byte action)
        {
            if (action == 0 && !loaded && !reloading) { GameAudio.Play(SoundCue.DryFire, transform.position);return; }
            Vector3 direction = motor.AimDirection;
            Vector3 eyeOffset = motor.PlayerCamera.transform.position - transform.position;
            if (Networked)
            {
                if (!network.IsOwner) return;
                if (action == 0 && !network.IsServerInitialized)
                {
                    if (!loaded || reloading || predictedShot || Time.time < nextLocalShot) return;
                    predictedShot = true;
                    nextLocalShot = Time.time + Firearm.ShotInterval;
                    ShowMuzzle(direction);
                    prediction.Fire(gameObject,handling.Pistol,motor.PlayerCamera.transform.position,VisibleMuzzle,direction,handling.Aiming,shotSequence+1,EffectMaterial);
                }
                network.Request(action, direction, eyeOffset,handling!=null && handling.Aiming,action==0?++shotSequence:shotSequence);
            }
            else Act(action, direction, eyeOffset,handling!=null && handling.Aiming);
        }
        public void TickAuthority()
        {
            TickSabre();
            if (reloading && (!Equipped || SabreEquipped || motor.LocomotionLocked || (hands != null && hands.HasHeldBall))) reloading = false;
            if (reloading && Time.time >= reloadUntil) { reloading = false; loaded = true; }
        }
        public bool Act(byte action, Vector3 direction, Vector3 eyeOffset,bool aimed=false,int seed=-1)
        {
            if (!Equipped || motor.IsDead || motor.LocomotionLocked || (hands != null && hands.HasHeldBall) || !float.IsFinite(direction.sqrMagnitude) || direction.sqrMagnitude < .5f) return false;
            if ((action == 2) != SabreEquipped) return false;
            if (action == 1)
            {
                if (loaded || reloading || Time.time < nextAttack) return false;
                reloading = true; reloadUntil = Time.time + Firearm.ReloadDuration; return true;
            }
            if (action > 2 || Time.time < nextAttack || (action == 0 && (!loaded || reloading))) return false;
            if (action == 0) { loaded = false; nextAttack = Time.time + Firearm.ShotInterval; }
            else { reloading = false; nextAttack = Time.time + .9f; }
            direction.Normalize();
            if (action == 2)
            {
                BeginSabre(direction);
                if (Networked && network.IsServerInitialized) network.PublishAttack(action, transform.position);
                else ShowAttack(action, transform.position);
                return true;
            }
            Vector3 bodyEye = transform.position + Vector3.up * (motor.IsCrouched ? .75f : 1.65f);
            Vector3 eye = bodyEye;
            if (float.IsFinite(eyeOffset.sqrMagnitude) && eyeOffset.sqrMagnitude <= 16f)
                eye = transform.position + eyeOffset;
            if (FirearmTrace.Cast(gameObject, bodyEye, eye, out var eyeBlock))
                eye = eyeBlock.point + (bodyEye - eye).normalized * .02f;
            var definition=handling.Pistol;
            Vector3 muzzle = definition.MuzzlePoint(eye,direction,aimed);
            var shot=FirearmCombat.Resolve(gameObject,definition,eye,muzzle,direction,aimed,seed<0?++shotSequence:seed,true)[0];
            ShowShot(shot);
            if (Networked && network.IsServerInitialized) network.PublishShot(shot);
            return true;
        }
        public void SetState(bool hasRound, bool isReloading) { loaded = hasRound; reloading = isReloading; }
        public void ShowAttack(byte action, Vector3 end)
        {
            if (action == 2) { visualAttackStarted = Time.time; GetComponent<SabreAnimation>()?.PlaySlash(); }
            if (action == 2) GameAudio.Play(SoundCue.Knife, transform.position);
            if(action == 2) stab = 1;
        }
        Vector3 VisibleMuzzle => motor.PlayerCamera.enabled && !motor.IsThirdPerson ? ViewMuzzle.position : WorldMuzzle.position;
        void ShowMuzzle(Vector3 direction)
        {
            recoil = 1f; fireStarted = Time.time;
            if(handling!=null) handling.Fire(motor.PlayerCamera.enabled && !motor.IsThirdPerson?ViewMuzzle:WorldMuzzle,direction);
        }
        public void RejectPredictedShot() { predictedShot = false; }
        public void ShowShot(FirearmShot shot)
        {
            if(predictedShot) { predictedShot=false;prediction.Confirm(new[]{shot});return; }
            ShowMuzzle((shot.End - shot.Start).normalized);
            var definition=handling.Pistol;
            Vector3 origin=VisibleMuzzle;
            if(FirearmTrace.Cast(gameObject,shot.Start,origin,out var obstruction)) origin=shot.Start;
            PistolBullet.Spawn(origin, shot, EffectMaterial,definition.TracerWidth,true,definition.TracerSpeed);
        }
        void LateUpdate()
        {
            AnimateSeparateWeapons();
        }
        void OnGUI()
        {
            if (GetComponent<PlayerHud>() != null) return;
            if (!Equipped || !motor.InputActive || motor.LocomotionLocked || (hands != null && hands.HasHeldBall)) return;
            GUI.Label(new Rect(Screen.width-290,Screen.height-65,280,55),SabreEquipped ? "Сабля · ЛКМ — удар" : reloading ? "Перезарядка…" : (loaded ? "Пистолет: 1 / ∞" : "Пистолет: 0 / ∞ — R") + "\nЛКМ — выстрел · R — перезарядка");
        }
    }
}


