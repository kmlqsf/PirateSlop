using UnityEngine;
using System.Collections.Generic;

namespace PirateSlop.Harpoon
{
    [RequireComponent(typeof(Rigidbody))]
    public class HarpoonProjectile : MonoBehaviour
    {
        public enum HarpoonState
        {
            Idle,
            Flying,
            Attached,
            Rewinding
        }

        public static readonly List<HarpoonProjectile> ActiveAttached = new();

        public HarpoonGun Gun { get; private set; }
        public HarpoonState State => state;
        public bool IsAttached => state == HarpoonState.Attached;
        public bool IsRewinding => state == HarpoonState.Rewinding;
        public bool IsFlying => state == HarpoonState.Flying;
        public float CurrentCableLength => currentCableLength;

        [SerializeField] Transform knotTransform;
        [SerializeField] float maxRange = 50f;
        [SerializeField] float rewindSpeed = 32f;

        Rigidbody body;
        Collider col;
        HarpoonHookTarget hookTarget;
        HarpoonState state = HarpoonState.Flying;
        float currentCableLength = 45f;
        Rigidbody shipBody;
        ShipController launcherShip;
        ShipController targetShip;
        bool isStaticTarget;
        Transform hitParent;
        Vector3 hitLocalPos;
        Quaternion hitLocalRot;

        public Vector3 KnotPosition => knotTransform != null ? knotTransform.position : transform.position;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            col = GetComponent<Collider>();
            hookTarget = GetComponent<HarpoonHookTarget>();
            if (hookTarget == null) hookTarget = gameObject.AddComponent<HarpoonHookTarget>();
            hookTarget.Projectile = this;
            if (knotTransform == null) knotTransform = transform.Find("Visual/Harpoon_Rope_Knot") ?? transform;
        }

        public void Launch(HarpoonGun launcher, Vector3 initialVelocity)
        {
            Gun = launcher;
            state = HarpoonState.Flying;
            body.isKinematic = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = initialVelocity;
            if (Gun != null && Gun.ShipBody != null)
            {
                shipBody = Gun.ShipBody;
                launcherShip = shipBody.GetComponent<ShipController>();
            }
        }

        void FixedUpdate()
        {
            if (state == HarpoonState.Flying)
            {
                if (Gun != null && Gun.Muzzle != null)
                {
                    float dist = Vector3.Distance(Gun.Muzzle.position, transform.position);
                    if (dist > maxRange || transform.position.y <= 0.05f)
                    {
                        DetachAndRewind();
                        return;
                    }
                }
                if (body.linearVelocity.sqrMagnitude > 1f)
                {
                    transform.forward = body.linearVelocity.normalized;
                }
            }
            else if (state == HarpoonState.Attached)
            {
                if (hitParent != null)
                {
                    transform.position = hitParent.TransformPoint(hitLocalPos);
                    transform.rotation = hitParent.rotation * hitLocalRot;
                }
                SimulateTetherPhysics();
            }
        }

        void SimulateTetherPhysics()
        {
            if (Gun != null && Gun.NetworkShip != null && !Gun.NetworkShip.IsServerInitialized) return;

            if (Gun == null || Gun.Muzzle == null)
            {
                DetachAndRewind();
                return;
            }

            if (hitParent == null && !isStaticTarget)
            {
                DetachAndRewind();
                return;
            }

            Vector3 pointA = Gun.Muzzle.position;
            Vector3 pointB = hitParent != null ? hitParent.TransformPoint(hitLocalPos) : transform.position;

            Vector3 diff = pointB - pointA;
            float dist = diff.magnitude;

            if (dist > maxRange * 1.4f)
            {
                DetachAndRewind();
                return;
            }

            Vector3 flatDiff = Vector3.ProjectOnPlane(diff, Vector3.up);
            float flatDist = flatDiff.magnitude;
            if (flatDist < 0.01f) return;
            Vector3 u = flatDiff / flatDist;

            float limit = currentCableLength;
            float excess = Mathf.Max(0f, dist - limit);

            if (targetShip != null && launcherShip != null)
            {
                float alignA = Mathf.Max(0f, -Vector3.Dot(launcherShip.transform.forward, u));
                float alignB = Mathf.Max(0f, Vector3.Dot(targetShip.transform.forward, u));

                float powerA = (launcherShip.IsFrozen || launcherShip.IsAnchored) ? 0f : alignA * Mathf.Max(launcherShip.Speed, launcherShip.SailDeploy * launcherShip.MaxSpeed);
                float powerB = (targetShip.IsFrozen || targetShip.IsAnchored) ? 0f : alignB * Mathf.Max(targetShip.Speed, targetShip.SailDeploy * targetShip.MaxSpeed);

                if (powerA > powerB + 0.3f)
                {
                    if (targetShip.IsAnchored)
                    {
                        if (dist >= limit * 0.95f)
                        {
                            launcherShip.ApplyTowing(pointA, u * 150000f, 0f);
                        }
                    }
                    else
                    {
                        float towSpeed = launcherShip.MaxSpeed * Mathf.Lerp(0.55f, 0.9f, powerB / Mathf.Max(0.01f, powerA));
                        if (dist >= limit * 0.92f)
                        {
                            launcherShip.SetTowingSpeedLimit(towSpeed);
                            float targetSpeed = Mathf.Max(launcherShip.Speed, 2.5f);
                            float massB = targetShip.ImpulseMass;
                            Vector3 v2 = targetShip.transform.forward * targetShip.Speed + targetShip.PushVelocity;
                            float currentSpeedB = -Vector3.Dot(v2, u);
                            float neededAccel = Mathf.Max(0f, targetSpeed - currentSpeedB) * 4f + excess * 15f;
                            Vector3 pullB = -u * (massB * neededAccel);
                            targetShip.ApplyTowing(pointB, pullB, -1f);
                            launcherShip.ApplyTowing(pointA, u * (neededAccel * massB * 0.2f), towSpeed);
                        }
                    }
                }
                else if (powerB > powerA + 0.3f)
                {
                    if (launcherShip.IsAnchored)
                    {
                        if (dist >= limit * 0.95f)
                        {
                            targetShip.ApplyTowing(pointB, -u * 150000f, 0f);
                        }
                    }
                    else
                    {
                        float towSpeed = targetShip.MaxSpeed * Mathf.Lerp(0.55f, 0.9f, powerA / Mathf.Max(0.01f, powerB));
                        if (dist >= limit * 0.92f)
                        {
                            targetShip.SetTowingSpeedLimit(towSpeed);
                            float targetSpeed = Mathf.Max(targetShip.Speed, 2.5f);
                            float massA = launcherShip.ImpulseMass;
                            Vector3 v1 = launcherShip.transform.forward * launcherShip.Speed + launcherShip.PushVelocity;
                            float currentSpeedA = Vector3.Dot(v1, u);
                            float neededAccel = Mathf.Max(0f, targetSpeed - currentSpeedA) * 4f + excess * 15f;
                            Vector3 pullA = u * (massA * neededAccel);
                            launcherShip.ApplyTowing(pointA, pullA, -1f);
                            targetShip.ApplyTowing(pointB, -u * (neededAccel * massA * 0.2f), towSpeed);
                        }
                    }
                }
                else
                {
                    if (dist >= limit * 0.95f)
                    {
                        if (powerA > 0.5f && powerB > 0.5f)
                        {
                            launcherShip.ApplyTowing(pointA, u * 150000f, 0f);
                            targetShip.ApplyTowing(pointB, -u * 150000f, 0f);
                        }
                        else
                        {
                            float tension = excess * 60000f;
                            launcherShip.ApplyTowing(pointA, u * tension, -1f);
                            targetShip.ApplyTowing(pointB, -u * tension, -1f);
                        }
                    }
                }
            }
            else if (launcherShip != null)
            {
                if (dist >= limit * 0.95f)
                {
                    float align1 = Mathf.Max(0f, -Vector3.Dot(u, launcherShip.transform.forward));
                    if (align1 > 0.1f)
                    {
                        launcherShip.ApplyTowing(pointA, u * (excess * 150000f + 50000f), 0f);
                    }
                }

                Vector3 up = launcherShip.transform.up;
                if (Vector3.Dot(up, Vector3.up) < 0.98f && shipBody != null)
                {
                    Vector3 upright = Vector3.Cross(up, Vector3.up) * (shipBody.mass * 35f);
                    shipBody.AddTorque(upright, ForceMode.Force);
                }
            }
        }

        public float GetConstraintWeight(ShipController ship)
        {
            if (ship == null) return 0.5f;
            if (targetShip == null || launcherShip == null) return 1.0f;

            bool isLauncher = ship.gameObject == launcherShip.gameObject;
            bool isTarget = ship.gameObject == targetShip.gameObject;
            if (!isLauncher && !isTarget) return 0f;

            if (ship.IsAnchored) return 0f;

            ShipController otherShip = isLauncher ? targetShip : launcherShip;
            if (otherShip.IsAnchored) return 1.0f;

            Vector3 pointA = Gun != null && Gun.Muzzle != null ? Gun.Muzzle.position : launcherShip.transform.position;
            Vector3 pointB = hitParent != null ? hitParent.TransformPoint(hitLocalPos) : targetShip.transform.position;
            Vector3 diff = pointB - pointA;
            Vector3 u = Vector3.ProjectOnPlane(diff, Vector3.up);
            if (u.sqrMagnitude < 0.001f) return 0.5f;
            u.Normalize();

            float alignA = Mathf.Max(0f, -Vector3.Dot(launcherShip.transform.forward, u));
            float alignB = Mathf.Max(0f, Vector3.Dot(targetShip.transform.forward, u));

            float powerA = (launcherShip.IsFrozen || launcherShip.IsAnchored) ? 0f : alignA * Mathf.Max(launcherShip.Speed, launcherShip.SailDeploy * launcherShip.MaxSpeed);
            float powerB = (targetShip.IsFrozen || targetShip.IsAnchored) ? 0f : alignB * Mathf.Max(targetShip.Speed, targetShip.SailDeploy * targetShip.MaxSpeed);

            if (powerA > powerB + 0.3f)
            {
                return isLauncher ? 0f : 1.0f;
            }
            if (powerB > powerA + 0.3f)
            {
                return isTarget ? 0f : 1.0f;
            }

            return 0.5f;
        }

        public static void ConstrainHarpoons(ShipController ship, ref Vector3 position, Quaternion rotation)
        {
            if (ship == null || ActiveAttached.Count == 0) return;

            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < ActiveAttached.Count; i++)
                {
                    var harpoon = ActiveAttached[i];
                    if (harpoon == null || harpoon.state != HarpoonState.Attached) continue;

                    Vector3 local, other;

                    if (harpoon.launcherShip != null && ship.gameObject == harpoon.launcherShip.gameObject)
                    {
                        local = harpoon.Gun != null && harpoon.Gun.Muzzle != null
                            ? harpoon.launcherShip.transform.InverseTransformPoint(harpoon.Gun.Muzzle.position)
                            : Vector3.zero;
                        other = harpoon.hitParent != null
                            ? harpoon.hitParent.TransformPoint(harpoon.hitLocalPos)
                            : harpoon.transform.position;
                    }
                    else if (harpoon.targetShip != null && ship.gameObject == harpoon.targetShip.gameObject)
                    {
                        local = harpoon.hitLocalPos;
                        other = harpoon.Gun != null && harpoon.Gun.Muzzle != null
                            ? harpoon.Gun.Muzzle.position
                            : (harpoon.shipBody != null ? harpoon.shipBody.position : harpoon.transform.position);
                    }
                    else continue;

                    Vector3 anchor = position + rotation * local;
                    Vector3 flat = Vector3.ProjectOnPlane(anchor - other, Vector3.up);
                    float height = anchor.y - other.y;
                    float limit = Mathf.Sqrt(Mathf.Max(1f, harpoon.currentCableLength * harpoon.currentCableLength - height * height));

                    if (flat.magnitude > limit)
                    {
                        float weight = harpoon.GetConstraintWeight(ship);
                        if (weight > 0.001f)
                        {
                            Vector3 correction = flat.normalized * (flat.magnitude - limit);
                            position -= correction * weight;
                        }
                    }
                }
            }
        }

        void Update()
        {
            if (state == HarpoonState.Rewinding)
            {
                if (Gun == null || Gun.Muzzle == null)
                {
                    Destroy(gameObject);
                    return;
                }
                Vector3 target = Gun.Muzzle.position;
                transform.position = Vector3.MoveTowards(transform.position, target, rewindSpeed * Time.deltaTime);
                if (Vector3.Distance(transform.position, target) < 0.35f)
                {
                    Gun.OnProjectileReturned(this);
                    Destroy(gameObject);
                }
            }
            else if (state == HarpoonState.Attached && hitParent != null)
            {
                transform.position = hitParent.TransformPoint(hitLocalPos);
                transform.rotation = hitParent.rotation * hitLocalRot;
            }
        }

        void OnCollisionEnter(Collision collision)
        {
            if (state != HarpoonState.Flying) return;
            if (Gun != null && (collision.transform.IsChildOf(Gun.transform) || (shipBody != null && collision.transform.IsChildOf(shipBody.transform))))
                return;

            if (collision.gameObject.layer == 4)
            {
                DetachAndRewind();
                return;
            }

            bool isShip = collision.collider.GetComponentInParent<ShipController>() != null
                          || collision.collider.GetComponentInParent<PirateSlop.Networking.NetworkShip>() != null
                          || collision.collider.CompareTag("Ship");

            bool isRock = collision.gameObject.layer == 8 || collision.collider.CompareTag("Rock")
                          || (collision.collider.attachedRigidbody == null && collision.gameObject.layer == 0);

            if (isShip || isRock)
            {
                Attach(collision, isShip);
            }
            else
            {
                DetachAndRewind();
            }
        }

        void Attach(Collision collision, bool isShip)
        {
            state = HarpoonState.Attached;
            body.isKinematic = true;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            if (col != null) col.isTrigger = true;

            var contact = collision.GetContact(0);
            transform.position = contact.point;

            hitParent = collision.collider.transform;
            hitLocalPos = hitParent.InverseTransformPoint(contact.point);
            hitLocalRot = Quaternion.Inverse(hitParent.rotation) * transform.rotation;

            launcherShip = shipBody != null ? (shipBody.GetComponent<ShipController>() ?? shipBody.GetComponentInParent<ShipController>()) : null;
            if (launcherShip == null && shipBody != null)
            {
                var netShip = shipBody.GetComponent<PirateSlop.Networking.NetworkShip>() ?? shipBody.GetComponentInParent<PirateSlop.Networking.NetworkShip>();
                if (netShip != null) launcherShip = netShip.Motor;
            }

            targetShip = isShip && collision.collider != null ? (collision.collider.GetComponentInParent<ShipController>() ?? collision.collider.GetComponent<ShipController>()) : null;
            if (targetShip == null && isShip && collision.collider != null)
            {
                var netShip = collision.collider.GetComponentInParent<PirateSlop.Networking.NetworkShip>() ?? collision.collider.GetComponent<PirateSlop.Networking.NetworkShip>();
                if (netShip != null) targetShip = netShip.Motor;
            }

            isStaticTarget = !isShip || targetShip == null;

            if (Gun != null && Gun.Muzzle != null)
            {
                float dist = Vector3.Distance(Gun.Muzzle.position, contact.point);
                currentCableLength = Mathf.Clamp(dist, 8f, maxRange);
            }

            if (!ActiveAttached.Contains(this)) ActiveAttached.Add(this);

            GameAudio.Play(SoundCue.BulletMetal, transform.position, 1.0f);
            if (Gun != null) Gun.OnProjectileAttached(this);

            if (Gun != null && Gun.NetworkShip != null && Gun.NetworkShip.IsServerInitialized && Gun.MountIndex >= 0)
            {
                var targetNetObj = collision.collider != null ? collision.collider.GetComponentInParent<FishNet.Object.NetworkObject>() : null;
                Gun.NetworkShip.HarpoonAttach(Gun.MountIndex, isShip, targetNetObj, hitLocalPos, hitLocalRot, currentCableLength);
            }
        }

        public void AttachFromNetwork(bool isShip, FishNet.Object.NetworkObject targetNetObj, Vector3 localHitPos, Quaternion localHitRot, float cableLength)
        {
            state = HarpoonState.Attached;
            body.isKinematic = true;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            if (col != null) col.isTrigger = true;

            Transform targetTransform = targetNetObj != null ? targetNetObj.transform : null;
            hitParent = targetTransform;
            hitLocalPos = localHitPos;
            hitLocalRot = localHitRot;

            if (hitParent != null)
            {
                transform.position = hitParent.TransformPoint(hitLocalPos);
                transform.rotation = hitParent.rotation * hitLocalRot;
            }

            launcherShip = shipBody != null ? (shipBody.GetComponent<ShipController>() ?? shipBody.GetComponentInParent<ShipController>()) : null;
            if (launcherShip == null && shipBody != null)
            {
                var netShip = shipBody.GetComponent<PirateSlop.Networking.NetworkShip>() ?? shipBody.GetComponentInParent<PirateSlop.Networking.NetworkShip>();
                if (netShip != null) launcherShip = netShip.Motor;
            }

            targetShip = isShip && targetNetObj != null ? (targetNetObj.GetComponent<ShipController>() ?? targetNetObj.GetComponentInChildren<ShipController>()) : null;
            isStaticTarget = !isShip || targetShip == null;
            currentCableLength = Mathf.Clamp(cableLength, 8f, maxRange);

            if (!ActiveAttached.Contains(this)) ActiveAttached.Add(this);
            GameAudio.Play(SoundCue.BulletMetal, transform.position, 1.0f);
            if (Gun != null) Gun.OnProjectileAttached(this);
        }

        public void SetCableLength(float length)
        {
            currentCableLength = Mathf.Clamp(length, 8f, maxRange);
        }

        public void DetachAndRewind()
        {
            if (state == HarpoonState.Rewinding) return;
            state = HarpoonState.Rewinding;
            ActiveAttached.Remove(this);
            hitParent = null;
            transform.SetParent(null);
            body.isKinematic = true;
            if (col != null) col.enabled = false;
            if (Gun != null) Gun.OnProjectileRewinding(this);
        }

        void OnDestroy()
        {
            ActiveAttached.Remove(this);
        }
    }
}
