using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkEquipment
    {
        readonly SyncVar<bool> iceBeamActive = new();
        readonly SyncVar<Vector3> iceBeamStart = new();
        readonly SyncVar<Vector3> iceBeamEnd = new();
        readonly SyncVar<bool> iceBeamContact = new();
        float iceHoldUntil, lastIceInputAt, nextIceInput, nextIceBeamTick, previousIceBeamTick;
        bool sentIceHeld;
        int iceBeamAudioVariant;
        GameObject iceBeamVisual;
        AudioSource iceBeamAudio, iceBeamContactAudio;
        public bool IceBeaming => iceBeamActive.Value;

        void ReadIceBeamInput(bool can, Mouse mouse)
        {
            bool held = can && Item == InventoryItem.IceStaff && mouse != null && mouse.leftButton.isPressed;
            if (held && (!sentIceHeld || Time.time >= nextIceInput) || !held && sentIceHeld)
            {
                nextIceInput = Time.time + .1f;
                IceBeamInputServerRpc(held, motor.AimDirection, motor.PlayerCamera != null ? motor.PlayerCamera.transform.position - transform.position : Vector3.up * 1.5f);
            }
            sentIceHeld = held;
        }

        [ServerRpc]
        void IceBeamInputServerRpc(bool held, Vector3 forward, Vector3 eyeOffset)
        {
            if (!held) { iceHoldUntil = 0; StopIceBeam(); return; }
            if (!CanUse || Item != InventoryItem.IceStaff || motor.IsFrozen || motor.IsDowned || !float.IsFinite(forward.sqrMagnitude) || forward.sqrMagnitude < .5f) return;
            lastIceInputAt = Time.time;
            iceHoldUntil = Time.time + 2f;
            direction.Value = forward.normalized;
            staffEyeOffset = eyeOffset;
        }

        void StartIceBeam()
        {
            if (Time.time >= iceHoldUntil || CurrentStaff == null) return;
            BeginAction(7, float.PositiveInfinity, inventory.SelectedSlot);
            iceBeamActive.Value = true;
            previousIceBeamTick = Time.time;
            nextIceBeamTick = Time.time;
            UpdateIceBeamAuthority();
        }

        void StopIceBeam()
        {
            lastFrozenContact = null;
            lastFrozenKey = null;
            bool active = iceBeamActive.Value;
            iceBeamActive.Value = false;
            iceBeamContact.Value = false;
            if (action.Value == 7) action.Value = 0;
            if (Item == InventoryItem.IceStaff && action.Value == 6)
            {
                action.Value = 0;
                staffCastUntil.Value = 0;
                staffCastItem.Value = InventoryItem.None;
                staffCastAnchor.Value = null;
                if (!motor.IsDead) StaffCancelObserversRpc(transform.position + Vector3.up);
            }
            if (active && Staves != null && Staves.Length > 2 && Staves[2] != null) nextShot = Time.time + Staves[2].Cooldown;
        }

        void UpdateIceBeamAuthority()
        {
            if (Item == InventoryItem.IceStaff && action.Value == 6 && Time.time >= iceHoldUntil) { StopIceBeam(); return; }
            if (!iceBeamActive.Value) return;
            if (!CanUse || Item != InventoryItem.IceStaff || action.Value != 7 || actionSlot != inventory.SelectedSlot || motor.IsFrozen || motor.IsDowned || Time.time >= iceHoldUntil) { StopIceBeam(); return; }
            if (Time.time < nextIceBeamTick) return;
            var settings = CurrentStaff;
            if (settings == null) { StopIceBeam(); return; }
            float deltaTime = Mathf.Clamp(Time.time - previousIceBeamTick, 0f, .25f);
            previousIceBeamTick = Time.time;
            nextIceBeamTick = Time.time + .125f;
            Vector3 forward = direction.Value.normalized;
            Vector3 eye = StaffEye(staffEyeOffset);
            Vector3 start = eye + Quaternion.LookRotation(forward) * settings.LaunchOffset;
            Vector3 target = eye + forward * settings.Range;
            if (FirearmTrace.Cast(gameObject, eye, target, out var eyeHit)) target = eyeHit.point;
            RaycastHit hit;
            bool contact;
            Vector3 end;
            if (FirearmTrace.Cast(gameObject, eye, start, out hit)) { start = eye; end = hit.point; contact = true; }
            else
            {
                Vector3 beamDirection = (target - start).sqrMagnitude > .0001f ? (target - start).normalized : forward;
                end = start + beamDirection * settings.Range;
                contact = FirearmTrace.Cast(gameObject, start, end, out hit);
                if (contact) end = hit.point;
            }
            bool onWater = hit.collider != null && hit.collider.GetComponent<FrozenWaterTile>() != null;
            if (WaterImpactPhysics.Cross(OceanSurface.Instance, start, end, 0f, out var water, out _)) { end = water; hit = default; contact = true; onWater = true; }
            iceBeamStart.Value = start;
            iceBeamEnd.Value = end;
            iceBeamContact.Value = contact;
            if (Time.time - lastIceInputAt > .4f) return;
            if (onWater) FreezeWater(end, settings.FrozenWaterSeconds);
            else lastFrozenContact = null;
            var health = hit.collider != null ? hit.collider.GetComponentInParent<CombatHealth>() : null;
            var player = health != null ? health.GetComponent<NetworkPlayer>() : null;
            var source = GetComponent<NetworkPlayer>();
            var networkHealth = health != null ? health.GetComponent<NetworkHealth>() : null;
            if (health == null || health.IsDead || player == null || source == null || player == source || networkHealth == null || networkHealth.DeveloperInvulnerable || player.Ship != null && player.Ship.IsSinking) return;
            if (!SessionController.FriendlyFire && source.TeamId.Value > 0 && source.TeamId.Value == player.TeamId.Value) return;
            health.Damage(settings.BeamDamagePerSecond * deltaTime, gameObject);
            networkHealth.Slow(settings.SlowSeconds, settings.SlowMultiplier);
        }

        void PresentIceBeam()
        {
            if (!IsClientInitialized || !IceBeaming || Item != InventoryItem.IceStaff || !Active || IsOwner && !sentIceHeld) { ClearIceBeamVisuals(); return; }
            if (iceBeamVisual == null) iceBeamVisual = StaffVfx.IceBeam();
            Vector3 start = iceBeamStart.Value;
            var socket = IsOwner && !motor.IsThirdPerson ? staffHeldViewSocket : staffHeldWorldSocket;
            if (socket != null) start = socket.position;
            Vector3 end = iceBeamEnd.Value;
            bool contact = iceBeamContact.Value;
            if (IsOwner && motor.PlayerCamera != null && CurrentStaff != null)
            {
                var shot = FirearmTrace.Resolve(gameObject, motor.PlayerCamera.transform.position, start, motor.AimDirection, CurrentStaff.Range, out _, 28f);
                end = shot.End; contact = shot.Hit || shot.Water;
                if ((end - start).sqrMagnitude > CurrentStaff.Range * CurrentStaff.Range) end = start + (end - start).normalized * CurrentStaff.Range;
            }
            StaffVfx.UpdateIceBeam(iceBeamVisual, start, end, contact);
            if (iceBeamAudio == null) iceBeamAudioVariant = Random.Range(0, 2);
            GameAudio.Loop(ref iceBeamAudio, SoundCue.IceStaffBeam, socket != null ? socket : transform, 1f, variant: iceBeamAudioVariant);
            if (contact) GameAudio.Loop(ref iceBeamContactAudio, SoundCue.IceStaffBeamContact, iceBeamVisual.transform.Find("Contact"), .8f);
            else StaffAudio.Stop(ref iceBeamContactAudio);
        }

        void ClearIceBeamVisuals()
        {
            StaffAudio.Stop(ref iceBeamAudio);
            StaffAudio.Stop(ref iceBeamContactAudio);
            if (iceBeamVisual != null) { iceBeamVisual.GetComponent<LineRenderer>().enabled = false; StaffVfx.FinishStatus(iceBeamVisual); iceBeamVisual = null; }
        }
    }
}
