using UnityEngine;
using UnityEngine.InputSystem;

namespace PirateSlop.Harpoon
{
    public class HarpoonGun : MonoBehaviour, IWeaponTarget
    {
        [Header("Mount & Pivots")]
        [SerializeField] Transform baseYaw;
        [SerializeField] Transform barrelPitch;
        [SerializeField] Transform muzzle;
        [SerializeField] Transform winchDrum;
        [SerializeField] Transform restingHarpoon;
        [SerializeField] Transform cameraMount;
        [SerializeField] Transform drumAxle;

        [Header("Projectile & Rope")]
        [SerializeField] HarpoonProjectile projectilePrefab;
        [SerializeField] LineRenderer ropeRenderer;
        [SerializeField] float launchSpeed = 38f;

        [Header("Aim Limits")]
        [SerializeField] float minYaw = -70f;
        [SerializeField] float maxYaw = 70f;
        [SerializeField] float minPitch = -20f;
        [SerializeField] float maxPitch = 35f;
        [SerializeField] float aimSensitivity = 0.12f;

        [Header("Durability & Repair")]
        [SerializeField] float maxHealth = 100f;
        [SerializeField] ShipDamageSection mountSection;

        float currentHealth = 100f;
        bool isBroken;
        int repairStrikes;

        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public bool IsBroken => isBroken;
        public bool StructurallyAvailable => !isBroken && (mountSection == null || mountSection.State != ShipSectionState.Destroyed);
        public int RepairStrikes => repairStrikes;

        public void SetMountSection(ShipDamageSection section) => mountSection = section;

        public void ReceiveWeaponHit(float damage, GameObject attacker) => TakeDamage(damage, attacker);

        public void TakeDamage(float damage, GameObject attacker = null)
        {
            if (isBroken) return;
            currentHealth = Mathf.Max(0f, currentHealth - damage);
            GameAudio.Play(SoundCue.BulletMetal, transform.position, 0.85f);
            CombatVfx.Impact(transform.position + Vector3.up * 0.3f, Vector3.up, true, true);
            if (currentHealth <= 0f)
            {
                BreakGun();
            }
            if (NetworkShip != null && NetworkShip.IsServerInitialized && MountIndex >= 0)
            {
                NetworkShip.PublishHarpoonState(MountIndex, currentHealth, isBroken);
            }
        }

        public void BreakGun()
        {
            if (isBroken) return;
            isBroken = true;
            currentHealth = 0f;
            repairStrikes = 0;
            ReleaseControl();
            if (activeProjectile != null) activeProjectile.DetachAndRewind();
            if (barrelPitch != null) barrelPitch.localRotation = Quaternion.Euler(-25f, 0, 15f);
            GameAudio.Play(SoundCue.ShipCollision, transform.position, 1.0f);
            CombatVfx.Impact(transform.position + Vector3.up * 0.4f, Vector3.up, true, true);
        }

        public bool RepairStrike(GameObject repairer)
        {
            if (!isBroken) return false;
            repairStrikes++;
            GameAudio.Play(SoundCue.BulletMetal, transform.position, 0.9f);
            CombatVfx.Impact(transform.position + Vector3.up * 0.4f, Vector3.up, false, true);
            if (repairStrikes >= 3)
            {
                RepairFully();
                return true;
            }
            return false;
        }

        public void RepairFully()
        {
            isBroken = false;
            currentHealth = maxHealth;
            repairStrikes = 0;
            if (barrelPitch != null) barrelPitch.localRotation = Quaternion.Euler(currentPitch, 0, 0);
            GameAudio.Play(SoundCue.ReloadReady, transform.position, 1.0f);
            RepairReveal.Show(gameObject);
        }

        public int MountIndex { get; private set; } = -1;
        public HarpoonShipMount Mount { get; private set; }
        public PirateSlop.Networking.NetworkShip NetworkShip => Mount != null ? Mount.GetComponentInParent<PirateSlop.Networking.NetworkShip>() : GetComponentInParent<PirateSlop.Networking.NetworkShip>();

        public void InitializeMount(HarpoonShipMount mount, int index)
        {
            Mount = mount;
            MountIndex = index;
        }

        public Transform Muzzle => muzzle;
        public Transform CameraMount => cameraMount;
        public Rigidbody ShipBody => shipBody != null ? shipBody : (shipBody = GetComponentInParent<Rigidbody>());
        public AdvancedPlayerController Operator => operatorPlayer;
        public HarpoonProjectile ActiveProjectile => activeProjectile;
        public bool IsOccupied => operatorPlayer != null;

        Rigidbody shipBody;
        AdvancedPlayerController operatorPlayer;
        HarpoonProjectile activeProjectile;
        Transform restingTip;
        Transform restingKnot;
        int enteredFrame = -1;
        int exitedFrame = -1;
        float currentYaw;
        float currentPitch;
        float lastDistance;
        float nextAimSync;
        Vector3 cachedAxleDir;

        void Awake()
        {
            shipBody = GetComponentInParent<Rigidbody>();
            if (muzzle == null) muzzle = transform.Find("Visual/Base_Yaw/Barrel_Pitch/Barrel_Muzzle_Ring") ?? transform.Find("Base_Yaw/Barrel_Pitch/Barrel_Muzzle_Ring") ?? transform;
            if (cameraMount == null) cameraMount = transform.Find("Visual/Base_Yaw/Barrel_Pitch/CameraMount") ?? transform.Find("CameraMount") ?? transform;
            if (baseYaw == null) baseYaw = transform.Find("Visual/Base_Yaw") ?? transform.Find("Base_Yaw");
            if (barrelPitch == null && baseYaw != null) barrelPitch = baseYaw.Find("Barrel_Pitch");
            if (winchDrum == null && baseYaw != null) winchDrum = baseYaw.Find("Winch_Drum");
            if (drumAxle == null && baseYaw != null) drumAxle = baseYaw.Find("Winch_Axle");

            if (barrelPitch != null)
            {
                if (restingHarpoon == null) restingHarpoon = barrelPitch.Find("Harpoon_Projectile");
                if (restingTip == null) restingTip = barrelPitch.Find("Harpoon_Tip");
                if (restingKnot == null) restingKnot = barrelPitch.Find("Harpoon_Rope_Knot");
            }
            if (restingHarpoon == null) restingHarpoon = transform.Find("Visual/Base_Yaw/Barrel_Pitch/Harpoon_Projectile") ?? transform.Find("Base_Yaw/Barrel_Pitch/Harpoon_Projectile");
            if (restingTip == null) restingTip = transform.Find("Visual/Base_Yaw/Barrel_Pitch/Harpoon_Tip") ?? transform.Find("Base_Yaw/Barrel_Pitch/Harpoon_Tip");
            if (restingKnot == null) restingKnot = transform.Find("Visual/Base_Yaw/Barrel_Pitch/Harpoon_Rope_Knot") ?? transform.Find("Base_Yaw/Barrel_Pitch/Harpoon_Rope_Knot");

            if (drumAxle != null) cachedAxleDir = drumAxle.forward;
            else cachedAxleDir = transform.right;

            if (ropeRenderer == null) ropeRenderer = GetComponent<LineRenderer>() ?? GetComponentInChildren<LineRenderer>();
            if (ropeRenderer != null)
            {
                ropeRenderer.positionCount = 24;
                ropeRenderer.enabled = true;
            }
        }

        void SetRestingHarpoonVisible(bool visible)
        {
            if (restingHarpoon != null && restingHarpoon.gameObject.activeSelf != visible)
                restingHarpoon.gameObject.SetActive(visible);
            if (restingTip != null && restingTip.gameObject.activeSelf != visible)
                restingTip.gameObject.SetActive(visible);
            if (restingKnot != null && restingKnot.gameObject.activeSelf != visible)
                restingKnot.gameObject.SetActive(visible);
        }

        public bool InRange(AdvancedPlayerController player)
        {
            if (player == null || player.IsDead) return false;
            return Vector3.Distance(player.transform.position, transform.position) <= 3.8f;
        }

        public bool TakeControl(AdvancedPlayerController player)
        {
            if (Time.frameCount == exitedFrame || isBroken || !StructurallyAvailable || player == null || player.IsDead || !InRange(player) || (operatorPlayer != null && operatorPlayer != player))
                return false;

            operatorPlayer = player;
            player.ActiveHarpoon = this;
            enteredFrame = Time.frameCount;
            return true;
        }

        public void SetRemoteOperator(AdvancedPlayerController player)
        {
            if (player != null && player.IsLocal)
            {
                TakeControl(player);
            }
            else
            {
                operatorPlayer = player;
            }
        }

        public void SetRemoteAim(float yaw, float pitch)
        {
            if (operatorPlayer != null && operatorPlayer.IsLocal) return;
            currentYaw = yaw;
            currentPitch = pitch;
            if (baseYaw != null) baseYaw.localRotation = Quaternion.Euler(0, 0, currentYaw);
            if (barrelPitch != null) barrelPitch.localRotation = Quaternion.Euler(currentPitch, 0, 0);
        }

        public void ReleaseControl()
        {
            exitedFrame = Time.frameCount;
            if (operatorPlayer != null)
            {
                if (NetworkShip != null && operatorPlayer.IsLocal && MountIndex >= 0)
                {
                    NetworkShip.ReleaseHarpoonControl(MountIndex);
                }
                if (operatorPlayer.ActiveHarpoon == this)
                    operatorPlayer.ActiveHarpoon = null;
                operatorPlayer = null;
            }
        }

        void Update()
        {
            if (mountSection != null && mountSection.State == ShipSectionState.Destroyed && !isBroken)
            {
                BreakGun();
            }
            HandleOperatorInput();
            UpdateDrumAndRope();
        }

        void HandleOperatorInput()
        {
            if (operatorPlayer == null) return;
            if (Time.frameCount == enteredFrame) return;

            var keyboard = Keyboard.current;
            var mouse = Mouse.current;

            if (!operatorPlayer.InputActive || operatorPlayer.IsDead || !InRange(operatorPlayer) ||
                operatorPlayer.ActiveHarpoon != this || keyboard == null || mouse == null ||
                keyboard.eKey.wasPressedThisFrame)
            {
                ReleaseControl();
                return;
            }

            Vector2 delta = mouse.delta.ReadValue() * aimSensitivity;
            currentYaw = Mathf.Clamp(currentYaw + delta.x, minYaw, maxYaw);
            currentPitch = Mathf.Clamp(currentPitch + delta.y, minPitch, maxPitch);

            if (baseYaw != null) baseYaw.localRotation = Quaternion.Euler(0, 0, currentYaw);
            if (barrelPitch != null) barrelPitch.localRotation = Quaternion.Euler(currentPitch, 0, 0);

            if (NetworkShip != null && MountIndex >= 0 && Time.time >= nextAimSync)
            {
                nextAimSync = Time.time + 0.05f;
                NetworkShip.HarpoonAim(MountIndex, currentYaw, currentPitch);
            }

            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (activeProjectile == null)
                {
                    Fire();
                }
                else
                {
                    if (NetworkShip != null && MountIndex >= 0)
                        NetworkShip.HarpoonDetach(MountIndex);
                    activeProjectile.DetachAndRewind();
                }
            }
            else if (mouse.rightButton.wasPressedThisFrame && activeProjectile != null)
            {
                if (NetworkShip != null && MountIndex >= 0)
                    NetworkShip.HarpoonDetach(MountIndex);
                activeProjectile.DetachAndRewind();
            }

            if (activeProjectile != null && activeProjectile.IsAttached)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    float deltaLength = Mathf.Sign(scroll) * 1.5f;
                    if (NetworkShip != null && MountIndex >= 0)
                    {
                        NetworkShip.HarpoonWinch(MountIndex, deltaLength);
                    }
                    else
                    {
                        float newLen = activeProjectile.CurrentCableLength - deltaLength;
                        activeProjectile.SetCableLength(newLen);
                        RotateWinchDrum(deltaLength * 70f);
                        GameAudio.Play(SoundCue.BulletMetal, transform.position, 0.4f);
                    }
                }
            }
        }

        public void Fire()
        {
            if (projectilePrefab == null || muzzle == null) return;

            Vector3 spawnPos = muzzle.position;
            Vector3 vel = muzzle.forward * launchSpeed;
            if (ShipBody != null) vel += ShipBody.linearVelocity;

            if (NetworkShip != null && MountIndex >= 0)
            {
                NetworkShip.HarpoonFire(MountIndex, spawnPos, vel);
            }
            LaunchInternal(spawnPos, vel);
        }

        public void LaunchFromNetwork(Vector3 spawnPos, Vector3 vel)
        {
            if (activeProjectile != null) return;
            LaunchInternal(spawnPos, vel);
        }

        void LaunchInternal(Vector3 spawnPos, Vector3 vel)
        {
            if (projectilePrefab == null || muzzle == null) return;

            activeProjectile = Instantiate(projectilePrefab, spawnPos, Quaternion.LookRotation(vel.normalized, Vector3.up));
            activeProjectile.Launch(this, vel);

            SetRestingHarpoonVisible(false);
            if (ropeRenderer != null) ropeRenderer.enabled = true;

            lastDistance = 0f;
            GameAudio.Play(SoundCue.Cannon, muzzle.position, 1.0f);
            GameAudio.Play(SoundCue.HookThrow, muzzle.position, 0.8f);
        }

        public void ApplyNetworkState(float health, bool broken)
        {
            currentHealth = health;
            if (broken && !isBroken)
            {
                BreakGun();
            }
            else if (!broken && isBroken)
            {
                RepairFully();
            }
        }

        public void ApplyRepairNetworkState(float health, bool broken, int strikes, Vector3 strikePoint)
        {
            currentHealth = health;
            repairStrikes = strikes;
            GameAudio.Play(SoundCue.BulletMetal, strikePoint, 0.9f);
            CombatVfx.Impact(strikePoint, Vector3.up, false, true);
            if (!broken && isBroken)
            {
                RepairFully();
            }
        }

        public void OnProjectileAttached(HarpoonProjectile proj)
        {
        }

        public void OnProjectileRewinding(HarpoonProjectile proj)
        {
        }

        public void OnProjectileReturned(HarpoonProjectile proj)
        {
            activeProjectile = null;
            SetRestingHarpoonVisible(true);
            if (ropeRenderer != null) ropeRenderer.enabled = true;
        }

        public void RotateWinchDrum(float angle)
        {
            if (winchDrum == null) return;
            winchDrum.Rotate(0, 0, angle, Space.Self);
        }

        void UpdateDrumAndRope()
        {
            SetRestingHarpoonVisible(activeProjectile == null);

            if (ropeRenderer == null || muzzle == null) return;

            Vector3 drumTop = winchDrum != null ? (winchDrum.position + transform.up * 0.125f) : muzzle.position;
            Vector3 pMuzzle = muzzle.position;

            Vector3 pTarget;
            float sag;

            if (activeProjectile != null)
            {
                pTarget = activeProjectile.KnotPosition;
                float dist = Vector3.Distance(pMuzzle, pTarget);

                if (activeProjectile.IsFlying)
                {
                    float delta = dist - lastDistance;
                    if (delta > 0.001f)
                    {
                        RotateWinchDrum(delta * 120f);
                        lastDistance = dist;
                    }
                    sag = 0.35f;
                }
                else if (activeProjectile.IsRewinding)
                {
                    RotateWinchDrum(-Time.deltaTime * 720f);
                    sag = 0.15f;
                }
                else
                {
                    float slack = activeProjectile.IsAttached ? Mathf.Max(0f, activeProjectile.CurrentCableLength - dist) : 0f;
                    sag = 0.04f + slack * 0.4f;
                }
            }
            else
            {
                pTarget = restingKnot != null ? restingKnot.position : (restingHarpoon != null ? restingHarpoon.position + restingHarpoon.forward * 0.24f : pMuzzle);
                sag = 0.015f;
            }

            int count = ropeRenderer.positionCount;
            if (count < 8)
            {
                count = 24;
                ropeRenderer.positionCount = count;
            }

            int drumSegmentCount = 6;
            for (int i = 0; i < drumSegmentCount; i++)
            {
                float t = (float)i / (drumSegmentCount - 1);
                Vector3 pt = Vector3.Lerp(drumTop, pMuzzle, t) + Vector3.down * (0.02f * 4f * t * (1f - t));
                ropeRenderer.SetPosition(i, pt);
            }

            for (int i = drumSegmentCount; i < count; i++)
            {
                float t = (float)(i - drumSegmentCount) / (count - drumSegmentCount - 1);
                Vector3 pt = Vector3.Lerp(pMuzzle, pTarget, t) + Vector3.down * (sag * 4f * t * (1f - t));
                ropeRenderer.SetPosition(i, pt);
            }

            if (!ropeRenderer.enabled) ropeRenderer.enabled = true;
        }

        void OnGUI()
        {
            if (operatorPlayer == null || !operatorPlayer.InputActive) return;

            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;
            float size = 6f;
            float gap = 3f;
            Color col = new Color(1f, 1f, 1f, 0.85f);

            DrawCrosshairBar(new Rect(cx - gap - size, cy - 1f, size, 2f), col);
            DrawCrosshairBar(new Rect(cx + gap, cy - 1f, size, 2f), col);
            DrawCrosshairBar(new Rect(cx - 1f, cy - gap - size, 2f, size), col);
            DrawCrosshairBar(new Rect(cx - 1f, cy + gap, 2f, size), col);

            if (activeProjectile != null && activeProjectile.IsAttached)
            {
                ContextPrompt.Offer("Колесико — длина троса · ПКМ/ЛКМ — смотать · E — выйти", 40);
            }
            else
            {
                ContextPrompt.Offer("Мышь — прицел · ЛКМ — выстрел · E — выйти", 40);
            }
        }

        static Texture2D whitePixel;
        static void DrawCrosshairBar(Rect r, Color c)
        {
            if (whitePixel == null)
            {
                whitePixel = new Texture2D(1, 1);
                whitePixel.SetPixel(0, 0, Color.white);
                whitePixel.Apply();
            }
            GUI.color = c;
            GUI.DrawTexture(r, whitePixel);
            GUI.color = Color.white;
        }

        void OnDisable()
        {
            ReleaseControl();
        }
    }
}
