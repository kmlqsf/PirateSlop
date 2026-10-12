using UnityEngine;
using PirateSlop.Networking;

namespace PirateSlop
{
    [CreateAssetMenu(menuName = "PirateSlop/Staff Settings")]
    public sealed class StaffSettings : ScriptableObject
    {
        public InventoryItem Item = InventoryItem.FireStaff;
        [Min(.1f)] public float CastSeconds = 2f;
        [Min(.1f)] public float Cooldown = 1.5f;
        [Min(1f)] public float Range = 100f;
        [Min(1f)] public float ProjectileSpeed = 12f;
        [Min(1f)] public float TurnDegreesPerSecond = 120f;
        [Min(.1f)] public float Lifetime = 8f;
        [Min(.01f)] public float ProjectileRadius = .18f;
        [Min(0f)] public float Damage = 25f;
        [Min(0f)] public float BeamDamagePerSecond = 2f;
        [Min(.1f)] public float BurnSeconds = 2f;
        [Min(.1f)] public float BlastRadius = 3f;
        [Min(.1f)] public float SlowSeconds = 4f;
        [Range(.1f, 1f)] public float SlowMultiplier = .5f;
        [Min(1f)] public float WaterRadiusMultiplier = 5f;
        [Min(.1f)] public float FrozenWaterSeconds = 7f;
        public Color Color = new(1f, .25f, .025f);
        public Vector3 ViewPosition = new(.3f, -.28f, .6f);
        public Vector3 LaunchOffset = new(.25f, -.05f, .8f);
    }
}
