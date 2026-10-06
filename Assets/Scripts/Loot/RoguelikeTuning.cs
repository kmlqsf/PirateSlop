using System;
using System.IO;
using UnityEngine;

namespace PirateSlop
{
    public enum UpgradeEffect
    {
        SeaLegs,
        DoubleJump,
        LightBoots,
        DeckAcrobat,
        ToughPirate,
        LeadResolve,
        SecondWind,
        CharmedCoin,
        SeaPact,
        SabreMaster,
        Bloodletter,
        GhostSabre,
        DryPowder,
        QuickHands,
        Sharpshooter,
        Ricochet,
        ShotEcho,
        LongRangeCharge,
        GunnersEye,
        HeavyCannonball,
        FastCarpenterCommon,
        FastCarpenterRare,
        Helmsman,
        HeartyCatch,
        LuckyBaitCommon,
        LuckyBaitRare,
        UnusualCatch,
        FishSupply,
        PirateFeast,
        Seeker,
        SharpEye,
        LuckyCoin,
        SplitVolley,
        WaterRun,
        PushingBullets,
        ExtraPocket,
        ExtraAmmoSlot,
        RopeSprinter,
        RumSupply,
        LuckyDie,
        ExtraMonkey
    }

    [Serializable]
    public sealed class RoguelikeTuning
    {
        public float runMultiplier = 1.3f, jumpHeightMultiplier = 1.5f, maxHealthMultiplier = 2f;
        public float knockbackMultiplier = .6f, regenDelay = 12f, regenPerSecond = .02f, regenLimit = .5f;
        public float coinInvulnerability = 2f, pactDelay = 6f, pactHealth = .5f;
        public float sabreMultiplier = 1.2f, bleedFraction = .25f, bleedDuration = 4f;
        public int ghostSwingInterval = 4;
        public float ghostRange = 8f, ghostSpeed = 22f, ghostRadius = .4f;
        public float firearmMultiplier = 1.15f, reloadMultiplier = .8f, headshotMultiplier = 1.5f;
        public float ricochetRange = 10f, ricochetDamage = .6f, echoDelay = .3f, echoDamage = .5f;
        public float cannonSpeedMultiplier = 1.3f, knockdownMultiplier = 1.3f;
        public float repairCommon = 1.25f, repairRare = 1.5f, helmMultiplier = 1.25f;
        public float fishHealingMultiplier = 1.3f, fishingCommon = 1.25f, fishingRare = 1.5f;
        public float unusualCatchMultiplier = 1.5f, feastFraction = .5f;
        public int fishSupplyCount = 8, rerolls = 2, rerollChestInterval = 3;
        public float spyglassObserveSeconds = 2f, spyglassMarkSeconds = 15f, splitAngle = 10f;
        public float waterRunSeconds = 4f, waterRechargeSeconds = 10f, bulletPushSpeed = 1.5f, ropeMultiplier = 3f;
        public static RoguelikeTuning Current { get; private set; } = new();
        public static void Load()
        {
            Current = new();
            try
            {
                string path = Path.Combine(Application.streamingAssetsPath, "Roguelike", "UpgradeEffects.json");
                if (File.Exists(path)) Receive(File.ReadAllText(path));
            }
            catch (Exception e) { Debug.LogWarning("ROGUELIKE_EFFECTS: " + e.Message); }
        }
        public static void Receive(string json)
        {
            var config = JsonUtility.FromJson<RoguelikeTuning>(json);
            if (config == null) return;
            foreach (var field in typeof(RoguelikeTuning).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                if (field.FieldType == typeof(float) && (!float.IsFinite((float)field.GetValue(config)) || (float)field.GetValue(config) <= 0f))
                    field.SetValue(config, field.GetValue(new RoguelikeTuning()));
                if (field.FieldType == typeof(int) && (int)field.GetValue(config) < 1)
                    field.SetValue(config, field.GetValue(new RoguelikeTuning()));
            }
            Current = config;
        }
    }
}
