using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PirateSlop.Networking
{
    [DefaultExecutionOrder(60)]
    public sealed class NetworkHolyGrenadeHands : NetworkBehaviour
    {
        public NetworkHolyGrenade Prefab;
        public Material ArcMaterial;
        public float ThrowSpeed = 14f;
        public float FuseSeconds = 3f;
        readonly SyncVar<float> remaining = new();
        public bool Primed => remaining.Value > 0;
        PlayerInventory inventory;
        NetworkEquipment equipment;
        NetworkWeapon weapon;
        AdvancedPlayerController motor;
        int primedSlot = -1;
        float deadline;
        bool aiming;
        LineRenderer arc;
        readonly Vector3[] points = new Vector3[251];
        float flashAt = -10, flashStrength;
        float botBlindedUntil;
        public bool BotBlinded => Time.time < botBlindedUntil;
        void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
            equipment = GetComponent<NetworkEquipment>();
            weapon = GetComponent<NetworkWeapon>();
            motor = GetComponent<AdvancedPlayerController>();
        }
        bool CanUse => IsSpawned && equipment.Active && equipment.Item == InventoryItem.HolyGrenade && (inventory.Fishing == null || (!inventory.Fishing.IsFishing && !inventory.Fishing.IsEating)) && !(GetComponent<DirectShipControls>()?.BlocksPrimary ?? false);
        void Update()
        {
            if (!IsSpawned) return;
            if (IsServerInitialized && Primed)
            {
                if (Time.time >= deadline)
                {
                    if (weapon.ConsumeEquipment(primedSlot, InventoryItem.HolyGrenade))
                        NetworkHolyGrenade.Explode(this, transform.position + Vector3.up * 1.3f, Prefab);
                    ClearPrime();
                }
                else if (!CanUse || inventory.SelectedSlot != primedSlot) DropPrimed();
                else remaining.Value = Mathf.Max(.001f, deadline - Time.time);
            }
            if (!IsOwner) return;
            var mouse = Mouse.current;
            if (!CanUse || !motor.InputActive || PlayerInventory.LootWindowOpen || mouse == null)
            {
                aiming = false;
                if (arc != null) arc.enabled = false;
                return;
            }
            if (mouse.rightButton.wasPressedThisFrame) PrimeServerRpc();
            if (mouse.leftButton.wasPressedThisFrame && !inventory.InteractionUsed && !GetComponent<CannonHands>().CanPickUpBall()) aiming = true;
            if (aiming && mouse.leftButton.wasReleasedThisFrame)
            {
                ThrowServerRpc(motor.AimDirection);
                aiming = false;
            }
            if (arc != null) arc.enabled = aiming;
        }
        void LateUpdate()
        {
            if (!IsSpawned) return;
            if (equipment.Item == InventoryItem.HolyGrenade)
            {
                if (equipment.View != null) foreach (var fuse in equipment.View.GetComponentsInChildren<HolyGrenadeFuse>(true)) fuse.SetBurn(Primed, remaining.Value / FuseSeconds);
                if (equipment.World != null) foreach (var fuse in equipment.World.GetComponentsInChildren<HolyGrenadeFuse>(true)) fuse.SetBurn(Primed, remaining.Value / FuseSeconds);
            }
            if (!IsOwner || !aiming || !CanUse) return;
            if (arc == null)
            {
                var root = new GameObject("HolyGrenadeTrajectory");
                arc = root.AddComponent<LineRenderer>();
                arc.sharedMaterial = ArcMaterial;
                arc.startWidth = arc.endWidth = .035f;
                arc.startColor = arc.endColor = new Color(1f, .88f, .45f);
                arc.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                arc.receiveShadows = false;
            }
            arc.enabled = true;
            GetLaunch(motor.AimDirection, out var point, out var velocity);
            points[0] = point;
            int count = 1;
            float duration = Primed ? remaining.Value : 5f;
            for (float t = 0; t < duration && count < points.Length; t += NetworkHolyGrenade.Step)
            {
                bool hit = NetworkHolyGrenade.Advance(transform, null, ref point, ref velocity, Mathf.Min(NetworkHolyGrenade.Step, duration - t), out _);
                points[count++] = point;
                if (hit) break;
            }
            arc.positionCount = count;
            for (int i = 0; i < count; i++) arc.SetPosition(i, points[i]);
        }
        public void GetLaunch(Vector3 forward, out Vector3 point, out Vector3 velocity)
        {
            forward.Normalize();
            Vector3 eye = transform.position + Vector3.up * (motor.IsCrouched ? .8f : 1.5f);
            point = eye + forward * .55f;
            if (FirearmTrace.Cast(gameObject, eye, point, out var hit)) point = hit.point - forward * .15f;
            velocity = forward * ThrowSpeed;
            var ship = GetComponent<ShipDeckPassenger>()?.Ship;
            if (ship != null) velocity += ship.GetComponent<ShipController>().CannonPointVelocity(point);
        }
        [ServerRpc]
        void PrimeServerRpc()
        {
            if (!CanUse || Primed || Prefab == null) return;
            primedSlot = inventory.SelectedSlot;
            deadline = Time.time + FuseSeconds;
            remaining.Value = FuseSeconds;
        }
        [ServerRpc]
        void ThrowServerRpc(Vector3 forward)
        {
            if (!CanUse || !float.IsFinite(forward.sqrMagnitude) || forward.sqrMagnitude < .5f) return;
            Release(forward, false);
        }
        public bool TryBotThrow(Vector3 forward)
        {
            var player = GetComponent<NetworkPlayer>();
            if (!IsServerInitialized || player == null || !player.IsBot.Value || !CanUse || Primed || Prefab == null ||
                !float.IsFinite(forward.sqrMagnitude) || forward.sqrMagnitude < .5f) return false;
            primedSlot = inventory.SelectedSlot; deadline = Time.time + FuseSeconds; remaining.Value = FuseSeconds;
            bool released = Release(forward, false);
            if (!released) ClearPrime();
            return released;
        }
        public bool DropPrimed()
        {
            if (!IsServerInitialized || !Primed) return false;
            return Release(transform.forward, true);
        }
        bool Release(Vector3 forward, bool drop)
        {
            if (Prefab == null) return false;
            int slot = Primed ? primedSlot : inventory.SelectedSlot;
            if (!weapon.ConsumeEquipment(slot, InventoryItem.HolyGrenade)) { ClearPrime(); return false; }
            GetLaunch(forward, out var point, out var velocity);
            if (drop) velocity = Vector3.zero;
            var grenade = Instantiate(Prefab, point, Quaternion.identity);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(grenade.gameObject, gameObject.scene);
            grenade.Launch(this, velocity, Primed ? Mathf.Max(.001f, deadline - Time.time) : -1f);
            ServerManager.Spawn(grenade.NetworkObject);
            ClearPrime();
            return true;
        }
        void ClearPrime() { remaining.Value = 0; primedSlot = -1; }
        public override void OnStopServer()
        {
            if (Primed) DropPrimed();
            base.OnStopServer();
        }
        public void Flash(float strength)
        {
            if (IsServerInitialized && GetComponent<NetworkPlayer>() is NetworkPlayer player && player.IsBot.Value && strength > .05f)
                botBlindedUntil = Mathf.Max(botBlindedUntil, Time.time + 1.65f * Mathf.Clamp01(strength));
            if (IsServerInitialized && Owner != null && Owner.IsValid) FlashTargetRpc(Owner, Mathf.Clamp01(strength));
        }
        [TargetRpc]
        void FlashTargetRpc(FishNet.Connection.NetworkConnection connection, float strength)
        {
            flashStrength = Mathf.Max(CurrentFlash, strength);
            flashAt = Time.unscaledTime;
        }
        float CurrentFlash => flashStrength * (1f - Mathf.SmoothStep(0, 1, (Time.unscaledTime - flashAt - 1f) / .65f));
        public void DrawFlash()
        {
            if (!IsOwner || motor.IsDead || motor.PlayerCamera == null || !motor.PlayerCamera.enabled || CurrentFlash <= 0) return;
            var color = GUI.color;
            var matrix = GUI.matrix;
            int depth = GUI.depth;
            GUI.matrix = Matrix4x4.identity;
            GUI.depth = -10000;
            GUI.color = new Color(1, 1, 1, CurrentFlash);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = color;
            GUI.matrix = matrix;
            GUI.depth = depth;
        }
        public void BurstSound(Vector3 point) => BurstSoundObserversRpc(point);
        [ObserversRpc(RunLocally = true)]
        void BurstSoundObserversRpc(Vector3 point)
        {
            GameAudio.Play(SoundCue.HolyFlash, point);
            if (Prefab != null) HolyGrenadeFuse.Burst(point, Prefab.GetComponentInChildren<HolyGrenadeFuse>());
        }
        void OnDisable() { aiming = false; if (arc != null) arc.enabled = false; flashStrength = 0; }
        void OnDestroy() { if (arc != null) Destroy(arc.gameObject); }
    }
}
