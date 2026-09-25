using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using PirateSlop.Networking;

namespace PirateSlop
{
    public enum WhalePOIState
    {
        Idle,
        Agitated,
        Diving,
        Cleared
    }

    public sealed class WhaleLootPoint : MonoBehaviour
    {
        public Animator WhaleAnimator;
        public Transform WhaleModel;
        public WhaleChest Chest;
        public List<WhaleHarpoonPin> Harpoons = new();
        public float ChallengeDuration = 30f;

        WhalePOIState state = WhalePOIState.Idle;
        float remainingTime;
        bool timerStarted;
        int initialHarpoonCount;

        float nextThrashTime;
        float thrashTimer;
        float thrashDuration = 1.4f;
        float targetThrashYaw;
        float targetThrashRoll;
        float currentThrashYaw;
        float currentThrashRoll;

        Vector3 basePosition;
        Quaternion baseRotation;
        float diveTimer;

        string currentPrompt = "";
        float currentPullProgress = 0f;
        bool showPullProgress = false;
        string notificationMessage = "";
        float notificationTimer = 0f;
        Color notificationColor = Color.white;

        public WhalePOIState State => state;
        public float RemainingTime => remainingTime;
        public int RemainingHarpoons
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Harpoons.Count; i++)
                {
                    if (Harpoons[i] != null && !Harpoons[i].IsRemoved) count++;
                }
                return count;
            }
        }

        void Start()
        {
            basePosition = transform.position;
            baseRotation = transform.rotation;
            remainingTime = ChallengeDuration;
            initialHarpoonCount = Harpoons.Count;

            if (WhaleAnimator != null)
            {
                WhaleAnimator.SetFloat("Speed", 0.45f);
            }
        }

        public void NotifyPullStarted()
        {
            if (state == WhalePOIState.Idle)
            {
                state = WhalePOIState.Agitated;
                timerStarted = true;
                nextThrashTime = Time.time + Random.Range(1.8f, 3f);
                if (WhaleAnimator != null)
                {
                    WhaleAnimator.SetFloat("Speed", 1.5f);
                }
            }
        }

        public void OnHarpoonRemoved(WhaleHarpoonPin harpoon)
        {
            if (WhaleAnimator != null)
            {
                WhaleAnimator.SetTrigger("Bite");
            }

            int left = RemainingHarpoons;
            if (left == 0)
            {
                state = WhalePOIState.Cleared;
                timerStarted = false;
                if (WhaleAnimator != null)
                {
                    WhaleAnimator.SetFloat("Speed", 0.4f);
                }
                currentThrashYaw = 0f;
                currentThrashRoll = 0f;
                ShowNotification("ВСЕ ГАРПУНЫ ИЗВЛЕЧЕНЫ! СУНДУК РАЗБЛОКИРОВАН!", Color.green, 4f);
            }
            else
            {
                ShowNotification($"ГАРПУН ВЫТАЩЕН! (Осталось: {left})", Color.yellow, 2f);
            }
        }

        public void OnChestOpened(AdvancedPlayerController player)
        {
            if (player != null)
            {
                var inv = player.GetComponent<PlayerInventory>();
                if (inv != null)
                {
                    for (int i = 0; i < 6; i++)
                    {
                        if (inv.ItemAt(i) == InventoryItem.None)
                        {
                            inv.SetBallCount(i, 5, InventoryItem.Cannonball);
                            break;
                        }
                    }
                }
            }

            GameAudio.Play(SoundCue.Pickup, transform.position);
            ShowNotification("ДОБЫЧА ПОЛУЧЕНА: ДРЕВНИЕ СОКРОВИЩА, ЯДРА И ПРИПАСЫ!", new Color(1f, 0.85f, 0.2f), 5f);
        }

        void ShowNotification(string text, Color color, float duration)
        {
            notificationMessage = text;
            notificationColor = color;
            notificationTimer = duration;
        }

        void Update()
        {
            UpdateLifecycle();
            UpdatePlayerInteraction();
        }

        void UpdateLifecycle()
        {
            if (notificationTimer > 0f)
            {
                notificationTimer -= Time.deltaTime;
            }

            switch (state)
            {
                case WhalePOIState.Idle:
                case WhalePOIState.Cleared:
                    float idleBob = Mathf.Sin(Time.time * 0.9f) * 0.18f;
                    float idleRoll = Mathf.Sin(Time.time * 0.6f) * 1.5f;
                    transform.position = basePosition + Vector3.up * idleBob;
                    transform.rotation = baseRotation * Quaternion.Euler(0f, 0f, idleRoll);
                    break;

                case WhalePOIState.Agitated:
                    if (timerStarted)
                    {
                        remainingTime -= Time.deltaTime;
                        if (remainingTime <= 0f)
                        {
                            StartDiving();
                            return;
                        }
                    }

                    if (Time.time >= nextThrashTime && thrashTimer <= 0f)
                    {
                        thrashTimer = thrashDuration;
                        nextThrashTime = Time.time + Random.Range(2.8f, 4.5f);
                        float side = Random.value > 0.5f ? 1f : -1f;
                        targetThrashYaw = side * Random.Range(20f, 28f);
                        targetThrashRoll = side * Random.Range(14f, 20f);

                        if (WhaleAnimator != null)
                        {
                            WhaleAnimator.SetTrigger("Bite");
                        }
                        GameAudio.Play(SoundCue.Splash, transform.position);
                    }

                    if (thrashTimer > 0f)
                    {
                        thrashTimer -= Time.deltaTime;
                        float phase = 1f - (thrashTimer / thrashDuration);
                        float wave = Mathf.Sin(phase * Mathf.PI);
                        currentThrashYaw = Mathf.Lerp(0f, targetThrashYaw, wave);
                        currentThrashRoll = Mathf.Lerp(0f, targetThrashRoll, wave);
                    }
                    else
                    {
                        currentThrashYaw = Mathf.MoveTowards(currentThrashYaw, 0f, Time.deltaTime * 30f);
                        currentThrashRoll = Mathf.MoveTowards(currentThrashRoll, 0f, Time.deltaTime * 30f);
                    }

                    float agitatedBob = Mathf.Sin(Time.time * 2.2f) * 0.3f;
                    transform.position = basePosition + Vector3.up * agitatedBob;
                    transform.rotation = baseRotation * Quaternion.Euler(0f, currentThrashYaw, currentThrashRoll);
                    break;

                case WhalePOIState.Diving:
                    diveTimer += Time.deltaTime;
                    float divePitch = Mathf.Lerp(0f, 32f, Mathf.Clamp01(diveTimer / 2f));
                    transform.rotation = Quaternion.Slerp(transform.rotation, baseRotation * Quaternion.Euler(divePitch, 0f, 0f), Time.deltaTime * 2f);
                    transform.position += transform.forward * (-7f * Time.deltaTime) + Vector3.down * (3.8f * Time.deltaTime);

                    if (diveTimer >= 5.5f)
                    {
                        Destroy(gameObject);
                    }
                    break;
            }
        }

        void StartDiving()
        {
            state = WhalePOIState.Diving;
            timerStarted = false;
            diveTimer = 0f;
            if (WhaleAnimator != null)
            {
                WhaleAnimator.SetFloat("Speed", 1.8f);
            }
            GameAudio.Play(SoundCue.WaterSplash, transform.position);
            ShowNotification("ВРЕМЯ ВЫШЛО! КИТ УПЛЫВАЕТ В ГЛУБИНУ!", Color.red, 5f);
        }

        void UpdatePlayerInteraction()
        {
            currentPrompt = "";
            showPullProgress = false;

            if (state == WhalePOIState.Diving) return;

            var cam = Camera.main;
            if (cam == null) cam = Camera.current;
            if (cam == null)
            {
                var p = FindAnyObjectByType<AdvancedPlayerController>();
                if (p != null) cam = p.PlayerCamera;
            }
            if (cam == null) cam = FindAnyObjectByType<Camera>();
            if (cam == null) return;

            var mouse = Mouse.current;
            var kb = Keyboard.current;
            bool lmbHeld = mouse != null ? mouse.leftButton.isPressed : Input.GetMouseButton(0);
            bool ePressed = (kb != null && kb.eKey.wasPressedThisFrame) || Input.GetKeyDown(KeyCode.E);

            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            if (Physics.Raycast(ray, out var hit, 5.5f, ~0, QueryTriggerInteraction.Ignore))
            {
                var pin = hit.collider.GetComponentInParent<WhaleHarpoonPin>();
                if (pin != null && !pin.IsRemoved)
                {
                    currentPrompt = "[УДЕРЖИВАЙТЕ ЛКМ] ВЫТАЩИТЬ ГАРПУН";
                    showPullProgress = true;
                    currentPullProgress = pin.Progress;

                    if (lmbHeld)
                    {
                        pin.RegisterPullThisFrame();
                        currentPullProgress = pin.Progress;
                    }
                    return;
                }

                var chest = hit.collider.GetComponentInParent<WhaleChest>();
                if (chest != null)
                {
                    if (RemainingHarpoons > 0)
                    {
                        currentPrompt = $"СУНДУК ЗАКРЫТ ГАРПУНАМИ! (Осталось: {RemainingHarpoons})";
                        if (ePressed)
                        {
                            var player = cam.GetComponentInParent<AdvancedPlayerController>();
                            chest.Interact(player);
                        }
                    }
                    else if (!chest.IsOpened)
                    {
                        currentPrompt = "[E] ОТКРЫТЬ СУНДУК";
                        if (ePressed)
                        {
                            var player = cam.GetComponentInParent<AdvancedPlayerController>();
                            chest.Interact(player);
                        }
                    }
                    return;
                }
            }
        }

        void OnGUI()
        {
            if (state == WhalePOIState.Diving && diveTimer > 4.5f) return;

            float screenW = Screen.width;
            float screenH = Screen.height;

            var activeCam = Camera.main;
            if (activeCam == null) activeCam = Camera.current;
            if (activeCam != null && state != WhalePOIState.Diving)
            {
                var screenPoint = activeCam.WorldToScreenPoint(transform.position + Vector3.up * 20f);
                if (screenPoint.z > 0 && screenPoint.x >= 0 && screenPoint.x <= screenW && screenPoint.y >= 0 && screenPoint.y <= screenH)
                {
                    var mapLabelStyle = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 22,
                        fontStyle = FontStyle.Bold,
                        normal = { textColor = Color.yellow }
                    };
                    var labelContent = new GUIContent("ТОЧКА ЛУТА: РАНЕНЫЙ КИТ (30 СЕК)");
                    var labelSize = mapLabelStyle.CalcSize(labelContent);
                    var labelRect = new Rect(screenPoint.x - labelSize.x * 0.5f - 10f, screenH - screenPoint.y - 18f, labelSize.x + 20f, 36f);
                    GUI.Box(labelRect, GUIContent.none);
                    GUI.Label(labelRect, labelContent, mapLabelStyle);
                }
            }

            if (state == WhalePOIState.Agitated && timerStarted)
            {
                var timerStyle = new GUIStyle(GUI.skin.box);
                timerStyle.fontSize = 20;
                timerStyle.fontStyle = FontStyle.Bold;
                timerStyle.alignment = TextAnchor.MiddleCenter;

                Color timeColor = remainingTime > 12f ? Color.white : (remainingTime > 6f ? Color.yellow : Color.red);
                timerStyle.normal.textColor = timeColor;

                string timeText = $"⏳ ВРЕМЯ ДО ПОГРУЖЕНИЯ КИТА: {remainingTime:F1} сек | ГАРПУНОВ: {RemainingHarpoons}/{initialHarpoonCount}";
                GUI.Box(new Rect(screenW * 0.5f - 260f, 30f, 520f, 44f), timeText, timerStyle);
            }

            if (!string.IsNullOrEmpty(currentPrompt))
            {
                var promptStyle = new GUIStyle(GUI.skin.box);
                promptStyle.fontSize = 18;
                promptStyle.fontStyle = FontStyle.Bold;
                promptStyle.alignment = TextAnchor.MiddleCenter;
                promptStyle.normal.textColor = Color.white;

                GUI.Box(new Rect(screenW * 0.5f - 220f, screenH * 0.5f + 45f, 440f, 38f), currentPrompt, promptStyle);

                if (showPullProgress && currentPullProgress > 0f)
                {
                    float barW = 260f;
                    float barH = 14f;
                    float barX = screenW * 0.5f - barW * 0.5f;
                    float barY = screenH * 0.5f + 90f;

                    GUI.Box(new Rect(barX, barY, barW, barH), "");
                    var fillTex = Texture2D.whiteTexture;
                    var oldColor = GUI.color;
                    GUI.color = Color.Lerp(Color.yellow, Color.green, currentPullProgress);
                    GUI.DrawTexture(new Rect(barX + 2f, barY + 2f, (barW - 4f) * currentPullProgress, barH - 4f), fillTex);
                    GUI.color = oldColor;
                }
            }

            if (notificationTimer > 0f && !string.IsNullOrEmpty(notificationMessage))
            {
                var notifStyle = new GUIStyle(GUI.skin.box);
                notifStyle.fontSize = 22;
                notifStyle.fontStyle = FontStyle.Bold;
                notifStyle.alignment = TextAnchor.MiddleCenter;
                notifStyle.normal.textColor = notificationColor;

                GUI.Box(new Rect(screenW * 0.5f - 320f, screenH * 0.22f, 640f, 50f), notificationMessage, notifStyle);
            }
        }
    }
}
