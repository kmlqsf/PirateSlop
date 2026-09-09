using UnityEngine;

namespace PirateSlop
{
    [CreateAssetMenu(menuName="PirateSlop/Firearm Definition")]
    public sealed class FirearmDefinition : ScriptableObject
    {
        public FirearmSettings Ballistics = new();
        public SoundCue Sound = SoundCue.Pistol;
        public AudioClip[] ShotClips;
        [Range(0,1)] public float ShotVolume=.9f;
        public float AudibleDistance=180;
        public int Pellets = 1, Capacity = 1;
        public float DamageCap = 100, HipSpread = 1.2f, AimSpread = .08f, MovingSpread = .5f;
        public float AimSeconds = .18f, AimFov = 55, CameraKick = 1.4f, CameraYaw = .25f;
        public float KickDegrees = 12, KickDistance = .075f, KickRecovery = .28f, FlashPower = 1;
        public float TracerWidth = .022f, TracerSpeed = 450;
        public Vector3 HipPosition = new(.22f,-.25f,.45f), AimPosition = new(0,-.15f,.3f);
        public Vector3 MuzzleOffset = new(0,.075f,.75f);
        public float ShooterKnockback;
        public bool Scope;
        public Vector3 MuzzlePoint(Vector3 eye,Vector3 forward,bool aimed) => eye+Quaternion.LookRotation(forward)*((aimed?AimPosition:HipPosition)+MuzzleOffset);
        void OnValidate()
        {
            Pellets=Mathf.Clamp(Pellets,1,32);Capacity=Mathf.Max(1,Capacity);
            HipSpread=Mathf.Clamp(HipSpread,0,25);AimSpread=Mathf.Clamp(AimSpread,0,25);MovingSpread=Mathf.Max(0,MovingSpread);
            AimSeconds=Mathf.Max(.08f,AimSeconds);AimFov=Mathf.Clamp(AimFov,10,90);KickRecovery=Mathf.Max(.05f,KickRecovery);
            DamageCap=Mathf.Max(0,DamageCap);TracerWidth=Mathf.Clamp(TracerWidth,.005f,.08f);TracerSpeed=Mathf.Max(50,TracerSpeed);
            if(Ballistics==null) Ballistics=new FirearmSettings();
            Ballistics.Range=Mathf.Clamp(Ballistics.Range,1,500);Ballistics.ShotInterval=Mathf.Max(.06f,Ballistics.ShotInterval);Ballistics.ReloadDuration=Mathf.Max(.2f,Ballistics.ReloadDuration);
        }
        public Vector3 PelletDirection(Vector3 forward, int index, int seed, float spread)
        {
            if(spread<=0) return forward.normalized;
            float phase=(seed&1023)*.61803399f;
            float radius=Pellets<=1 ? .65f : Mathf.Sqrt((index+.5f)/Pellets);
            float angle=index*2.39996323f+phase;
            float tangent=Mathf.Tan(spread*Mathf.Deg2Rad)*radius;
            return Quaternion.LookRotation(forward)*new Vector3(Mathf.Cos(angle)*tangent,Mathf.Sin(angle)*tangent,1).normalized;
        }
    }
    public static class FirearmCombat
    {
        public static FirearmShot[] Resolve(GameObject shooter,FirearmDefinition definition,Vector3 eye,Vector3 muzzle,Vector3 direction,bool aiming,int seed,bool damage)
        {
            int count=Mathf.Clamp(definition.Pellets,1,32);
            var shots=new FirearmShot[count];
            var totals=new System.Collections.Generic.Dictionary<CombatHealth,float>();
            var others=new System.Collections.Generic.Dictionary<IWeaponTarget,float>();
            var motor=shooter.GetComponent<AdvancedPlayerController>();
            float spread=aiming ? definition.AimSpread : definition.HipSpread;
            if(motor!=null) spread+=definition.MovingSpread*Mathf.Clamp01(motor.PlanarSpeed/8)*(aiming?.3f:1);
            var settings=definition.Ballistics;
            for(int i=0;i<count;i++)
            {
                Vector3 ray=definition.PelletDirection(direction,i,seed,spread);
                Vector3 barrel=muzzle+(count>1 ? Quaternion.LookRotation(direction)*Vector3.right*(i<count/2 ? -.049f:.049f):Vector3.zero);
                shots[i]=FirearmTrace.Resolve(shooter,eye,barrel,ray,settings.Range,out var hit);
                if(!damage || hit.collider==null) continue;
                float distance=Vector3.Distance(eye,hit.point);
                float falloff=Mathf.InverseLerp(settings.FalloffStart,settings.FalloffEnd,distance);
                var health=hit.collider.GetComponentInParent<CombatHealth>();
                if(health!=null)
                {
                    var body=health.GetComponent<CharacterController>();
                    bool head=body!=null && health.transform.InverseTransformPoint(hit.point).y>=body.center.y+body.height*.5f-.3f;
                    float amount=Mathf.Lerp(head?settings.NearHeadDamage:settings.NearDamage,head?settings.FarHeadDamage:settings.FarDamage,falloff);
                    totals.TryGetValue(health,out float previous);totals[health]=previous+amount;
                }
                else foreach(var component in hit.collider.GetComponentsInParent<MonoBehaviour>())
                    if(component is IWeaponTarget target)
                    {others.TryGetValue(target,out float previous);others[target]=previous+Mathf.Lerp(settings.NearDamage,settings.FarDamage,falloff);break;}
            }
            foreach(var hit in totals) hit.Key.Damage(Mathf.Min(definition.DamageCap,hit.Value),shooter);
            foreach(var hit in others) hit.Key.ReceiveWeaponHit(Mathf.Min(definition.DamageCap,hit.Value),shooter);
            return shots;
        }
    }
}
