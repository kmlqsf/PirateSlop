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
        void Update() => RefreshVisual();
        public void RefreshVisual()
        {
            var item = GetComponent<PirateSlop.Networking.NetworkFish>();
            var ammo = item != null ? item.CurrentItem : Ammo;
            if (visibleAmmo == ammo && ammoVisual != null) return;
            int index = ammo == PirateSlop.Networking.InventoryItem.Cannonball ? 0 : (int)ammo - 7;
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
            ammoVisual.transform.localScale = AmmoModels[index].transform.localScale * (GetComponent<SphereCollider>().radius / .12f);
            foreach (var renderer in ammoVisual.GetComponentsInChildren<Renderer>(true)) renderer.enabled = true;
            visibleAmmo = ammo;
        }
        public bool Loaded { get; set; }
        public bool Held { get; set; }
        public PirateSlop.Networking.NetworkCannon Network { get; set; }
        public Rigidbody Body => GetComponent<Rigidbody>();
        public Rigidbody PlatformBody { get; set; }
        Vector3 deckVelocity;
        bool rolling;
        Vector3 deckLocalPosition;
        Quaternion deckLocalRotation;
        float nextImpactAudio;
        void OnCollisionEnter(Collision collision)
        {
            if (Held || Loaded || GetComponent<CannonShotDamage>() != null) return;
            var ship = collision.collider.GetComponentInParent<ShipController>();
            if (ship != null && !Body.isKinematic && collision.contactCount > 0 && collision.GetContact(0).normal.y > .5f)
            {
                Vector3 relative = Body.linearVelocity - ship.CannonPointVelocity(transform.position);
                RollOnPlatform(ship.GetComponent<Rigidbody>());
                deckVelocity = ship.transform.InverseTransformDirection(relative);
            }
            if (collision.relativeVelocity.sqrMagnitude < .5f || Time.time < nextImpactAudio) return;
            GameAudio.Play(SoundCue.Load, transform.position, .5f);
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
            if (Held || Loaded || GetComponent<CannonShotDamage>() != null) return;
            if (PlatformBody == null || !Body.isKinematic) return;
            if (rolling) RollOnDeck();
            if (PlatformBody == null) return;
            Body.position = PlatformBody.transform.TransformPoint(deckLocalPosition);
            Body.rotation = PlatformBody.rotation * deckLocalRotation;
        }
        void LateUpdate()
        {
            if (Held || Loaded || PlatformBody == null || !Body.isKinematic || GetComponent<CannonShotDamage>() != null) return;
            transform.SetPositionAndRotation(PlatformBody.transform.TransformPoint(deckLocalPosition), PlatformBody.rotation * deckLocalRotation);
        }
        float Radius => GetComponent<SphereCollider>().radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
        bool Cast(Vector3 origin, Vector3 direction, float distance, out RaycastHit nearest)
        {
            nearest = default;
            float best = distance + 1f;
            foreach (var hit in Physics.SphereCastAll(origin, Radius * .95f, direction, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.attachedRigidbody == Body || hit.collider.transform.IsChildOf(transform) || hit.distance >= best) continue;
                best = hit.distance; nearest = hit;
            }
            return nearest.collider != null;
        }
        public void RollOnPlatform(Rigidbody platform)
        {
            transform.SetParent(null, true);
            AttachToPlatform(platform);
            rolling = PlatformBody != null;
            Body.isKinematic = rolling;
            Body.useGravity = !rolling;
        }
        void RollOnDeck()
        {
            var frame = PlatformBody.transform;
            float dt = Time.fixedDeltaTime;
            deckVelocity += frame.InverseTransformDirection(Physics.gravity) * dt;
            Vector3 point = frame.TransformPoint(deckLocalPosition);
            Vector3 velocity = frame.TransformDirection(deckVelocity);
            float remaining = dt;
            for (int i = 0; i < 4 && remaining > .0001f; i++)
            {
                float distance = velocity.magnitude * remaining;
                if (distance < .00001f) break;
                if (!Cast(point, velocity.normalized, distance + .005f, out var hit)) { point += velocity * remaining; break; }
                float travel = Mathf.Max(0, hit.distance - .005f);
                point += velocity.normalized * travel;
                remaining *= 1f - Mathf.Clamp01(travel / distance);
                float normalSpeed = Vector3.Dot(velocity, hit.normal);
                if (normalSpeed < 0) velocity -= hit.normal * normalSpeed * (normalSpeed < -1f ? 1.25f : 1f);
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
        public void Release()
        {
            transform.SetParent(null, true);
            AttachToPlatform(null);
            Body.isKinematic = false;
            Body.useGravity = true;
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            if (Cast(transform.position + Vector3.up * .05f, Vector3.down, 4f, out var floor))
            {
                var ship = floor.collider.GetComponentInParent<ShipController>();
                if (ship != null) RollOnPlatform(ship.GetComponent<Rigidbody>());
            }
        }
    }
}
