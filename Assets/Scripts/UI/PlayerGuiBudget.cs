using System;
using System.Collections.Generic;
using PirateSlop.Networking;
using PirateSlop.Ships;
using UnityEngine;

namespace PirateSlop
{
    public static class PlayerGuiBudget
    {
        public static bool Enabled { get; private set; } = true;
        static readonly HashSet<Type> fixedLayout = new()
        {
            typeof(AdvancedPlayerController), typeof(BalanceStonePlayer), typeof(WhalePlayerInteraction),
            typeof(ClamPlayerInteraction), typeof(NetworkCrewBell), typeof(ShipV3PlayerInteraction),
            typeof(CrewPresentation), typeof(DirectShipControls), typeof(SwimPresentation),
            typeof(RiggingPlayerInteraction), typeof(SternCargoPlayerInteraction), typeof(NetworkShipPush),
            typeof(NetworkHullRepair), typeof(PlayerInventory), typeof(PlayerHud), typeof(CannonDismantle),
            typeof(CannonHands), typeof(NetworkFishing), typeof(PirateWeapon), typeof(NetworkEquipment),
            typeof(ShipSpyglassView), typeof(ContextPrompt), typeof(CombatHealth), typeof(DamageFeedback),
            typeof(PlayerFreezeScreen)
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => Enabled = !Array.Exists(Environment.GetCommandLineArgs(), a => a == "-performanceLegacyGuiLayout");

        public static void Configure(GameObject player)
        {
            if (!Enabled) return;
            foreach (var behaviour in player.GetComponents<MonoBehaviour>())
                if (behaviour != null && fixedLayout.Contains(behaviour.GetType())) behaviour.useGUILayout = false;
        }
    }
}
