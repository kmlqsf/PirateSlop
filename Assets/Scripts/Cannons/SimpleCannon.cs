using UnityEngine;
using PirateSlop.Networking;
namespace PirateSlop
{
    public sealed class SimpleCannon : MonoBehaviour
    {
        public Transform Muzzle;
        public float LaunchSpeed = 30f;
        public Transform BarrelPivot, Breech;
        public float MinElevation = -10f, MaxElevation = 25f;
        public float Elevation { get; private set; } = 3f;
        Quaternion barrelRest;
        AdvancedPlayerController grip;
        float lastGrip;
        public NetworkCannon Network { get; set; }
        public CannonballCrate Crate { get; set; }
        public int Index { get; set; }
        public const float LoadRadius = 1.15f;
        Cannonball loaded;
        float loadStarted = -1f;
        Vector3 loadStart;
        public bool IsLoading => loadStarted >= 0f;
        public bool CanLoadFrom(Vector3 point)
        {
            if (Muzzle == null || Vector3.Distance(point, Muzzle.position) > LoadRadius) return false;
            Vector3 delta = Muzzle.position - point;
            foreach (var hit in Physics.RaycastAll(point, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) && hit.collider.GetComponentInParent<Cannonball>() == null) return false;
            return true;
        }
        GameObject firingPlayer;
        Cannonball supply;
        Rigidbody supplyPlatform;
        Vector3 supplyPosition;
        Quaternion supplyRotation;
        float nextFireTime;
        [SerializeField] float fuseDuration = 1.4f;
        float ignitionTime = -1f;
        float visibleFuse = -1f;
        LineRenderer fuse;
        Transform ember;
        ParticleSystem sparks;
        Light fuseLight;
        Material ropeMaterial, emberMaterial;
        Vector3 fuseBase, fuseTip;
        public bool IsIgnited => ignitionTime >= 0f || visibleFuse >= 0f;
        public float FuseProgress => ignitionTime < 0f ? 0f : Mathf.Clamp01((Time.time - ignitionTime) / Mathf.Max(.1f, fuseDuration));
        void Awake() { if (BarrelPivot != null) barrelRest = BarrelPivot.localRotation; }
        public bool InBreechRange(AdvancedPlayerController player) => player != null && !player.IsDead && BarrelPivot != null && Vector3.Distance(player.transform.position + Vector3.up, BarrelPivot.position) <= 4f;
        public void SetElevation(float value)
        {
            Elevation = Mathf.Clamp(value, MinElevation, MaxElevation);
            if (BarrelPivot != null) BarrelPivot.localRotation = Quaternion.AngleAxis(3f - Elevation, BarrelPivot.parent.InverseTransformDirection(transform.right)) * barrelRest;
        }
        public bool DragBreech(AdvancedPlayerController player, float degrees, bool holding)
        {
            if (grip != null && (!InBreechRange(grip) || Time.time - lastGrip > .75f)) grip = null;
            if (!holding) { if (grip == player) grip = null; return false; }
            if (!float.IsFinite(degrees) || !InBreechRange(player) || (grip != null && grip != player)) return false;
            grip = player; lastGrip = Time.time;
            SetElevation(Elevation + Mathf.Clamp(degrees, -15f, 15f)); return true;
        }
        void Start()
        {
            if (supply != null) return;
            var ship = GetComponentInParent<ShipController>();
            if (ship != null) InitializeSupply(ship.GetComponentInChildren<Cannonball>(true));
        }
        public void InitializeSupply(Cannonball ball)
        {
            if (supply != null || ball == null) return;
            supply = ball; supplyPlatform = GetComponentInParent<Rigidbody>();
            var anchor = Crate != null ? Crate.SpawnPoint : ball.transform;
            supplyPosition = supplyPlatform.transform.InverseTransformPoint(anchor.position);
            supplyRotation = Quaternion.Inverse(supplyPlatform.rotation) * anchor.rotation;
        }
        public void ResetSupply()
        {
            loaded = null; loadStarted = -1f; firingPlayer = null; ignitionTime = -1f; ShowFuse(-1f);
            if (Crate != null) { Crate.ResetSupply(); return; }
            if (supply == null) return;
            supply.Loaded = supply.Held = false;
            supply.Body.isKinematic = true;
            supply.transform.SetParent(supplyPlatform.transform, false);
            supply.transform.localPosition = supplyPosition; supply.transform.localRotation = supplyRotation;
            supply.GetComponent<Collider>().enabled = true;
            supply.AttachToPlatform(supplyPlatform); supply.gameObject.SetActive(true);
        }
        public void SpawnShot(Vector3 position, Vector3 velocity, bool authoritative, InventoryItem ammo = InventoryItem.Cannonball)
        {
            if (supply == null) return;
            GameAudio.Play(SoundCue.Cannon, position);
            CombatVfx.Fire(Muzzle.position, velocity.normalized, true);
            var shot = Instantiate(supply, position, Quaternion.identity);
            shot.Ammo = ammo;
            shot.name = "FiredCannonball"; shot.Network = null; shot.Loaded = shot.Held = false;
            shot.gameObject.SetActive(true);
            shot.RefreshVisual();
            shot.transform.SetParent(null, true);
            shot.AttachToPlatform(null);
            shot.GetComponent<Collider>().enabled = false;
            shot.Body.isKinematic = true; shot.Body.useGravity = false;
            var projectile=shot.gameObject.AddComponent<CannonShotDamage>();
            projectile.Authoritative=authoritative;projectile.Source=GetComponentInParent<ShipController>().transform;
            projectile.Velocity=velocity; projectile.Ammo=ammo;
            projectile.Attacker = authoritative ? firingPlayer : null;
            projectile.Radius=shot.GetComponent<SphereCollider>().radius*Mathf.Max(shot.transform.lossyScale.x,shot.transform.lossyScale.y,shot.transform.lossyScale.z);
            if (shot.FlightTrailMaterial != null)
            {
                var trail = shot.gameObject.AddComponent<TrailRenderer>();
                trail.sharedMaterial = shot.FlightTrailMaterial;
                trail.time = .22f; trail.minVertexDistance = .08f;
                trail.startWidth = .18f; trail.endWidth = .015f;
                trail.startColor = CannonAmmo.Color(ammo); trail.endColor = new Color(.5f, .5f, .5f, 0f);
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.receiveShadows = false;
            }
            Destroy(shot.gameObject, 20f);
        }
        public bool IsLoaded => loaded != null;
        public bool TryLoad(Cannonball ball)
        {
            if (IsLoaded || ball == null || ball.Loaded || !CanLoadFrom(ball.transform.position)) return false;
            loaded = ball; ball.Loaded = true; ball.Held = false; ball.GetComponent<Collider>().enabled = false;
            ball.Body.isKinematic = true;
            ball.AttachToPlatform(GetComponentInParent<Rigidbody>());
            ball.transform.SetParent(Muzzle, true);
            loadStart = ball.transform.localPosition;
            if (loadStart.magnitude < .35f) loadStart = Vector3.forward * .6f;
            ball.transform.localPosition = loadStart;
            loadStarted = Time.time;
            ball.RefreshVisual();
            ball.gameObject.SetActive(true);
            return true;
        }
        public void Fire(GameObject attacker = null)
        {
            if (Network != null && Network.IsClientInitialized && !Network.IsServerInitialized) { Network.RequestFire(Index); return; }
            if (!IsLoaded || IsLoading || IsIgnited || Time.time < nextFireTime) return;
            firingPlayer = attacker;
            ignitionTime = Time.time;
            ShowFuse(0f);
            if (Network != null && Network.IsServerInitialized) Network.NotifyIgnited(Index);
        }
        void Update()
        {
            if (IsLoading && loaded != null)
            {
                float t = Mathf.Clamp01((Time.time - loadStarted) / .45f);
                loaded.transform.localPosition = Vector3.Lerp(loadStart, Vector3.back * .5f, Mathf.SmoothStep(0f, 1f, t));
                if (t >= 1f)
                {
                    loaded.gameObject.SetActive(false);
                    loadStarted = -1f;
                    GameAudio.Play(SoundCue.Load, Muzzle.position);
                }
            }
            if (ignitionTime >= 0f && (Network == null || Network.IsServerInitialized))
            {
                ShowFuse(FuseProgress);
                if (FuseProgress >= 1f) Shoot();
            }
        }
        void Shoot()
        {
            ignitionTime = -1f;
            ShowFuse(-1f);
            if (!IsLoaded) return;
            nextFireTime = Time.time + 6f;
            InitializeSupply(loaded);
            Vector3 inherited = GetComponentInParent<ShipController>().CannonPointVelocity(Muzzle.position);
            Vector3 position = Muzzle.position + Muzzle.forward * .35f;
            Vector3 velocity = Muzzle.forward * LaunchSpeed + inherited;
            var ammo = loaded.Ammo;
            SpawnShot(position, velocity, true, ammo);
            var motor=GetComponentInParent<ShipController>();
            motor.ApplyCannonImpulse(Muzzle.position,-Muzzle.forward,4.95f);
            GetComponent<CannonCarriage>()?.Recoil();
            ResetSupply();
            if (Network != null && Network.IsServerInitialized) Network.NotifyFired(Index, position, velocity, ammo);
        }
        void CreateFuse()
        {
            if (fuse != null || BarrelPivot == null) return;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            ropeMaterial = new Material(shader);
            ropeMaterial.color = new Color(.28f, .19f, .08f);
            emberMaterial = new Material(shader);
            emberMaterial.color = new Color(1f, .32f, .025f);
            var root = new GameObject("CannonFuse");
            root.transform.SetParent(BarrelPivot, false);
            fuse = root.AddComponent<LineRenderer>();
            fuse.useWorldSpace = false; fuse.positionCount = 3;
            fuse.startWidth = fuse.endWidth = .025f;
            fuse.numCapVertices = 3; fuse.numCornerVertices = 3;
            fuse.sharedMaterial = ropeMaterial;
            Vector3 origin = Breech != null ? Breech.position : BarrelPivot.position;
            fuseBase = BarrelPivot.InverseTransformPoint(origin + transform.up * .16f);
            fuseTip = BarrelPivot.InverseTransformPoint(origin + transform.up * .42f - transform.forward * .2f);
            var glow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            glow.name = "FuseEmber"; Destroy(glow.GetComponent<Collider>());
            ember = glow.transform; ember.SetParent(root.transform, false); ember.localScale = Vector3.one * .055f;
            glow.GetComponent<Renderer>().sharedMaterial = emberMaterial;
            fuseLight = glow.AddComponent<Light>();
            fuseLight.color = new Color(1f, .35f, .05f); fuseLight.range = 1.4f; fuseLight.intensity = 1.5f;
            var sparkObject = new GameObject("FuseSparks");
            sparkObject.transform.SetParent(root.transform, false);
            sparks = sparkObject.AddComponent<ParticleSystem>();
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = sparks.main;
            main.playOnAwake = false; main.loop = true; main.startLifetime = .25f;
            main.startSpeed = .55f; main.startSize = .018f;
            main.startColor = new Color(1f, .65f, .12f); main.maxParticles = 24;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = sparks.emission; emission.rateOverTime = 28f;
            var shape = sparks.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .025f;
            sparks.GetComponent<ParticleSystemRenderer>().sharedMaterial = emberMaterial;
        }
        public void ShowFuse(float progress)
        {
            visibleFuse = progress;
            if (fuse == null && progress < 0f) return;
            CreateFuse();
            if (fuse == null) return;
            bool burning = progress >= 0f;
            fuse.gameObject.SetActive(burning);
            if (!burning) { sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); return; }
            Vector3 tip = Vector3.Lerp(fuseTip, fuseBase, Mathf.Clamp01(progress));
            fuse.SetPosition(0, fuseBase);
            fuse.SetPosition(1, Vector3.Lerp(fuseBase, tip, .5f) + Vector3.right * .025f * (1f - progress));
            fuse.SetPosition(2, tip);
            ember.localPosition = tip;
            sparks.transform.localPosition = tip;
            fuseLight.intensity = 1.4f + Mathf.Sin(Time.time * 47f) * .4f;
            if (!sparks.isPlaying) sparks.Play();
        }
        void OnDestroy()
        {
            if (ropeMaterial != null) Destroy(ropeMaterial);
            if (emberMaterial != null) Destroy(emberMaterial);
        }
    }
}
