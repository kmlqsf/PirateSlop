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
        float grabStarted, savedUntil;
        SailSystem savedSails;
        int savedRope;
        bool ropeLockOwned;
        int Participant => GetComponent<NetworkPlayer>() != null ? GetComponent<NetworkPlayer>().ParticipantId.Value : -1;
        public bool IsDragging => grabbed != null;
        public HelmInteraction TurningHelm => grabbed != null ? grabbed.Helm : null;
        static readonly RaycastHit[] rayBuffer = new RaycastHit[16];
        static readonly RaycastHit[] sphereBuffer = new RaycastHit[16];
        static readonly RaycastHit[] obstacleBuffer = new RaycastHit[16];
        const float HoverRadius = .14f;
        float focusStarted, focusLostAt = -10f, lowerAmount;
        bool awaitPrimaryRelease;
        public bool BlocksPrimary => grabbed != null || hovered != null || awaitPrimaryRelease;
        public float LowerAmount => Mathf.SmoothStep(0f, 1f, lowerAmount);
        public bool ItemHidden => lowerAmount >= .999f;

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
            if (hovered != null) focusStarted = Time.unscaledTime;
            else focusLostAt = Time.unscaledTime;
            if (hovered != null) hovered.Highlight(true);
        }
        void LateUpdate()
        {
            bool lower = grabbed != null || hovered != null && Time.unscaledTime - focusStarted >= .18f;
            if (hovered == null && Time.unscaledTime - focusLostAt < .16f) lower = lowerAmount > 0f;
            lowerAmount = Mathf.MoveTowards(lowerAmount, lower ? 1f : 0f, Time.unscaledDeltaTime / .24f);
        }
        bool InRange(ShipControlHandle handle)
        {
            if (handle == null) return false;
            if (handle.Sails != null) return handle.Sails.InRange(motor, handle.RopeIndex);
            if (handle.Helm != null) return handle.Helm.InRange(motor);
            if (handle.Capstan != null) return handle.Capstan.InRange(motor);
            return handle.Cannon != null && handle.Cannon.InBreechRange(motor);
        }
        void Update()
        {
            if (Mouse.current == null || !Mouse.current.leftButton.isPressed) awaitPrimaryRelease = false;
            if ((hovered != null || grabbed != null) && Mouse.current != null && Mouse.current.leftButton.isPressed) awaitPrimaryRelease = true;
            if (grabbed == null && ropeLockOwned) Release();
            nearbySails = null;
            if (inventory != null && (inventory.HandsOccupied || (inventory.Fishing != null && inventory.Fishing.IsFishing))) { Release(); Hover(null); return; }
            var mouse = Mouse.current;
            if (!motor.InputActive || motor.OtherLocomotionLocked || motor.IsKnockedBack || (motor.IsSwimming || motor.IsClimbing) || mouse == null || (inventory != null && inventory.Placing) || (hands != null && hands.HasHeldBall))
            { Release(); Hover(null); return; }
            if (grabbed != null)
            {
                var kb = Keyboard.current;
                if (!mouse.leftButton.isPressed || !InRange(grabbed) || kb != null && (kb.qKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)) { Release(); Hover(null); return; }
                if (grabbed.Sails != null)
                {
                    int owner = grabbed.Sails.Owner(grabbed.RopeIndex);
                    if (owner != 0 && owner != Participant || owner == 0 && Time.unscaledTime - grabStarted > 1.5f) { Release(); Hover(null); return; }
                    nearbySails = grabbed.Sails;
                }
                if (grabbed.Capstan != null) { motor.LookAtPoint(grabbed.Capstan.transform.position + Vector3.up * 0.75f); grabbed.Capstan.UpdatePush(motor, grabbed.SpokeIndex); return; }
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
                else if (grabbed.Sails != null) pendingDrag -= delta.y / 420f;
                else pendingDrag += delta.y * .12f;
                if (Time.unscaledTime >= nextSend) Send(true);
                return;
            }
            var camera = motor.PlayerCamera;
            RaycastHit nearest = default;
            float distance = 3f;
            int rayCount = Physics.RaycastNonAlloc(camera.transform.position, camera.transform.forward, rayBuffer, distance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < rayCount; i++)
            {
                var hit = rayBuffer[i];
                if (!hit.transform.IsChildOf(transform) && hit.distance < distance) { nearest = hit; distance = hit.distance; }
            }
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
                int sphereCount = Physics.SphereCastNonAlloc(camera.transform.position, HoverRadius, camera.transform.forward, sphereBuffer, 3f, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < sphereCount; i++)
                {
                    var hit = sphereBuffer[i];
                    var candidate = hit.collider.GetComponentInParent<ShipControlHandle>();
                    if (candidate == null || candidate.Cannon != null || hit.distance >= best || !InRange(candidate)) continue;
                    if (nearest.collider != null && nearest.collider.GetComponentInParent<ShipControlHandle>() != candidate && nearest.distance + HoverRadius < hit.distance) continue;
                    Vector3 target = hit.collider.ClosestPoint(camera.transform.position + camera.transform.forward * (hit.distance + HoverRadius));
                    bool blocked = false;
                    Vector3 delta = target - camera.transform.position;
                    int obsCount = Physics.RaycastNonAlloc(camera.transform.position, delta.normalized, obstacleBuffer, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
                    for (int j = 0; j < obsCount; j++)
                    {
                        var obstacle = obstacleBuffer[j];
                        if (!obstacle.transform.IsChildOf(transform) && obstacle.collider.GetComponentInParent<ShipControlHandle>() != candidate) { blocked = true; break; }
                    }
                    if (blocked) continue;
                    handle = candidate; best = hit.distance;
                }
            }
            Hover(handle != null && handle.Cannon == null && InRange(handle) ? handle : null);
            if (hovered != null && mouse.leftButton.wasPressedThisFrame)
            {
                awaitPrimaryRelease = true;
                if (hovered.Sails != null && hovered.Sails.Owner(hovered.RopeIndex) != 0 && hovered.Sails.Owner(hovered.RopeIndex) != Participant) return;
                grabbed = hovered; pointer = new Vector2(Screen.width * .5f, Screen.height * .5f); pendingDrag = 0;
                grabStarted = Time.unscaledTime;
                if (grabbed.Sails != null) { ropeLockOwned = true; motor.SailPullLocked = true; nearbySails = grabbed.Sails; }
                if (grabbed.Capstan != null)
                {
                    if (!grabbed.Capstan.IsAnchored && grabbed.Capstan.Progress >= 1f)
                    {
                        grabbed = null;
                        return;
                    }
                    grabbed.Capstan.BeginPush(motor, grabbed.SpokeIndex);
                    return;
                }
                Send(true); return;
            }
            if (hovered != null) { nearbySails = hovered.Sails; return; }
            foreach (var sails in SailSystem.Active)
                if (sails.InRange(motor)) { nearbySails = sails; break; }
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
            if (grabbed.Sails != null)
            {
                var network = grabbed.Sails.GetComponent<NetworkShip>();
                if (network != null) network.DragSailRope(grabbed.RopeIndex, pendingDrag, holding);
                else grabbed.Sails.Drag(grabbed.RopeIndex, motor, pendingDrag, holding);
            }
            else if (grabbed.Helm != null)
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
            if (ropeLockOwned && motor != null) motor.SailPullLocked = false;
            ropeLockOwned = false;
            if (grabbed != null && grabbed.Sails != null)
            {
                if (grabbed.Sails.Owner(grabbed.RopeIndex) == Participant)
                { savedSails = grabbed.Sails; savedRope = grabbed.RopeIndex; savedUntil = Time.unscaledTime + 1.2f; }
                motor.SailPullLocked = false;
            }
            if (grabbed != null) { Send(false); grabbed.Highlight(false); }
            grabbed = null; pendingDrag = 0;
        }
        void OnDisable() { Release(); Hover(null); lowerAmount = 0f; awaitPrimaryRelease = false; }
        void OnGUI()
        {
            if (motor == null || !motor.InputActive) return;
            var handle = grabbed != null ? grabbed : hovered;
            string hint = handle != null && handle.Sails == null ? (handle.Helm != null ? "ЛКМ + движение мышью по кругу — повернуть штурвал" : handle.Capstan != null ? (!handle.Capstan.StructurallyAvailable ? "Шпиль сломан" : (handle.Capstan.IsAnchored || handle.Capstan.Progress < 1f ? "ЛКМ + идти по кругу — поднять якорь" : "Якорь поднят · Удерживайте [E] (1.5 сек) — Сбросить")) : "ЛКМ + мышь вверх/вниз — наклонить ствол") : "";
            if (handle != null && handle.Cannon != null) hint += "   " + handle.Cannon.Elevation.ToString("F0") + "°";
            if (handle != null && handle.Helm != null)
            {
                float rudder = handle.Helm.CurrentRudderNormalized;
                hint += Mathf.Abs(rudder) < .008f ? " · РУЛЬ ПРЯМО" : $" · {(rudder < 0f ? "ВЛЕВО" : "ВПРАВО")} {Mathf.Abs(rudder) * 100f:0}%";
            }
            if (grabbed != null && grabbed.Helm != null) PirateHudStyle.Diamond(new Vector2(pointer.x, Screen.height - pointer.y), 8, PirateHudStyle.Gold);
            if (hint.Length > 0 && InRange(handle)) ContextPrompt.Offer(hint, 35);
            var displayed = nearbySails;
            if (displayed == null && handle != null && handle.Helm != null) displayed = handle.Helm.GetComponentInParent<SailSystem>();
            if (displayed == null && Time.unscaledTime < savedUntil) displayed = savedSails;
            if (displayed == null) return;
            DrawSails(displayed, handle != null && handle.Sails == displayed ? handle.RopeIndex : -1);
        }
        void DrawSails(SailSystem sails, int selected)
        {
            float scale = Mathf.Clamp(Screen.height / 1080f, .65f, 1.25f);
            var matrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            float width = 330f, height = 88f + sails.RopeCount * 49f;
            var panel = new Rect(Screen.width / scale - width - 35f, Screen.height / scale * .5f - height * .5f, width, height);
            PirateHudStyle.Panel(panel);
            PirateHudStyle.Label(new Rect(panel.x + 15, panel.y + 8, width - 30, 25), $"ПАРУСА   ·   ТЯГА {sails.EffectiveDeploy * 100f:0}%", PirateHudStyle.Gold);
            for (int i = 0; i < sails.RopeCount; i++)
            {
                float y = panel.y + 41 + i * 49;
                bool occupied = sails.Owner(i) != 0;
                bool own = sails.Owner(i) == Participant;
                float efficiency = sails.Efficiency(i), value = sails.Tension(i);
                string state = efficiency <= 0f ? "УТРАЧЕН" : occupied ? own ? "ТЯНЕТЕ" : "ЗАНЯТ" : value <= .005f ? "ОСЛАБЛЕН" : "ЗАКРЕПЛЁН";
                var color = efficiency < 1f ? new Color(.9f, .46f, .32f) : i == selected ? PirateHudStyle.Gold : PirateHudStyle.Paper;
                if (i == selected) PirateHudStyle.Fill(new Rect(panel.x + 10, y, width - 20, 45), new Color(.4f, .32f, .17f, .25f));
                PirateHudStyle.Label(new Rect(panel.x + 20, y, width - 40, 22), $"{i + 1}. {sails.RopeName(i)}   {value * 100f:0}%", color, false, TextAnchor.MiddleLeft);
                PirateHudStyle.Bar(new Rect(panel.x + 20, y + 26, 138, 8), value, color);
                if (efficiency > 0f && efficiency < 1f) state += " / УРОН";
                PirateHudStyle.Label(new Rect(panel.x + 164, y + 20, width - 180, 23), state, color);
            }
            var ship = sails.GetComponent<ShipController>();
            PirateHudStyle.Label(new Rect(panel.x + 15, panel.yMax - 36, width - 30, 25), ship != null ? $"Ход {ship.Speed:0.0} м/с · каждый парус {100f / Mathf.Max(1, sails.RopeCount):0}%" : "", PirateHudStyle.Muted);
            GUI.matrix = matrix;
            if (selected >= 0)
            {
                bool busy = sails.Owner(selected) != 0 && sails.Owner(selected) != Participant;
                ContextPrompt.Offer(busy ? "Канат занят другим игроком" : grabbed != null ? "Мышь вниз — расправить · Мышь вверх — собрать · Отпустить ЛКМ — закрепить" : "ЛКМ — взять канат · Потянуть вниз — расправить; вверх — собрать", 40);
            }
            else if (sails == savedSails && Time.unscaledTime < savedUntil && sails.Owner(savedRope) == 0)
                ContextPrompt.Offer($"{sails.RopeName(savedRope)}: закреплено {sails.Tension(savedRope) * 100f:0}%", 35);
        }
    }
}
