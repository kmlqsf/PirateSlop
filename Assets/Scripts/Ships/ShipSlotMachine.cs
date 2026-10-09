using PirateSlop.Networking;
using UnityEngine;
using System.Collections.Generic;

namespace PirateSlop.Ships
{
    [DisallowMultipleComponent]
    public sealed class ShipSlotMachine : MonoBehaviour
    {
        public ShipSlotMachineSettings Settings;
        public Transform[] Reels;
        public Transform Lever, FishSlot, LeverGrip, PrizeOutlet, IntakeVisual;
        public Vector3 IntakeStart = new(0, 1.53f, .78f), IntakeEnd = new(0, 1.53f, -.1f);
        public static readonly HashSet<ShipSlotMachine> Active = new();
        public Collider Housing;
        public NetworkFish[] PrizePrefabs;
        NetworkShip ship;
        Quaternion leverRest;
        int seenSequence = -1, stoppedReels;
        bool ready;
        AudioSource spinAudio;
        bool returnPlayed;
        byte shownPhase;
        float shownPull;
        Vector3 intakeScale;
        public NetworkShip Ship => ship != null ? ship : ship = GetComponentInParent<NetworkShip>();
        public float Clock => Ship != null && Ship.TimeManager != null ? (float)Ship.TimeManager.Tick * (float)Ship.TimeManager.TickDelta : 0f;
        public NetworkFish Prefab(InventoryItem item)
        {
            int index = CannonAmmo.IsBall(item) ? (int)InventoryItem.Cannonball : (int)item;
            return PrizePrefabs != null && index >= 0 && index < PrizePrefabs.Length ? PrizePrefabs[index] : null;
        }
        void Awake() { if (Lever != null) leverRest = Lever.localRotation; if (IntakeVisual != null) intakeScale = IntakeVisual.localScale; }
        void OnEnable() { Active.Add(this); }
        void LateUpdate()
        {
            if (Ship == null || !Ship.IsSpawned || Settings == null) return;
            Ship.TickSlotMachine(this);
            var state = Ship.SlotState;
            float age = Mathf.Max(0f, Clock - state.StartedAt);
            bool changed = state.Sequence != seenSequence;
            bool audible = ready && (changed || state.Phase == 2) && age < Settings.SpinSeconds + .25f;
            if (changed)
            {
                if (ready && shownPhase == 2 && state.Phase == 3 && (stoppedReels & 4) == 0)
                    GameAudio.Play(SoundCue.SlotReelStop, Reels[2].position);
                seenSequence = state.Sequence;
                shownPhase = state.Phase;
                stoppedReels = 0;
                returnPlayed = false;
                if (!ready && state.Phase == 2)
                    for (int i = 0; i < 3; i++)
                        if (age >= Settings.FirstReelSeconds + Settings.ReelStopGap * i) stoppedReels |= 1 << i;
                if (ready && state.Phase == 4) GameAudio.Play(SoundCue.SlotFishInsert, FishSlot.position);
            }
            float spinScale = state.Phase == 2 ? Mathf.Clamp01((Settings.SpinSeconds - age) / 1.5f) * .65f : 0f;
            GameAudio.Loop(ref spinAudio, SoundCue.SlotReelSpin, transform, spinScale);
            if (spinAudio != null)
            {
                spinAudio.pitch = 1f; spinAudio.loop = false;
                if (changed && state.Phase == 2 && spinAudio.clip != null && age < spinAudio.clip.length)
                    spinAudio.time = age;
                if (state.Phase != 2 || age >= Settings.SpinSeconds) GameAudio.StopLoop(spinAudio);
            }
            if (state.Phase == 2 && age >= .25f && !returnPlayed)
            {
                returnPlayed = true;
                if (audible && age < .6f) GameAudio.Play(SoundCue.SlotLeverReturn, LeverGrip.position);
            }
            if (Reels != null)
                for (int i = 0; i < Reels.Length && i < 3; i++)
                {
                    if (Reels[i] == null) continue;
                    float angle = -state.Symbol(i) * 60f;
                    if (state.Phase == 2)
                    {
                        float duration = Settings.FirstReelSeconds + Settings.ReelStopGap * i;
                        float t = Mathf.Clamp01(age / duration);
                        float travel = 360f * (7 + i) + Mathf.Repeat(angle - state.Previous(i) * -60f, 360f);
                        angle = -state.Previous(i) * 60f + travel * (1f - Mathf.Pow(1f - t, 3f));
                        if (t >= 1f && (stoppedReels & (1 << i)) == 0)
                        {
                            stoppedReels |= 1 << i;
                            if (audible) GameAudio.Play(SoundCue.SlotReelStop, Reels[i].position);
                        }
                    }
                    Reels[i].localRotation = Quaternion.AngleAxis(angle, Vector3.right);
                }
            if (Lever != null)
            {
                float pull = state.Phase == 2 ? 1f - Mathf.Clamp01((age - .25f) / .6f) : state.Phase == 1 ? state.LeverPull : 0f;
                if (!ready) shownPull = pull;
                float previousPull = shownPull;
                shownPull = state.Phase == 2 ? pull : Mathf.MoveTowards(shownPull, pull, Time.deltaTime * 4f);
                bool manualPull = state.Phase == 1 && state.LeverHeld || changed && state.Phase == 2 && age < .25f;
                if (ready && manualPull && shownPull > previousPull &&
                    Mathf.FloorToInt(shownPull * 10f) > Mathf.FloorToInt(previousPull * 10f))
                    GameAudio.Play(SoundCue.SlotLeverPull, LeverGrip.position);
                Lever.localRotation = leverRest * Quaternion.AngleAxis(65f * shownPull, Vector3.right);
            }
            if (IntakeVisual != null)
            {
                IntakeVisual.gameObject.SetActive(state.Phase == 4);
                if (state.Phase == 4)
                {
                    float t = Mathf.Clamp01(age / Settings.IntakeSeconds);
                    float travel = t * t * (3f - 2f * t);
                    IntakeVisual.localPosition = Vector3.Lerp(IntakeStart, IntakeEnd, travel);
                    IntakeVisual.localScale = intakeScale * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.65f, 1f, t)));
                }
            }
            ready = true;
        }
        void OnDisable() { Active.Remove(this); GameAudio.StopLoop(spinAudio); ready = false; seenSequence = -1; }
        public bool CanReach(NetworkPlayer player, bool lever)
        {
            if (player == null || FishSlot == null || LeverGrip == null || Ship == null || !Ship.IsSpawned || Ship.IsSinking ||
                player.Eliminated.Value || player.Motor.IsDead || player.Motor.IsFrozen || player.Motor.IsSwimming ||
                player.Motor.IsClimbing || player.Motor.IsDowned || player.Motor.LocomotionLocked &&
                !(player.Motor.ShipActivityLocked && player.GetComponent<ShipSlotMachinePlayer>()?.HeldMachine == this)) return false;
            var weapon = player.GetComponent<NetworkWeapon>();
            var hands = player.GetComponent<CannonHands>();
            var fishing = player.GetComponent<NetworkFishing>();
            var point = lever ? transform.TransformPoint(new Vector3(0, 1.1f, .45f)) : FishSlot.position;
            return weapon != null && !weapon.LootHandsBusy &&
                (hands == null || !hands.HasHeldBall) &&
                (fishing == null || !fishing.CarryingCatch && !fishing.IsFishing && !fishing.IsEating) &&
                Vector3.Distance(player.transform.position + Vector3.up, point) <= 3f && weapon.CanReach(point, transform);
        }
        public string IntakeHint()
        {
            var state = Ship.SlotState;
            if (state.Phase == 4) return "Автомат принимает рыбу…";
            return state.Phase == 0 ? "E — вставить одну обычную рыбу в верхний слот" : "Приёмник занят";
        }
        public static string SymbolName(SlotSymbol symbol) => symbol switch
        {
            SlotSymbol.Fish => "три рыбы", SlotSymbol.SkillPoint => "очко навыка", SlotSymbol.Mystery => "случайный предмет",
            SlotSymbol.Cannonball => "особое ядро", SlotSymbol.Weapon => "случайное оружие", _ => "пушка"
        };
    }
}
