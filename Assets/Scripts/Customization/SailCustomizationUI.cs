using System.Collections.Generic;
using UnityEngine;

namespace PirateSlop.Customization
{
    public class SailCustomizationUI : MonoBehaviour
    {
        public static SailCustomizationUI Instance { get; private set; }
        public static bool IsOpen { get; private set; }

        enum GizmoMode { None, Move, Scale, Rotate }

        Camera targetCamera;
        SailCustomizer customizer;

        Vector3 originalCamPos;
        Quaternion originalCamRot;
        float originalFov;

        Vector3 shipCenter;
        Vector3 currentTarget;
        Vector3 desiredTarget;
        float yaw = 215f;
        float pitch = 22f;
        float currentDistance = 34f;
        float desiredDistance = 34f;

        int selectedPart = 0;
        int hoveredPart = -1;
        int activeLayer = 0; // 0 = Layer 1, 1 = Layer 2
        bool uniformScale = true;

        string currentPresetName = "Слот 1";
        readonly string[] presetSlots = new string[] { "Слот 1", "Слот 2", "Слот 3", "Слот 4", "Слот 5" };

        // Gizmo interaction state
        GizmoMode activeGizmoDrag = GizmoMode.None;
        Vector2 gizmoDragStartMouse;
        Vector2 gizmoStartOffset;
        Vector2 gizmoStartScale;
        float gizmoStartRotation;
        Rect gizmoCenterHandleRect;
        Rect gizmoScaleHandleRect;
        Rect gizmoRotateHandleRect;

        static readonly Color[] QuickColors = new Color[]
        {
            new Color(1.00f, 1.00f, 1.00f), // Белый
            new Color(0.96f, 0.90f, 0.78f), // Холст
            new Color(0.92f, 0.20f, 0.15f), // Красный
            new Color(0.18f, 0.48f, 0.95f), // Синий
            new Color(0.12f, 0.12f, 0.12f), // Черный
            new Color(0.18f, 0.75f, 0.32f), // Зеленый
            new Color(0.98f, 0.76f, 0.18f), // Золотой
            new Color(0.70f, 0.25f, 0.85f), // Пурпурный
            new Color(0.98f, 0.50f, 0.15f), // Оранжевый
            new Color(0.25f, 0.80f, 0.88f)  // Морской
        };

        Rect panelRect = new Rect(0, 0, 480, 740);
        Vector2 scrollPos;
        GUIStyle headerStyle, sectionStyle, labelStyle, smallStyle, buttonStyle, activeTabStyle, inactiveTabStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticState()
        {
            IsOpen = false;
            Instance = null;
        }

        void Awake()
        {
            if (Instance == null) Instance = this;
        }

        void OnDisable()
        {
            IsOpen = false;
        }

        void OnDestroy()
        {
            IsOpen = false;
            if (Instance == this) Instance = null;
        }

        public static void Open(SailCustomizer targetCustomizer = null, Camera cam = null)
        {
            if (Instance == null)
            {
                var go = new GameObject("SailCustomizationUI");
                Instance = go.AddComponent<SailCustomizationUI>();
            }

            Instance.StartCustomization(targetCustomizer, cam);
        }

        public static void Close()
        {
            if (Instance != null)
            {
                Instance.EndCustomization();
            }
            IsOpen = false;
        }

        void StartCustomization(SailCustomizer targetCustomizer, Camera cam)
        {
            if (targetCustomizer == null)
            {
                targetCustomizer = FindAnyObjectByType<SailCustomizer>();
                if (targetCustomizer == null)
                {
                    var menuShip = GameObject.Find("MenuShip");
                    if (menuShip != null)
                    {
                        targetCustomizer = menuShip.GetComponent<SailCustomizer>() ?? menuShip.AddComponent<SailCustomizer>();
                    }
                }
            }

            customizer = targetCustomizer;
            if (customizer != null)
            {
                customizer.InitializeSails();
                if (SailCustomizationStorage.Load(out var savedData, out var savedTextures))
                {
                    customizer.ApplyCustomization(savedData, savedTextures, false);
                }
            }

            targetCamera = cam != null ? cam : (Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>());
            if (targetCamera != null)
            {
                originalCamPos = targetCamera.transform.position;
                originalCamRot = targetCamera.transform.rotation;
                originalFov = targetCamera.fieldOfView;
            }

            shipCenter = customizer != null ? customizer.transform.position : Vector3.zero;
            desiredTarget = currentTarget = shipCenter + Vector3.up * 8f;
            desiredDistance = currentDistance = 34f;
            yaw = 215f;
            pitch = 22f;

            SelectPart(0);
            IsOpen = true;
        }

        void EndCustomization()
        {
            IsOpen = false;

            if (customizer != null)
            {
                SailCustomizationStorage.Save(customizer.CurrentData, customizer.CurrentTextures);
                customizer.SetHighlight(-1, false);
            }

            if (targetCamera != null)
            {
                targetCamera.transform.position = originalCamPos;
                targetCamera.transform.rotation = originalCamRot;
                targetCamera.fieldOfView = originalFov;
            }

            var mb = FindAnyObjectByType<PirateSlop.MenuBackdrop>();
            if (mb != null)
            {
                mb.ApplyShipCustomization();
                mb.PositionView(0f);
            }
        }

        void Update()
        {
            if (!IsOpen || targetCamera == null) return;

            float panelWidth = Mathf.Min(500, Screen.width * 0.40f);
            panelRect = new Rect(Screen.width - panelWidth - 20, 20, panelWidth, Screen.height - 40);

            Vector2 mousePos = Input.mousePosition;
            Vector2 mouseGuiPos = new Vector2(mousePos.x, Screen.height - mousePos.y);
            bool isOverUI = panelRect.Contains(mouseGuiPos);

            // Handle Gizmo Dragging
            if (activeGizmoDrag != GizmoMode.None)
            {
                if (Input.GetMouseButtonUp(0))
                {
                    activeGizmoDrag = GizmoMode.None;
                }
                else if (customizer != null && customizer.CurrentData != null)
                {
                    var sail = customizer.CurrentData.sails[selectedPart];
                    Vector2 delta = mouseGuiPos - gizmoDragStartMouse;

                    if (activeGizmoDrag == GizmoMode.Move)
                    {
                        float sensX = 0.0035f;
                        float sensY = -0.0035f;
                        Vector2 newOffset = gizmoStartOffset + new Vector2(delta.x * sensX, delta.y * sensY);
                        newOffset.x = Mathf.Clamp(newOffset.x, -1f, 1f);
                        newOffset.y = Mathf.Clamp(newOffset.y, -1f, 1f);
                        sail.SetOffset(activeLayer, newOffset);
                        customizer.UpdateVisuals();
                    }
                    else if (activeGizmoDrag == GizmoMode.Scale)
                    {
                        float scaleFactor = 1f + (delta.x - delta.y) * 0.005f;
                        Vector2 newScale = gizmoStartScale * scaleFactor;
                        newScale.x = Mathf.Clamp(newScale.x, 0.1f, 3.0f);
                        newScale.y = Mathf.Clamp(newScale.y, 0.1f, 3.0f);
                        sail.SetScale(activeLayer, newScale);
                        customizer.UpdateVisuals();
                    }
                    else if (activeGizmoDrag == GizmoMode.Rotate)
                    {
                        float rotDelta = (delta.x + delta.y) * 0.5f;
                        float newRot = Mathf.Repeat(gizmoStartRotation + rotDelta + 180f, 360f) - 180f;
                        sail.SetRotation(activeLayer, newRot);
                        customizer.UpdateVisuals();
                    }
                }
            }
            else if (!isOverUI)
            {
                // Check if starting click on gizmo handles
                if (Input.GetMouseButtonDown(0))
                {
                    var sail = (customizer != null && customizer.CurrentData != null) ? customizer.CurrentData.sails[selectedPart] : null;
                    if (sail != null && sail.GetHasDecal(activeLayer))
                    {
                        if (gizmoCenterHandleRect.Contains(mouseGuiPos))
                        {
                            activeGizmoDrag = GizmoMode.Move;
                            gizmoDragStartMouse = mouseGuiPos;
                            gizmoStartOffset = sail.GetOffset(activeLayer);
                            return;
                        }
                        if (gizmoScaleHandleRect.Contains(mouseGuiPos))
                        {
                            activeGizmoDrag = GizmoMode.Scale;
                            gizmoDragStartMouse = mouseGuiPos;
                            gizmoStartScale = sail.GetScale(activeLayer);
                            return;
                        }
                        if (gizmoRotateHandleRect.Contains(mouseGuiPos))
                        {
                            activeGizmoDrag = GizmoMode.Rotate;
                            gizmoDragStartMouse = mouseGuiPos;
                            gizmoStartRotation = sail.GetRotation(activeLayer);
                            return;
                        }
                    }
                }

                // Camera Orbit / Zoom
                if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
                {
                    yaw += Input.GetAxis("Mouse X") * 3.5f;
                    pitch -= Input.GetAxis("Mouse Y") * 2.5f;
                    pitch = Mathf.Clamp(pitch, 5f, 75f);
                }

                float scroll = Input.mouseScrollDelta.y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    desiredDistance = Mathf.Clamp(desiredDistance - scroll * 2.5f, 6f, 55f);
                }

                // 3D Raycasting against Part Colliders
                Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
                int hitPart = -1;

                if (customizer != null)
                {
                    customizer.InitializeSails();
                    var cols = customizer.PartColliders;
                    if (cols != null && cols.Length > 0)
                    {
                        var hits = Physics.RaycastAll(ray, 300f);
                        float closestDist = float.MaxValue;
                        foreach (var hit in hits)
                        {
                            for (int i = 0; i < cols.Length; i++)
                            {
                                if (cols[i] != null && hit.collider == cols[i])
                                {
                                    if (hit.distance < closestDist)
                                    {
                                        closestDist = hit.distance;
                                        hitPart = i;
                                    }
                                }
                            }
                        }
                    }
                }

                if (hitPart != hoveredPart)
                {
                    hoveredPart = hitPart;
                    if (customizer != null) customizer.SetHighlight(hoveredPart, hoveredPart >= 0);
                }

                if (Input.GetMouseButtonDown(0) && hitPart >= 0)
                {
                    SelectPart(hitPart);
                }
            }
            else
            {
                if (hoveredPart >= 0)
                {
                    hoveredPart = -1;
                    if (customizer != null) customizer.SetHighlight(-1, false);
                }
            }

            currentTarget = Vector3.Lerp(currentTarget, desiredTarget, Time.deltaTime * 6f);
            currentDistance = Mathf.Lerp(currentDistance, desiredDistance, Time.deltaTime * 6f);

            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            targetCamera.transform.position = currentTarget + rot * (Vector3.back * currentDistance);
            targetCamera.transform.LookAt(currentTarget);
        }

        void SelectPart(int index)
        {
            selectedPart = Mathf.Clamp(index, 0, SailCustomizer.TotalParts - 1);
            if (customizer != null)
            {
                customizer.InitializeSails();
                customizer.SetSelectedPart(selectedPart);

                if (customizer.PartColliders != null && selectedPart < customizer.PartColliders.Length)
                {
                    var col = customizer.PartColliders[selectedPart];
                    if (col != null)
                    {
                        desiredTarget = col.bounds.center;
                        desiredDistance = selectedPart >= 4 ? 9f : 15f;
                        return;
                    }
                }
            }
            desiredTarget = shipCenter + Vector3.up * 8f;
            desiredDistance = 34f;
        }

        void ResetFocus()
        {
            desiredTarget = shipCenter + Vector3.up * 8f;
            desiredDistance = 34f;
        }

        void PrepareStyles()
        {
            if (headerStyle != null) return;

            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = PirateHudStyle.Paper }
            };

            sectionStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = PirateHudStyle.Gold }
            };

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = new Color(0.90f, 0.90f, 0.88f) }
            };

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            activeTabStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.08f, 0.08f, 0.08f) }
            };

            inactiveTabStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Normal,
                normal = { textColor = PirateHudStyle.Muted }
            };
        }

        void OnGUI()
        {
            if (!IsOpen || customizer == null) return;

            PrepareStyles();

            // 1. Draw 3D In-World Label over Selected Part
            if (customizer.PartColliders != null && selectedPart < customizer.PartColliders.Length)
            {
                var col = customizer.PartColliders[selectedPart];
                if (col != null && targetCamera != null)
                {
                    Vector3 screenPoint = targetCamera.WorldToScreenPoint(col.bounds.center + Vector3.up * 1.2f);
                    if (screenPoint.z > 0)
                    {
                        float labelX = screenPoint.x - 85;
                        float labelY = Screen.height - screenPoint.y - 15;
                        Rect badgeRect = new Rect(labelX, labelY, 170, 26);
                        PirateHudStyle.Fill(badgeRect, new Color(0.05f, 0.05f, 0.05f, 0.88f));
                        PirateHudStyle.Brush(badgeRect, PirateHudStyle.Gold, true);
                        string shortName = selectedPart < SailCustomizer.PartDisplayNames.Length ? SailCustomizer.PartDisplayNames[selectedPart] : $"Часть {selectedPart + 1}";
                        GUI.Label(badgeRect, $"▶ {shortName} ◀", labelStyle);
                    }
                }
            }

            // 2. Draw 3D Interactive Transform Gizmo over active Decal
            DrawDecalGizmo();

            // 3. Draw Side Customization Panel
            PirateHudStyle.Fill(panelRect, new Color(0.07f, 0.08f, 0.10f, 0.96f));
            PirateHudStyle.Brush(new Rect(panelRect.x, panelRect.y, panelRect.width, 2.5f), PirateHudStyle.Gold, true);
            PirateHudStyle.Brush(new Rect(panelRect.x, panelRect.yMax - 2.5f, panelRect.width, 2.5f), PirateHudStyle.Gold, true);
            PirateHudStyle.Brush(new Rect(panelRect.x, panelRect.y, 2.5f, panelRect.height), PirateHudStyle.Gold, true);
            PirateHudStyle.Brush(new Rect(panelRect.xMax - 2.5f, panelRect.y, 2.5f, panelRect.height), PirateHudStyle.Gold, true);

            GUILayout.BeginArea(new Rect(panelRect.x + 14, panelRect.y + 14, panelRect.width - 28, panelRect.height - 28));

            GUILayout.Label("КАСТОМИЗАЦИЯ КОРАБЛЯ", headerStyle);
            PirateHudStyle.Brush(GUILayoutUtility.GetRect(panelRect.width - 28, 2), PirateHudStyle.Gold, true);
            GUILayout.Space(6);

            // Streamer Mode Toggle (Пункт 9)
            GUILayout.BeginHorizontal();
            bool streamerModeNow = SailCustomizer.StreamerMode;
            bool newStreamer = GUILayout.Toggle(streamerModeNow, " Режим стримера (Скрыть логотипы)", GUILayout.Height(22));
            if (newStreamer != streamerModeNow)
            {
                SailCustomizer.StreamerMode = newStreamer;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(6);

            // Part Selection Tabs: Row 1 (Sails)
            GUILayout.Label("Выбор паруса или флага:", smallStyle);
            GUILayout.BeginHorizontal();
            for (int pIdx = 0; pIdx < 4; pIdx++)
            {
                bool isActive = (selectedPart == pIdx);
                string tabName = isActive ? $"▶ Парус {pIdx + 1} ◀" : $"Парус {pIdx + 1}";
                var oldBg = GUI.backgroundColor;
                if (isActive) GUI.backgroundColor = PirateHudStyle.Gold;
                if (GUILayout.Button(tabName, isActive ? activeTabStyle : inactiveTabStyle, GUILayout.Height(28)))
                {
                    SelectPart(pIdx);
                }
                GUI.backgroundColor = oldBg;
            }
            GUILayout.EndHorizontal();

            // Part Selection Tabs: Row 2 (Flags - Пункт 6)
            GUILayout.BeginHorizontal();
            string[] flagLabels = new string[] { "🏴 Роджер (Флаг)", "Вымпел 1", "Вымпел 2" };
            for (int fIdx = 0; fIdx < 3; fIdx++)
            {
                int partIndex = 4 + fIdx;
                bool isActive = (selectedPart == partIndex);
                string tabName = isActive ? $"▶ {flagLabels[fIdx]} ◀" : flagLabels[fIdx];
                var oldBg = GUI.backgroundColor;
                if (isActive) GUI.backgroundColor = PirateHudStyle.Gold;
                if (GUILayout.Button(tabName, isActive ? activeTabStyle : inactiveTabStyle, GUILayout.Height(28)))
                {
                    SelectPart(partIndex);
                }
                GUI.backgroundColor = oldBg;
            }
            GUILayout.EndHorizontal();

            string currentPartTitle = selectedPart < SailCustomizer.PartDisplayNames.Length ? SailCustomizer.PartDisplayNames[selectedPart] : $"Элемент {selectedPart + 1}";
            GUILayout.Label($"Выбран: {currentPartTitle}", labelStyle);
            GUILayout.Space(6);

            scrollPos = GUILayout.BeginScrollView(scrollPos);

            customizer.CurrentData.EnsureCapacity();
            var sail = customizer.CurrentData.sails[selectedPart];

            // 1. Color Picker
            GUILayout.Label("1. БАЗОВЫЙ ЦВЕТ ТКАНИ", sectionStyle);
            GUILayout.BeginHorizontal();
            Rect colorSwatchRect = GUILayoutUtility.GetRect(54, 28, GUILayout.Width(54), GUILayout.Height(28));
            PirateHudStyle.Fill(new Rect(colorSwatchRect.x - 2, colorSwatchRect.y - 2, colorSwatchRect.width + 4, colorSwatchRect.height + 4), PirateHudStyle.Gold);
            PirateHudStyle.Fill(colorSwatchRect, sail.baseColor);

            GUILayout.BeginVertical();
            GUILayout.Label($"R: {sail.baseColor.r:F2}  G: {sail.baseColor.g:F2}  B: {sail.baseColor.b:F2}", labelStyle);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.Space(4);

            // Quick swatches
            GUILayout.BeginHorizontal();
            float swatchW = (panelRect.width - 50) / QuickColors.Length;
            for (int cIdx = 0; cIdx < QuickColors.Length; cIdx++)
            {
                var col = QuickColors[cIdx];
                bool isThis = Mathf.Approximately(sail.baseColor.r, col.r) && Mathf.Approximately(sail.baseColor.g, col.g) && Mathf.Approximately(sail.baseColor.b, col.b);

                Rect sr = GUILayoutUtility.GetRect(swatchW, 24, GUILayout.Width(swatchW), GUILayout.Height(24));
                PirateHudStyle.Fill(sr, isThis ? PirateHudStyle.Gold : new Color(0.35f, 0.35f, 0.35f));
                Rect inner = new Rect(sr.x + 2, sr.y + 2, sr.width - 4, sr.height - 4);
                PirateHudStyle.Fill(inner, col);

                if (Event.current.type == EventType.MouseDown && sr.Contains(Event.current.mousePosition))
                {
                    sail.baseColor = col;
                    customizer.UpdateVisuals();
                    Event.current.Use();
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(6);

            // RGB sliders
            GUILayout.BeginHorizontal();
            GUILayout.Label($"R: {sail.baseColor.r:F2}", GUILayout.Width(50));
            float valR = GUILayout.HorizontalSlider(sail.baseColor.r, 0f, 1f);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label($"G: {sail.baseColor.g:F2}", GUILayout.Width(50));
            float valG = GUILayout.HorizontalSlider(sail.baseColor.g, 0f, 1f);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label($"B: {sail.baseColor.b:F2}", GUILayout.Width(50));
            float valB = GUILayout.HorizontalSlider(sail.baseColor.b, 0f, 1f);
            GUILayout.EndHorizontal();

            if (!Mathf.Approximately(valR, sail.baseColor.r) || !Mathf.Approximately(valG, sail.baseColor.g) || !Mathf.Approximately(valB, sail.baseColor.b))
            {
                sail.baseColor = new Color(valR, valG, valB, 1f);
                customizer.UpdateVisuals();
            }

            GUILayout.Space(12);

            // 2. Weathering and Grime (Пункт 4)
            GUILayout.Label("2. СТАРЕНИЕ И ИЗНОС (WEATHERING & GRIME)", sectionStyle);

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Потёртости ткани: {sail.weathering * 100f:F0}%", smallStyle, GUILayout.Width(170));
            float newWeathering = GUILayout.HorizontalSlider(sail.weathering, 0f, 1f);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Грязь и старение: {sail.grime * 100f:F0}%", smallStyle, GUILayout.Width(170));
            float newGrime = GUILayout.HorizontalSlider(sail.grime, 0f, 1f);
            GUILayout.EndHorizontal();

            if (!Mathf.Approximately(newWeathering, sail.weathering) || !Mathf.Approximately(newGrime, sail.grime))
            {
                sail.weathering = newWeathering;
                sail.grime = newGrime;
                customizer.UpdateVisuals();
            }

            GUILayout.Space(12);

            // 3. Multi-Layer Decals (Пункт 5)
            GUILayout.Label("3. ДЕКАЛИ И ЛОГОТИПЫ (СЛОИ)", sectionStyle);

            GUILayout.BeginHorizontal();
            bool isLayer0 = (activeLayer == 0);
            var prevBg0 = GUI.backgroundColor;
            if (isLayer0) GUI.backgroundColor = PirateHudStyle.Gold;
            string l0Text = sail.hasDecal ? "★ Слой 1 (Основной)" : "Слой 1 (Основной)";
            if (GUILayout.Button(l0Text, isLayer0 ? activeTabStyle : inactiveTabStyle, GUILayout.Height(30)))
            {
                activeLayer = 0;
            }
            GUI.backgroundColor = prevBg0;

            bool isLayer1 = (activeLayer == 1);
            var prevBg1 = GUI.backgroundColor;
            if (isLayer1) GUI.backgroundColor = PirateHudStyle.Gold;
            string l1Text = sail.hasDecal2 ? "★ Слой 2 (Узор)" : "Слой 2 (Узор)";
            if (GUILayout.Button(l1Text, isLayer1 ? activeTabStyle : inactiveTabStyle, GUILayout.Height(30)))
            {
                activeLayer = 1;
            }
            GUI.backgroundColor = prevBg1;
            GUILayout.EndHorizontal();
            GUILayout.Space(6);

            // Layer actions
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Выбрать файл", buttonStyle, GUILayout.Height(32)))
            {
                string path = SailImageLoader.PromptOpenFile();
                if (!string.IsNullOrEmpty(path))
                {
                    var tex = SailImageLoader.LoadImageFromFile(path);
                    if (tex != null)
                    {
                        customizer.CurrentTextures[selectedPart, activeLayer] = tex;
                        sail.SetHasDecal(activeLayer, true);
                        customizer.UpdateVisuals();
                    }
                }
            }

            if (GUILayout.Button("Из буфера", buttonStyle, GUILayout.Height(32)))
            {
                var tex = SailImageLoader.LoadImageFromClipboard();
                if (tex != null)
                {
                    customizer.CurrentTextures[selectedPart, activeLayer] = tex;
                    sail.SetHasDecal(activeLayer, true);
                    customizer.UpdateVisuals();
                }
            }
            GUILayout.EndHorizontal();

            bool currentHasDecal = sail.GetHasDecal(activeLayer);
            Texture2D activeTex = customizer.CurrentTextures[selectedPart, activeLayer];

            if (currentHasDecal && activeTex != null)
            {
                GUILayout.Space(4);
                GUILayout.BeginHorizontal();
                GUILayout.Label($"✓ Загружено: {activeTex.width}x{activeTex.height} px", smallStyle);
                if (GUILayout.Button("Удалить слой", GUILayout.Width(110), GUILayout.Height(24)))
                {
                    sail.SetHasDecal(activeLayer, false);
                    customizer.CurrentTextures[selectedPart, activeLayer] = null;
                    customizer.UpdateVisuals();
                }
                GUILayout.EndHorizontal();

                // Blend Modes
                GUILayout.Space(6);
                GUILayout.Label("Режим наложения:", smallStyle);
                GUILayout.BeginHorizontal();
                string[] blendNames = new string[] { "Обычный", "Без белого", "Без чёрного", "Умножение" };
                int[] blendValues = new int[] { 0, 3, 4, 1 };
                int activeBlend = sail.GetBlendMode(activeLayer);
                for (int mIdx = 0; mIdx < blendNames.Length; mIdx++)
                {
                    bool isBlend = (activeBlend == blendValues[mIdx]);
                    var oldBg = GUI.backgroundColor;
                    if (isBlend) GUI.backgroundColor = PirateHudStyle.Gold;
                    if (GUILayout.Button(blendNames[mIdx], isBlend ? activeTabStyle : inactiveTabStyle, GUILayout.Height(26)))
                    {
                        sail.SetBlendMode(activeLayer, blendValues[mIdx]);
                        customizer.UpdateVisuals();
                    }
                    GUI.backgroundColor = oldBg;
                }
                GUILayout.EndHorizontal();

                // Decal Color Tint
                GUILayout.Space(6);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Тонировка:", smallStyle, GUILayout.Width(80));
                Color[] logoQuick = new Color[] { Color.white, Color.black, new Color(0.98f, 0.76f, 0.18f), new Color(0.92f, 0.20f, 0.15f) };
                string[] logoQuickNames = new string[] { "Белый", "Чёрный", "Золотой", "Красный" };
                for (int lq = 0; lq < logoQuick.Length; lq++)
                {
                    if (GUILayout.Button(logoQuickNames[lq], GUILayout.Height(24)))
                    {
                        sail.SetDecalColor(activeLayer, logoQuick[lq]);
                        customizer.UpdateVisuals();
                    }
                }
                GUILayout.EndHorizontal();

                // Decal Transform
                GUILayout.Space(10);
                GUILayout.Label("Трансформация слоя (или используйте Gizmo на 3D-модели):", smallStyle);
                uniformScale = GUILayout.Toggle(uniformScale, " Синхронный масштаб (X = Y)");

                Vector2 curScale = sail.GetScale(activeLayer);
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Масштаб X: {curScale.x:F2}", smallStyle, GUILayout.Width(110));
                float sx = GUILayout.HorizontalSlider(curScale.x, 0.1f, 3.0f);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label($"Масштаб Y: {curScale.y:F2}", smallStyle, GUILayout.Width(110));
                float sy = GUILayout.HorizontalSlider(curScale.y, 0.1f, 3.0f);
                GUILayout.EndHorizontal();

                if (!Mathf.Approximately(sx, curScale.x) || !Mathf.Approximately(sy, curScale.y))
                {
                    if (uniformScale)
                    {
                        float nv = !Mathf.Approximately(sx, curScale.x) ? sx : sy;
                        sail.SetScale(activeLayer, new Vector2(nv, nv));
                    }
                    else
                    {
                        sail.SetScale(activeLayer, new Vector2(sx, sy));
                    }
                    customizer.UpdateVisuals();
                }

                Vector2 curOffset = sail.GetOffset(activeLayer);
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Смещение X: {curOffset.x:F2}", smallStyle, GUILayout.Width(110));
                float ox = GUILayout.HorizontalSlider(curOffset.x, -1.0f, 1.0f);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label($"Смещение Y: {curOffset.y:F2}", smallStyle, GUILayout.Width(110));
                float oy = GUILayout.HorizontalSlider(curOffset.y, -1.0f, 1.0f);
                GUILayout.EndHorizontal();

                if (!Mathf.Approximately(ox, curOffset.x) || !Mathf.Approximately(oy, curOffset.y))
                {
                    sail.SetOffset(activeLayer, new Vector2(ox, oy));
                    customizer.UpdateVisuals();
                }

                float curRot = sail.GetRotation(activeLayer);
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Вращение: {curRot:F0}°", smallStyle, GUILayout.Width(110));
                float nRot = GUILayout.HorizontalSlider(curRot, -180f, 180f);
                GUILayout.EndHorizontal();

                if (!Mathf.Approximately(nRot, curRot))
                {
                    sail.SetRotation(activeLayer, nRot);
                    customizer.UpdateVisuals();
                }
            }
            else
            {
                GUILayout.Label("В этом слое нет логотипа (загрузите файл или вставьте из буфера)", smallStyle);
            }

            GUILayout.Space(14);

            // 4. Presets System (Пункт 1)
            GUILayout.Label("4. ПРЕСЕТЫ ДИЗАЙНА КОРАБЛЯ", sectionStyle);

            GUILayout.BeginHorizontal();
            for (int prIdx = 0; prIdx < presetSlots.Length; prIdx++)
            {
                string slot = presetSlots[prIdx];
                bool isSelected = (currentPresetName == slot);
                var oldBg = GUI.backgroundColor;
                if (isSelected) GUI.backgroundColor = PirateHudStyle.Gold;
                if (GUILayout.Button(slot, isSelected ? activeTabStyle : inactiveTabStyle, GUILayout.Height(28)))
                {
                    currentPresetName = slot;
                }
                GUI.backgroundColor = oldBg;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(4);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"Сохранить в «{currentPresetName}»", buttonStyle, GUILayout.Height(32)))
            {
                SailCustomizationStorage.SavePreset(currentPresetName, customizer.CurrentData, customizer.CurrentTextures);
            }

            if (GUILayout.Button($"Загрузить «{currentPresetName}»", buttonStyle, GUILayout.Height(32)))
            {
                if (SailCustomizationStorage.LoadPreset(currentPresetName, out var pData, out var pTex))
                {
                    customizer.ApplyCustomization(pData, pTex, true);
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(12);

            // Reset part
            if (GUILayout.Button("Сбросить текущий элемент (По умолчанию)", buttonStyle, GUILayout.Height(30)))
            {
                sail.Reset();
                customizer.CurrentTextures[selectedPart, 0] = null;
                customizer.CurrentTextures[selectedPart, 1] = null;
                customizer.UpdateVisuals();
            }

            GUILayout.EndScrollView();

            GUILayout.Space(8);

            // Bottom controls
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Вид на корабль", buttonStyle, GUILayout.Height(36)))
            {
                ResetFocus();
            }

            var finishBg = GUI.backgroundColor;
            GUI.backgroundColor = PirateHudStyle.Gold;
            if (GUILayout.Button("Готово", buttonStyle, GUILayout.Height(36)))
            {
                Close();
            }
            GUI.backgroundColor = finishBg;
            GUILayout.EndHorizontal();

            GUILayout.EndArea();

            // Bottom-left hint bar
            var hintRect = new Rect(20, Screen.height - 45, 620, 30);
            PirateHudStyle.Fill(hintRect, new Color(0.05f, 0.05f, 0.05f, 0.88f));
            PirateHudStyle.Brush(hintRect, PirateHudStyle.Gold, true);
            GUI.Label(hintRect, " Вращение камеры: зажать ЛКМ / ПКМ · Зум: колесико · Интерактивный Gizmo: тяните за маркеры на парусе", smallStyle);
        }

        void DrawDecalGizmo()
        {
            if (customizer == null || targetCamera == null || customizer.PartColliders == null) return;
            if (selectedPart >= customizer.PartColliders.Length) return;

            var col = customizer.PartColliders[selectedPart];
            if (col == null) return;

            var sail = customizer.CurrentData.sails[selectedPart];
            if (!sail.GetHasDecal(activeLayer)) return;

            Vector3 center3D = col.bounds.center;
            Vector2 offset = sail.GetOffset(activeLayer);
            Vector2 scale = sail.GetScale(activeLayer);
            float rot = sail.GetRotation(activeLayer);

            // Estimate screen position of decal center
            Vector3 worldPos = center3D + col.transform.right * (offset.x * 2.8f) + col.transform.up * (offset.y * 2.8f);
            Vector3 screenCenter3D = targetCamera.WorldToScreenPoint(worldPos);

            if (screenCenter3D.z <= 0) return; // Behind camera

            Vector2 screenCenter = new Vector2(screenCenter3D.x, Screen.height - screenCenter3D.y);

            float gizmoSize = Mathf.Clamp(50f * scale.x * (15f / Mathf.Max(5f, screenCenter3D.z)), 35f, 130f);

            // Bounding box on screen
            Rect boxRect = new Rect(screenCenter.x - gizmoSize, screenCenter.y - gizmoSize, gizmoSize * 2f, gizmoSize * 2f);
            PirateHudStyle.Brush(boxRect, new Color(0.98f, 0.76f, 0.18f, 0.75f), false);

            // Center Move Handle
            gizmoCenterHandleRect = new Rect(screenCenter.x - 14, screenCenter.y - 14, 28, 28);
            PirateHudStyle.Fill(gizmoCenterHandleRect, activeGizmoDrag == GizmoMode.Move ? PirateHudStyle.Gold : new Color(0.1f, 0.1f, 0.1f, 0.85f));
            PirateHudStyle.Brush(gizmoCenterHandleRect, PirateHudStyle.Gold, true);
            GUI.Label(new Rect(screenCenter.x - 10, screenCenter.y - 10, 20, 20), "✥", labelStyle);

            // Scale Handle (Bottom-Right)
            gizmoScaleHandleRect = new Rect(boxRect.xMax - 10, boxRect.yMax - 10, 20, 20);
            PirateHudStyle.Fill(gizmoScaleHandleRect, activeGizmoDrag == GizmoMode.Scale ? PirateHudStyle.Gold : new Color(0.2f, 0.5f, 0.9f, 0.85f));
            PirateHudStyle.Brush(gizmoScaleHandleRect, Color.white, true);
            GUI.Label(new Rect(boxRect.xMax - 8, boxRect.yMax - 8, 16, 16), "⤡", smallStyle);

            // Rotate Handle (Top)
            gizmoRotateHandleRect = new Rect(screenCenter.x - 10, boxRect.yMin - 26, 20, 20);
            PirateHudStyle.Fill(gizmoRotateHandleRect, activeGizmoDrag == GizmoMode.Rotate ? PirateHudStyle.Gold : new Color(0.9f, 0.3f, 0.2f, 0.85f));
            PirateHudStyle.Brush(gizmoRotateHandleRect, Color.white, true);
            PirateHudStyle.Brush(new Rect(screenCenter.x - 1, boxRect.yMin - 16, 2, 16), PirateHudStyle.Gold, false);
            GUI.Label(new Rect(screenCenter.x - 7, boxRect.yMin - 24, 16, 16), "↻", smallStyle);
        }
    }
}
