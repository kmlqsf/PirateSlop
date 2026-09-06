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
        bool grounded, sliding, reloading, loaded, holding, cannonLoaded;
        int selected, slots;
        bool ready;
        void Awake()
        {
            player = GetComponent<AdvancedPlayerController>(); weapon = GetComponent<PirateWeapon>();
            inventory = GetComponent<PlayerInventory>(); hands = GetComponent<CannonHands>();
            ship = GetComponent<ShipController>(); helm = GetComponentInChildren<HelmInteraction>();
            sails = GetComponent<SailSystem>(); cannon = GetComponent<SimpleCannon>();
        }
        void LateUpdate()
        {
            if (player != null)
            {
                bool local = player.PlayerCamera != null && player.PlayerCamera.enabled;
                if (local)
                {
                    var passenger = GetComponent<ShipDeckPassenger>();
                    var platform = passenger != null && passenger.Ship != null ? passenger.Ship.GetComponent<ShipController>() : null;
                    GameAudio.Ambience(platform != null ? platform.Speed / platform.MaxSpeed : 0f);
                }
                if (ready && !player.IsDead)
                {
                    if (player.IsGrounded && !grounded) GameAudio.Play(SoundCue.Land, transform.position);
                    if (!player.IsSwimming && !player.IsGrounded && grounded && player.VerticalSpeed > 0f) GameAudio.Play(SoundCue.Jump, transform.position);
                    if (player.IsSliding && !sliding) GameAudio.Play(SoundCue.Slide, transform.position);
                    if (player.IsGrounded && !player.IsSliding && player.PlanarSpeed > .5f && Time.time >= stepAt)
                    {
                        GameAudio.Play(SoundCue.Footstep, transform.position, player.IsCrouched ? .4f : 1f);
                        stepAt = Time.time + Mathf.Clamp(2f / player.PlanarSpeed, .24f, .65f);
                    }
                    if (weapon != null)
                    {
                        if (weapon.Reloading && !reloading) GameAudio.Play(SoundCue.Reload, transform.position);
                        if (weapon.Loaded && !loaded) GameAudio.Play(SoundCue.ReloadReady, transform.position);
                    }
                    if (local && inventory != null)
                    {
                        if (selected != inventory.SelectedSlot) GameAudio.Play(SoundCue.Select, transform.position, 1f, true);
                        if ((inventory.CannonSlots & ~slots) != 0) GameAudio.Play(SoundCue.Pickup, transform.position, 1f, true);
                    }
                    if (local && hands != null && hands.HasHeldBall && !holding) GameAudio.Play(SoundCue.Pickup, transform.position);
                }
                grounded = player.IsGrounded; sliding = player.IsSliding;
                if (weapon != null) { loaded = weapon.Loaded; reloading = weapon.Reloading; }
                if (inventory != null) { selected = inventory.SelectedSlot; slots = inventory.CannonSlots; }
                if (hands != null) holding = hands.HasHeldBall;
            }
            if (ship != null && helm != null && sails != null && ready)
            {
                if (Time.time >= motionAt)
                {
                    if (Mathf.Abs(helm.CurrentRudderNormalized - rudder) > .003f) { GameAudio.Play(SoundCue.Wheel, helm.transform.position); motionAt = Time.time + .85f; }
                    else if (Mathf.Abs(sails.DeployPercentage - sail) > .001f) { GameAudio.Play(SoundCue.Sail, transform.position + Vector3.up * 5f); motionAt = Time.time + .45f; }
                    rudder = helm.CurrentRudderNormalized; sail = sails.DeployPercentage;
                }
                if (ship.Speed > .3f && Time.time >= creakAt) { GameAudio.Play(SoundCue.Creak, transform.position + Vector3.up * 4f); creakAt = Time.time + Random.Range(5f, 11f); }
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
    }
}
