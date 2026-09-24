using UnityEngine;
using UnityEngine.InputSystem;
using PirateSlop.Networking;

namespace PirateSlop
{
    [DefaultExecutionOrder(-50)]
    public sealed class CapstanStation : MonoBehaviour
    {
        [SerializeField] Transform rotor;
        [SerializeField] Transform anchorPin;
        [SerializeField] GameObject coil2;
        [SerializeField] GameObject coil3;
        [SerializeField] Transform pawlLeft;
        [SerializeField] Transform pawlRight;
        [SerializeField] Collider bodyCollider;
        [SerializeField] Collider spokeHandleEast;
        [SerializeField] Collider spokeHandleWest;
        [SerializeField] Mesh intactRotorMesh;
        [SerializeField] Mesh damagedRotorMesh;
        [SerializeField] float maxPinLift = 0.20f;

        ShipController ship;
        NetworkShip networkShip;
        ShipDamageSection capstanSection;
        MeshFilter rotorFilter;

        float localAngle;
        float progress = 1f;
        float dropHoldTime;
        bool isPushing;
        int activeSpokeIndex = -1;
        float lastPlayerAngle;
        float accumulatedDelta;
        float nextSyncTime;
        float reverseSpinRemaining;
        float reverseSpinSpeed;
        float lastClickAngle;
        float pinRestZ;
        float currentPinLift;

        public float Progress => networkShip != null && networkShip.IsSpawned ? (isPushing ? Mathf.Max(networkShip.AnchorRaiseProgress, progress) : networkShip.AnchorRaiseProgress) : progress;
        public bool IsAnchored => networkShip != null && networkShip.IsSpawned ? networkShip.AnchorDropped : (ship != null && ship.IsAnchored);
        public bool StructurallyAvailable { get; set; } = true;
        public bool BotPushing { get; set; }
        public int PusherCount => (isPushing ? 1 : 0) + (BotPushing ? 1 : 0);

        void Awake()
        {
            ship = GetComponentInParent<ShipController>();
            networkShip = GetComponentInParent<NetworkShip>();
            if (rotor == null) rotor = transform.Find("CapstanVisual/Capstan_Rotor");
            if (anchorPin == null && rotor != null) anchorPin = rotor.Find("Capstan_AnchorPin");
            if (coil2 == null && rotor != null) coil2 = rotor.Find("Capstan_Chain_Coil2")?.gameObject;
            if (coil3 == null && rotor != null) coil3 = rotor.Find("Capstan_Chain_Coil3")?.gameObject;
            if (pawlLeft == null) pawlLeft = transform.Find("CapstanVisual/Capstan_Pawl_Left");
            if (pawlRight == null) pawlRight = transform.Find("CapstanVisual/Capstan_Pawl_Right");
            if (anchorPin != null)
            {
                pinRestZ = 0f;
                anchorPin.localPosition = Vector3.zero;
            }

            if (rotor != null) rotorFilter = rotor.GetComponent<MeshFilter>();
            if (rotorFilter != null && intactRotorMesh == null) intactRotorMesh = rotorFilter.sharedMesh;
#if UNITY_EDITOR
            if (damagedRotorMesh == null)
            {
                damagedRotorMesh = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Models/Ships/Capstan/Capstan_Rotor_Damaged.asset");
            }
#endif
            var destruction = GetComponentInParent<ShipDestruction>();
            if (destruction != null)
            {
                foreach (var s in destruction.Sections)
                {
                    if (s != null && s.SectionId == 1059) { capstanSection = s; break; }
                }
            }
        }

        public bool InRange(AdvancedPlayerController player)
        {
            if (!StructurallyAvailable || player == null) return false;
            return Vector3.Distance(transform.position, player.transform.position) <= 2.8f;
        }

        public void CancelPush()
        {
            if (!isPushing) return;
            isPushing = false;
            activeSpokeIndex = -1;
            accumulatedDelta = 0f;
        }

        public void BeginPush(AdvancedPlayerController player, int spokeIndex)
        {
            if (player == null || !StructurallyAvailable || (!IsAnchored && Progress >= 1f)) return;
            isPushing = true;
            activeSpokeIndex = spokeIndex;
            Vector3 offset = player.transform.position - transform.position;
            lastPlayerAngle = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
        }

        public void UpdatePush(AdvancedPlayerController player, int spokeIndex)
        {
            if (!isPushing || player == null || !StructurallyAvailable) return;
            if (!IsAnchored && Progress >= 1f)
            {
                EndPush(player);
                return;
            }
            Vector3 offset = player.transform.position - transform.position;
            float currentAngle = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
            float delta = Mathf.DeltaAngle(lastPlayerAngle, currentAngle);
            lastPlayerAngle = currentAngle;

            float absDelta = Mathf.Abs(delta);
            if (absDelta > 0.05f && absDelta < 30f)
            {
                float multiplier = PusherCount > 1 ? 2.0f : 1.0f;
                ApplyRotationDelta(absDelta * multiplier);
            }
        }

        public void EndPush(AdvancedPlayerController player)
        {
            isPushing = false;
            activeSpokeIndex = -1;
            if (accumulatedDelta > 0f)
            {
                FlushNetworkDelta();
            }
        }

        void ApplyRotationDelta(float delta)
        {
            localAngle += delta;
            accumulatedDelta += delta;

            if (Mathf.Abs(localAngle - lastClickAngle) >= 30f)
            {
                lastClickAngle = localAngle;
                GameAudio.Play(SoundCue.BulletMetal, transform.position, 0.45f);
            }

            progress = Mathf.Clamp01(progress + delta / (360f * 3f));

            if (networkShip != null && networkShip.IsClientInitialized)
            {
                if (Time.unscaledTime >= nextSyncTime)
                {
                    FlushNetworkDelta();
                }
            }
            else
            {
                if (progress >= 1f && ship != null && ship.IsAnchored)
                {
                    ship.SetAnchored(false);
                    OnAnchorFullyRaised();
                }
            }
        }

        void FlushNetworkDelta()
        {
            if (networkShip != null && networkShip.IsClientInitialized && accumulatedDelta > 0f)
            {
                networkShip.RaiseAnchorDeltaServerRpc(accumulatedDelta);
                accumulatedDelta = 0f;
                nextSyncTime = Time.unscaledTime + 0.05f;
            }
        }

        public void BotPushDelta(float delta)
        {
            ApplyRotationDelta(delta);
        }

        public void DropAnchor()
        {
            if (!StructurallyAvailable) return;
            if (networkShip != null && networkShip.IsClientInitialized)
            {
                networkShip.DropAnchorServerRpc();
            }
            else
            {
                if (ship != null) ship.SetAnchored(true);
                progress = 0f;
                OnAnchorDropped();
            }
        }

        public void OnAnchorDropped()
        {
            progress = 0f;
            reverseSpinRemaining = 1.8f;
            reverseSpinSpeed = 1400f;
            float curSpeed = ship != null ? ship.Speed : (networkShip != null && networkShip.Motor != null ? networkShip.Motor.Speed : 0f);
            bool moving = Mathf.Abs(curSpeed) > 1.2f;

            GameAudio.Play(SoundCue.Splash, transform.position + Vector3.down * 4f, 1.0f);
            GameAudio.Play(SoundCue.BulletMetal, transform.position, 0.85f);
            GameAudio.Play(SoundCue.ShipCollision, transform.position, moving ? 1.0f : 0.45f);

            if (moving)
            {
                FirstPersonFeedback.Kick(transform.position, Vector3.down, 0.35f);
                Vector3 dropPt = transform.position + transform.forward * 8f;
                var ocean = OceanSurface.Instance;
                if (ocean != null) dropPt.y = ocean.Height(dropPt);
                CombatVfx.Splash(dropPt, 2.2f);
            }
        }

        public void OnAnchorFullyRaised()
        {
            progress = 1f;
            isPushing = false;
            GameAudio.Play(SoundCue.ChestClose, transform.position, 0.85f);
        }

        void Update()
        {
            float curProgress = Progress;

            if (reverseSpinRemaining > 0f)
            {
                reverseSpinRemaining -= Time.deltaTime;
                localAngle -= reverseSpinSpeed * Time.deltaTime;
                reverseSpinSpeed = Mathf.Max(240f, reverseSpinSpeed - 600f * Time.deltaTime);
            }

            if (rotor != null)
            {
                rotor.localRotation = Quaternion.Euler(0, 0, localAngle);
            }

            if (anchorPin != null)
            {
                float targetLift;
                if (reverseSpinRemaining > 0f)
                {
                    float dropFraction = 1f - Mathf.Clamp01(reverseSpinRemaining / 1.8f);
                    float smoothDrop = Mathf.SmoothStep(0f, 1f, dropFraction);
                    targetLift = Mathf.Lerp(0f, maxPinLift, smoothDrop);
                }
                else if (IsAnchored || curProgress < 1f)
                {
                    targetLift = Mathf.Lerp(maxPinLift, 0f, curProgress);
                }
                else
                {
                    targetLift = 0f;
                }

                float liftSpeed = (reverseSpinRemaining > 0f || isPushing) ? 0.45f : 0.20f;
                currentPinLift = Mathf.MoveTowards(currentPinLift, targetLift, liftSpeed * Time.deltaTime);
                anchorPin.localPosition = new Vector3(0f, 0f, pinRestZ + currentPinLift);
            }

            if (coil2 != null) coil2.SetActive(curProgress >= 0.33f);
            if (coil3 != null) coil3.SetActive(curProgress >= 0.66f);

            if (pawlLeft != null || pawlRight != null)
            {
                float phase = Mathf.Repeat(localAngle, 30f) / 30f;
                float lift = phase < 0.75f ? phase * 8f : (1f - phase) * 24f;
                if (pawlLeft != null) pawlLeft.localRotation = Quaternion.Euler(0, 0, 48.7f + lift);
                if (pawlRight != null) pawlRight.localRotation = Quaternion.Euler(0, 0, 131.3f - lift);
            }

            bool damaged = !StructurallyAvailable || (capstanSection != null && capstanSection.State != ShipSectionState.Intact);
            if (rotorFilter != null && damagedRotorMesh != null && intactRotorMesh != null)
            {
                rotorFilter.sharedMesh = damaged ? damagedRotorMesh : intactRotorMesh;
            }
            if (spokeHandleEast != null)
            {
                spokeHandleEast.transform.localEulerAngles = damaged ? new Vector3(0, 0, -28f) : Vector3.zero;
                spokeHandleEast.transform.localPosition = damaged ? new Vector3(0.70f, 0, 0.75f) : new Vector3(0.75f, 0, 0.94f);
            }

            CheckLocalInteraction(curProgress);
        }

        void CheckLocalInteraction(float curProgress)
        {
            var kb = Keyboard.current;
            var motor = FindLocalPlayerMotor();
            if (motor == null || motor.IsDead || !motor.InputActive)
            {
                dropHoldTime = 0f;
                return;
            }

            var camera = motor.PlayerCamera ?? Camera.main;
            if (camera == null) return;

            float dist = Vector3.Distance(transform.position, motor.transform.position);
            if (dist > 3.0f)
            {
                dropHoldTime = 0f;
                return;
            }

            if (!StructurallyAvailable)
            {
                dropHoldTime = 0f;
                if (isPushing) CancelPush();
                var inv = motor.GetComponent<PlayerInventory>();
                if (inv == null || !inv.MalletSelected)
                {
                    Vector3 toCapstan = (transform.position + Vector3.up * 0.7f) - camera.transform.position;
                    if (Vector3.Dot(camera.transform.forward, toCapstan.normalized) > 0.6f)
                    {
                        ContextPrompt.Offer("ШПИЛЬ СЛОМАН · Почините молотком", 85);
                    }
                }
                return;
            }

            if (isPushing)
            {
                string pushText = PusherCount > 1
                    ? $"ПОДЪЁМ ЯКОРЯ (x2 вдвоём) · Идите по кругу · {Mathf.RoundToInt(curProgress * 100)}%"
                    : $"ПОДЪЁМ ЯКОРЯ · Идите по кругу · {Mathf.RoundToInt(curProgress * 100)}%";
                ContextPrompt.Offer(pushText, 85);
                return;
            }

            Vector3 capstanCenter = transform.position + Vector3.up * 0.65f;
            Vector3 toCenter = capstanCenter - camera.transform.position;
            float lookAngle = Vector3.Angle(camera.transform.forward, toCenter);
            bool aiming = lookAngle < 40f;
            if (!aiming && Physics.Raycast(camera.transform.position, camera.transform.forward, out var hit, 3.2f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform == transform || hit.transform.IsChildOf(transform)) aiming = true;
            }

            if (aiming)
            {
                if (!IsAnchored && curProgress >= 1f)
                {
                    if (kb != null && kb.eKey.isPressed)
                    {
                        dropHoldTime += Time.deltaTime;
                        int pct = Mathf.RoundToInt(Mathf.Clamp01(dropHoldTime / 1.5f) * 100f);
                        ContextPrompt.Offer($"СБРОС ЯКОРЯ · [E] {pct}%", 90);
                        if (dropHoldTime >= 1.5f)
                        {
                            dropHoldTime = 0f;
                            DropAnchor();
                        }
                    }
                    else
                    {
                        dropHoldTime = 0f;
                        ContextPrompt.Offer("ЯКОРЬ ПОДНЯТ · Удерживайте [E] (1.5 сек) — Сбросить якорь", 85);
                    }
                    return;
                }
                else
                {
                    dropHoldTime = 0f;
                    ContextPrompt.Offer($"ЯКОРЬ НА ДНЕ ({Mathf.RoundToInt(curProgress * 100)}%) · Зажмите ЛКМ на рукояти — Поднять якорь", 85);
                    return;
                }
            }

            dropHoldTime = 0f;
        }

        AdvancedPlayerController FindLocalPlayerMotor()
        {
            foreach (var p in NetworkPlayer.Active)
            {
                if (p != null && p.IsOwner && !p.IsBot.Value && p.Motor != null) return p.Motor;
            }
            foreach (var apc in FindObjectsByType<AdvancedPlayerController>(FindObjectsSortMode.None))
            {
                if (apc != null && apc.InputActive && !apc.IsDead) return apc;
            }
            return null;
        }
    }
}
