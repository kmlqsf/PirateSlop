using PirateSlop;
using PirateSlop.Networking;
using UnityEngine;

public partial class AdvancedPlayerController
{
    NetworkPlayer upgradePlayer;
    bool airJumpUsed, waterReady = true, waterWasSolid, waterUpgradeInitialized;
    float waterRunRemaining, waterRecharge;
    bool Upgrade(UpgradeEffect effect)
    {
        if (upgradePlayer == null) upgradePlayer = GetComponent<NetworkPlayer>();
        return upgradePlayer != null && upgradePlayer.HasUpgrade(effect);
    }
    bool SimulateUpgradeWater(float dt, bool solid)
    {
        if (!Upgrade(UpgradeEffect.WaterRun)) { waterWasSolid = solid; return false; }
        if (!waterUpgradeInitialized) { waterUpgradeInitialized = true; waterReady = true; }
        if (solid)
        {
            waterRunRemaining = 0f;
            waterRecharge += dt;
            if (waterRecharge >= RoguelikeTuning.Current.waterRechargeSeconds) waterReady = true;
        }
        else
        {
            if (waterWasSolid && waterReady)
            {
                waterRunRemaining = RoguelikeTuning.Current.waterRunSeconds;
                waterReady = false;
            }
            waterRecharge = 0f;
            waterRunRemaining = Mathf.Max(0f, waterRunRemaining - dt);
        }
        waterWasSolid = solid;
        return waterRunRemaining > 0f && !IsKnockedBack;
    }
    float UpgradeSwimSpeed(bool submerged, bool sprint)
    {
        if (submerged && Upgrade(UpgradeEffect.SeaLegs))
        {
            var ship = upgradePlayer.Ship;
            if (ship != null && ship.Motor != null) return ship.Motor.BaseMaxSpeed;
        }
        return sprint ? fastSwimSpeed : swimSpeed;
    }
}
