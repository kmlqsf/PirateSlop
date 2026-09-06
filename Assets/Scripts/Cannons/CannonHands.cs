using UnityEngine;
using UnityEngine.InputSystem;
namespace PirateSlop
{
    [RequireComponent(typeof(AdvancedPlayerController))]
    [DefaultExecutionOrder(20)]
    public sealed class CannonHands : MonoBehaviour
    {
        AdvancedPlayerController player;
        Cannonball held;
        SimpleCannon aimed;
        float distance;
        void Awake() => player = GetComponent<AdvancedPlayerController>();
        void OnDisable() => Drop();
        void Drop()
        {
            if (held != null)
            {
                var platform = held.GetComponentInParent<Rigidbody>();
                if (platform == null)
                    foreach (var candidate in FindObjectsByType<Rigidbody>(FindObjectsSortMode.None))
                        if (candidate.GetComponent<PirateSlop.Networking.NetworkShip>() != null) { platform = candidate; break; }
                held.AttachToPlatform(platform);
                held.Body.isKinematic = true;
            }
            held = null;
        }
        void LateUpdate()
        {
            aimed = null;
            var mouse = Mouse.current;
            if (!player.InputActive || player.LocomotionLocked || mouse == null) { Drop(); return; }
            var camera = player.PlayerCamera;
            var ray = new Ray(camera.transform.position, camera.transform.forward);
            // Ignore the local player's body, including when using F1.
            RaycastHit nearest = default;
            float best = 4f;
            foreach (var hit in Physics.RaycastAll(ray, 4f, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) && (held == null || hit.collider.gameObject != held.gameObject) && hit.distance < best)
                { nearest = hit; best = hit.distance; }
            if (nearest.collider != null) aimed = nearest.collider.GetComponentInParent<SimpleCannon>();
            if (held == null && mouse.leftButton.wasPressedThisFrame && nearest.collider != null)
            {
                var ball = nearest.collider.GetComponent<Cannonball>();
                if (ball != null && !ball.Loaded)
                {
                    held = ball; distance = Mathf.Clamp(best, .6f, 3f);
                    if (!ball.Body.isKinematic)
                    {
                        ball.Body.linearVelocity = Vector3.zero;
                        ball.Body.angularVelocity = Vector3.zero;
                    }
                    ball.Body.isKinematic = true;
                    ball.transform.SetParent(null, true);
                }
            }
            if (held != null)
            {
                if (!mouse.leftButton.isPressed) { Drop(); return; }
                distance = Mathf.Clamp(distance + mouse.scroll.ReadValue().y * .002f, .6f, 3f);
                held.transform.position = ray.GetPoint(Mathf.Min(distance, Mathf.Max(.2f, best - .13f)));
                foreach (var cannon in FindObjectsByType<SimpleCannon>(FindObjectsSortMode.None))
                    if (cannon.TryLoad(held))
                    {
                        var network = cannon.GetComponentInParent<PirateSlop.Networking.NetworkCannon>();
                        if (network != null) network.RequestLoad(network.transform.InverseTransformPoint(held.transform.position));
                        held = null; break;
                    }
            }
            else if (aimed != null && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) aimed.Fire();
        }
        void OnGUI()
        {
            if (player == null || !player.InputActive || player.LocomotionLocked) return;
            GUI.Label(new Rect(Screen.width / 2f - 4, Screen.height / 2f - 10, 20, 20), "+");
            string text = held != null ? "Поднеси ядро к дулу • Колесо — ближе/дальше • Отпусти ЛКМ — бросить" : aimed != null ? (aimed.IsLoaded ? "E — выстрелить" : "Поднеси ядро к дулу, удерживая ЛКМ") : "";
            if (text.Length > 0) GUI.Box(new Rect(Screen.width / 2f - 310, Screen.height - 125, 620, 28), text);
        }
    }
}
