using System;
using PirateSlop.Networking;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PirateSlop
{
    public sealed class RoguelikeUpgradeView : IDisposable
    {
        public static readonly Color Ink = new(.075f, .12f, .13f);
        public static readonly Color Paper = new(.94f, .89f, .76f);
        public static readonly Color Gold = new(.86f, .69f, .39f);
        public GameObject Root { get; }
        public Canvas Canvas { get; }
        public Action<int> Picked, Hovered, FilterChanged;
        public Action<bool> TabChanged;
        public Action Closed, PulseToggled, Rerolled;
        readonly Font serif, sans;
        readonly Sprite face, rounded, coin;
        readonly Texture2D roundedTexture, coinTexture;
        readonly Texture2D atlas, cardMask;
        readonly Shader glowShader;
        readonly RectTransform menu, stage, collection, grid, hint, toast;
        readonly GameObject offers, empty;
        readonly Text title, subtitle, points, hintTitle, hintCount, toastTitle, toastName, footer, detail, pulseLabel;
        readonly Button chooseTab, ownedTab, rerollButton;
        readonly Image hintGlow;
        readonly Text[] filterLabels = new Text[5];
        readonly Image[] filterImages = new Image[5];
        readonly CardSlot[] slots = new CardSlot[3];
        readonly ScrollRect ownedScroll;
        readonly float[] focus = new float[3];
        int revision = -1, lastFilter = -1, offerCount, pointCount, ownedCount;
        bool lastOwned, lastWaiting;
        string lastPending;
        bool collectionShown;

        sealed class CardSlot
        {
            public RectTransform Root;
            public Text Name, Category, Effect, Description, Rarity, Suit, ReverseSuit, Action;
            public RawImage Art;
            public Image Ribbon, Glow;
            public Material GlowMaterial;
            public Outline Outline;
            public Button Pick, Surface;
        }

        public RoguelikeUpgradeView()
        {
            serif = Font.CreateDynamicFontFromOSFont(new[] { "Georgia", "Times New Roman" }, 48);
            sans = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Arial" }, 24);
            var texture = Resources.Load<Texture2D>("RoguelikeUI/CardFace");
            cardMask = texture;
            glowShader = Resources.Load<Shader>("RoguelikeUI/RarityGlow");
            atlas = Resources.Load<Texture2D>("RoguelikeUI/CardArt");
            if (texture != null) face = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100);
            roundedTexture = Shape(64, 10);
            coinTexture = Shape(64, 32);
            rounded = Sprite.Create(roundedTexture, new Rect(0, 0, 64, 64), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(12, 12, 12, 12));
            coin = Sprite.Create(coinTexture, new Rect(0, 0, 64, 64), new Vector2(.5f, .5f), 100);
            Root = new GameObject("RoguelikeUpgradeCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Root.layer = 5;
            Root.hideFlags = HideFlags.DontSave;
            Canvas = Root.GetComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.vertexColorAlwaysGammaSpace = true;
            Canvas.sortingOrder = 24000;
            var scaler = Root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440, 900);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var root = (RectTransform)Root.transform;
            menu = Stretch("ChoiceMenu", root);
            var veil = ImageAt("SeaVeil", menu, Vector2.zero, Vector2.zero, new Color(.015f, .038f, .048f, .88f));
            StretchRect(veil.rectTransform);
            veil.raycastTarget = true;
            stage = RectAt("CardTable", menu, Vector2.zero, new Vector2(1320, 840));
            Label("HeadingKicker", stage, new Vector2(0, 393), new Vector2(500, 22), "ДОЛЯ ЭКИПАЖА", 14, Gold);
            title = Label("Heading", stage, new Vector2(0, 353), new Vector2(800, 62), "Выберите улучшение", 42, Paper, true);
            subtitle = Label("Subtitle", stage, new Vector2(0, 312), new Vector2(780, 28), "Одна карта — ваша. Выбирайте с умом.", 19, Paper);
            chooseTab = SmallButton("ChoiceTab", stage, new Vector2(-174, 276), new Vector2(250, 33), "ВЫБРАТЬ КАРТУ", () => TabChanged?.Invoke(false));
            ownedTab = SmallButton("OwnedTab", stage, new Vector2(174, 276), new Vector2(280, 33), "МОЯ КОЛОДА", () => TabChanged?.Invoke(true));
            var close = SmallButton("Close", stage, new Vector2(610, 393), new Vector2(38, 38), "×", () => Closed?.Invoke());
            close.GetComponentInChildren<Text>().fontSize = 26;
            points = Label("PointCount", stage, new Vector2(533, 341), new Vector2(200, 34), "", 18, Gold);
            offers = RectAt("DealtCards", stage, Vector2.zero, Vector2.zero).gameObject;
            for (int i = 0; i < slots.Length; i++) slots[i] = BuildCard(offers.transform, i);
            empty = RectAt("EmptyHand", stage, new Vector2(0, -10), new Vector2(750, 400)).gameObject;
            Label("EmptyTitle", empty.transform, new Vector2(0, 45), new Vector2(740, 80), "Новая добыча — новый выбор", 32, Paper, true);
            Label("EmptyDescription", empty.transform, new Vector2(0, -50), new Vector2(630, 80), "Откройте сундук вместе с экипажем, чтобы получить очко улучшения.", 22, Paper);
            collection = RectAt("OwnedCollection", stage, Vector2.zero, Vector2.zero);
            string[] filters = { "Все", "Обычные", "Редкие", "Эпические", "Легендарные" };
            for (int i = 0; i < filters.Length; i++)
            {
                int index = i;
                var button = SmallButton("RarityFilter" + i, collection, new Vector2((i - 2) * 207, 229), new Vector2(196, 34), filters[i], () => FilterChanged?.Invoke(index));
                filterLabels[i] = button.GetComponentInChildren<Text>();
                filterImages[i] = button.GetComponent<Image>();
            }
            var scrollRoot = RectAt("CollectionScroll", collection, new Vector2(0, -48), new Vector2(1090, 494));
            ownedScroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            ownedScroll.horizontal = false;
            ownedScroll.vertical = true;
            ownedScroll.inertia = false;
            ownedScroll.movementType = ScrollRect.MovementType.Clamped;
            ownedScroll.scrollSensitivity = 45;
            var viewport = Stretch("Viewport", scrollRoot);
            viewport.offsetMax = new Vector2(-18, 0);
            viewport.gameObject.AddComponent<RectMask2D>();
            var hit = viewport.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            grid = RectAt("OwnedCards", viewport, Vector2.zero, new Vector2(0, 360));
            grid.anchorMin = new Vector2(0, 1);
            grid.anchorMax = Vector2.one;
            grid.pivot = new Vector2(.5f, 1);
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(240, 360);
            layout.spacing = new Vector2(24, 22);
            layout.padding = new RectOffset(18, 10, 12, 12);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 4;
            ownedScroll.viewport = viewport;
            ownedScroll.content = grid;
            var track = ImageAt("ScrollTrack", scrollRoot, new Vector2(540, 0), new Vector2(5, 494), new Color(Paper.r, Paper.g, Paper.b, .15f), rounded);
            track.raycastTarget = true;
            var scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            var handle = ImageAt("ScrollHandle", track.transform, Vector2.zero, new Vector2(9, 80), Gold, rounded);
            handle.raycastTarget = true;
            scrollbar.handleRect = handle.rectTransform;
            scrollbar.targetGraphic = handle;
            ownedScroll.verticalScrollbar = scrollbar;
            ownedScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            detail = Label("OwnedCardDetail", collection, new Vector2(0, -337), new Vector2(1100, 65), "Наведите на карту, чтобы прочитать её эффект.", 18, Paper);
            rerollButton = SmallButton("RerollCards", stage, new Vector2(0, -331), new Vector2(315, 34), "R — перебросить карты", () => Rerolled?.Invoke());
            footer = Label("Controls", stage, new Vector2(0, -378), new Vector2(760, 30), "1–3 / ← → — карта     Enter — взять", 18, Paper);
            Label("BattleStatus", stage, new Vector2(0, -409), new Vector2(720, 22), "Бой продолжается, пока вы выбираете", 13, new Color(.73f, .77f, .73f));
            SmallButton("CloseHint", stage, new Vector2(535, -380), new Vector2(200, 30), "V / Esc — закрыть", () => Closed?.Invoke());
            var pulseButton = SmallButton("PulseSetting", stage, new Vector2(-520, -380), new Vector2(230, 30), "Сигнал очков: вкл", () => PulseToggled?.Invoke());
            pulseLabel = pulseButton.GetComponentInChildren<Text>();
            pulseLabel.fontSize = 13;
            hint = RectAt("AvailableUpgrade", root, new Vector2(-30, 168), new Vector2(300, 82));
            hint.anchorMin = hint.anchorMax = new Vector2(1, 0);
            hint.pivot = new Vector2(1, 0);
            hintGlow = ImageAt("PulseGlow", hint, Vector2.zero, new Vector2(322, 98), new Color(Gold.r, Gold.g, Gold.b, .2f), rounded);
            ImageAt("HintInk", hint, Vector2.zero, new Vector2(300, 82), new Color(Ink.r, Ink.g, Ink.b, .96f), rounded);
            ImageAt("Coin", hint, new Vector2(-110, 0), new Vector2(47, 47), Gold, coin);
            Label("VKey", hint, new Vector2(-110, 0), new Vector2(47, 47), "V", 27, Ink, true);
            hintTitle = Label("HintTitle", hint, new Vector2(35, 15), new Vector2(215, 28), "Доступно улучшение", 19, Paper);
            hintCount = Label("HintCount", hint, new Vector2(35, -16), new Vector2(215, 25), "", 16, Gold);
            toast = RectAt("ChosenCardToast", root, new Vector2(0, -109), new Vector2(650, 80));
            toast.anchorMin = toast.anchorMax = new Vector2(.5f, 1);
            ImageAt("ToastInk", toast, Vector2.zero, new Vector2(650, 80), new Color(Ink.r, Ink.g, Ink.b, .97f), rounded);
            toastTitle = Label("ToastTitle", toast, new Vector2(0, 19), new Vector2(630, 25), "КАРТА В ВАШЕЙ КОЛОДЕ", 14, Gold);
            toastName = Label("ToastName", toast, new Vector2(0, -14), new Vector2(620, 36), "", 25, Paper, true);
            menu.gameObject.SetActive(false);
            hint.gameObject.SetActive(false);
            toast.gameObject.SetActive(false);
        }

        CardSlot BuildCard(Transform parent, int index)
        {
            var root = RectAt("PlayingCard" + (index + 1), parent, new Vector2((index - 1) * 380, -35), new Vector2(342, 570));
            ImageAt("PaperShadow", root, new Vector2(7, -11), root.sizeDelta, new Color(0, 0, 0, .5f), face ?? rounded);
            var glow = ImageAt("RarityGlow", root, Vector2.zero, new Vector2(430, 658), Color.white);
            glow.enabled = false;
            Material glowMaterial = null;
            if (glowShader != null && cardMask != null)
            {
                glowMaterial = new Material(glowShader) { hideFlags = HideFlags.HideAndDontSave };
                glowMaterial.SetTexture("_CardMask", cardMask);
                glowMaterial.SetFloat("_Phase", index * 1.71f);
                glow.material = glowMaterial;
            }
            var paper = ImageAt("IvoryStock", root, Vector2.zero, root.sizeDelta, Color.white, face ?? rounded);
            paper.raycastTarget = true;
            var slot = new CardSlot { Root = root, Glow = glow, GlowMaterial = glowMaterial };
            slot.Outline = paper.gameObject.AddComponent<Outline>();
            slot.Outline.effectDistance = new Vector2(2, -2);
            slot.Outline.effectColor = new Color(Gold.r, Gold.g, Gold.b, 0);
            slot.Surface = paper.gameObject.AddComponent<Button>();
            slot.Surface.transition = Selectable.Transition.None;
            slot.Surface.navigation = new Navigation { mode = Navigation.Mode.None };
            slot.Surface.onClick.AddListener(() => Picked?.Invoke(index));
            var pointer = root.gameObject.AddComponent<UpgradeCardPointer>();
            pointer.Entered = () => Hovered?.Invoke(index);
            slot.Suit = TopLabel("Suit", root, new Vector2(35, -35), new Vector2(35, 28), "♣", 26, Ink, true);
            var reverse = RectAt("ReversePips", root, new Vector2(136, -250), new Vector2(35, 28));
            reverse.localRotation = Quaternion.Euler(0, 0, 180);
            slot.ReverseSuit = Label("ReverseSuit", reverse, Vector2.zero, new Vector2(35, 28), "♣", 26, Ink, true);
            slot.Ribbon = ImageAt("RarityRibbon", root, new Vector2(0, 238), new Vector2(203, 27), Ink, rounded);
            slot.Rarity = Label("Rarity", slot.Ribbon.transform, Vector2.zero, new Vector2(194, 27), "", 12, Paper);
            slot.Art = ArtAt(root, new Vector2(0, 139), new Vector2(236, 155));
            slot.Category = Label("Category", root, new Vector2(0, 47), new Vector2(268, 21), "", 12, Ink);
            slot.Name = Label("Name", root, new Vector2(0, 1), new Vector2(269, 70), "", 26, Ink, true);
            BestFit(slot.Name, 22, 26);
            ImageAt("PrintedRule", root, new Vector2(0, -46), new Vector2(235, 1), new Color(Ink.r, Ink.g, Ink.b, .36f));
            slot.Effect = Label("MainEffect", root, new Vector2(0, -81), new Vector2(267, 52), "", 22, Ink);
            slot.Effect.fontStyle = FontStyle.Bold;
            BestFit(slot.Effect, 20, 22);
            slot.Description = Label("Description", root, new Vector2(0, -166), new Vector2(263, 105), "", 18, Ink);
            slot.Description.lineSpacing = .92f;
            BestFit(slot.Description, 17, 18);
            slot.Pick = SmallButton("TakeCard", root, new Vector2(0, -241), new Vector2(217, 34), "ВЗЯТЬ КАРТУ", () => Picked?.Invoke(index));
            slot.Pick.GetComponent<Image>().color = Ink;
            slot.Action = slot.Pick.GetComponentInChildren<Text>();
            slot.Action.color = Paper;
            slot.Action.fontSize = 14;
            return slot;
        }

        public void Refresh(UpgradeSnapshot snapshot, bool owned, int filter, UpgradeCard[] visibleOwned, bool waiting, string pending)
        {
            pointCount = snapshot.points;
            ownedCount = snapshot.owned.Length;
            if (revision == snapshot.revision && lastOwned == owned && lastFilter == filter && lastWaiting == waiting && lastPending == pending) return;
            revision = snapshot.revision;
            lastOwned = collectionShown = owned;
            lastFilter = filter;
            lastWaiting = waiting;
            lastPending = pending;
            title.text = owned ? "Ваша колода" : "Выберите улучшение";
            subtitle.text = owned ? "Всё, что море подарило вам в этом бою" : "Одна карта — ваша. Выбирайте с умом.";
            points.text = pointCount > 0 ? Points(pointCount) : "";
            rerollButton.gameObject.SetActive(!owned && snapshot.rerolls > 0);
            rerollButton.interactable = !waiting;
            rerollButton.GetComponentInChildren<Text>().text = "R — перебросить карты  ·  " + snapshot.rerolls;
            ownedTab.GetComponentInChildren<Text>().text = "МОЯ КОЛОДА  ·  " + ownedCount;
            chooseTab.GetComponent<Image>().color = !owned ? new Color(Gold.r, Gold.g, Gold.b, .2f) : Color.clear;
            ownedTab.GetComponent<Image>().color = owned ? new Color(Gold.r, Gold.g, Gold.b, .2f) : Color.clear;
            offers.SetActive(!owned && snapshot.offers.Length > 0);
            empty.SetActive(!owned && snapshot.offers.Length == 0 || owned && visibleOwned.Length == 0);
            collection.gameObject.SetActive(owned);
            empty.GetComponentsInChildren<Text>()[0].text = owned ? ownedCount == 0 ? "Ваша первая карта ещё впереди" : "Карт этой редкости пока нет" : pointCount > 0 ? "Море готовит вашу удачу…" : "Новая добыча — новый выбор";
            empty.GetComponentsInChildren<Text>()[1].text = owned ? "Выбранные улучшения пополняют вашу колоду до конца боя." : "Откройте сундук вместе с экипажем, чтобы получить очко улучшения.";
            offerCount = Mathf.Min(3, snapshot.offers.Length);
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                slot.Root.gameObject.SetActive(i < offerCount);
                if (i >= offerCount) continue;
                var card = snapshot.offers[i];
                Color rarity = RarityColor(card.rarity);
                slot.Name.text = card.name;
                slot.Category.text = UpgradeCardPresentation.Category(card.id);
                slot.Effect.text = UpgradeCardPresentation.Highlight(card.id);
                slot.Effect.color = rarity;
                slot.Description.text = UpgradeCardPresentation.Description(card);
                slot.Rarity.text = UpgradeCardPresentation.RarityName(card.rarity);
                slot.Ribbon.color = rarity;
                slot.Suit.color = slot.ReverseSuit.color = rarity;
                slot.Suit.text = slot.ReverseSuit.text = Suit(card.rarity);
                int glowRank = UpgradeCardPresentation.Rank(card.rarity);
                slot.Glow.enabled = glowRank > 0 && slot.GlowMaterial != null;
                if (slot.GlowMaterial != null)
                {
                    slot.GlowMaterial.SetColor("_GlowColor", RarityGlowColor(card.rarity));
                    slot.GlowMaterial.SetFloat("_Rarity", glowRank);
                }
                SetArt(slot.Art, card.id);
                slot.Pick.interactable = slot.Surface.interactable = !waiting;
                slot.Action.text = waiting ? card.id == pending ? "ВЫБИРАЕМ…" : "" : "ВЗЯТЬ КАРТУ";
            }
            if (!owned) return;
            string[] names = { "Все", "Обычные", "Редкие", "Эпические", "Легендарные" };
            for (int i = 0; i < filterLabels.Length; i++)
            {
                int count = 0;
                foreach (var card in snapshot.owned) if (i == 0 || UpgradeCardPresentation.Rank(card.rarity) == i - 1) count++;
                filterLabels[i].text = names[i] + "  " + count;
                filterImages[i].color = filter == i ? new Color(Gold.r, Gold.g, Gold.b, .24f) : new Color(Ink.r, Ink.g, Ink.b, .38f);
            }
            for (int i = grid.childCount - 1; i >= 0; i--)
            {
                grid.GetChild(i).gameObject.SetActive(false);
                Release(grid.GetChild(i).gameObject);
            }
            foreach (var card in visibleOwned) BuildOwnedCard(card, snapshot.owned);
            grid.sizeDelta = new Vector2(0, Mathf.CeilToInt(visibleOwned.Length / 4f) * 382 + 24);
            ownedScroll.verticalNormalizedPosition = 1;
            detail.text = visibleOwned.Length == 0 ? "" : "Наведите на карту, чтобы прочитать её эффект.";
        }

        void BuildOwnedCard(UpgradeCard card, UpgradeCard[] all)
        {
            var root = RectAt("Owned_" + card.id, grid, Vector2.zero, new Vector2(240, 360));
            var paper = ImageAt("Paper", root, Vector2.zero, root.sizeDelta, Color.white, face ?? rounded);
            paper.raycastTarget = true;
            var pointer = paper.gameObject.AddComponent<UpgradeCardPointer>();
            pointer.Entered = () => detail.text = card.name + " — " + UpgradeCardPresentation.Description(card);
            Color rarity = RarityColor(card.rarity);
            Label("Suit", root, new Vector2(-87, 142), new Vector2(30, 34), Suit(card.rarity), 25, rarity, true);
            Label("Rarity", root, new Vector2(0, 147), new Vector2(160, 21), UpgradeCardPresentation.RarityName(card.rarity), 10, rarity);
            var art = ArtAt(root, new Vector2(0, 76), new Vector2(151, 100));
            SetArt(art, card.id);
            var name = Label("Name", root, new Vector2(0, -13), new Vector2(186, 60), card.name, 21, Ink, true);
            BestFit(name, 18, 21);
            var effect = Label("Effect", root, new Vector2(0, -83), new Vector2(185, 73), UpgradeCardPresentation.Highlight(card.id), 18, rarity);
            effect.fontStyle = FontStyle.Bold;
            BestFit(effect, 16, 18);
            bool upgraded = Array.Exists(all, c => c.supersedes == card.id);
            Label("Status", root, new Vector2(0, -142), new Vector2(178, 22), upgraded ? "ПОВЫШЕНО" : UpgradeCardPresentation.Category(card.id), 10, Ink);
        }

        public void Tick(bool windowOpen, bool hudAllowed, int focused, float time, float deltaTime, bool pulseEnabled, string lead, string message, float until, Color noticeColor)
        {
            menu.gameObject.SetActive(windowOpen);
            hint.gameObject.SetActive(!windowOpen && hudAllowed && (pointCount > 0 || ownedCount > 0));
            pulseLabel.text = pulseEnabled ? "Сигнал очков: вкл" : "Сигнал очков: выкл";
            hintTitle.text = pointCount > 0 ? "Доступно улучшение" : "Моя колода";
            hintCount.text = pointCount > 0 ? Points(pointCount) : ownedCount + " карт  ·  V — открыть";
            float pulse = pulseEnabled && pointCount > 0 ? .5f + .5f * Mathf.Sin(time * Mathf.PI * 1.25f) : 0;
            hintGlow.color = new Color(Gold.r, Gold.g, Gold.b, pointCount > 0 ? .08f + pulse * .25f : 0);
            hintGlow.rectTransform.localScale = Vector3.one * (1 + pulse * .035f);
            for (int i = 0; windowOpen && !collectionShown && i < slots.Length; i++)
            {
                focus[i] = Mathf.MoveTowards(focus[i], i == focused ? 1 : 0, deltaTime * 7);
                slots[i].Root.anchoredPosition = new Vector2((i - (offerCount - 1) * .5f) * 380, -45 + focus[i] * 10);
                slots[i].Root.localRotation = Quaternion.Euler(0, 0, (1 - i) * 3.4f * (1 - focus[i]));
                slots[i].Root.localScale = Vector3.one * (1 + focus[i] * .025f);
                slots[i].Outline.effectColor = new Color(Gold.r, Gold.g, Gold.b, focus[i] * .85f);
                if (slots[i].GlowMaterial != null && slots[i].Glow.enabled)
                {
                    slots[i].GlowMaterial.SetFloat("_Focus", focus[i]);
                    slots[i].GlowMaterial.SetFloat("_EffectTime", time);
                }
            }
            bool notice = time < until && !string.IsNullOrEmpty(message);
            footer.text = notice && windowOpen ? lead + ": " + message : collectionShown ? "Колесо мыши — листать колоду" : "1–3 / ← → — карта     Enter — взять";
            footer.color = notice && windowOpen ? Paper : new Color(.8f, .83f, .76f);
            toast.gameObject.SetActive(notice && !windowOpen && hudAllowed);
            if (!notice) return;
            toastTitle.text = lead == "Получено" ? "КАРТА В ВАШЕЙ КОЛОДЕ" : "ВЫБОР ОБНОВИЛСЯ";
            toastTitle.color = Gold;
            toastName.text = message;
        }

        public void Hide() { Root.SetActive(false); }
        public void Show() { Root.SetActive(true); }

        RawImage ArtAt(Transform parent, Vector2 position, Vector2 size)
        {
            var container = RectAt("Illustration", parent, position, size);
            var imageRect = Stretch("Engraving", container);
            var image = imageRect.gameObject.AddComponent<RawImage>();
            image.texture = atlas;
            image.raycastTarget = false;
            var ratio = imageRect.gameObject.AddComponent<AspectRatioFitter>();
            ratio.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            ratio.aspectRatio = atlas != null ? atlas.width / 4f / (atlas.height / 3f) : 1;
            return image;
        }

        void SetArt(RawImage image, string id)
        {
            int index = ArtIndex(id);
            var region = ArtRegion(index);
            image.uvRect = new Rect(region.x / 1448f, 1 - region.yMax / 1086f, region.width / 1448f, region.height / 1086f);
            image.GetComponent<AspectRatioFitter>().aspectRatio = region.width / region.height;
            image.enabled = atlas != null;
        }

        static Rect ArtRegion(int index) => index switch
        {
            0 => new Rect(27, 6, 335, 360),
            1 => new Rect(418, 14, 264, 352),
            2 => new Rect(730, 41, 354, 301),
            3 => new Rect(1096, 67, 351, 264),
            4 => new Rect(25, 423, 364, 259),
            5 => new Rect(401, 395, 324, 321),
            6 => new Rect(755, 365, 311, 344),
            7 => new Rect(1116, 382, 305, 328),
            8 => new Rect(34, 711, 336, 351),
            9 => new Rect(412, 777, 317, 269),
            10 => new Rect(810, 710, 220, 366),
            _ => new Rect(1148, 723, 225, 345)
        };

        public static int ArtIndex(string id)
        {
            if (id == "ExtraMonkey") return 10;
            if (id == "RumSupply") return 11;
            if (id == "PirateFeast" || id == "HeartyCatch") return 6;
            return UpgradeCardPresentation.Category(id) switch
            {
                "ДВИЖЕНИЕ" => 0, "ЖИВУЧЕСТЬ" => 1, "САБЛЯ" => 2, "ЛИЧНОЕ ОРУЖИЕ" => 3,
                "КАНОНИР" => 4, "ПЛОТНИК" => 5, "РЫБАЛКА" => 6, "ДОБЫЧА" => 7, "ЭКИПАЖ" => 8, "УДАЧА" => 9, _ => 7
            };
        }

        public static Color RarityColor(string rarity) => rarity switch
        {
            "Rare" => new Color(.11f, .34f, .40f), "Epic" => new Color(.36f, .18f, .36f),
            "Legendary" => new Color(.44f, .27f, .055f), _ => new Color(.20f, .25f, .23f)
        };

        static Color RarityGlowColor(string rarity) => rarity switch
        {
            "Rare" => new Color(.08f, .48f, 1f),
            "Epic" => new Color(.60f, .16f, 1f),
            "Legendary" => new Color(1f, .54f, .08f),
            _ => Color.clear
        };

        static string Suit(string rarity) => rarity switch { "Rare" => "♦", "Epic" => "♠", "Legendary" => "♥", _ => "♣" };
        public static string Points(int count)
        {
            int end = count % 100;
            string word = end is >= 11 and <= 14 ? "очков" : count % 10 == 1 ? "очко" : count % 10 is >= 2 and <= 4 ? "очка" : "очков";
            return count + " " + word + " выбора";
        }

        RectTransform RectAt(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = 5;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        RectTransform Stretch(string name, Transform parent)
        {
            var rect = RectAt(name, parent, Vector2.zero, Vector2.zero);
            StretchRect(rect);
            return rect;
        }

        static void StretchRect(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        Image ImageAt(string name, Transform parent, Vector2 position, Vector2 size, Color color, Sprite sprite = null)
        {
            var image = RectAt(name, parent, position, size).gameObject.AddComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            image.type = sprite == rounded ? Image.Type.Sliced : Image.Type.Simple;
            image.raycastTarget = false;
            return image;
        }

        Text Label(string name, Transform parent, Vector2 position, Vector2 size, string value, int fontSize, Color color, bool useSerif = false)
        {
            var text = RectAt(name, parent, position, size).gameObject.AddComponent<Text>();
            text.font = useSerif ? serif : sans;
            text.fontSize = fontSize;
            text.color = color;
            text.text = value;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.supportRichText = false;
            text.raycastTarget = false;
            return text;
        }

        Text TopLabel(string name, RectTransform parent, Vector2 position, Vector2 size, string value, int fontSize, Color color, bool useSerif)
        {
            var text = Label(name, parent, position, size, value, fontSize, color, useSerif);
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0, 1);
            return text;
        }

        Button SmallButton(string name, Transform parent, Vector2 position, Vector2 size, string value, Action clicked)
        {
            var image = ImageAt(name, parent, position, size, new Color(Ink.r, Ink.g, Ink.b, .6f), rounded);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = button.colors;
            colors.highlightedColor = new Color(1.2f, 1.2f, 1.12f);
            colors.pressedColor = new Color(.8f, .8f, .72f);
            colors.disabledColor = new Color(.6f, .6f, .6f, .8f);
            button.colors = colors;
            button.onClick.AddListener(() => clicked());
            Label("Caption", image.transform, Vector2.zero, size - new Vector2(10, 0), value, 16, Paper);
            return button;
        }

        static void BestFit(Text text, int minimum, int maximum)
        {
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = minimum;
            text.resizeTextMaxSize = maximum;
        }

        static Texture2D Shape(int size, float radius)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(Mathf.Abs(x + .5f - size * .5f) - (size * .5f - radius), 0);
                float dy = Mathf.Max(Mathf.Abs(y + .5f - size * .5f) - (size * .5f - radius), 0);
                pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy)));
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        static void Release(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }

        public void Dispose()
        {
            Release(Root);
            Release(serif);
            Release(sans);
            Release(face);
            Release(rounded);
            Release(coin);
            Release(roundedTexture);
            Release(coinTexture);
            foreach (var slot in slots) if (slot != null) Release(slot.GlowMaterial);
        }
    }

    public sealed class UpgradeCardPointer : MonoBehaviour, IPointerEnterHandler
    {
        public Action Entered;
        public void OnPointerEnter(PointerEventData eventData) => Entered?.Invoke();
    }
}
