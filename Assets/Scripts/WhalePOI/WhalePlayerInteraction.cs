using PirateSlop.Networking;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PirateSlop
{
    [DefaultExecutionOrder(-80)]
    public sealed class WhalePlayerInteraction : MonoBehaviour
    {
        public static bool InputCaptured { get; private set; }
        public static bool InputBlocked { get; private set; }
        NetworkWeapon network;
        AdvancedPlayerController motor;
        WhaleHarpoonPin selected, hovered;
        WhaleLootPoint nearbyWhale;
        public WhaleHarpoonPin HeldPin => localOwner ? confirmed && selected != null && !selected.IsRemoved ? selected : null : network != null ? network.WhalePin : null;
        float nextSend, rock, pull, grabStarted, messageUntil;
        uint attempt;
        bool localOwner, waitForRelease, confirmed;
        string blockedReason, message;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetInputState() { InputCaptured = InputBlocked = false; }
        void Awake() { network = GetComponent<NetworkWeapon>(); motor = GetComponent<AdvancedPlayerController>(); }
        void Update()
        {
            if (network == null || !network.IsOwner || !network.IsClientInitialized) return;
            localOwner = true;
            hovered = null; blockedReason = null;
            var support = GetComponent<ShipDeckPassenger>()?.Support;
            nearbyWhale = support != null ? support.GetComponent<WhaleLootPoint>() : null;
            InputCaptured = InputBlocked = selected != null;
            var mouse = Mouse.current; var camera = motor.PlayerCamera;
            if (mouse != null && !mouse.leftButton.isPressed) waitForRelease = false;
            if (mouse == null || camera == null || !motor.InputActive || motor.IsDead) { Release(); InputBlocked = false; return; }
            if (selected == null)
            {
                var ray = new Ray(camera.transform.position, camera.transform.forward);
                float range = WhaleHarpoonPin.ReachDistance + Vector3.Distance(ray.origin, WhaleHarpoonPin.ReachOrigin(network)) + 2f;
                float nearest = float.MaxValue;
                foreach (var whale in WhaleLootPoint.Active)
                {
                    if (whale == null || !whale.IsSpawned || whale.gameObject.scene != gameObject.scene) continue;
                    foreach (var pin in whale.Harpoons)
                    {
                        if (pin == null || pin.IsRemoved || !pin.Aim(ray, range, out float distance) || distance >= nearest) continue;
                        nearest = distance; hovered = pin;
                    }
                }
                InputBlocked = hovered != null;
                if (hovered != null)
                {
                    nearbyWhale = hovered.Owner;
                    blockedReason = hovered.Owner.HarpoonBlockReason(network, hovered.Index);
                    if (mouse.leftButton.isPressed && !waitForRelease)
                    {
                        if (blockedReason != null) { Notify(blockedReason); waitForRelease = true; }
                        else
                        {
                            selected = hovered; confirmed = false; message = null; rock = pull = 0;
                            InputCaptured = InputBlocked = true; grabStarted = Time.unscaledTime; nextSend = grabStarted + .035f;
                            network.WhaleInput(selected.Owner.NetworkObject, selected.Index, true, 0, 0, ++attempt, true);
                        }
                    }
                }
            }
            if (selected == null) return;
            nearbyWhale = selected.Owner;
            if (!mouse.leftButton.isPressed) { Release(); return; }
            if (selected.IsRemoved) { Notify("Гарпун извлечён — освободи остальные связки"); waitForRelease = true; Release(); return; }
            blockedReason = selected.Owner.HarpoonBlockReason(network, selected.Index);
            if (blockedReason != null) { Notify(blockedReason); waitForRelease = true; Release(); return; }
            if (!confirmed && Time.unscaledTime - grabStarted > 2f) { Notify("Нет подтверждения захвата — отпусти ЛКМ и попробуй снова"); waitForRelease = true; Release(); return; }
            var state = selected.Owner.Work(selected.Index);
            if (confirmed)
            {
                motor.SetInteractionCameraPull(state.Strokes < 6 ? state.Rock * .18f : state.Pull * .55f);
                var delta = mouse.delta.ReadValue(); rock += delta.x / 80f; pull -= delta.y / 320f;
            }
            if (Time.unscaledTime < nextSend) return;
            nextSend = Time.unscaledTime + .035f;
            network.WhaleInput(selected.Owner.NetworkObject, selected.Index, true, rock, pull, attempt); rock = pull = 0;
        }
        public void GripResult(FishNet.Object.NetworkObject target, int index, uint request, bool accepted, string reason)
        {
            if (selected == null || selected.Owner.NetworkObject != target || selected.Index != index || request != attempt) return;
            if (accepted) { confirmed = true; return; }
            Notify(string.IsNullOrEmpty(reason) ? "Не удалось схватить гарпун" : reason);
            waitForRelease = true;
            Release(); InputBlocked = hovered != null;
        }
        void Notify(string value) { message = value; messageUntil = Time.unscaledTime + 3f; }
        void Release()
        {
            if (selected != null && selected.Owner != null && network.IsSpawned) network.WhaleInput(selected.Owner.NetworkObject, selected.Index, false, 0, 0, attempt);
            selected = null; confirmed = false; InputCaptured = false; InputBlocked = hovered != null; rock = pull = 0;
            motor?.SetInteractionCameraPull(0);
        }
        void OnDisable() { if (network != null && localOwner) { Release(); InputBlocked = false; } }
        void OnGUI()
        {
            if (network == null || !network.IsOwner || motor.IsDead || RoguelikeUpgradeUI.WindowOpen || DeveloperMenu.IsOpen || PlayerInventory.LootWindowOpen || SessionController.MenuOpen) return;
            var whale = nearbyWhale;
            var pin = selected != null ? selected : hovered;
            if (whale == null && (string.IsNullOrEmpty(message) || Time.unscaledTime >= messageUntil)) return;
            if (whale != null)
            {
                string status = whale.State == WhalePOIState.Diving ? "Кит погружается — прыгай в воду!" : whale.ChestUnlocked ? "Сундук освобождён — его можно забрать" : $"Освободи по одному гарпуну каждой связки: {whale.FreedPairs}/3";
                if (whale.State == WhalePOIState.Agitated) status += $" · До погружения: {whale.RemainingTime:F0} с";
                PirateHudStyle.Panel(new Rect(Screen.width * .5f - 340, 30, 680, 48), status);
            }
            string hint = "Наведи прицел на выступающий стержень гарпуна\nЛКМ — удерживай и двигай мышью влево-вправо\nПосле ослабления тяни мышью вниз";
            if (whale != null && whale.ChestUnlocked) hint = "Все три связки сняты — забери сундук";
            if (pin != null && pin.Owner != null)
            {
                var s = pin.Owner.Work(pin.Index);
                if (selected == null) hint = blockedReason ?? "ЛКМ — удерживай, чтобы схватить гарпун\nМышь ← → — раскачивай до полного ослабления";
                else if (!confirmed) hint = "Захватываем гарпун…";
                else if (s.Strokes < 6) hint = $"Гарпун схвачен · Ослабление: {s.Strokes}/6\nЛКМ + мышь ← → — раскачивай до упора в обе стороны\nОтпусти ЛКМ — отпустить гарпун";
                else
                {
                    hint = $"Гарпун ослаблен · Вытягивание: {s.Pull * 100f:F0}%\nЛКМ + мышь вниз — тяни гарпун на себя\nОтпусти ЛКМ — отпустить гарпун";
                    if (Vector3.Dot((transform.position + Vector3.up - pin.EmbeddedPoint).normalized, pin.Outward) <= .2f) hint = "Гарпун ослаблен\nПодойди к выступающему концу со стороны края кита\nЛКМ + мышь вниз — тяни гарпун от кита";
                }
                PirateHudStyle.Bar(new Rect(Screen.width * .5f - 150, Screen.height * .5f + 72, 300, 10), pin.Progress, PirateHudStyle.Gold);
            }
            if (!string.IsNullOrEmpty(message) && Time.unscaledTime < messageUntil) hint = message + (waitForRelease ? "\nОтпусти ЛКМ и попробуй снова" : "");
            if (motor.InputActive) ContextPrompt.Offer(hint, 100);
            else ContextPrompt.Draw(hint);
        }
    }
}
