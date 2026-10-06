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
        public Vector3 HipPosition = new(.28f,-.28f,.55f), AimPosition = new(0,-.18f,.4f);
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
        public static float Spread(GameObject shooter, FirearmDefinition definition, bool aiming)
        {
            var motor = shooter.GetComponent<AdvancedPlayerController>();
            float spread = aiming ? definition.AimSpread : definition.HipSpread;
            if (motor != null) spread += definition.MovingSpread * Mathf.Clamp01(motor.PlanarSpeed / 8) * (aiming ? .3f : 1);
            return spread;
        }
        public static FirearmShot[] Resolve(GameObject shooter,FirearmDefinition definition,Vector3 eye,Vector3 muzzle,Vector3 direction,bool aiming,int seed,bool damage,float damageScale = 1f,float spreadOverride = -1f)
        {
            int count = Mathf.Clamp(definition.Pellets, 1, 32);
            var shots = new FirearmShot[count];
            var batch = damage ? new FirearmDamageBatch(shooter, definition) : null;
            var monkeyHits = new System.Collections.Generic.HashSet<PirateSlop.Ships.ShipMonkey>();
            var totals = new System.Collections.Generic.Dictionary<CombatHealth, float>();
            var headTotals = new System.Collections.Generic.Dictionary<CombatHealth, float>();
            var hitPoints = new System.Collections.Generic.Dictionary<CombatHealth, Vector3>();
            var others = new System.Collections.Generic.Dictionary<IWeaponTarget, float>();
            float spread = spreadOverride >= 0f ? spreadOverride : Spread(shooter, definition, aiming);
            var settings = definition.Ballistics;
            var upgradePlayer = shooter.GetComponent<PirateSlop.Networking.NetworkPlayer>();
            float weaponMultiplier = upgradePlayer != null && upgradePlayer.HasUpgrade(UpgradeEffect.DryPowder) ? RoguelikeTuning.Current.firearmMultiplier : 1f;
            float headMultiplier = upgradePlayer != null && upgradePlayer.HasUpgrade(UpgradeEffect.Sharpshooter) ? RoguelikeTuning.Current.headshotMultiplier : 1f;
            for (int i = 0; i < count; i++)
            {
                Vector3 ray = definition.PelletDirection(direction, i, seed, spread);
                Vector3 barrel = muzzle + (count > 1 ? Quaternion.LookRotation(direction) * Vector3.right * (i < count / 2 ? -.049f : .049f) : Vector3.zero);
                shots[i] = FirearmTrace.Resolve(shooter, eye, barrel, ray, settings.Range, out var hit, definition.TracerSpeed);
                if (!damage) continue;
                if (shots[i].Water)
                {
                    UnderwaterFirearmProjectile.Spawn(shooter, definition, batch, ref shots[i]);
                    continue;
                }
                if (hit.collider == null) continue;
                var monkey = hit.collider.GetComponent<PirateSlop.Ships.ShipMonkeyHitbox>();
                if (monkey != null && monkey.Monkey != null) { monkeyHits.Add(monkey.Monkey); continue; }
                float distance = Vector3.Distance(eye, hit.point);
                float falloff = Mathf.InverseLerp(settings.FalloffStart, settings.FalloffEnd, distance);
                var health = hit.collider.GetComponentInParent<CombatHealth>();
                if (health != null)
                {
                    var body = health.GetComponent<CharacterController>();
                    bool head = body != null && health.transform.InverseTransformPoint(hit.point).y >= body.center.y + body.height * .5f - .3f;
                    float amount = Mathf.Lerp(head ? settings.NearHeadDamage : settings.NearDamage, head ? settings.FarHeadDamage : settings.FarDamage, falloff);
                    totals.TryGetValue(health, out float previous); totals[health] = previous + amount;
                    if (head) { headTotals.TryGetValue(health, out float previousHead); headTotals[health] = previousHead + amount; }
                    if (!hitPoints.ContainsKey(health)) hitPoints[health] = hit.point + hit.normal * .02f;
                }
                else foreach (var component in hit.collider.GetComponentsInParent<MonoBehaviour>())
                    if (component is IWeaponTarget target)
                    { others.TryGetValue(target, out float previous); others[target] = previous + Mathf.Lerp(settings.NearDamage, settings.FarDamage, falloff); break; }
            }
            foreach (var monkey in monkeyHits) monkey.ReceiveFirearmShot(shooter);
            foreach (var hit in totals)
            {
                headTotals.TryGetValue(hit.Key, out float headDamage);
                float headBonus = 1f + (headMultiplier - 1f) * (headDamage / Mathf.Max(.001f, hit.Value));
                float amount = Mathf.Min(definition.DamageCap, hit.Value) * headBonus * weaponMultiplier * damageScale;
                float before = hit.Key.Current;
                hit.Key.Damage(amount, shooter);
                UpgradeCombat.AfterHit(hit.Key, before, shooter, true, direction);
                if (before > hit.Key.Current) UpgradeCombat.Ricochet(hit.Key, amount, shooter, hitPoints[hit.Key]);
            }
            foreach (var hit in others) hit.Key.ReceiveWeaponHit(Mathf.Min(definition.DamageCap, hit.Value) * weaponMultiplier * damageScale, shooter);
            return shots;
        }

    }
}
