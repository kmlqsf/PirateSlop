using UnityEngine;
namespace PirateSlop
{
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public sealed class Cannonball : MonoBehaviour
    {
        public PirateSlop.Networking.InventoryItem Ammo = PirateSlop.Networking.InventoryItem.Cannonball;
        public GameObject[] AmmoModels;
        public Material FlightTrailMaterial;
        GameObject ammoVisual;
        PirateSlop.Networking.InventoryItem visibleAmmo = PirateSlop.Networking.InventoryItem.None;
        float visualRadius;

        Rigidbody body;
        SphereCollider sphereCol;
        PirateSlop.Networking.NetworkFish networkFish;
        CannonShotDamage shotDamage;
        static readonly RaycastHit[] castBuffer = new RaycastHit[32];
        static readonly Collider[] overlapBuffer = new Collider[32];

        public Rigidbody Body => body != null ? body : (body = GetComponent<Rigidbody>());
        public PirateSlop.Networking.InventoryItem CurrentAmmo => (networkFish != null ? networkFish : (networkFish = GetComponent<PirateSlop.Networking.NetworkFish>())) is { } item ? item.CurrentItem : Ammo;
        public SphereCollider SphereCol => sphereCol != null ? sphereCol : (sphereCol = GetComponent<SphereCollider>());
        public CannonShotDamage ShotDamage => shotDamage != null ? shotDamage : (shotDamage = GetComponent<CannonShotDamage>());

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            sphereCol = GetComponent<SphereCollider>();
            visualRadius = sphereCol.radius;
            networkFish = GetComponent<PirateSlop.Networking.NetworkFish>();
            shotDamage = GetComponent<CannonShotDamage>();
        }

        void Update()
        {
            RefreshVisual();
            UpdateManualDropAudio();
        }
        public void RefreshVisual()
        {
            var item = networkFish != null ? networkFish : (networkFish = GetComponent<PirateSlop.Networking.NetworkFish>());
            var ammo = item != null ? item.CurrentItem : Ammo;
            if (visibleAmmo == ammo && ammoVisual != null) return;
            int index = ammo == PirateSlop.Networking.InventoryItem.Cannonball ? 0 : ammo == PirateSlop.Networking.InventoryItem.BoardingHook ? 5 : (int)ammo - 7;
            if (AmmoModels == null || index < 0 || index >= AmmoModels.Length || AmmoModels[index] == null) return;
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child.name != "AmmoVisual") continue;
                child.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(child.gameObject); else DestroyImmediate(child.gameObject);
            }
            foreach (var renderer in GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            ammoVisual = Instantiate(AmmoModels[index], transform);
            ammoVisual.name = "AmmoVisual";
            ammoVisual.transform.localPosition = Vector3.zero;
            ammoVisual.transform.localRotation = Quaternion.identity;
            if (visualRadius <= 0f) visualRadius = SphereCol.radius;
            ammoVisual.transform.localScale = AmmoModels[index].transform.localScale * (visualRadius / .12f);
            foreach (var renderer in ammoVisual.GetComponentsInChildren<Renderer>(true)) renderer.enabled = true;
            SphereCol.radius = visualRadius;
            if (ammo == PirateSlop.Networking.InventoryItem.BoardingHook)
            {
                float lowest = 0f;
                foreach (var filter in ammoVisual.GetComponentsInChildren<MeshFilter>(true))
                {
                    var bounds = filter.sharedMesh.bounds;
                    for (int corner = 0; corner < 8; corner++)
                    {
                        Vector3 point = bounds.center + Vector3.Scale(bounds.extents, new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                        lowest = Mathf.Min(lowest, transform.InverseTransformPoint(filter.transform.TransformPoint(point)).y);
                    }
                }
                SphereCol.radius = Mathf.Max(.025f, -lowest);
            }
            visibleAmmo = ammo;
        }
        public bool Loaded { get; set; }
        public bool Held { get; set; }
        public PirateSlop.Networking.NetworkCannon Network { get; set; }
        public Rigidbody PlatformBody { get; set; }
        Vector3 deckVelocity;
        bool rolling;
        bool RestingHook => CurrentAmmo == PirateSlop.Networking.InventoryItem.BoardingHook;
        Vector3 deckLocalPosition;
        Quaternion deckLocalRotation;
        float nextImpactAudio;
        float nextRollAudio;
        bool manualDropPending;
        float manualDropDeadline;
        public void ArmManualDropAudio()
        {
            manualDropPending = true;
            manualDropDeadline = Time.time + 2.5f;
        }
        void UpdateManualDropAudio()
        {
            if (!manualDropPending) return;
            if (Time.time > manualDropDeadline || Held || Loaded || ShotDamage != null)
            {
                if (Time.time > manualDropDeadline || Loaded || ShotDamage != null) manualDropPending = false;
                return;
            }
            foreach (var hit in Physics.RaycastAll(transform.position + Vector3.up * .05f, Vector3.down, Radius + .15f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.normal.y < .5f || hit.distance > Radius + .085f || hit.collider.GetComponentInParent<ShipController>() == null) continue;
                GameAudio.Play(SoundCue.CannonballDrop, hit.point, .7f);
                manualDropPending = false;
                nextImpactAudio = Time.time + .75f;
                break;
            }
        }
        void OnCollisionEnter(Collision collision)
        {
            if (Held || Loaded || ShotDamage != null) return;
            var ship = collision.collider.GetComponentInParent<ShipController>();
            if (ship != null && !Body.isKinematic && collision.contactCount > 0 && collision.GetContact(0).normal.y > .5f)
            {
                Vector3 relative = Body.linearVelocity - ship.CannonPointVelocity(transform.position);
                RollOnPlatform(ship.GetComponent<Rigidbody>());
                deckVelocity = ship.transform.InverseTransformDirection(relative);
            }
            if (manualDropPending && ship != null && collision.contactCount > 0 && collision.GetContact(0).normal.y > .5f)
            {
                GameAudio.Play(SoundCue.CannonballDrop, collision.GetContact(0).point, .7f);
                manualDropPending = false;
                nextImpactAudio = Time.time + .75f;
                return;
            }
            if (collision.relativeVelocity.sqrMagnitude < .5f || Time.time < nextImpactAudio) return;
            GameAudio.Play(SoundCue.CannonballDrop, transform.position, Mathf.Clamp(collision.relativeVelocity.magnitude / 4f, .3f, 1f));
            nextImpactAudio = Time.time + .25f;
        }
        public void AttachToPlatform(Rigidbody platform)
        {
            rolling = false;
            deckVelocity = Vector3.zero;
            PlatformBody = platform;
            if (platform != null && platform != Body) { deckLocalPosition = platform.transform.InverseTransformPoint(transform.position); deckLocalRotation = Quaternion.Inverse(platform.rotation) * transform.rotation; }
            else PlatformBody = null;
        }
        void FixedUpdate()
        {
            if (Held || Loaded || ShotDamage != null) return;
            if (PlatformBody == null || !Body.isKinematic) return;
            if (rolling) RollOnDeck();
            else if (RestingHook)
            {
                Vector3 point = PlatformBody.transform.TransformPoint(deckLocalPosition);
                if (!Cast(point + PlatformBody.transform.up * .05f, -PlatformBody.transform.up, .18f, out var floor) ||
                    floor.collider.GetComponentInParent<ShipController>() != PlatformBody.GetComponent<ShipController>())
                {
                    Vector3 inherited = PlatformVelocity(point);
                    Body.position = point;
                    AttachToPlatform(null);
                    Body.isKinematic = false; Body.useGravity = true;
                    Body.linearVelocity = inherited;
                    return;
                }
            }
            if (PlatformBody == null) return;
            Body.position = PlatformBody.transform.TransformPoint(deckLocalPosition);
            Body.rotation = PlatformBody.rotation * deckLocalRotation;
        }
        void LateUpdate()
        {
            if (Held || Loaded || PlatformBody == null || !Body.isKinematic || ShotDamage != null) return;
            transform.SetPositionAndRotation(PlatformBody.transform.TransformPoint(deckLocalPosition), PlatformBody.rotation * deckLocalRotation);
        }
        float Radius => SphereCol.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
        bool Cast(Vector3 origin, Vector3 direction, float distance, out RaycastHit nearest, bool includeBalls = false)
        {
            nearest = default;
            float best = distance + 1f;
            int count = Physics.SphereCastNonAlloc(origin, Radius * .95f, direction, castBuffer, distance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = castBuffer[i];
                var otherBall = hit.collider.GetComponentInParent<Cannonball>();
                if (otherBall != null && (!includeBalls || !CanCollideWith(otherBall))) continue;
                if (otherBall == null && hit.collider.GetComponentInParent<Networking.NetworkFish>() != null) continue;
                if (hit.collider.GetComponentInParent<AdvancedPlayerController>() != null) continue;
                if (hit.collider.attachedRigidbody == Body || hit.collider.transform.IsChildOf(transform) || hit.distance >= best) continue;
                best = hit.distance; nearest = hit;
            }
            return nearest.collider != null;
        }
        public void RollOnPlatform(Rigidbody platform)
        {
            RefreshVisual();
            if (RestingHook && platform != null && Cast(transform.position + platform.transform.up * .08f, -platform.transform.up, .3f, out var floor))
                transform.position = floor.point + platform.transform.up * (Radius + .005f);
            transform.SetParent(null, true);
            AttachToPlatform(platform);
            rolling = PlatformBody != null && !RestingHook;
            Body.isKinematic = PlatformBody != null;
            Body.useGravity = PlatformBody == null;
        }
        void RollOnDeck()
        {
            var frame = PlatformBody.transform;
            float dt = Time.fixedDeltaTime;
            deckVelocity += frame.InverseTransformDirection(Physics.gravity) * dt;
            if (deckVelocity.magnitude > .45f && Time.time >= nextRollAudio)
            {
                GameAudio.Play(SoundCue.CannonballRoll, transform.position, Mathf.Clamp01(deckVelocity.magnitude / 3f));
                nextRollAudio = Time.time + Random.Range(.85f, 1.3f);
            }
            Vector3 point = frame.TransformPoint(deckLocalPosition);
            Vector3 velocity = frame.TransformDirection(deckVelocity);
            ResolveBallOverlaps(ref point, ref velocity);
            float remaining = dt;
            for (int i = 0; i < 4 && remaining > .0001f; i++)
            {
                float distance = velocity.magnitude * remaining;
                if (distance < .00001f) break;
                if (!Cast(point, velocity.normalized, distance + .005f, out var hit, true)) { point += velocity * remaining; break; }
                float travel = Mathf.Max(0, hit.distance - .005f);
                point += velocity.normalized * travel;
                remaining *= 1f - Mathf.Clamp01(travel / distance);
                var other = hit.collider.GetComponentInParent<Cannonball>();
                if (other != null) ResolveBallImpact(other, point, hit.normal, ref velocity);
                else
                {
                    float normalSpeed = Vector3.Dot(velocity, hit.normal);
                    if (normalSpeed < 0) velocity -= hit.normal * normalSpeed * (normalSpeed < -1f ? 1.25f : 1f);
                }
                velocity *= Mathf.Exp(-.25f * dt);
            }
            Vector3 moved = frame.InverseTransformPoint(point) - deckLocalPosition;
            Vector3 tangent = Vector3.ProjectOnPlane(moved, Vector3.up);
            if (tangent.sqrMagnitude > .0000001f)
                deckLocalRotation = Quaternion.AngleAxis(tangent.magnitude / Radius * Mathf.Rad2Deg, Vector3.Cross(Vector3.up, tangent).normalized) * deckLocalRotation;
            deckLocalPosition = frame.InverseTransformPoint(point);
            deckVelocity = frame.InverseTransformDirection(velocity);
            if (!Cast(point + frame.up * .05f, -frame.up, 6f, out var floor) || floor.collider.GetComponentInParent<ShipController>() != PlatformBody.GetComponent<ShipController>())
            {
                var ship = PlatformBody.GetComponent<ShipController>();
                Vector3 inherited = ship != null ? ship.CannonPointVelocity(point) : PlatformBody.GetPointVelocity(point);
                Body.position = point;
                AttachToPlatform(null);
                Body.isKinematic = false; Body.useGravity = true;
                Body.linearVelocity = velocity + inherited;
            }
        }
        bool CanCollideWith(Cannonball other) => other != this && !other.Held && !other.Loaded && other.ShotDamage == null;
        Vector3 PlatformVelocity(Vector3 point)
        {
            if (PlatformBody == null) return Vector3.zero;
            var ship = PlatformBody.GetComponent<ShipController>();
            return ship != null ? ship.CannonPointVelocity(point) : PlatformBody.GetPointVelocity(point);
        }
        Vector3 LoosePosition => rolling && PlatformBody != null ? PlatformBody.transform.TransformPoint(deckLocalPosition) : Body.position;
        Vector3 LooseVelocity => rolling && PlatformBody != null ? PlatformBody.transform.TransformDirection(deckVelocity) + PlatformVelocity(LoosePosition) : Body.isKinematic ? PlatformVelocity(LoosePosition) : Body.linearVelocity;
        float InverseLooseMass => rolling || !Body.isKinematic ? 1f / Mathf.Max(.01f, Body.mass) : 0f;
        void MoveLooseBall(Vector3 offset)
        {
            if (rolling && PlatformBody != null)
            {
                deckLocalPosition += PlatformBody.transform.InverseTransformVector(offset);
                Body.position = PlatformBody.transform.TransformPoint(deckLocalPosition);
            }
            else if (!Body.isKinematic) Body.position += offset;
        }
        void ResolveBallImpact(Cannonball other, Vector3 point, Vector3 normal, ref Vector3 velocity)
        {
            float closing = Vector3.Dot(velocity + PlatformVelocity(point) - other.LooseVelocity, normal);
            if (closing >= 0f) return;
            float mine = InverseLooseMass, theirs = other.InverseLooseMass;
            float impulse = -1.25f * closing / (mine + theirs);
            velocity += normal * (impulse * mine);
            Vector3 change = -normal * (impulse * theirs);
            if (other.rolling && other.PlatformBody != null) other.deckVelocity += other.PlatformBody.transform.InverseTransformDirection(change);
            else if (!other.Body.isKinematic) { other.Body.WakeUp(); other.Body.linearVelocity += change; }
        }
        void ResolveBallOverlaps(ref Vector3 point, ref Vector3 velocity)
        {
            int count = Physics.OverlapSphereNonAlloc(point + transform.TransformVector(SphereCol.center), Radius, overlapBuffer, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var other = overlapBuffer[i].GetComponentInParent<Cannonball>();
                if (other == null || !CanCollideWith(other)) continue;
                Vector3 difference = point + transform.TransformVector(SphereCol.center) - other.LoosePosition - other.transform.TransformVector(other.SphereCol.center);
                float distance = difference.magnitude, separation = Radius + other.Radius;
                if (distance >= separation) continue;
                Vector3 normal = distance > .0001f ? difference / distance : PlatformBody.transform.right;
                float mine = InverseLooseMass, theirs = other.InverseLooseMass;
                float share = mine / (mine + theirs);
                if (other.rolling && other.PlatformBody != null && Mathf.Abs(Vector3.Dot(normal, PlatformBody.transform.up)) > .5f)
                    share = Vector3.Dot(normal, PlatformBody.transform.up) > 0f ? 1f : 0f;
                Vector3 correction = normal * (separation - distance + .001f);
                point += correction * share;
                other.MoveLooseBall(-correction * (1f - share));
                ResolveBallImpact(other, point, normal, ref velocity);
            }
        }
        public void Release()
        {
            RefreshVisual();
            transform.SetParent(null, true);
            AttachToPlatform(null);
            Body.isKinematic = false;
            Body.useGravity = true;
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            if (Cast(transform.position + Vector3.up * .05f, Vector3.down, RestingHook ? .18f : 4f, out var floor))
            {
                var ship = floor.collider.GetComponentInParent<ShipController>();
                if (ship != null) RollOnPlatform(ship.GetComponent<Rigidbody>());
            }
        }
    }
}
