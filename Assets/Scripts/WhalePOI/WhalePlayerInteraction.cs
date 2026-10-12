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
            if (mouse == null || camera == null || motor.IsDead) { Release(); InputBlocked = false; return; }
            if (!motor.InputActive && selected != null) Release();
            if (selected == null)
            {
                var ray = new Ray(camera.transform.position, camera.transform.forward);
                var origin = WhaleHarpoonPin.ReachOrigin(network);
                float range = WhaleHarpoonPin.ReachDistance + Vector3.Distance(ray.origin, WhaleHarpoonPin.ReachOrigin(network)) + 2f;
                float nearest = float.MaxValue, nearbyDistance = WhaleHarpoonPin.ReachDistance+2f;
                foreach (var whale in WhaleLootPoint.Active)
                {
                    if (whale == null || !whale.IsSpawned) continue;
                    foreach (var pin in whale.Harpoons)
                    {
                        if (pin == null || pin.IsRemoved) continue;
                        float proximity = pin.ShaftDistance(origin);
                        if (proximity < nearbyDistance) { nearbyDistance = proximity; nearbyWhale = whale; }
                        if (proximity > WhaleHarpoonPin.ReachDistance+2f || !pin.Aim(ray, range, out float distance) || distance >= nearest) continue;
                        nearest = distance; hovered = pin;
                    }
                }
                InputBlocked = motor.InputActive && hovered != null;
                if (hovered != null)
                {
                    nearbyWhale = hovered.Owner;
                    blockedReason = hovered.Owner.HarpoonBlockReason(network, hovered.Index);
                    if (motor.InputActive && mouse.leftButton.isPressed && !waitForRelease)
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
        bool ShowUI => network != null && network.IsOwner && motor != null && !motor.IsDead && !RoguelikeUpgradeUI.WindowOpen && !DeveloperMenu.IsOpen && !PlayerInventory.LootWindowOpen && !SessionController.MenuOpen;
        void LateUpdate()
        {
            if (ShowUI && motor.InputActive && (selected!=null || hovered!=null || nearbyWhale!=null && !nearbyWhale.ChestUnlocked || !string.IsNullOrEmpty(message) && Time.unscaledTime < messageUntil)) ContextPrompt.Offer(Hint(),100);
        }
        void OnGUI()
        {
            if (!ShowUI) return;
            var whale = nearbyWhale;
            var pin = selected != null ? selected : hovered;
            if (whale == null && (string.IsNullOrEmpty(message) || Time.unscaledTime >= messageUntil)) return;
            if (whale != null)
            {
                string status = whale.State == WhalePOIState.Diving ? "Кит погружается — прыгай в воду!" : whale.ChestUnlocked ? "Сундук освобождён — его можно забрать" : $"Освободи по одному гарпуну каждой связки: {whale.FreedPairs}/3";
                if (whale.State == WhalePOIState.Agitated) status += $" · До погружения: {whale.RemainingTime:F0} с";
                PirateHudStyle.Panel(new Rect(Screen.width * .5f - 340, 30, 680, 48), status);
            }
            if (selected!=null && confirmed) DrawGuide();
            if (!motor.InputActive) ContextPrompt.Draw(Hint());
        }
        string Hint()
        {
            var whale=nearbyWhale;
            var pin=selected!=null ? selected : hovered;
            string hint = "Наведи прицел на выступающий стержень гарпуна\nЛКМ — удерживай и двигай мышью влево-вправо\nПосле ослабления тяни мышью вниз";
            if (whale != null && whale.ChestUnlocked) hint = "Сундук свободен\nНаведи прицел на сундук · E — открыть · F — забрать";
            if (pin != null && pin.Owner != null)
            {
                var s = pin.Owner.Work(pin.Index);
                if (selected == null) hint = blockedReason ?? "ЛКМ — удерживай, чтобы схватить гарпун\nМышь ← → — раскачивай до полного ослабления";
                else if (!confirmed) hint = "Захватываем гарпун…";
                else if (s.Strokes < 6) hint = $"Гарпун схвачен · Ослабление: {s.Strokes}/6\nУдерживай ЛКМ · двигай мышь {(s.LastSide>0 ? "ВЛЕВО ←" : "ВПРАВО →")} до подсвеченного края\nОтпусти ЛКМ — отпустить гарпун";
                else
                {
                    hint = $"Гарпун ослаблен · Вытягивание: {s.Pull * 100f:F0}%\nЛКМ + мышь вниз — тяни гарпун на себя\nОтпусти ЛКМ — отпустить гарпун";
                }
            }
            if (!string.IsNullOrEmpty(message) && Time.unscaledTime < messageUntil) hint = message + (waitForRelease ? "\nОтпусти ЛКМ и попробуй снова" : "");
            return hint;
        }
        void DrawGuide()
        {
            var state=selected.Owner.Work(selected.Index);
            var panel=new Rect(Screen.width*.5f-185,Screen.height*.5f+64,370,104);
            PirateHudStyle.Brush(panel,PirateHudStyle.Ink);
            var label=new Rect(panel.x+12,panel.y+8,panel.width-24,27);
            var track=new Rect(panel.x+44,panel.y+49,panel.width-88,12);
            if(state.Strokes<6)
            {
                bool left=state.LastSide>0;
                PirateHudStyle.Label(label,left ? "← ДВИГАЙ МЫШЬ ВЛЕВО" : "ДВИГАЙ МЫШЬ ВПРАВО →",PirateHudStyle.Gold);
                PirateHudStyle.Fill(track,PirateHudStyle.Muted);
                float target=left ? track.x : track.xMax;
                PirateHudStyle.Fill(new Rect(target-8,track.y-7,16,26),PirateHudStyle.Gold);
                float cursor=Mathf.Lerp(track.x,track.xMax,(state.Rock+1)*.5f);
                PirateHudStyle.Fill(new Rect(cursor-4,track.y-5,8,22),PirateHudStyle.Paper);
                PirateHudStyle.Label(new Rect(panel.x+12,panel.y+76,panel.width-24,22),$"Раскачивание {state.Strokes}/6 · дойди до края, затем обратно",PirateHudStyle.Paper);
            }
            else
            {
                PirateHudStyle.Label(label,"↓ ТЯНИ МЫШЬ ВНИЗ, УДЕРЖИВАЯ ЛКМ",PirateHudStyle.Gold);
                PirateHudStyle.Bar(track,state.Pull,PirateHudStyle.Gold);
                PirateHudStyle.Label(new Rect(panel.x+12,panel.y+76,panel.width-24,22),$"Вытягивание {state.Pull*100:F0}%",PirateHudStyle.Paper);
            }
        }
    }
}
