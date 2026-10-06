using PirateSlop.Networking;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace PirateSlop
{
    public sealed partial class RoguelikeUpgradeUI
    {
        RoguelikeUpgradeView view;
        GameObject ownEventSystem;
        bool pulseEnabled;

        void EnsurePresentation()
        {
            if (view != null) return;
            pulseEnabled = PlayerPrefs.GetInt("PirateSlop.UpgradePulse", 1) != 0;
            view = new RoguelikeUpgradeView();
            view.Picked = SubmitChoice;
            view.Rerolled = SubmitReroll;
            view.Hovered = index => { if (!awaiting) focusedCard = index; };
            view.Closed = () => Close(true);
            view.TabChanged = owned =>
            {
                showOwned = owned;
                GameAudio.Play(SoundCue.Select, Vector3.zero, .4f, true);
            };
            view.FilterChanged = filter =>
            {
                rarityFilter = filter;
                RefreshOwned();
            };
            view.PulseToggled = () =>
            {
                pulseEnabled = !pulseEnabled;
                PlayerPrefs.SetInt("PirateSlop.UpgradePulse", pulseEnabled ? 1 : 0);
                PlayerPrefs.Save();
            };
            if (EventSystem.current == null)
            {
                ownEventSystem = new GameObject("UpgradeEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                ownEventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
        }

        void LateUpdate()
        {
            if (player == null || !player.IsOwner || !player.IsClientInitialized)
            {
                view?.Hide();
                return;
            }
            EnsurePresentation();
            view.Show();
            view.Refresh(player.Upgrades, showOwned, rarityFilter, displayedOwned, awaiting, pendingCard);
            view.Tick(open, !SessionController.MenuOpen && !DeveloperMenu.IsOpen, focusedCard,
                Time.unscaledTime, Time.unscaledDeltaTime, pulseEnabled,
                notificationLead, notification, notificationUntil, notificationColor);
        }

        void OnDestroy()
        {
            view?.Dispose();
            if (ownEventSystem != null) Destroy(ownEventSystem);
        }

        static Color RarityColor(string rarity) => RoguelikeUpgradeView.RarityColor(rarity);
    }
}
