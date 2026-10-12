using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkEquipment
    {
        public StaffSettings[] Staves;
        public NetworkStaffProjectile[] StaffProjectiles;
        readonly System.Collections.Generic.List<NetworkStaffProjectile> guidedStaffProjectiles = new();
        float nextStaffGuide;
        uint staffGuideSequence, lastStaffGuideSequence;
        readonly SyncVar<uint> staffCastUntil = new();
        readonly SyncVar<bool> staffCastWater = new();
        readonly SyncVar<Vector3> staffCastPoint = new();
        readonly SyncVar<Vector3> staffCastNormal = new(Vector3.up);
        readonly SyncVar<NetworkObject> staffCastAnchor = new();
        readonly SyncVar<InventoryItem> staffCastItem = new(InventoryItem.None);
        Vector3 staffEyeOffset;
        GameObject staffChargeVisual, staffTargetVisual;
        AudioSource staffChargeAudio, staffTargetAudio;
        AudioSource staffHeldAudio;
        Transform staffHeldViewSocket, staffHeldWorldSocket;
        float nextStaffIdleSound;
        StaffCastingAnimation.Pose staffPose = StaffCastingAnimation.Idle;
        float staffReleasedAt = -10f;
        InventoryItem staffReleasedItem = InventoryItem.None;
        public Vector3 StaffSupportOffset => staffPose.Support;
        public Vector3 StaffWristRotation => staffPose.Wrist;

        void ResetStaffPose()
        {
            staffPose = StaffCastingAnimation.Idle;
            staffReleasedAt = -10f;
            staffReleasedItem = InventoryItem.None;
        }

        void ApplyStaffPose(ref Vector3 position, ref Vector3 rotation)
        {
            var target = StaffCastingAnimation.Idle;
            if (Active && CurrentStaff != null)
            {
                if (StaffCasting && staffCastItem.Value == Item)
                    target = StaffCastingAnimation.Charge(Item, 1f - StaffCastRemaining / CurrentStaff.CastSeconds);
                else if (IceBeaming)
                    target = StaffCastingAnimation.Release(Item, Mathf.Max(.0f, Time.time - staffReleasedAt), true);
                else if (staffReleasedItem == Item)
                    target = StaffCastingAnimation.Release(Item, Time.time - staffReleasedAt, false);
            }
            staffPose = StaffCastingAnimation.Blend(staffPose, target, 1f - Mathf.Exp(-Time.deltaTime * 22f));
            position += staffPose.Position;
            rotation += staffPose.Rotation;
        }

        void CreateStaffHeldEffects()
        {
            staffHeldViewSocket = staffHeldWorldSocket = null;
            nextStaffIdleSound = Time.time + Random.Range(4f, 7f);
            if (!Staff || CurrentStaff == null) return;
            foreach (var root in new[] { view, world })
            {
                if (root == null) continue;
                Transform socket = null;
                foreach (var t in root.GetComponentsInChildren<Transform>()) if (t.name == "Muzzle") { socket = t; break; }
                if (socket == null) continue;
                StaffVfx.Held(socket, CurrentStaff);
                if (root == view) staffHeldViewSocket = socket; else staffHeldWorldSocket = socket;
            }
        }

        void PresentStaffHeldAudio()
        {
            if (!IsClientInitialized || !Active || !Staff) { ClearStaffHeldAudio(); return; }
            var socket = IsOwner && !motor.IsThirdPerson ? staffHeldViewSocket : staffHeldWorldSocket;
            if (socket == null) { ClearStaffHeldAudio(); return; }
            if (Item != InventoryItem.FireStaff)
            {
                ClearStaffHeldAudio();
                if (StaffCasting || IceBeaming) { nextStaffIdleSound = Time.time + Random.Range(7f, 13f); return; }
                if (Time.time >= nextStaffIdleSound)
                {
                    nextStaffIdleSound = Time.time + Random.Range(7f, 13f);
                    GameAudio.Play(Item == InventoryItem.IceStaff ? SoundCue.IceStaffHeld : SoundCue.LightningStaffHeld, socket.position, Random.Range(.35f, .55f));
                }
                return;
            }
            GameAudio.Loop(ref staffHeldAudio, SoundCue.StaffBurnLoop, socket, StaffCasting ? .07f : .18f);
            if (staffHeldAudio != null) { staffHeldAudio.transform.SetParent(socket, false); staffHeldAudio.maxDistance = 7f; staffHeldAudio.minDistance = .7f; }
        }

        void ClearStaffHeldAudio() => StaffAudio.Stop(ref staffHeldAudio);
        public bool StaffCasting => action.Value == 6 && staffCastUntil.Value != 0;
        public float StaffCastRemaining => staffCastUntil.Value == 0 || TimeManager == null ? 0f : Mathf.Max(0, unchecked((int)(staffCastUntil.Value - TimeManager.Tick))) * (float)TimeManager.TickDelta;
        Vector3 StaffCastWorldPoint => staffCastAnchor.Value != null ? staffCastAnchor.Value.transform.TransformPoint(staffCastPoint.Value) : staffCastPoint.Value;
        Vector3 StaffCastWorldNormal => staffCastAnchor.Value != null ? staffCastAnchor.Value.transform.TransformDirection(staffCastNormal.Value) : staffCastNormal.Value;
        public bool Staff => Item >= InventoryItem.FireStaff && Item <= InventoryItem.IceStaff;
        StaffSettings CurrentStaff => Staff && Staves != null && (int)Item - 29 < Staves.Length ? Staves[(int)Item - 29] : null;

        Vector3 StaffEye(Vector3 eyeOffset)
        {
            Vector3 eye = transform.position + Vector3.up * (motor.IsCrouched ? .8f : 1.5f);
            if (float.IsFinite(eyeOffset.sqrMagnitude) && eyeOffset.sqrMagnitude < 4f && !FirearmTrace.Cast(gameObject, eye, transform.position + eyeOffset, out _)) eye = transform.position + eyeOffset;
            return eye;
        }

        bool StaffTarget(Vector3 eye, Vector3 forward, float range, out Vector3 point) => StaffTarget(eye, forward, range, out point, out _, out _);

        bool StaffTarget(Vector3 eye, Vector3 forward, float range, out Vector3 point, out NetworkShip anchor, out Vector3 normal) => StaffTarget(eye, forward, range, out point, out anchor, out normal, out _);

        bool StaffTarget(Vector3 eye, Vector3 forward, float range, out Vector3 point, out NetworkShip anchor, out Vector3 normal, out bool onWater)
        {
            onWater = false;
            point = eye + forward.normalized * range;
            anchor = null; normal = Vector3.up;
            bool hit = FirearmTrace.Cast(gameObject, eye, point, out var surface);
            if (hit) { normal = surface.normal; point = surface.point + normal * .025f; anchor = surface.collider.GetComponentInParent<NetworkShip>(); }
            if (WaterImpactPhysics.Cross(OceanSurface.Instance, eye, point, 0f, out var water, out _)) { onWater = true; point = water + Vector3.up * .025f; anchor = null; normal = Vector3.up; hit = true; }
            return hit;
        }

        void UseStaff(byte request, Vector3 forward, Vector3 eyeOffset)
        {
            var settings = CurrentStaff;
            if (request != 0 || settings == null || motor.IsFrozen || motor.IsDowned) return;
            Vector3 eye = StaffEye(eyeOffset);
            bool hit = StaffTarget(eye, forward, settings.Range, out var target, out var anchor, out var normal, out var onWater);
            if (Item == InventoryItem.LightningStaff && !hit) return;
            int index = (int)Item - 29;
            if (Item == InventoryItem.FireStaff && (StaffProjectiles == null || index >= StaffProjectiles.Length || StaffProjectiles[index] == null)) return;
            staffCastWater.Value = onWater;
            staffEyeOffset = eyeOffset;
            direction.Value = forward.normalized;
            staffCastItem.Value = Item;
            staffCastAnchor.Value = anchor != null ? anchor.GetComponent<NetworkObject>() : null;
            staffCastPoint.Value = anchor != null ? anchor.transform.InverseTransformPoint(target) : target;
            staffCastNormal.Value = anchor != null ? anchor.transform.InverseTransformDirection(normal) : normal;
            staffCastUntil.Value = TimeManager.Tick + (uint)Mathf.CeilToInt(settings.CastSeconds / (float)TimeManager.TickDelta);
            BeginAction(6, settings.CastSeconds, inventory.SelectedSlot);
        }

        void UpdateStaffCasting()
        {
            if (staffCastUntil.Value == 0) return;
            if (action.Value != 6 || !CanUse || motor.IsFrozen || motor.IsDowned || Item != staffCastItem.Value || inventory.SelectedSlot != actionSlot)
            {
                if (!motor.IsDead) StaffCancelObserversRpc(transform.position + Vector3.up);
                if (action.Value == 6) action.Value = 0;
                staffCastUntil.Value = 0; staffCastItem.Value = InventoryItem.None; staffCastAnchor.Value = null;
            }
        }

        void FinishStaffCast()
        {
            var settings = CurrentStaff;
            if (!StaffCasting || settings == null || settings.Item != staffCastItem.Value) return;
            Vector3 forward = direction.Value.normalized;
            Vector3 eye = StaffEye(staffEyeOffset);
            bool released = false;
            if (Item == InventoryItem.IceStaff)
            {
                StartIceBeam();
                released = IceBeaming;
            }
            else if (Item == InventoryItem.LightningStaff)
            {
                Vector3 point = StaffCastWorldPoint;
                StaffCombat.Impact(GetComponent<NetworkPlayer>(), point, settings, staffCastWater.Value ? settings.WaterRadiusMultiplier : 1f);
                StaffImpactObserversRpc(point, settings.Item, staffCastAnchor.Value, staffCastPoint.Value, staffCastWater.Value ? settings.WaterRadiusMultiplier : 1f);
                released = true;
            }
            else
            {
                int index = (int)Item - 29;
                Vector3 start = eye + Quaternion.LookRotation(forward) * settings.LaunchOffset;
                if (StaffProjectiles != null && index < StaffProjectiles.Length && StaffProjectiles[index] != null && !FirearmTrace.Cast(gameObject, eye, start, out _))
                {
                    StaffTarget(eye, forward, settings.Range, out var target);
                    var projectile = Instantiate(StaffProjectiles[index], start, Quaternion.LookRotation(forward));
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(projectile.gameObject, gameObject.scene);
                    projectile.Launch(GetComponent<NetworkPlayer>(), (target - start).normalized);
                    ServerManager.Spawn(projectile.NetworkObject);
                    guidedStaffProjectiles.RemoveAll(p => p == null || !p.IsSpawned);
                    guidedStaffProjectiles.Add(projectile);
                    released = true;
                }
            }
            staffCastUntil.Value = 0; staffCastItem.Value = InventoryItem.None; staffCastAnchor.Value = null;
            if (!released) { StaffCancelObserversRpc(eye); return; }
            recoilAt = Time.time;
            StaffCastObserversRpc(eye, settings.Item);
            nextShot = Time.time + settings.Cooldown;
        }

        void PresentStaffCasting()
        {
            if (!IsClientInitialized || Application.isBatchMode) return;
            if (!StaffCasting || StaffCastRemaining <= 0f || !Staff || CurrentStaff == null || Item != staffCastItem.Value) { ClearStaffCastVisuals(); return; }
            var root = IsOwner && !motor.IsThirdPerson ? view : world;
            if (root != null)
            {
                Transform socket = root;
                foreach (var t in root.GetComponentsInChildren<Transform>()) if (t.name == "Muzzle") { socket = t; break; }
                if (staffChargeVisual == null) staffChargeVisual = StaffVfx.Charge(socket, CurrentStaff);
                else { staffChargeVisual.transform.SetParent(socket, false); staffChargeVisual.transform.localPosition = Vector3.zero; }
                bool startAudio = staffChargeAudio == null;
                float progress = Mathf.Clamp01(1f - StaffCastRemaining / CurrentStaff.CastSeconds);
                StaffVfx.UpdateCharge(staffChargeVisual, progress);
                GameAudio.Loop(ref staffChargeAudio, StaffAudio.Charge(Item), socket, .4f + progress * .6f);
                if (staffChargeAudio != null)
                {
                    staffChargeAudio.transform.SetParent(socket, false);
                    staffChargeAudio.loop = false;
                    staffChargeAudio.pitch = staffChargeAudio.clip.length / CurrentStaff.CastSeconds;
                    if (startAudio) staffChargeAudio.time = Mathf.Min(staffChargeAudio.clip.length - .01f, progress * staffChargeAudio.clip.length);
                }
            }
            if (staffCastItem.Value == InventoryItem.LightningStaff)
            {
                if (staffTargetVisual == null) staffTargetVisual = StaffVfx.Telegraph(CurrentStaff.Color, CurrentStaff.BlastRadius * (staffCastWater.Value ? CurrentStaff.WaterRadiusMultiplier : 1f));
                staffTargetVisual.transform.SetPositionAndRotation(StaffCastWorldPoint, Quaternion.identity);
                GameAudio.Loop(ref staffTargetAudio, SoundCue.LightningStaffTarget, staffTargetVisual.transform, .65f);
            }
            else if (staffTargetVisual != null) { Destroy(staffTargetVisual); staffTargetVisual = null; }
        }

        void ClearStaffCastVisuals()
        {
            StaffAudio.Stop(ref staffChargeAudio); StaffAudio.Stop(ref staffTargetAudio);
            if (staffChargeVisual != null) { StaffVfx.FinishStatus(staffChargeVisual); staffChargeVisual = null; }
            if (staffTargetVisual != null) { Destroy(staffTargetVisual); staffTargetVisual = null; }
        }

        void ReadStaffGuidance(bool can)
        {
            if (!can || !Staff || (Item == InventoryItem.LightningStaff || Item == InventoryItem.IceStaff) || Time.time < nextStaffGuide || motor.PlayerCamera == null) return;
            nextStaffGuide = Time.time + 1f / 30f;
            GuideStaffServerRpc(motor.AimDirection, motor.PlayerCamera.transform.position - transform.position, ++staffGuideSequence);
        }

        [ServerRpc]
        void GuideStaffServerRpc(Vector3 forward, Vector3 eyeOffset, uint sequence, Channel channel = Channel.Unreliable)
        {
            if (!CanUse || !Staff || unchecked((int)(sequence - lastStaffGuideSequence)) <= 0 || !float.IsFinite(forward.sqrMagnitude) || forward.sqrMagnitude < .5f) return;
            lastStaffGuideSequence = sequence;
            var settings = CurrentStaff;
            if (settings == null) return;
            direction.Value = forward.normalized;
            if (StaffCasting) staffEyeOffset = eyeOffset;
            guidedStaffProjectiles.RemoveAll(p => p == null || !p.IsSpawned);
            if (guidedStaffProjectiles.Count == 0) return;
            foreach (var projectile in guidedStaffProjectiles) if (projectile.Settings.Item == Item) projectile.Guide(forward);
        }

        [ObserversRpc(RunLocally = true)]
        void StaffCancelObserversRpc(Vector3 point)
        {
            staffReleasedAt = -10f;
            staffReleasedItem = InventoryItem.None;
            if (IsClientInitialized) GameAudio.Play(SoundCue.StaffCancel, point);
        }

        [ObserversRpc(RunLocally = true)]
        void StaffCastObserversRpc(Vector3 point, InventoryItem item)
        {
            staffReleasedAt = Time.time;
            staffReleasedItem = item;
            if (!IsClientInitialized || Staves == null || (int)item - 29 < 0 || (int)item - 29 >= Staves.Length) return;
            var root = IsOwner && !motor.IsThirdPerson ? view : world;
            if (Item == item && root != null) foreach (var socket in root.GetComponentsInChildren<Transform>()) if (socket.name == "Muzzle") { point = socket.position; break; }
            StaffVfx.Cast(point, Staves[(int)item - 29].Color);
            if (item != InventoryItem.IceStaff) GameAudio.Play(StaffAudio.Cast(item), point);
        }

        [ObserversRpc(RunLocally = true)]
        void StaffImpactObserversRpc(Vector3 point, InventoryItem item, NetworkObject anchor, Vector3 localPoint, float radiusMultiplier)
        {
            if (!IsClientInitialized || Staves == null || (int)item - 29 < 0 || (int)item - 29 >= Staves.Length) return;
            var settings = Staves[(int)item - 29];
            StaffVfx.Impact(point, settings, true, anchor != null ? anchor.transform : null, localPoint, radiusMultiplier);
            GameAudio.Play(StaffAudio.Impact(item), point);
        }
    }
}
