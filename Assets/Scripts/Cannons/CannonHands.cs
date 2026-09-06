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
        float nextHeldSync;
        void Awake() => player = GetComponent<AdvancedPlayerController>();
        void OnDisable() => Drop();
        void Drop()
        {
            if (held != null)
            {
                held.Held = false;
                held.GetComponent<Collider>().enabled = true;
                if (held.Network != null && held.Network.IsClientInitialized)
                    held.Network.RequestBall(false, held.transform.position);
                else
                {
                    held.Release();
                    var platform = GetComponent<ShipDeckPassenger>().Ship;
                    held.Body.linearVelocity = platform != null ? platform.GetPointVelocity(held.transform.position) : Vector3.zero;
                    held.Body.angularVelocity = Vector3.zero;
                }
            }
            held = null;
        }
        void LateUpdate()
        {
            aimed = null;
            if (held != null && !held.Held) held = null;
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
                    ball.Held = true; ball.AttachToPlatform(null);
                    ball.GetComponent<Collider>().enabled = false;
                    ball.transform.SetParent(null, true);
                    if (ball.Network != null && ball.Network.IsClientInitialized) ball.Network.RequestBall(true, ball.transform.position);
                }
            }
            if (held != null)
            {
                if (!mouse.leftButton.isPressed) { Drop(); return; }
                distance = Mathf.Clamp(distance + mouse.scroll.ReadValue().y * .002f, .6f, 3f);
                held.transform.position = ray.GetPoint(Mathf.Min(distance, Mathf.Max(.2f, best - .13f)));
                if (held.Network != null && held.Network.IsClientInitialized && Time.unscaledTime >= nextHeldSync)
                { nextHeldSync = Time.unscaledTime + .05f; held.Network.RequestBall(true, held.transform.position); }
                foreach (var cannon in FindObjectsByType<SimpleCannon>(FindObjectsSortMode.None))
                    if (!cannon.IsLoaded && Vector3.Distance(held.transform.position, cannon.Muzzle.position) <= .4f)
                    {
                        var network = cannon.GetComponentInParent<PirateSlop.Networking.NetworkCannon>();
                        if (network != null && network.IsClientInitialized)
                        {
                            if (held.Network != network) continue;
                            held.Held = false; held.GetComponent<Collider>().enabled = true;
                            network.RequestLoad(network.transform.InverseTransformPoint(held.transform.position));
                        }
                        else if (!cannon.TryLoad(held)) continue;
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
