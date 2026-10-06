using UnityEngine;

namespace PirateSlop
{
    public sealed class PistolBullet : MonoBehaviour
    {
        static readonly PistolBullet[] pool = new PistolBullet[256];
        static int next;
        public int Generation { get; private set; }
        bool authoritative;
        public static PistolBullet Spawn(Vector3 start, FirearmShot shot, Material material, float width, bool detailed, float speed, bool confirmed = true)
        {
            if (!Application.isPlaying || Application.isBatchMode) return null;
            int index = next++ % pool.Length;
            for (int i = 0; i < pool.Length; i++)
            {
                int candidate = (index + i) % pool.Length;
                if (pool[candidate] == null || !pool[candidate].gameObject.activeSelf) { index = candidate; break; }
            }
            if (pool[index] == null) pool[index] = new GameObject("FirearmTracerPool").AddComponent<PistolBullet>();
            var bullet = pool[index];
            if (bullet.gameObject.activeSelf && bullet.tracer != null && !bullet.impacted && bullet.authoritative && !bullet.shot.Water)
                FirearmImpact.Present(bullet.shot, false);
            bullet.Initialize(start, shot, material, width, detailed, speed);
            bullet.authoritative = confirmed;
            return bullet;
        }
        public void Confirm(FirearmShot result)
        {
            shot = result;
            authoritative = true;
            if (shot.Water) return;
            if (underwater) { FinishWater(); underwater = false; }
            if (age >= duration) { FirearmImpact.Present(shot, impactEffects); impacted = true; }
        }
        public static void ResolveWaterImpact(FirearmShot result)
        {
            foreach (var bullet in pool)
            {
                if (bullet == null || !bullet.gameObject.activeSelf || bullet.shot.ProjectileId != result.ProjectileId || result.ProjectileId == 0) continue;
                bullet.shot = result;
                bullet.impacted = true;
                bullet.FinishSmoke();
                bullet.FinishWater();
                bullet.gameObject.SetActive(false);
                FirearmImpact.Present(result, bullet.impactEffects);
                return;
            }
            FirearmImpact.Present(result, true);
        }
        FirearmShot shot;
        Vector3 start, waterHead, waterVelocity;
        LineRenderer tracer, glow;
        Material material;
        float age, duration, waterAge;
        bool impacted, impactEffects, underwater, passbyPlayed;
        static float nextPassbyAudio;
        float width;
        CannonSmokeTrail smoke;
        UnderwaterProjectileTrail waterTrail;
        Vector3 smokeHead;
        float smokeProgress;

        public void Initialize(Vector3 visibleStart, FirearmShot result, Material template, float thickness = .022f, bool showImpact = true, float speed = 450)
        {
            FinishSmoke();
            FinishWater();
            age = waterAge = 0f;
            impacted = underwater = passbyPlayed = false;
            authoritative = true;
            Generation++;
            gameObject.SetActive(true);
            start = visibleStart;
            shot = result;
            smokeHead = start;
            smokeProgress = 0f;
            if (!ProjectileWaterFlight.IsSubmerged(start)) smoke = CannonSmokeTrail.Create(start, .3f);
            duration = Mathf.Clamp(Vector3.Distance(start, shot.End) / Mathf.Max(50f, speed), shot.Water ? 0f : .025f, .45f);
            impactEffects = showImpact;
            width = thickness;
            var material = Resources.Load<Material>("FirearmGlow");
            if (material == null) material = template;
            if (tracer == null) tracer = gameObject.AddComponent<LineRenderer>();
            tracer.enabled = true;
            tracer.sharedMaterial = material;
            tracer.useWorldSpace = true;
            tracer.positionCount = 2;
            tracer.startWidth = width * .2f;
            tracer.endWidth = width;
            tracer.startColor = new Color(.7f, .5f, .2f, 0);
            tracer.endColor = new Color(1.6f, 1.35f, .8f, .85f);
            tracer.numCapVertices = 2;
            tracer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tracer.receiveShadows = false;
            if (glow == null)
            {
                var halo = new GameObject("TracerGlow");
                halo.transform.SetParent(transform, false);
                glow = halo.AddComponent<LineRenderer>();
            }
            glow.enabled = true;
            glow.sharedMaterial = material;
            glow.useWorldSpace = true;
            glow.positionCount = 2;
            glow.startWidth = width;
            glow.endWidth = width * 3;
            glow.startColor = new Color(1, .35f, .05f, 0);
            glow.endColor = new Color(1, .6f, .18f, .08f);
            glow.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            glow.receiveShadows = false;
            if (duration > 0f) Draw(.01f);
        }
        void Draw(float time)
        {
            FirearmImpact.ResolvePoint(ref shot);
            float head = duration > .00001f ? Mathf.Clamp01(time / duration) : 1f;
            var listener = SpatialAudioTone.FindListener();
            if (!passbyPlayed && listener != null && (listener.transform.position - start).sqrMagnitude > 16f)
            {
                Vector3 segment = shot.End - start;
                float closest = Mathf.Clamp01(Vector3.Dot(listener.transform.position - start, segment) / Mathf.Max(.001f, segment.sqrMagnitude));
                Vector3 point = start + segment * closest;
                if (closest > .01f && head >= closest)
                {
                    passbyPlayed = true;
                    if ((point - listener.transform.position).sqrMagnitude < 16f && Time.unscaledTime >= nextPassbyAudio)
                    { GameAudio.Play(SoundCue.BulletPassby, point); nextPassbyAudio = Time.unscaledTime + .08f; }
                }
            }
            if (smoke != null && head > smokeProgress)
            {
                Vector3 position = Vector3.Lerp(start, shot.End, head);
                smoke.Segment(smokeHead, position);
                smokeHead = position;
                smokeProgress = head;
                if (head >= 1f) FinishSmoke();
            }
            float tail = duration > .00001f ? Mathf.Clamp01((time - Mathf.Min(.009f, duration * .45f)) / duration) : 1f;
            tracer.SetPosition(0, Vector3.Lerp(start, shot.End, tail));
            tracer.SetPosition(1, Vector3.Lerp(start, shot.End, head));
            tracer.widthMultiplier = Mathf.Clamp01(1f - Mathf.Max(0f, time - duration) / .06f);
            glow.SetPosition(0, tracer.GetPosition(0));
            glow.SetPosition(1, tracer.GetPosition(1));
            glow.widthMultiplier = tracer.widthMultiplier;
        }
        void Update()
        {
            if (tracer == null) return;
            float previousAge = age;
            age += Time.deltaTime;
            if (shot.Water)
            {
                if (!underwater)
                {
                    Draw(Mathf.Min(age, duration));
                    if (age < duration) return;
                    underwater = true;
                    waterHead = shot.End;
                    waterVelocity = shot.WaterVelocity;
                    FinishSmoke();
                    tracer.enabled = glow.enabled = false;
                    waterTrail = UnderwaterProjectileTrail.Create(waterHead, .018f);
                    if (impactEffects && !ProjectileWaterFlight.IsSubmerged(shot.Start)) CombatVfx.Splash(waterHead, .18f);
                }
                float remaining = Mathf.Min(age - Mathf.Max(previousAge, duration), ProjectileWaterFlight.BulletLifetime - waterAge);
                while (remaining > .00001f)
                {
                    float dt = Mathf.Min(Time.fixedDeltaTime, remaining);
                    Vector3 delta = ProjectileWaterFlight.IsSubmerged(waterHead)
                        ? ProjectileWaterFlight.Step(ref waterVelocity, dt, ProjectileWaterFlight.BulletDrag) : waterVelocity * dt;
                    if (waterTrail != null) waterTrail.Segment(waterHead, waterHead + delta);
                    waterHead += delta;
                    waterAge += dt;
                    remaining -= dt;
                }
                if (waterAge >= ProjectileWaterFlight.BulletLifetime + .0001f || age >= duration + ProjectileWaterFlight.BulletLifetime + .25f)
                    gameObject.SetActive(false);
                return;
            }
            Draw(age);
            if (age > duration + .1f) gameObject.SetActive(false);
            if (impacted || age < duration) return;
            impacted = true;
            if (authoritative) FirearmImpact.Present(shot, impactEffects);
        }
        void FinishSmoke()
        {
            if (smoke == null) return;
            smoke.Finish();
            smoke = null;
        }
        void FinishWater()
        {
            if (waterTrail == null) return;
            waterTrail.Finish();
            waterTrail = null;
        }
        void OnDisable() { FinishSmoke(); FinishWater(); }
        void OnDestroy() { FinishSmoke(); FinishWater(); }
    }
}
