using UnityEngine;

namespace PirateSlop
{
    public sealed class GameplayAudio : MonoBehaviour
    {
        AdvancedPlayerController player;
        PirateWeapon weapon;
        PlayerInventory inventory;
        CannonHands hands;
        ShipController ship;
        HelmInteraction helm;
        SailSystem sails;
        SimpleCannon cannon;
        float stepAt, motionAt, creakAt, rudder, sail, elevation;
        bool swimming, grounded, sliding, reloading, loaded, holding, cannonLoaded;
        int selected, slots, lastStepPhase = -1;
        bool ready, centerArmed;
        float centerRudder;
        float wheelAt, ropeAt, wheelLastMotion, wheelIdleSwitchAt;
        int wheelDirection, wheelIdleVoice = -1;
        bool wheelHeld;
        GameAudioBank wheelBank;
        GameAudioBank.Entry wheelIdleEntry;
        readonly AudioSource[] wheelIdleSources = new AudioSource[2];
        void Awake()
        {
            player = GetComponent<AdvancedPlayerController>(); weapon = GetComponent<PirateWeapon>();
            inventory = GetComponent<PlayerInventory>(); hands = GetComponent<CannonHands>();
            ship = GetComponent<ShipController>(); helm = GetComponentInChildren<HelmInteraction>();
            sails = GetComponent<SailSystem>(); cannon = GetComponent<SimpleCannon>();
            if (ship != null && helm != null)
            {
                wheelBank = Resources.Load<GameAudioBank>("GameAudioBank");
                if (wheelBank != null && wheelBank.Entries != null)
                    foreach (var entry in wheelBank.Entries)
                        if (entry != null && entry.Cue == SoundCue.WheelIdleLeather) { wheelIdleEntry = entry; break; }
            }
        }
        static readonly RaycastHit[] stepHits = new RaycastHit[16];
        static readonly System.Collections.Generic.Dictionary<Collider, bool> stoneColliderCache = new();

        static bool IsStoneSurface(Collider col)
        {
            if (col == null) return false;
            if (stoneColliderCache.TryGetValue(col, out bool isStone)) return isStone;
            string colName = col.name;
            var mat = col.sharedMaterial;
            string matName = mat != null ? mat.name : null;
            isStone = (colName != null && (colName.IndexOf("stone", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                           colName.IndexOf("rock", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                           colName.IndexOf("reef", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                           colName.IndexOf("cliff", System.StringComparison.OrdinalIgnoreCase) >= 0)) ||
                      (matName != null && (matName.IndexOf("stone", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                           matName.IndexOf("rock", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                           matName.IndexOf("reef", System.StringComparison.OrdinalIgnoreCase) >= 0));
            stoneColliderCache[col] = isStone;
            return isStone;
        }

        SoundCue StepCue()
        {
            var passenger = GetComponent<ShipDeckPassenger>();
            if (passenger != null && passenger.Ship != null)
                return player.PlanarSpeed > 5f ? SoundCue.FootstepWoodRun : SoundCue.FootstepWood;
            RaycastHit nearest = default;
            float distance = 1.5f;
            int count = Physics.RaycastNonAlloc(transform.position + Vector3.up * .3f, Vector3.down, stepHits, distance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = stepHits[i];
                if (!hit.transform.IsChildOf(transform) && hit.distance < distance) { nearest = hit; distance = hit.distance; }
            }
            if (nearest.collider != null && IsStoneSurface(nearest.collider))
                return SoundCue.FootstepStone;
            return SoundCue.Footstep;
        }
        void LateUpdate()
        {
            if (player != null)
            {
                bool local = player.IsLocal && player.PlayerCamera != null && (player.PlayerCamera.enabled || player.ActiveParrot != null);
                if (local)
                {
                    var passenger = GetComponent<ShipDeckPassenger>();
                    var platform = player.ActiveParrot == null && passenger != null && passenger.Ship != null ? passenger.Ship.GetComponent<ShipController>() : null;
                    var flooding = platform != null ? platform.GetComponent<ShipFlooding>() : null;
                    GameAudio.Ambience(platform != null ? platform.Speed / Mathf.Max(.01f, platform.MaxSpeed) : 0f, platform != null, flooding != null ? flooding.Level : 0f);
                }
                if (ready && !player.IsDead)
                {
                    if (player.IsSwimming && !swimming) GameAudio.Play(SoundCue.WaterSplash, transform.position);
                    if (player.IsGrounded && !grounded) GameAudio.Play(SoundCue.Land, transform.position);
                    if (!player.IsSwimming && !player.IsGrounded && grounded && player.VerticalSpeed > 0f) GameAudio.Play(SoundCue.Jump, transform.position);
                    if (player.IsSliding && !sliding) GameAudio.Play(SoundCue.Slide, transform.position);
                    int phase = player.ViewMotion != null ? Mathf.FloorToInt(player.ViewMotion.StepPhase / Mathf.PI) : -1;
                    bool step = local && !player.IsThirdPerson && phase >= 0 ? phase != lastStepPhase && lastStepPhase >= 0 : Time.time >= stepAt;
                    if (player.IsGrounded && !player.IsSliding && player.PlanarSpeed > .5f && step)
                    {
                        GameAudio.Play(StepCue(), transform.position, player.IsCrouched ? .4f : 1f);
                        stepAt = Time.time + Mathf.Clamp(2f / player.PlanarSpeed, .24f, .65f);
                    }
                    lastStepPhase = phase;
                    if (weapon != null)
                    {
                        if (weapon.Reloading && !reloading) GameAudio.Play(SoundCue.Reload, transform.position);
                        if (weapon.Loaded && !loaded) GameAudio.Play(SoundCue.ReloadReady, transform.position);
                    }
                    if (local && inventory != null)
                    {
                        if (selected != inventory.SelectedSlot)
                        {
                            var cue = inventory.HasSabre(inventory.SelectedSlot) ? SoundCue.SwordEquip : inventory.HasSabre(selected) ? SoundCue.SwordSheathe : SoundCue.Select;
                            GameAudio.Play(cue, transform.position, 1f, true);
                        }
                        if ((inventory.CannonSlots & ~slots) != 0) GameAudio.Play(SoundCue.Pickup, transform.position, 1f, true);
                    }
                    if (local && hands != null && hands.HasHeldBall && !holding) GameAudio.Play(SoundCue.Pickup, transform.position);
                }
                swimming = player.IsSwimming;
                grounded = player.IsGrounded; sliding = player.IsSliding;
                if (weapon != null) { loaded = weapon.Loaded; reloading = weapon.Reloading; }
                if (inventory != null) { selected = inventory.SelectedSlot; slots = inventory.CannonSlots; }
                if (hands != null) holding = hands.HasHeldBall;
            }
            if (ship != null && helm != null && sails != null && ready)
            {
                float currentRudder = helm.CurrentRudderNormalized;
                bool gripping = helm.IsControlling;
                if (gripping && !wheelHeld) { wheelLastMotion = Time.time; wheelDirection = 0; }
                float wheelDelta = currentRudder - rudder;
                if (gripping && Mathf.Abs(wheelDelta) > .0015f)
                {
                    int direction = wheelDelta > 0f ? 1 : -1;
                    if (wheelDirection != 0 && direction != wheelDirection && Time.time >= ropeAt)
                    {
                        GameAudio.Play(SoundCue.WheelReverseRope, helm.transform.position);
                        ropeAt = Time.time + .4f;
                    }
                    wheelDirection = direction;
                    wheelLastMotion = Time.time;
                    if (Time.time >= wheelAt)
                    {
                        GameAudio.Play(SoundCue.Wheel, helm.transform.position);
                        wheelAt = Time.time + Random.Range(.65f, .9f);
                    }
                }
                if (!gripping) wheelDirection = 0;
                UpdateWheelIdle(gripping && Time.time - wheelLastMotion > .35f);
                wheelHeld = gripping;
                rudder = currentRudder;
                if (Mathf.Abs(currentRudder) > .035f) centerArmed = true;
                if (centerArmed && (Mathf.Abs(currentRudder) < .008f || currentRudder * centerRudder < 0f))
                { GameAudio.Play(SoundCue.Place, helm.transform.position, .45f); centerArmed = false; }
                centerRudder = currentRudder;
                if (Time.time >= motionAt)
                {
                    if (Mathf.Abs(sails.DeployPercentage - sail) > .001f) { GameAudio.Play(SoundCue.Sail, transform.position + Vector3.up * 5f); motionAt = Time.time + 1.15f; }
                    sail = sails.DeployPercentage;
                }
                if (Time.time >= creakAt) { GameAudio.Play(SoundCue.Creak, transform.position + Vector3.up * 4f); creakAt = Time.time + Random.Range(5f, 11f); }
            }
            if (cannon != null)
            {
                if (ready && cannon.IsLoaded && !cannonLoaded) GameAudio.Play(SoundCue.Load, cannon.Muzzle.position);
                if (ready && Mathf.Abs(cannon.Elevation - elevation) > .5f && Time.time >= motionAt)
                { GameAudio.Play(SoundCue.Barrel, cannon.transform.position); elevation = cannon.Elevation; motionAt = Time.time + .35f; }
                cannonLoaded = cannon.IsLoaded;
            }
            ready = true;
        }

        void UpdateWheelIdle(bool idle)
        {
            if (wheelIdleEntry == null || wheelIdleEntry.Clips == null || wheelIdleEntry.Clips.Length < 2 ||
                wheelIdleEntry.Clips[0] == null || wheelIdleEntry.Clips[1] == null || wheelBank == null) return;
            if (idle)
            {
                if (wheelIdleSources[0] == null) CreateWheelIdleSources();
                if (wheelIdleVoice < 0 || Time.time >= wheelIdleSwitchAt)
                {
                    wheelIdleVoice = wheelIdleVoice < 0 ? Random.Range(0, 2) : 1 - wheelIdleVoice;
                    var source = wheelIdleSources[wheelIdleVoice];
                    source.Stop();
                    source.clip = wheelIdleEntry.Clips[wheelIdleVoice];
                    source.volume = 0f;
                    source.Play();
                    wheelIdleSwitchAt = Time.time + Mathf.Max(.6f, source.clip.length / source.pitch - .55f);
                }
            }
            else wheelIdleVoice = -1;

            float fullVolume = wheelBank.Master * wheelBank.Effects * wheelIdleEntry.Volume;
            for (int i = 0; i < wheelIdleSources.Length; i++)
            {
                var source = wheelIdleSources[i];
                if (source == null) continue;
                source.maxDistance = wheelIdleEntry.Distance;
                float target = idle && i == wheelIdleVoice ? fullVolume : 0f;
                source.volume = Mathf.MoveTowards(source.volume, target, Mathf.Max(.001f, fullVolume) * Time.deltaTime / .55f);
                if (source.volume <= .001f && i != wheelIdleVoice && source.isPlaying) source.Stop();
            }
        }

        void CreateWheelIdleSources()
        {
            for (int i = 0; i < wheelIdleSources.Length; i++)
            {
                var go = new GameObject("WheelIdleLeather" + (i + 1));
                go.transform.SetParent(helm.transform, false);
                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 1f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.dopplerLevel = 0f;
                source.minDistance = 2f;
                source.maxDistance = wheelIdleEntry.Distance;
                source.pitch = .82f;
                source.priority = 135;
                go.AddComponent<SpatialAudioTone>();
                wheelIdleSources[i] = source;
            }
        }

        void OnDisable()
        {
            foreach (var source in wheelIdleSources) if (source != null) source.Stop();
            wheelIdleVoice = -1;
            wheelHeld = false;
        }
    }
}
