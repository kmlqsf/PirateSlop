using UnityEngine;

namespace PirateSlop
{
    [RequireComponent(typeof(AdvancedPlayerController))]
    public sealed class PlayerAnimatorDriver : MonoBehaviour
    {
        static readonly int Speed = Animator.StringToHash("Speed");
        static readonly int Crouched = Animator.StringToHash("Crouched");
        static readonly int Sliding = Animator.StringToHash("Sliding");
        static readonly int Grounded = Animator.StringToHash("Grounded");

        AdvancedPlayerController motor;
        Animator animator;
        float airborneTime;
        bool wasSwimming;
        string swimState;

        void Awake()
        {
            motor = GetComponent<AdvancedPlayerController>();
            animator = GetComponentInChildren<Animator>(true);
        }

        void LateUpdate()
        {
            if (animator == null || !animator.enabled || animator.runtimeAnimatorController == null) return;
            if (motor.IsSwimming)
            {
                var state = motor.PlanarSpeed > .3f ? "Swim" : "TreadWater";
                if (!wasSwimming || state != swimState) animator.CrossFadeInFixedTime(state, .2f);
                swimState = state; wasSwimming = true;
                return;
            }
            if (wasSwimming) { animator.CrossFadeInFixedTime("Locomotion", .15f); wasSwimming = false; }
            animator.SetFloat(Speed, motor.PlanarSpeed, 0.08f, Time.deltaTime);
            animator.SetBool(Crouched, motor.IsCrouched);
            animator.SetBool(Sliding, motor.IsSliding);
            airborneTime = motor.IsGrounded || motor.LocomotionLocked ? 0 : airborneTime + Time.deltaTime;
            animator.SetBool(Grounded, motor.IsGrounded || motor.LocomotionLocked || (motor.VerticalSpeed <= 0 && airborneTime < .08f));
        }
    }
}
