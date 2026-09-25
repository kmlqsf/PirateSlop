using UnityEngine;

namespace PirateSlop
{
    [RequireComponent(typeof(Collider))]
    public sealed class WhaleHarpoonPin : MonoBehaviour
    {
        public WhaleLootPoint Owner;
        public float PullDuration = 1.6f;
        public Vector3 LocalPullDirection = Vector3.forward;
        public float MaxPullDistance = 0.85f;

        Vector3 initialLocalPosition;
        Quaternion initialLocalRotation;
        float progress;
        bool isRemoved;
        bool beingPulled;

        public float Progress => progress;
        public bool IsRemoved => isRemoved;

        void Awake()
        {
            initialLocalPosition = transform.localPosition;
            initialLocalRotation = transform.localRotation;
        }

        public void RegisterPullThisFrame()
        {
            if (isRemoved) return;
            beingPulled = true;
            if (Owner != null) Owner.NotifyPullStarted();
        }

        void Update()
        {
            if (isRemoved) return;

            if (beingPulled)
            {
                progress += Time.deltaTime / Mathf.Max(0.1f, PullDuration);
                Vector3 shake = Random.insideUnitSphere * (0.025f * progress);
                transform.localPosition = initialLocalPosition + LocalPullDirection * (progress * MaxPullDistance) + shake;

                if (progress >= 1f)
                {
                    EjectHarpoon();
                }
            }
            else
            {
                progress = Mathf.MoveTowards(progress, 0f, Time.deltaTime * 1.8f);
                transform.localPosition = initialLocalPosition + LocalPullDirection * (progress * MaxPullDistance);
            }

            beingPulled = false;
        }

        void EjectHarpoon()
        {
            isRemoved = true;
            progress = 1f;
            transform.SetParent(null, true);

            var rb = gameObject.AddComponent<Rigidbody>();
            rb.mass = 25f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            Vector3 throwDir = (-transform.forward + Vector3.up * 0.8f + Random.insideUnitSphere * 0.25f).normalized;
            rb.AddForce(throwDir * 13f, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * 16f, ForceMode.Impulse);

            GameAudio.Play(SoundCue.BulletFlesh, transform.position);

            if (Owner != null)
            {
                Owner.OnHarpoonRemoved(this);
            }

            var splashTracker = gameObject.AddComponent<HarpoonWaterImpact>();
            splashTracker.Initialize(0f);
        }
    }

    public sealed class HarpoonWaterImpact : MonoBehaviour
    {
        float waterHeight;
        bool splashed;

        public void Initialize(float surfaceHeight)
        {
            waterHeight = surfaceHeight;
            Destroy(gameObject, 6f);
        }

        void Update()
        {
            if (!splashed && transform.position.y <= waterHeight + 0.2f)
            {
                splashed = true;
                GameAudio.Play(SoundCue.WaterSplash, transform.position);
                var rb = GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearDamping = 4f;
                    rb.angularDamping = 3f;
                }
            }
        }
    }
}
