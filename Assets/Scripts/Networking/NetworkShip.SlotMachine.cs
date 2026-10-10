using System;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using PirateSlop.Ships;
using UnityEngine;

namespace PirateSlop.Networking
{
    public struct ShipSlotSnapshot
    {
        public int Sequence, Payer;
        public byte Phase, First, Second, Third, BeforeFirst, BeforeSecond, BeforeThird;
        public float StartedAt, LeverPull;
        public bool LeverHeld;
        public bool Win => First == Second && Second == Third;
        public int Symbol(int index) => index == 0 ? First : index == 1 ? Second : Third;
        public int Previous(int index) => index == 0 ? BeforeFirst : index == 1 ? BeforeSecond : BeforeThird;
    }

    public sealed partial class NetworkShip
    {
        readonly SyncVar<ShipSlotSnapshot> slotState = new();
        readonly System.Random slotRandom = new();
        readonly Queue<InventoryItem> slotPrizes = new();
        ShipSlotMachine slotMachine;
        ShipMonkey slotMonkey;
        PlayerUpgradeState slotPayer;
        bool slotSkill, slotRewardPending;
        float slotNextPrize, slotLeverHeartbeat;
        int slotPrizeIndex;
        public ShipSlotSnapshot SlotState => slotState.Value;
        public void UseSlotMachine(byte action, int selected) => UseSlotMachineServerRpc(action, selected, slotState.Value.Sequence);

        [ServerRpc(RequireOwnership = false)]
        void UseSlotMachineServerRpc(byte action, int selected, int expectedSequence, NetworkConnection sender = null)
        {
            if (sender == null || !sender.IsActive || SessionController.Instance == null || IsSinking) return;
            slotMachine ??= GetComponentInChildren<ShipSlotMachine>();
            var machine = slotMachine;
            if (machine == null || machine.Settings == null) return;
            var player = SessionController.Instance.GetPlayer(sender.ClientId);
            if (!machine.CanReach(player, action == 2) || action != 0 && action != 2) return;
            var state = slotState.Value;
            if (state.Sequence != expectedSequence) return;
            if (action == 0)
            {
                if (state.Phase != 0) return;
                if (!PrizePoolReady(machine) || player.UpgradeState == null || !player.GetComponent<NetworkWeapon>().ConsumeSlotFish(selected))
                { SlotRejectedTargetRpc(sender); return; }
                slotPayer = player.UpgradeState;
                slotMonkey = null;
                state.Sequence++; state.Phase = 4; state.Payer = player.ParticipantId.Value; state.StartedAt = machine.Clock;
                state.LeverPull = 0; state.LeverHeld = false;
                slotState.Value = state;
            }
            else if ((state.Phase == 1 || state.Phase == 4) && slotPayer != null && slotPayer == player.UpgradeState)
            {
                if (action == 2) RefundSlotFish(machine);
            }
        }

        public void PullSlotLever(float delta, bool holding) => PullSlotLeverServerRpc(delta, holding, slotState.Value.Sequence);

        public bool BeginMonkeySlotTurn(ShipMonkey actor, ShipSlotMachine machine)
        {
            if (!IsServerInitialized || !IsSpawned || IsSinking || actor == null || machine == null ||
                actor.GetComponentInParent<NetworkShip>() != this || machine.Ship != this || slotState.Value.Phase != 0 ||
                !PrizePoolReady(machine, false) || !MonkeySlotReach(actor, machine)) return false;
            slotMachine = machine; slotMonkey = actor; slotPayer = null;
            var state = slotState.Value;
            state.Sequence++; state.Phase = 1; state.Payer = -1; state.StartedAt = machine.Clock;
            state.LeverPull = 0; state.LeverHeld = true; slotLeverHeartbeat = Time.time;
            slotState.Value = state; return true;
        }

        bool MonkeySlotReach(ShipMonkey actor, ShipSlotMachine machine) => actor != null && actor.Visual != null &&
            actor.Visual.gameObject.activeInHierarchy && Vector3.Distance(actor.HeldPoint, machine.LeverGrip.position) <= 2f;

        public bool PullMonkeySlotLever(ShipMonkey actor, float delta)
        {
            if (!IsServerInitialized || actor == null || actor != slotMonkey || slotMachine == null || IsSinking ||
                slotState.Value.Phase != 1 || !float.IsFinite(delta) || !MonkeySlotReach(actor, slotMachine)) return false;
            var state = slotState.Value;
            slotLeverHeartbeat = Time.time; state.LeverHeld = true;
            state.LeverPull = Mathf.Clamp01(state.LeverPull + Mathf.Clamp(delta, 0f, .15f));
            if (state.LeverPull >= .999f)
            {
                PrepareSlotResult(slotMachine, ref state, true);
                state.Sequence++; state.Phase = 2; state.StartedAt = slotMachine.Clock; state.LeverHeld = false;
                slotRewardPending = true;
            }
            slotState.Value = state; return true;
        }

        public void ReleaseMonkeySlot(ShipMonkey actor)
        {
            if (!IsServerInitialized || actor == null || actor != slotMonkey) return;
            if (slotState.Value.Phase == 1) ResetMonkeySlot();
            else slotMonkey = null;
        }

        void ResetMonkeySlot()
        {
            var state = slotState.Value; state.Sequence++; state.Phase = 0; state.Payer = 0;
            state.LeverPull = 0; state.LeverHeld = false; slotState.Value = state;
            slotMonkey = null; slotPayer = null; slotPrizes.Clear(); slotSkill = slotRewardPending = false;
        }

        [ServerRpc(RequireOwnership = false)]
        void PullSlotLeverServerRpc(float delta, bool holding, int expectedSequence, NetworkConnection sender = null)
        {
            if (sender == null || !sender.IsActive || SessionController.Instance == null) return;
            slotMachine ??= GetComponentInChildren<ShipSlotMachine>();
            var machine = slotMachine;
            var player = SessionController.Instance.GetPlayer(sender.ClientId);
            var state = slotState.Value;
            if (machine == null || player == null || state.Sequence != expectedSequence || state.Phase != 1 ||
                slotPayer == null || slotPayer != player.UpgradeState) return;
            if (!holding) { state.LeverHeld = false; slotState.Value = state; return; }
            if (!float.IsFinite(delta) || !machine.CanReach(player, true)) return;
            float allowed = state.LeverHeld ? Mathf.Clamp(Time.time - slotLeverHeartbeat, 0f, .15f) * 3f : 0f;
            slotLeverHeartbeat = Time.time;
            state.LeverHeld = true;
            state.LeverPull = Mathf.Clamp01(state.LeverPull + Mathf.Clamp(delta, -allowed, allowed));
            if (state.LeverPull >= .999f)
            {
                PrepareSlotResult(machine, ref state);
                state.Sequence++; state.Phase = 2; state.StartedAt = machine.Clock; state.LeverHeld = false;
                slotRewardPending = true;
            }
            slotState.Value = state;
        }

        static bool SlotWeapon(InventoryItem item) => item == InventoryItem.Pistol || item == InventoryItem.Sabre ||
            item == InventoryItem.Musket || item == InventoryItem.DoubleBarrel || item == InventoryItem.HandMortar || item == InventoryItem.BombParrot ||
            item == InventoryItem.HolyGrenade || item == InventoryItem.ExplosiveBall || item == InventoryItem.Pufferfish || item == InventoryItem.Swordfish;
        static bool SlotEquipment(InventoryItem item) => !SlotWeapon(item) &&
            item != InventoryItem.Plank && item != InventoryItem.None;

        bool PrizePoolReady(ShipSlotMachine machine, bool upgrades = true)
        {
            return machine.Settings.WinWeights != null && machine.Settings.WinWeights.Length == 6 &&
                machine.PrizeOutlet != null && machine.Prefab(InventoryItem.Fish) != null && machine.Prefab(InventoryItem.Cannon) != null &&
                SessionController.Instance != null && (!upgrades || SessionController.Instance.SlotUpgradeAvailable);
        }

        void PrepareSlotResult(ShipSlotMachine machine, ref ShipSlotSnapshot state, bool monkeyTurn = false)
        {
            slotPrizes.Clear(); slotSkill = false; slotPrizeIndex = 0;
            state.BeforeFirst = state.First; state.BeforeSecond = state.Second; state.BeforeThird = state.Third;
            double wins = 0;
            for (int i = 0; i < 6; i++)
                if (!monkeyTurn || i != (int)SlotSymbol.SkillPoint) wins += SafeSlotWeight(machine.Settings.WinWeights[i]);
            double total = monkeyTurn ? wins : wins + SafeSlotWeight(machine.Settings.LossWeight);
            double roll = slotRandom.NextDouble() * total;
            int symbol = -1;
            bool allowWin = !monkeyTurn || slotRandom.NextDouble() >= .8d;
            for (int i = 0; allowWin && i < 6; i++)
            {
                if (monkeyTurn && i == (int)SlotSymbol.SkillPoint) continue;
                roll -= SafeSlotWeight(machine.Settings.WinWeights[i]);
                if (roll < 0) { symbol = i; break; }
            }
            if (symbol >= 0 && !PrepareSlotPrizes(machine, (SlotSymbol)symbol)) symbol = -1;
            if (symbol < 0)
            {
                slotPrizes.Clear(); slotSkill = false;
                state.First = (byte)slotRandom.Next(6); state.Second = (byte)slotRandom.Next(6); state.Third = (byte)slotRandom.Next(6);
                if (state.Win) state.Third = (byte)((state.Third + 1 + slotRandom.Next(5)) % 6);
            }
            else state.First = state.Second = state.Third = (byte)symbol;
        }
        static double SafeSlotWeight(float weight) => float.IsFinite(weight) && weight > 0f ? weight : 0d;
        bool PrepareSlotPrizes(ShipSlotMachine machine, SlotSymbol symbol)
        {
            if (symbol == SlotSymbol.SkillPoint)
            {
                if (!SessionController.Instance.CanAwardPersonalUpgrade(slotPayer)) return false;
                slotSkill = true; return true;
            }
            if (symbol == SlotSymbol.Fish)
            {
                for (int i = 0; i < 3; i++)
                {
                    var item = slotRandom.NextDouble() < machine.Settings.SpecialFishChance ? slotRandom.Next(2) == 0 ? InventoryItem.Pufferfish : InventoryItem.Swordfish : InventoryItem.Fish;
                    slotPrizes.Enqueue(machine.Prefab(item) != null ? item : InventoryItem.Fish);
                }
                return true;
            }
            if (symbol == SlotSymbol.Cannon) { slotPrizes.Enqueue(InventoryItem.Cannon); return machine.Prefab(InventoryItem.Cannon) != null; }
            InventoryItem prize;
            if (symbol == SlotSymbol.Cannonball)
            {
                var pool = new List<InventoryItem>();
                foreach (InventoryItem item in Enum.GetValues(typeof(InventoryItem)))
                    if (CannonAmmo.IsBall(item) && item != InventoryItem.Cannonball && machine.Prefab(item) != null) pool.Add(item);
                if (pool.Count == 0) return false;
                prize = pool[slotRandom.Next(pool.Count)];
            }
            else if (!ChestLootTable.TryRollItem(slotRandom, item => machine.Prefab(item) != null &&
                (symbol == SlotSymbol.Mystery ? SlotEquipment(item) : item == InventoryItem.Musket || item == InventoryItem.DoubleBarrel), out prize)) return false;
            slotPrizes.Enqueue(prize);
            return true;
        }
        public void TickSlotMachine(ShipSlotMachine machine)
        {
            if (!IsServerInitialized || !IsSpawned || machine == null) return;
            slotMachine = machine;
            var state = slotState.Value;
            if (state.Phase == 1 || state.Phase == 4)
            {
                if (state.Payer == -1 && slotPayer == null)
                {
                    if (IsSinking || slotMonkey == null || !MonkeySlotReach(slotMonkey, machine) ||
                        Time.time - slotLeverHeartbeat > 1f) ResetMonkeySlot();
                    return;
                }
                if (IsSinking || machine.Clock - state.StartedAt >= machine.Settings.PaidTimeout ||
                    slotPayer?.Player == null || !slotPayer.Player.IsSpawned || slotPayer.Player.Eliminated.Value) RefundSlotFish(machine);
                else if (state.Phase == 4 && machine.Clock - state.StartedAt >= machine.Settings.IntakeSeconds)
                {
                    state.Sequence++; state.Phase = 1; state.StartedAt = machine.Clock; slotState.Value = state;
                }
                else if (state.Phase == 1)
                {
                    if (state.LeverHeld && (Time.time - slotLeverHeartbeat > .65f || !machine.CanReach(slotPayer.Player, true))) state.LeverHeld = false;
                    if (!state.LeverHeld) state.LeverPull = Mathf.MoveTowards(state.LeverPull, 0f, Time.deltaTime * 3f);
                    slotState.Value = state;
                }
                return;
            }
            if (state.Phase == 2 && machine.Clock - state.StartedAt >= machine.Settings.SpinSeconds && slotRewardPending)
            {
                slotRewardPending = false;
                if (slotSkill) SessionController.Instance?.AwardPersonalUpgrade(slotPayer);
                state.Sequence++; state.Phase = 3; state.StartedAt = machine.Clock;
                slotState.Value = state; slotNextPrize = machine.Clock;
                SlotResultSoundObserversRpc(state.Win, state.First == (byte)SlotSymbol.SkillPoint || state.First == (byte)SlotSymbol.Cannon);
            }
            if (slotState.Value.Phase != 3) return;
            if (slotPrizes.Count > 0 && machine.Clock >= slotNextPrize)
            {
                EmitSlotPrize(machine, slotPrizes.Dequeue());
                slotNextPrize = machine.Clock + machine.Settings.PrizeGap;
            }
            if (slotPrizes.Count == 0 && machine.Clock - slotState.Value.StartedAt >= 2.2f)
            {
                state = slotState.Value; state.Sequence++; state.Phase = 0; state.Payer = 0; state.LeverPull = 0; state.LeverHeld = false;
                slotState.Value = state; slotPayer = null; slotMonkey = null;
            }
        }
        void RefundSlotFish(ShipSlotMachine machine)
        {
            var player = slotPayer?.Player;
            if (player == null || !player.IsSpawned || !player.GetComponent<NetworkWeapon>().AddItem(InventoryItem.Fish)) EmitSlotPrize(machine, InventoryItem.Fish);
            var state = slotState.Value; state.Sequence++; state.Phase = 0; state.Payer = 0; state.LeverPull = 0; state.LeverHeld = false;
            slotState.Value = state; slotPayer = null;
        }
        void EmitSlotPrize(ShipSlotMachine machine, InventoryItem prize)
        {
            var prefab = machine.Prefab(prize);
            if (prefab == null) return;
            Vector3 origin = machine.PrizeOutlet.position + machine.transform.forward * .12f;
            Vector3 offset = machine.transform.right * ((slotPrizeIndex % 3 - 1) * .4f);
            Vector3 candidate = machine.PrizeOutlet.position + machine.transform.forward * (.85f + (prize == InventoryItem.Cannon ? .45f : 0f)) + offset;
            Vector3 floor = candidate;
            float best = float.PositiveInfinity;
            foreach (var hit in Physics.RaycastAll(candidate + transform.up * .3f, -transform.up, 3f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<NetworkShip>() != this || hit.collider.transform.IsChildOf(machine.transform) || hit.distance >= best) continue;
                floor = hit.point; best = hit.distance;
            }
            var rotation = Quaternion.LookRotation(machine.transform.forward, transform.up) *
                Quaternion.Euler(0, 0, prize == InventoryItem.Fish || prize == InventoryItem.Pufferfish || prize == InventoryItem.Swordfish ? 90f : 0f);
            var extents = LootPlacement.VisualBounds(prefab);
            float lowest = float.PositiveInfinity;
            for (int i = 0; i < 8; i++)
            {
                var corner = Vector3.Scale(extents.center + Vector3.Scale(extents.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)), prefab.transform.localScale);
                lowest = Mathf.Min(lowest, Vector3.Dot(rotation * corner, transform.up));
            }
            float lift = .035f - lowest;
            var item = Instantiate(prefab, origin, rotation);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(item.gameObject, gameObject.scene);
            if (CannonAmmo.IsBall(prize)) item.SetAmmoItem(prize);
            item.Place(NetworkObject, origin, rotation);
            ServerManager.Spawn(item.NetworkObject);
            var ball = item.GetComponent<Cannonball>();
            if (ball != null) ball.Eject(machine.Housing, Motor.CannonPointVelocity(origin) + machine.transform.forward * 1.5f + transform.up * .4f);
            else item.gameObject.AddComponent<SlotPrizeFlight>().Begin(item, this, origin, floor + transform.up * lift, rotation, machine.Settings.PrizeFlightSeconds);
            slotPrizeIndex++;
            SlotPrizeSoundObserversRpc(prize, origin);
        }
        [ObserversRpc(RunLocally = true)]
        void SlotResultSoundObserversRpc(bool win, bool rare)
        {
            var machine = GetComponentInChildren<ShipSlotMachine>();
            if (machine != null) GameAudio.Play(win ? rare ? SoundCue.SlotJackpot : SoundCue.SlotWin : SoundCue.SlotLose, machine.transform.position);
        }
        [ObserversRpc(RunLocally = true)]
        void SlotPrizeSoundObserversRpc(InventoryItem item, Vector3 point) => GameAudio.Play(item == InventoryItem.Fish || item == InventoryItem.Pufferfish || item == InventoryItem.Swordfish ? SoundCue.SlotFishPayout : SoundCue.SlotPrizePayout, point);
        [TargetRpc]
        void SlotRejectedTargetRpc(NetworkConnection connection)
        {
            GameAudio.Play(SoundCue.SlotReject, transform.position, 1f, true);
            foreach (var player in NetworkPlayer.Active)
                if (player != null && player.IsOwner) player.GetComponent<PlayerInventory>()?.ShowMessage("Для ставки выберите слот с обычной рыбой");
        }
    }
}
