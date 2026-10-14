using PirateSlop.Networking;
using PirateSlop.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PirateSlop
{
    [DefaultExecutionOrder(-81)]
    public sealed class BalanceStonePlayer : MonoBehaviour
    {
        NetworkWeapon network;
        AdvancedPlayerController motor;
        BalanceEventTarget hovered;
        bool pickupRequested, releasing;
        int consumedFrame = -1;
        float requestAt = -10, distance = 1f, nextSend, messageUntil;
        string message;
        readonly RaycastHit[] hits = new RaycastHit[48];
        public Vector3 DragPoint { get; private set; }
        public void Notify(string text) { message = text; messageUntil = Time.unscaledTime + 2.5f; pickupRequested = false; }
        public bool ConsumedInput => consumedFrame == Time.frameCount;
        public bool BlocksInventory => network != null && network.CarryingBalanceStone || pickupRequested;
        public Transform HeldVisual => network != null && network.BalanceEvent != null ? network.BalanceEvent.CarriedVisual(network) : null;
        public float GripWeight => HeldVisual != null ? 1f : 0f;
        public Vector3 HandPoint => DragPoint;
        void Awake() { network = GetComponent<NetworkWeapon>(); motor = GetComponent<AdvancedPlayerController>(); }
        void Update()
        {
            hovered = null;
            if (network == null || !network.IsOwner || !network.IsClientInitialized || motor.PlayerCamera == null) return;
            var mouse = Mouse.current; var keys = Keyboard.current;
            bool carrying = network.CarryingBalanceStone;
            if (!carrying) releasing = false;
            if (mouse == null || !motor.InputActive || motor.IsSwimming || motor.IsClimbing || motor.IsDead || motor.IsDowned || motor.LocomotionLocked)
            {
                if (carrying && !releasing) { network.DropBalanceStone(); releasing = true; }
                if (Time.unscaledTime > requestAt + 1.5f) pickupRequested = false;
                return;
            }
            var camera = motor.PlayerCamera.transform;
            var ray = new Ray(camera.position, camera.forward);
            int count = Physics.RaycastNonAlloc(ray, hits, 4f, ~0, QueryTriggerInteraction.Ignore);
            float best = 4f;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.collider.transform.IsChildOf(transform) || hit.distance >= best) continue;
                var candidate = BalanceEventTarget.FromCollider(hit.collider);
                if (carrying && candidate != null && candidate.StoneIndex == network.BalanceStoneIndex && candidate.Owner == network.BalanceEvent) continue;
                best = hit.distance; hovered = candidate;
            }
            if (carrying)
            {
                pickupRequested = false; consumedFrame = Time.frameCount;
                if (releasing) return;
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > .001f) distance = Mathf.Clamp(distance + Mathf.Sign(scroll) * .3f, .7f, 4f);
                DragPoint = CannonHands.HeldPoint(ray, distance, best);
                bool discard = keys != null && keys.gKey.wasPressedThisFrame;
                if (!mouse.leftButton.isPressed || discard)
                { network.ReleaseBalanceStone(DragPoint, !discard); releasing = true; return; }
                if (Time.unscaledTime >= nextSend) { network.MoveBalanceStone(DragPoint); nextSend = Time.unscaledTime + .05f; }
                return;
            }
            if (pickupRequested)
            {
                consumedFrame = Time.frameCount;
                if (Time.unscaledTime > requestAt + 1.5f) Notify("Не удалось поднять камень. Попробуйте снова.");
                return;
            }
            if (hovered == null || hovered.Owner == null || hovered.StoneIndex < 0 || !mouse.leftButton.wasPressedThisFrame) return;
            consumedFrame = Time.frameCount;
            if (!hovered.Owner.CanTake(network, hovered.StoneIndex)) return;
            distance = Mathf.Clamp(best, .6f, 3f);
            DragPoint = CannonHands.HeldPoint(ray, distance, best);
            requestAt = Time.unscaledTime; pickupRequested = true; nextSend = 0;
            network.TakeBalanceStone(hovered.Owner.NetworkObject, hovered.StoneIndex);
        }
        void OnGUI()
        {
            if (network == null || !network.IsOwner || motor.IsDead || RoguelikeUpgradeUI.WindowOpen) return;
            string hint = network.CarryingBalanceStone ? "Удерживайте ЛКМ · колесо — ближе/дальше · отпустите камень над чашей · G — бросить" :
                hovered != null && hovered.Owner != null && hovered.StoneIndex >= 0 ? "Зажмите ЛКМ — взять камень" : null;
            if (hint == null && hovered != null && hovered.Owner != null && hovered.StoneIndex < 0)
                hint = $"Чаша весов: {hovered.Owner.Deposited}/5 · отпустите камень внутрь";
            if (hint == null && motor.PlayerCamera != null)
                foreach (var ladder in ShipLadder.Active)
                    if (ladder.name == "PitEscapeLadder" && ladder.gameObject.scene == gameObject.scene && ladder.CanGrab(transform.position, motor.PlayerCamera.transform.eulerAngles.y, motor.PlayerCamera.transform.eulerAngles.x))
                    { hint = "E — взяться за лестницу · W — выбраться из ямы"; break; }
            if (Time.unscaledTime < messageUntil && !string.IsNullOrEmpty(message)) hint = message;
            if (hint != null) PirateHudStyle.Panel(new Rect(Screen.width * .5f - 350, Screen.height * .72f, 700, 34), hint);
        }
    }
}
