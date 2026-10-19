#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
using System;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using PirateSlop.Harpoon;
using PirateSlop.Networking;
using UnityEngine;

namespace PirateSlop.Experimental
{
    public struct UpperWeaponState
    {
        public int Key;
        public double DisabledUntil;
        public uint Revision;
        public byte Hits;
    }

    public sealed partial class UpperDestructionV2
    {
        public NetworkFish FallenCannon, FallenMortar;
        readonly SyncList<UpperWeaponState> weapons = new();
        readonly Dictionary<int, UpperWeaponVisual> weaponVisuals = new();
        readonly Dictionary<int, WeaponSupport> supports = new();
        readonly RaycastHit[] supportHits = new RaycastHit[32];
        NetworkCannon cannons;
        int registeredCannons;

        sealed class WeaponSupport
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public ShipDamageSection[] Sections = new ShipDamageSection[4];
            public int[] Fragments = new int[4];
        }

        static readonly Vector3[] supportCorners = { new(-.5f, .05f, -.55f), new(.5f, .05f, -.55f), new(-.5f, .05f, .55f), new(.5f, .05f, .55f) };
        void InitializeWeapons() => cannons = GetComponent<NetworkCannon>();
        void InitializeWeaponPresentation() { }

        public UpperWeaponState Weapon(int key)
        {
            for (int i = 0; i < weapons.Count; i++) if (weapons[i].Key == key) return weapons[i];
            return new UpperWeaponState { Key = key };
        }

        public bool WeaponDisabled(int key)
        {
            var state = Weapon(key);
            if (state.DisabledUntil > Clock || state.Hits >= 3) return true;
            int assembly = key < 0 ? HarpoonAssembly(-1 - key) : -1;
            return key < 0 && (Stage(assembly) >= 3 || !HasAssemblySupport(assembly));
        }

        void StoreWeapon(UpperWeaponState value)
        {
            for (int i = 0; i < weapons.Count; i++) if (weapons[i].Key == value.Key) { weapons[i] = value; return; }
            weapons.Add(value);
        }

        public void RestoreWeaponDeadline(int index, double until)
        {
            if (!IsServerInitialized || until <= Clock) return;
            var state = Weapon(index); state.DisabledUntil = until; state.Revision++; StoreWeapon(state);
        }

        public bool HitWeapon(Collider collider, Vector3 point, Vector3 normal, Vector3 velocity, InventoryItem ammo, GameObject attacker)
        {
            if (!IsServerInitialized) return false;
            var cannon = collider.GetComponentInParent<SimpleCannon>();
            var harpoon = collider.GetComponentInParent<HarpoonGun>();
            if (cannon == null && harpoon == null) return false;
            var shooter = attacker != null ? attacker.GetComponent<NetworkPlayer>() : null;
            if (!SessionController.FriendlyFire && shooter != null && shooter.TeamId.Value > 0 && shooter.TeamId.Value == ship.TeamId.Value) return true;
            int key = cannon != null ? cannon.Index : -1 - harpoon.MountIndex;
            var state = Weapon(key); bool repeat = state.DisabledUntil > Clock;
            state.DisabledUntil = Clock + 15d; state.Revision++;
            if (harpoon != null)
            {
                int assembly = HarpoonAssembly(harpoon.MountIndex);
                destruction.DamageUpperAssembly(assembly, point, normal, velocity, ammo, attacker);
                state.Hits = (byte)Mathf.Min(3, Stage(assembly));
                harpoon.ApplyUpperState(harpoon.MaxHealth * (3 - state.Hits) / 3f, state.Hits >= 3);
                harpoon.ReleaseControl();
            }
            else cannon.CancelUpperFire();
            StoreWeapon(state); WeaponHitObserversRpc(key, state.Revision, repeat, state.Hits >= 3);
            return true;
        }

        [ObserversRpc]
        void WeaponHitObserversRpc(int key, uint revision, bool repeat, bool broken)
        {
            var target = WeaponTransform(key);
            if (target == null) return;
            if (key < 0) ship.HarpoonMount?.GetGun(-1 - key)?.ReleaseControl(false);
            GameAudio.Play(repeat ? SoundCue.KickCannon : SoundCue.BulletMetal, target.position, .9f);
            GameAudio.Play(SoundCue.LockpickJam, target.position, .55f);
            if (broken) GameAudio.Play(SoundCue.ShipCollision, target.position);
            if (weaponVisuals.TryGetValue(key, out var visual)) visual.Hit(revision);
        }

        Transform WeaponTransform(int key)
        {
            if (key >= 0) return cannons != null && cannons.Crate != null && key < cannons.Crate.Cannons.Count ? cannons.Crate.Cannons[key]?.transform : null;
            return ship.HarpoonMount != null ? ship.HarpoonMount.GetGun(-1 - key)?.transform : null;
        }

        int HarpoonAssembly(int mount)
        {
            for (int i = 0; i < Assemblies.Length; i++) if (Assemblies[i].Kind == UpperAssemblyKind.Harpoon && Assemblies[i].SailIndex == mount) return i;
            return -1;
        }

        void TickWeapons()
        {
            if (cannons == null || cannons.Crate == null) return;
            for (int i = registeredCannons; i < cannons.Crate.Cannons.Count; i++)
            {
                var cannon = cannons.Crate.Cannons[i];
                if (cannon == null) continue;
                cannon.UpperController = this;
                if (IsClientInitialized) RegisterVisual(i, cannon.transform, cannon.BarrelPivot, Vector3.right);
            }
            registeredCannons = cannons.Crate.Cannons.Count;
            for (int i = 0; i < 2; i++)
            {
                var gun = ship.HarpoonMount != null ? ship.HarpoonMount.GetGun(i) : null;
                if (gun == null) continue;
                gun.UpperController = this;
                if (IsClientInitialized && !weaponVisuals.ContainsKey(-1 - i)) RegisterVisual(-1 - i, gun.transform, gun.UpperPitchPivot, -gun.PitchAxis);
                var state = Weapon(-1 - i);
                if (state.DisabledUntil > Clock && gun.Operator != null) gun.ReleaseControl(false);
                int assembly = HarpoonAssembly(i);
                bool broken = state.Hits >= 3 || Stage(assembly) >= 3 || !HasAssemblySupport(assembly);
                if (gun.IsBroken != broken) gun.ApplyUpperState(broken ? 0 : gun.MaxHealth * (3 - Stage(assembly)) / 3f, broken);
            }
            foreach (var pair in weaponVisuals)
            {
                var state = Weapon(pair.Key);
                int assembly = pair.Key < 0 ? HarpoonAssembly(-1 - pair.Key) : -1;
                bool broken = pair.Key < 0 && (state.Hits >= 3 || Stage(assembly) >= 3 || !HasAssemblySupport(assembly));
                pair.Value.Present(state.DisabledUntil, Clock, broken);
            }
        }

        void RegisterVisual(int key, Transform root, Transform pivot, Vector3 axis)
        {
            if (pivot == null || weaponVisuals.ContainsKey(key)) return;
            var visual = root.gameObject.AddComponent<UpperWeaponVisual>();
            visual.Initialize(pivot, axis); weaponVisuals[key] = visual;
        }

        public Vector3 WeaponVisualPoint(int key, Vector3 point) => weaponVisuals.TryGetValue(key, out var visual) && visual != null ? visual.ProjectPoint(point) : point;

        public bool RepairHarpoon(HarpoonGun gun)
        {
            if (!IsServerInitialized) return true;
            int assembly = HarpoonAssembly(gun.MountIndex);
            if (assembly < 0 || !HasAssemblySupport(assembly) || !destruction.RepairUpperAssembly(assembly)) return false;
            var state = Weapon(-1 - gun.MountIndex); state.Hits = 0; StoreWeapon(state);
            return true;
        }

        public void NotifyBrokenHarpoon(HarpoonGun gun)
        {
            if (!IsServerInitialized) return;
            int assembly = HarpoonAssembly(gun.MountIndex);
            while (assembly >= 0 && Stage(assembly) < 3)
            {
                int previous = Stage(assembly);
                destruction.DamageUpperAssembly(assembly, gun.transform.position, transform.up, Vector3.zero, InventoryItem.Cannonball, null);
                if (Stage(assembly) <= previous) break;
            }
        }

        void SynchronizeHarpoonStage(int assembly)
        {
            int key = -1 - Assemblies[assembly].SailIndex;
            var state = Weapon(key);
            byte stage = (byte)Stage(assembly);
            if (state.Hits == stage) return;
            state.Hits = stage; state.Revision++; StoreWeapon(state);
        }

        void CheckWeaponSupports()
        {
            if (cannons == null || cannons.Crate == null) return;
            for (int i = 0; i < cannons.Crate.Cannons.Count; i++)
            {
                var cannon = cannons.Crate.Cannons[i];
                if (cannon == null || !cannon.gameObject.activeInHierarchy) continue;
                bool moved = !supports.TryGetValue(i, out var support) || (support.Position - cannon.transform.localPosition).sqrMagnitude > .0025f || Quaternion.Angle(support.Rotation, cannon.transform.localRotation) > 1f;
                if (moved)
                {
                    support = CaptureSupport(cannon); supports[i] = support;
                }
                bool alive = true;
                for (int p = 0; p < 4; p++)
                {
                    var section = support.Sections[p];
                    if (section == null || support.Fragments[p] >= 0 && (section.RemovedFragments & (1UL << support.Fragments[p])) != 0 || section.State == ShipSectionState.Destroyed) alive = false;
                }
                if (!alive) DropCannon(cannon);
            }
        }

        WeaponSupport CaptureSupport(SimpleCannon cannon)
        {
            var support = new WeaponSupport { Position = cannon.transform.localPosition, Rotation = cannon.transform.localRotation };
            for (int p = 0; p < 4; p++)
            {
                var point = cannon.transform.TransformPoint(supportCorners[p]);
                int count = Physics.RaycastNonAlloc(point + transform.up * .25f, -transform.up, supportHits, .7f, ~0, QueryTriggerInteraction.Ignore);
                float best = float.PositiveInfinity;
                for (int n = 0; n < count; n++)
                {
                    var hit = supportHits[n];
                    if (!hit.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<SimpleCannon>() != null || hit.collider.GetComponentInParent<Cannonball>() != null || hit.distance >= best) continue;
                    var section = PirateSlop.Ships.ShipV3CollisionBatch.ResolveSection(hit.collider, hit.point);
                    if (section == null) continue;
                    best = hit.distance; support.Sections[p] = section; support.Fragments[p] = NearestFragment(section, hit.point);
                }
            }
            return support;
        }

        static int NearestFragment(ShipDamageSection section, Vector3 point)
        {
            int result = -1; float best = float.PositiveInfinity;
            for (int i = 0; i < section.RepairCount; i++)
            {
                if ((section.RemovedFragments & (1UL << i)) != 0) continue;
                var source = section.RepairTransform(i);
                float distance = (source.TransformPoint(section.RepairBounds(i).ClosestPoint(source.InverseTransformPoint(point))) - point).sqrMagnitude;
                if (distance < best) { result = i; best = distance; }
            }
            return result;
        }

        void DropCannon(SimpleCannon cannon)
        {
            var prefab = cannon.IsMortar ? FallenMortar : FallenCannon;
            if (prefab == null) return;
            var position = cannon.transform.position; var rotation = cannon.transform.rotation;
            var dropped = Instantiate(prefab, position, rotation);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(dropped.gameObject, gameObject.scene);
            dropped.UpperDisabledUntil = Weapon(cannon.Index).DisabledUntil;
            var pose = dropped.GetComponent<UpperDroppedWeapon>();
            if (pose != null) pose.SetPose(cannon.Elevation, cannon.Traverse);
            if (!cannons.RemoveUpperCannon(cannon.Index)) { Destroy(dropped.gameObject); return; }
            dropped.Place(null, position, rotation);
            ServerManager.Spawn(dropped.NetworkObject);
            dropped.Kick(ship.Motor.CannonPointVelocity(position) + Vector3.down * .1f);
            supports.Remove(cannon.Index);
        }

        void StopWeapons()
        {
            foreach (var value in weaponVisuals.Values) if (value != null) Destroy(value);
            weaponVisuals.Clear(); supports.Clear(); registeredCannons = 0;
        }
    }
}
#endif
