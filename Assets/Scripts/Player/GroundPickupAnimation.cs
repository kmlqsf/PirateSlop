using UnityEngine;
using PirateSlop.Networking;

namespace PirateSlop
{
    [DefaultExecutionOrder(70)]
    public sealed class GroundPickupAnimation : MonoBehaviour
    {
        Animator animator;
        NetworkFishing fishing;
        int layer;
        bool wasPickingUp;
        float nextFinish;

        void Awake()
        {
            animator = GetComponent<SabreAnimation>()?.BodyAnimator;
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            fishing = GetComponent<NetworkFishing>();
            layer = animator != null ? animator.GetLayerIndex("GroundPickup") : -1;
        }

        void Update()
        {
            if (animator == null || layer < 0) return;
            bool pickingUp = fishing != null && fishing.IsPickingUp;
            if (pickingUp && !wasPickingUp)
            {
                animator.Play("GroundPickup.Pickup", layer, 0f);
                nextFinish = 0f;
            }
            animator.SetLayerWeight(layer, Mathf.MoveTowards(animator.GetLayerWeight(layer), pickingUp ? 1f : 0f, Time.deltaTime * 10f));
            if (pickingUp && Time.time >= nextFinish && animator.GetCurrentAnimatorStateInfo(layer).IsName("GroundPickup.Pickup") && animator.GetCurrentAnimatorStateInfo(layer).normalizedTime >= 1f)
            {
                fishing.FinishPickup();
                nextFinish = Time.time + .15f;
            }
            wasPickingUp = pickingUp;
        }

        void OnDisable()
        {
            if (animator != null && layer >= 0) animator.SetLayerWeight(layer, 0f);
            wasPickingUp = false;
        }
    }
}
