using System;
using UnityEngine;
using PirateSlop.Networking;

namespace PirateSlop
{
    public enum ShipSectionState : byte { Intact, Damaged, Critical, Destroyed, Repaired }
    public enum ShipSectionType : byte { Hull, Deck, Mast, Yard, Bowsprit, Rudder, Railing, Helm, Stairs, Fitting }
    public enum ShipDamageReason : byte { Hit, Fire, SupportLost, Flooding, Scripted }

    [Serializable]
    public struct ShipAmmoMultiplier
    {
        public InventoryItem Ammo;
        public float Multiplier;
    }

    [Serializable]
    public sealed class ShipSectionDefinition
    {
        public int SectionId;
        public string Name;
        public string SourceGroup;
        public ShipSectionType Type;
        public float MaxHealth = 180f;
        [Range(0f, 1f)] public float DamagedThreshold = .75f, CriticalThreshold = .3f;
        public ShipAmmoMultiplier[] Ammo = Array.Empty<ShipAmmoMultiplier>();
        public int[] Supports = Array.Empty<int>();
        public bool RequireAllSupports = true;
        public int RedirectSectionId;
        public bool CanFlood;
        public float BreachArea = 1f;
        public Vector3 BreachAnchor;
        public float DamagedLeak = .1f, CriticalLeak = .4f;
        public string[] SailNames = Array.Empty<string>();
        [Range(0f, 1f)] public float DamagedEfficiency = .8f, CriticalEfficiency = .35f, DestroyedEfficiency;
        public bool Repairable;
        [Range(0f, 1f)] public float MaxRepairHealth = .6f;
        public float DebrisMass = 80f, DebrisImpulse = 4f, DebrisLifetime = 15f, AngularSpeed = 2f;
        public int MaxDebris = 4;
        public float DamageMultiplier(InventoryItem ammo)
        {
            foreach (var item in Ammo) if (item.Ammo == ammo) return Mathf.Max(0f, item.Multiplier);
            return 1f;
        }
        public float Efficiency(ShipSectionState state) => state == ShipSectionState.Destroyed ? DestroyedEfficiency : state == ShipSectionState.Critical ? CriticalEfficiency : state == ShipSectionState.Intact ? 1f : DamagedEfficiency;
    }

    [CreateAssetMenu(menuName = "PirateSlop/Ship Destruction Profile")]
    public sealed class ShipDestructionProfile : ScriptableObject
    {
        public ShipSectionDefinition[] Sections = Array.Empty<ShipSectionDefinition>();
        public ShipFragmentConnection[] Structure = Array.Empty<ShipFragmentConnection>();
        public GameObject SectionsPrefab;
        public bool EnableFlooding;
        public Mesh[] SplinterMeshes = Array.Empty<Mesh>();
        public Material SplinterMaterial;
        public int SplintersPerHit = 12;
        public int FallbackSectionId;
        public float CannonDamage = 65f, FireDamagePerSecond = 4f;
        public float FloodCoefficient = .0015f, MaximumDepth = 8f;
        public float MediumWater = .3f, HighWater = .65f, CriticalWater = .95f;
        public float FloodSpeed = .45f, FloodAcceleration = .35f, FloodRudder = .35f, FloodHeel = 7f;
        public int PhysicalLimit = 12, CosmeticLimit = 48, PrewarmPerFrame = 4;
        public float DebrisDistance = 180f, LinearDamping = .4f, AngularDamping = .5f;
        public string DebrisLayer = "ShipDebris";
        public SoundCue BreakSound = SoundCue.Creak, FallSound = SoundCue.ShipCollision;
    }

    [Serializable]
    public sealed class ShipFragmentConnection
    {
        public int SectionId, Fragment;
        public int[] Neighbours = Array.Empty<int>();
        public bool Anchor, LoadBearing = true;
    }
}
