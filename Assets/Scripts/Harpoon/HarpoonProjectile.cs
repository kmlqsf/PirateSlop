using UnityEngine;

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
        ConfigurableJoint joint;
        HarpoonState state = HarpoonState.Flying;
        float currentCableLength = 45f;
        Rigidbody shipBody;
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
            if (Gun != null && Gun.ShipBody != null) shipBody = Gun.ShipBody;
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
                if (isStaticTarget && shipBody != null)
                {
                    Vector3 up = shipBody.transform.up;
                    if (Vector3.Dot(up, Vector3.up) < 0.98f)
                    {
                        Vector3 upright = Vector3.Cross(up, Vector3.up) * (shipBody.mass * 35f);
                        shipBody.AddTorque(upright, ForceMode.Force);
                    }
                    Vector3 av = shipBody.angularVelocity;
                    shipBody.angularVelocity = new Vector3(av.x * 0.85f, av.y, av.z * 0.85f);
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

            isStaticTarget = !isShip;

            if (shipBody != null && Gun != null && Gun.Muzzle != null)
            {
                joint = shipBody.gameObject.AddComponent<ConfigurableJoint>();
                joint.autoConfigureConnectedAnchor = false;
                joint.anchor = shipBody.transform.InverseTransformPoint(Gun.Muzzle.position);

                float dist = Vector3.Distance(Gun.Muzzle.position, contact.point);
                currentCableLength = Mathf.Clamp(dist, 8f, 45f);

                if (isShip && collision.collider.attachedRigidbody != null)
                {
                    joint.connectedBody = collision.collider.attachedRigidbody;
                    joint.connectedAnchor = collision.collider.attachedRigidbody.transform.InverseTransformPoint(contact.point);
                }
                else
                {
                    joint.connectedAnchor = contact.point;
                }

                joint.xMotion = ConfigurableJointMotion.Limited;
                joint.yMotion = ConfigurableJointMotion.Limited;
                joint.zMotion = ConfigurableJointMotion.Limited;
                joint.linearLimit = new SoftJointLimit { limit = currentCableLength };
                joint.linearLimitSpring = new SoftJointLimitSpring { spring = 80000f, damper = 8000f };
                joint.angularXMotion = ConfigurableJointMotion.Free;
                joint.angularYMotion = ConfigurableJointMotion.Free;
                joint.angularZMotion = ConfigurableJointMotion.Free;
                joint.breakForce = Mathf.Infinity;
                joint.breakTorque = Mathf.Infinity;
            }

            GameAudio.Play(SoundCue.BulletMetal, transform.position, 1.0f);
            if (Gun != null) Gun.OnProjectileAttached(this);
        }

        public void SetCableLength(float length)
        {
            currentCableLength = Mathf.Clamp(length, 8f, 45f);
            if (joint != null)
            {
                joint.linearLimit = new SoftJointLimit { limit = currentCableLength };
            }
        }

        public void DetachAndRewind()
        {
            if (state == HarpoonState.Rewinding) return;
            state = HarpoonState.Rewinding;
            if (joint != null)
            {
                Destroy(joint);
                joint = null;
            }
            hitParent = null;
            transform.SetParent(null);
            body.isKinematic = true;
            if (col != null) col.enabled = false;
            if (Gun != null) Gun.OnProjectileRewinding(this);
        }

        void OnDestroy()
        {
            if (joint != null) Destroy(joint);
        }
    }
}
