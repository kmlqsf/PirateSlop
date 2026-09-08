using UnityEngine;
using UnityEngine.InputSystem;
using PirateSlop.Networking;

namespace PirateSlop
{
    [DefaultExecutionOrder(15)]
    public sealed class CannonDismantle : MonoBehaviour
    {
        AdvancedPlayerController motor;
        PlayerInventory inventory;
        CannonHands hands;
        NetworkWeapon network;
        SimpleCannon target;
        float started, nextSend;
        float progress;
        string message;
        void Awake()
        {
            motor = GetComponent<AdvancedPlayerController>();
            inventory = GetComponent<PlayerInventory>();
            hands = GetComponent<CannonHands>();
            network = GetComponent<NetworkWeapon>();
        }
        public void Report(float value, string text) { progress = value; message = text; }
        void Cancel()
        {
            if (target != null && network != null && network.IsOwner) network.HoldDismantle(null, -1, false);
            target = null; progress = 0f; message = null;
        }
        void OnDisable() => Cancel();
        void Update()
        {
            var key = Keyboard.current;
            if (key == null || network == null || !network.IsOwner || !motor.InputActive || motor.IsSwimming ||
                motor.IsClimbing || motor.LocomotionLocked || hands.HasHeldBall || inventory.HandsOccupied || inventory.InteractionUsed)
            { Cancel(); return; }
            var camera = motor.PlayerCamera;
            RaycastHit nearest = default;
            float range = 5f;
            foreach (var hit in Physics.RaycastAll(camera.transform.position, camera.transform.forward, range, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) && hit.distance < range) { nearest = hit; range = hit.distance; }
            var aimed = nearest.collider != null ? nearest.collider.GetComponentInParent<SimpleCannon>() : null;
            if (target != null && aimed != target) { Cancel(); return; }
            if (key.eKey.wasPressedThisFrame && aimed != null && aimed.Network != null && !aimed.IsIgnited && !aimed.IsLoading)
            { target = aimed; started = Time.unscaledTime; nextSend = 0f; message = null; }
            if (target == null) return;
            if (!key.eKey.isPressed)
            {
                var cannon = target;
                bool tap = Time.unscaledTime - started < .3f;
                Cancel();
                if (tap && cannon.IsLoaded) cannon.Fire(gameObject);
                return;
            }
            if (Time.unscaledTime >= nextSend)
            {
                nextSend = Time.unscaledTime + .15f;
                network.HoldDismantle(target.Network.NetworkObject, target.Index, true);
            }
        }
        void OnGUI()
        {
            if (target == null || !motor.InputActive || Time.unscaledTime - started < .3f) return;
            var rect = new Rect(Screen.width * .5f - 160f, Screen.height * .5f + 65f, 320f, 44f);
            PirateHudStyle.Panel(rect, message ?? "Снятие пушки · удерживайте E 7 секунд");
            Color previous = GUI.color;
            GUI.color = new Color(.95f, .7f, .2f);
            GUI.DrawTexture(new Rect(rect.x + 5f, rect.yMax - 12f, (rect.width - 10f) * Mathf.Clamp01(progress), 7f), Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
