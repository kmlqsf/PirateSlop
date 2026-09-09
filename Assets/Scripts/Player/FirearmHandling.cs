using UnityEngine;
using UnityEngine.InputSystem;
using PirateSlop.Networking;

namespace PirateSlop
{
    [DefaultExecutionOrder(20)]
    public sealed class FirearmHandling : MonoBehaviour
    {
        public FirearmDefinition Pistol, Musket, Shotgun;
        AdvancedPlayerController motor;
        PlayerInventory inventory;
        PirateWeapon weapon;
        NetworkEquipment equipment;
        NetworkPlayer player;
        DirectShipControls controls;
        float lastShot=-10, aim, baselineFov, nextReady, bloom, lastYaw, lastPitch;
        Vector2 sway;
        bool heldAim;
        InventoryItem previous=InventoryItem.None;
        public FirearmDefinition Definition => inventory.PistolSelected ? Pistol : inventory.ItemAt(inventory.SelectedSlot)==InventoryItem.Musket ? Musket : inventory.ItemAt(inventory.SelectedSlot)==InventoryItem.DoubleBarrel ? Shotgun : null;
        public bool Local => player==null || player.IsOwner;
        public bool Available => Definition!=null && !ShipSpyglassView.IsViewing && !motor.IsDead && !motor.IsSwimming && !motor.IsClimbing && !motor.LocomotionLocked && !inventory.HandsOccupied && !GetComponent<CannonHands>().HasHeldBall && (inventory.Fishing==null || (!inventory.Fishing.IsFishing && !inventory.Fishing.IsEating)) && (controls==null || !controls.BlocksPrimary);
        public bool Aiming => Local && Available && heldAim;
        public float AimBlend => aim;
        public bool Ready => Time.time>=nextReady;
        public float ReticleRadius => Definition==null?3:Mathf.Clamp(Screen.height*.5f*Mathf.Tan(((Aiming?Definition.AimSpread:Definition.HipSpread)+Definition.MovingSpread*Mathf.Clamp01(motor.PlanarSpeed/8)*(Aiming?.3f:1))*Mathf.Deg2Rad)/Mathf.Tan(motor.PlayerCamera.fieldOfView*.5f*Mathf.Deg2Rad),3,65)+bloom*8;
        public Vector3 PosePosition { get; private set; }
        public Vector3 PoseRotation { get; private set; }
        void Awake()
        {
            motor=GetComponent<AdvancedPlayerController>();inventory=GetComponent<PlayerInventory>();weapon=GetComponent<PirateWeapon>();equipment=GetComponent<NetworkEquipment>();player=GetComponent<NetworkPlayer>();controls=GetComponent<DirectShipControls>();
        }
        void Update()
        {
            if(!Local) return;
            var selected=inventory.ItemAt(inventory.SelectedSlot);
            if(selected!=previous){previous=selected;nextReady=Time.time+.18f;aim=0;heldAim=false;lastYaw=motor.AimEuler.y;lastPitch=motor.AimEuler.x;sway=Vector2.zero;}
            bool reload=inventory.PistolSelected ? weapon.Reloading : equipment!=null && equipment.IsBusy;
            heldAim=Available && !reload && motor.InputActive && Mouse.current!=null && Mouse.current.rightButton.isPressed;
            if(heldAim && motor.IsThirdPerson) motor.SetThirdPerson(false);
            aim=Mathf.MoveTowards(aim,heldAim?1:0,Time.deltaTime/Mathf.Max(.08f,Definition!=null?Definition.AimSeconds:.18f));
            bloom=Mathf.MoveTowards(bloom,0,Time.deltaTime*5);
            if(Definition==null || !Available) { RestoreFov();PosePosition=PoseRotation=Vector3.zero;return; }
            if(baselineFov<=0) baselineFov=motor.PlayerCamera.fieldOfView;
            float fov=Definition.Scope && equipment!=null ? equipment.ScopeFov : Definition.AimFov;
            motor.PlayerCamera.fieldOfView=Mathf.Lerp(baselineFov,fov,Mathf.SmoothStep(0,1,aim));
            motor.AimSensitivityScale=Mathf.Lerp(1,Mathf.Tan(fov*Mathf.Deg2Rad*.5f)/Mathf.Tan(baselineFov*Mathf.Deg2Rad*.5f),aim);
            var angles=motor.AimEuler;
            Vector2 delta=new(Mathf.DeltaAngle(lastYaw,angles.y),Mathf.DeltaAngle(lastPitch,angles.x));lastYaw=angles.y;lastPitch=angles.x;
            sway=Vector2.Lerp(sway,Vector2.ClampMagnitude(delta,3),1-Mathf.Exp(-12*Time.deltaTime));
            float age=Time.time-lastShot;
            float kick=age<.025f?Mathf.SmoothStep(0,1,age/.025f):1-Mathf.SmoothStep(0,1,(age-.025f)/Definition.KickRecovery);
            float movement=Mathf.Clamp01(motor.PlanarSpeed/8)*(1-aim*.92f);
            float cycle=Time.time*10;
            PosePosition=new Vector3(-sway.x*.0015f,Mathf.Sin(cycle)*.004f*movement,-Definition.KickDistance*kick);
            PoseRotation=new Vector3(-Definition.KickDegrees*kick+sway.y*.12f,-sway.x*.18f,Mathf.Sin(cycle*.5f)*.65f*movement)*(1-aim*.55f);
        }
        public void Fire(Transform muzzle,Vector3 direction,bool cameraRecoil=true)
        {
            var definition=Definition;if(definition==null) return;
            lastShot=Time.time;bloom=1;
            Vector3 point=muzzle!=null?muzzle.position:motor.PlayerCamera.transform.position+direction*.65f;
            GameAudio.Firearm(definition,point);
            FirearmVfx.Fire(point,direction,definition.FlashPower);
            if(Local && cameraRecoil) motor.AddAimRecoil(definition.CameraKick*(Aiming?.7f:1),Random.Range(-definition.CameraYaw,definition.CameraYaw));
        }
        void RestoreFov()
        {
            if(baselineFov>0){motor.PlayerCamera.fieldOfView=baselineFov;baselineFov=0;}
            motor.AimSensitivityScale=1;
        }
        void OnDisable(){if(motor!=null && Local) RestoreFov();}
    }
}
