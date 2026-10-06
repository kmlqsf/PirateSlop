using PirateSlop.Networking;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PirateSlop
{
    [DefaultExecutionOrder(-200)]
    public sealed partial class RoguelikeUpgradeUI : MonoBehaviour
    {
        static RoguelikeUpgradeUI active;
        static int consumedFrame = -1;
        public static bool WindowOpen => active != null && active.open;
        public static bool BlocksInput => WindowOpen || consumedFrame == Time.frameCount;
        NetworkPlayer player;
        AdvancedPlayerController motor;
        bool open, showOwned, awaiting, requested, restoreCursor;
        float sentAt, nextRequest;
        string notification;
        float notificationUntil;
        string pendingCard;
        string notificationLead = "Получено";
        int pendingRevision;
        bool choiceResync, pendingReroll;
        bool receivedInitial;
        int previousPoints, focusedCard, rarityFilter;
        Color notificationColor;
        UpgradeCard[] displayedOwned = System.Array.Empty<UpgradeCard>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { active = null; consumedFrame = -1; }

        void Awake()
        {
            player = GetComponent<NetworkPlayer>();
            motor = GetComponent<AdvancedPlayerController>();
        }

        void OnEnable() { if (player != null) player.UpgradesChanged += Received; }
        void OnDisable()
        {
            if (player != null) player.UpgradesChanged -= Received;
            Close(false);
            view?.Hide();
        }

        void Received()
        {
            bool confirmed = false;
            foreach (var card in player.Upgrades.owned)
            {
                if (card.id != pendingCard) continue;
                confirmed = true;
                notification = card.name;
                notificationLead = "Получено";
                notificationColor = RarityColor(card.rarity);
                notificationUntil = Time.unscaledTime + 3f;
                GameAudio.Play(SoundCue.Select, Vector3.zero, 1f, true);
                break;
            }
            if (confirmed)
            {
                awaiting = false;
                pendingCard = null;
            }
            else if (awaiting && (choiceResync || player.Upgrades.revision != pendingRevision))
            {
                awaiting = false;
                pendingCard = null;
                notificationLead = pendingReroll ? "Новая рука" : "Выбор обновился";
                notification = pendingReroll ? "Карты переброшены" : "Выберите карту ещё раз";
                pendingReroll = false;
                notificationColor = PirateHudStyle.Gold;
                notificationUntil = Time.unscaledTime + 3f;
            }
            if (receivedInitial && player.Upgrades.points > previousPoints)
            {
                if (!open) GameAudio.Play(SoundCue.Select, Vector3.zero, .6f, true);
            }
            receivedInitial = true;
            previousPoints = player.Upgrades.points;
            RefreshOwned();
            if (!confirmed) return;
            focusedCard = 0;
            if (player.Upgrades.points == 0) Close(true);
        }

        void Update()
        {
            if (player == null || !player.IsOwner || !player.IsClientInitialized) { Close(false); return; }
            if (!requested)
            {
                requested = true;
                player.RequestUpgrades();
            }
            if (awaiting && Time.unscaledTime - sentAt > 3f)
            {
                sentAt = Time.unscaledTime;
                choiceResync = true;
                player.RequestUpgrades();
            }
            var keys = Keyboard.current;
            if (keys == null || !Application.isFocused) return;
            if (open)
            {
                if (DeveloperMenu.IsOpen || BotDebugPanel.ConsumedInput) { Close(false); return; }
                if (keys.vKey.wasPressedThisFrame || keys.escapeKey.wasPressedThisFrame) { Close(true); return; }
                if (!showOwned && !awaiting && player.Upgrades.offers.Length > 0)
                {
                    int count = player.Upgrades.offers.Length;
                    if (keys.leftArrowKey.wasPressedThisFrame) focusedCard = (focusedCard + count - 1) % count;
                    if (keys.rightArrowKey.wasPressedThisFrame || keys.tabKey.wasPressedThisFrame) focusedCard = (focusedCard + 1) % count;
                    if (keys.digit1Key.wasPressedThisFrame) focusedCard = 0;
                    if (keys.digit2Key.wasPressedThisFrame && count > 1) focusedCard = 1;
                    if (keys.digit3Key.wasPressedThisFrame && count > 2) focusedCard = 2;
                    if (keys.rKey.wasPressedThisFrame) SubmitReroll();
                    if (keys.enterKey.wasPressedThisFrame || keys.numpadEnterKey.wasPressedThisFrame) SubmitChoice(focusedCard);
                }
                return;
            }
            if (!keys.vKey.wasPressedThisFrame || SessionController.MenuOpen ||
                DeveloperMenu.IsOpen || motor.ShipActivityLocked || motor.ActiveParrot != null || ShipSpyglassView.IsViewing ||
                motor.IsClimbing || motor.BellPullLocked || motor.LocomotionLocked) return;
            active = this;
            open = true;
            consumedFrame = Time.frameCount;
            restoreCursor = PlayerInventory.LootWindowOpen || Cursor.lockState == CursorLockMode.Locked;
            GetComponent<PlayerInventory>()?.CloseLootForUpgrades();
            showOwned = player.Upgrades.points == 0;
            focusedCard = 0;
            GameAudio.Play(SoundCue.Select, Vector3.zero, .6f, true);
            AdvancedPlayerController.SetCursor(false);
            if (Time.unscaledTime >= nextRequest)
            {
                nextRequest = Time.unscaledTime + .25f;
                player.RequestUpgrades();
            }
        }

        void Close(bool restore)
        {
            if (!open) return;
            open = false;
            consumedFrame = Time.frameCount;
            if (active == this) active = null;
            if (restore && restoreCursor) AdvancedPlayerController.SetCursor(true);
        }

        void SubmitReroll()
        {
            if (awaiting || player.Upgrades.rerolls <= 0) return;
            pendingReroll = true; pendingCard = null; pendingRevision = player.Upgrades.revision; choiceResync = false; awaiting = true; sentAt = Time.unscaledTime;
            player.RerollUpgrade(pendingRevision);
        }

        void SubmitChoice(int index)
        {
            var snapshot = player.Upgrades;
            if (awaiting || index < 0 || index >= snapshot.offers.Length) return;
            var card = snapshot.offers[index];
            if (card == null) return;
            focusedCard = index; pendingReroll = false;
            pendingCard = card.id;
            pendingRevision = snapshot.revision;
            choiceResync = false;
            awaiting = true;
            sentAt = Time.unscaledTime;
            player.ChooseUpgrade(card.id, snapshot.revision);
        }

        void RefreshOwned()
        {
            var cards = new System.Collections.Generic.List<UpgradeCard>();
            foreach (var card in player.Upgrades.owned)
                if (rarityFilter == 0 || card.rarity == ((UpgradeRarity)(rarityFilter - 1)).ToString()) cards.Add(card);
            cards.Sort((a, b) =>
            {
                int order = UpgradeCardPresentation.Rank(b.rarity).CompareTo(UpgradeCardPresentation.Rank(a.rarity));
                return order != 0 ? order : string.Compare(a.name, b.name, System.StringComparison.CurrentCulture);
            });
            displayedOwned = cards.ToArray();
        }

    }
}
