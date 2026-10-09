using UnityEngine;

namespace PirateSlop
{
    [CreateAssetMenu(menuName = "PirateSlop/Hand Mortar Settings")]
    public sealed class HandMortarSettings : ScriptableObject
    {
        [Min(1)] public float LaunchSpeed = 22f;
        [Min(.1f)] public float FuseSeconds = 2.5f;
        [Min(.05f)] public float IgnitionSeconds = .2f;
        [Min(.2f)] public float ReloadSeconds = 10f;
        [Min(.1f)] public float ShotInterval = .6f;
        [Min(0)] public float ShooterKnockback = 14f;
        [Min(0)] public float ShooterLift = .8f;
        [Range(1, 5)] public float ShooterKnockdownSeconds = 2.5f;
        [Min(.01f)] public float BallRadius = .09f;
        [Range(0, 1)] public float Restitution = .6f;
        [Range(0, 1)] public float TangentialRetention = .8f;
        [Min(.1f)] public float BlastRadius = 4f;
        [Min(0)] public float BlastDamage = 100f;
        [Min(0)] public float DirectDamage = 100f;
        public bool ExplodeOnLivingHit = true;
        public bool ExplodeOnWaterEntry;
        public bool BlastFalloff = true;
        [Min(0)] public float OwnerGraceSeconds = .15f;
        public Vector3 ViewPosition = new(.24f, -.23f, .42f);
        public Vector3 LaunchOffset = new(.24f, -.11f, .97f);
    }
}
