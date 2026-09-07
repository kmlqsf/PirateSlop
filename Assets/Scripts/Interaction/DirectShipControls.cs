using UnityEngine;
using UnityEngine.InputSystem;
using PirateSlop.Networking;

namespace PirateSlop
{
    [DefaultExecutionOrder(-5)]
    public sealed class DirectShipControls : MonoBehaviour
    {
        AdvancedPlayerController motor;
        PlayerInventory inventory;
        CannonHands hands;
        ShipControlHandle hovered, grabbed;
        SailSystem nearbySails;
        Vector2 pointer;
        float pendingDrag, nextSend;
        public bool IsDragging => grabbed != null;
        public bool BlocksPrimary => grabbed != null || hovered != null;

        void Awake()
        {
            motor = GetComponent<AdvancedPlayerController>();
            inventory = GetComponent<PlayerInventory>();
            hands = GetComponent<CannonHands>();
        }
        void Hover(ShipControlHandle handle)
        {
            if (hovered == handle) return;
            if (hovered != null) hovered.Highlight(false);
            hovered = handle;
            if (hovered != null) hovered.Highlight(true);
        }
        bool InRange(ShipControlHandle handle)
        {
            if (handle == null) return false;
            if (handle.Helm != null) return handle.Helm.InRange(motor);
            return handle.Cannon != null && handle.Cannon.InBreechRange(motor);
        }
        void Update()
        {
            nearbySails = null;
            if (inventory != null && (inventory.HandsOccupied || (inventory.Fishing != null && inventory.Fishing.IsFishing))) { Release(); Hover(null); return; }
            var mouse = Mouse.current;
            if (!motor.InputActive || (motor.IsSwimming || motor.IsClimbing) || mouse == null || (inventory != null && inventory.Placing) || (hands != null && hands.HasHeldBall))
            { Release(); Hover(null); return; }
            if (grabbed != null)
            {
                if (!mouse.leftButton.isPressed || !InRange(grabbed)) { Release(); Hover(null); return; }
                Vector2 delta = mouse.delta.ReadValue();
                if (grabbed.Helm != null)
                {
                    var wheel = grabbed.Helm.Wheel;
                    Vector3 before = WheelDirection(wheel, pointer);
                    pointer += delta;
                    pointer.x = Mathf.Clamp(pointer.x, 0, Screen.width); pointer.y = Mathf.Clamp(pointer.y, 0, Screen.height);
                    Vector3 after = WheelDirection(wheel, pointer);
                    if (before.sqrMagnitude > .0025f && after.sqrMagnitude > .0025f) pendingDrag += Vector3.SignedAngle(before, after, wheel.forward);
                    else pendingDrag += delta.x * .3f;
                }
                else pendingDrag += delta.y * .12f;
                if (Time.unscaledTime >= nextSend) Send(true);
                return;
            }
            var camera = motor.PlayerCamera;
            RaycastHit nearest = default;
            float distance = 3f;
            foreach (var hit in Physics.RaycastAll(camera.transform.position, camera.transform.forward, distance, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) && hit.distance < distance) { nearest = hit; distance = hit.distance; }
            var handle = nearest.collider != null ? nearest.collider.GetComponentInParent<ShipControlHandle>() : null;
            if (handle == null && nearest.collider != null)
            {
                var cannon = nearest.collider.GetComponentInParent<SimpleCannon>();
                if (cannon != null && cannon.Breech != null && cannon.BarrelPivot != null)
                {
                    handle = cannon.Breech.GetComponent<ShipControlHandle>();
                }
            }
            if (handle == null)
            {
                float best = 3f;
                foreach (var hit in Physics.SphereCastAll(camera.transform.position, .18f, camera.transform.forward, 3f, ~0, QueryTriggerInteraction.Ignore))
                {
                    var candidate = hit.collider.GetComponentInParent<ShipControlHandle>();
                    if (candidate == null || candidate.Cannon == null || hit.distance >= best || !InRange(candidate)) continue;
                    if (nearest.collider != null && nearest.collider.GetComponentInParent<SimpleCannon>() != candidate.Cannon && nearest.distance + .18f < hit.distance) continue;
                    handle = candidate; best = hit.distance;
                }
            }
            Hover(InRange(handle) ? handle : null);
            if (hovered != null && mouse.leftButton.wasPressedThisFrame)
            {
                grabbed = hovered; pointer = new Vector2(Screen.width * .5f, Screen.height * .5f); pendingDrag = 0;
                Send(true); return;
            }
            if (hovered != null) return;
            foreach (var sails in FindObjectsByType<SailSystem>(FindObjectsSortMode.None))
                if (sails.InRange(motor)) { nearbySails = sails; break; }
            if (nearbySails == null) return;
            float scroll = mouse.scroll.ReadValue().y;
            float steps = Mathf.Abs(scroll) >= 120f ? scroll / 120f : scroll;
            float amount = Mathf.Clamp(steps * .075f, -.2f, .2f);
            if (Mathf.Abs(amount) < .0001f) return;
            var network = nearbySails.GetComponent<NetworkShip>();
            if (network != null) network.AdjustSails(amount);
            else nearbySails.AdjustSail(amount);
        }
        Vector3 WheelDirection(Transform wheel, Vector2 screenPoint)
        {
            var ray = motor.PlayerCamera.ScreenPointToRay(screenPoint);
            var plane = new Plane(wheel.forward, wheel.position);
            return plane.Raycast(ray, out float distance) ? ray.GetPoint(distance) - wheel.position : Vector3.zero;
        }
        void Send(bool holding)
        {
            if (grabbed == null) return;
            if (grabbed.Helm != null)
            {
                var network = grabbed.Helm.GetComponentInParent<NetworkShip>();
                if (network != null) network.DragWheel(pendingDrag, holding);
                else grabbed.Helm.Drag(motor, pendingDrag, holding);
            }
            else if (grabbed.Cannon != null)
            {
                var cannon = grabbed.Cannon;
                if (cannon.Network != null) cannon.Network.DragBreech(cannon.Index, pendingDrag, holding);
                else cannon.DragBreech(motor, pendingDrag, holding);
            }
            pendingDrag = 0; nextSend = Time.unscaledTime + .05f;
        }
        void Release()
        {
            if (grabbed != null) { Send(false); grabbed.Highlight(false); }
            grabbed = null; pendingDrag = 0;
        }
        void OnDisable() { Release(); Hover(null); }
        void OnGUI()
        {
            if (motor == null || !motor.InputActive) return;
            var handle = grabbed != null ? grabbed : hovered;
            string hint = handle != null ? (handle.Helm != null ? "ЛКМ + движение мышью по кругу — повернуть штурвал" : "ЛКМ + мышь вверх/вниз — наклонить ствол") : "";
            if (handle != null && handle.Cannon != null) hint += "   " + handle.Cannon.Elevation.ToString("F0") + "°";
            if (hint.Length > 0) GUI.Box(new Rect(Screen.width * .5f - 290, Screen.height - 205, 580, 28), hint);
            if (grabbed != null && grabbed.Helm != null) GUI.Box(new Rect(pointer.x - 4, Screen.height - pointer.y - 4, 8, 8), "");
            if (nearbySails == null) return;
            var ship = nearbySails.GetComponent<ShipController>();
            float deploy = nearbySails.DeployPercentage;
            Rect panel = new Rect(Screen.width * .5f - 235, Screen.height - 230, 470, 66);
            GUI.Box(panel, "Колесо мыши — паруса: " + Mathf.RoundToInt(deploy * 100f) + "%\nСкорость: " + ship.Speed.ToString("F1") + " / " + (ship.MaxSpeed * deploy).ToString("F1") + " м/с");
            Color old = GUI.color;
            GUI.color = new Color(.9f, .72f, .28f);
            GUI.DrawTexture(new Rect(panel.x + 12, panel.yMax - 12, (panel.width - 24) * deploy, 5), Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
