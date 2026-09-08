using UnityEngine;
using UnityEngine.InputSystem;
namespace PirateSlop
{
    [RequireComponent(typeof(AdvancedPlayerController))]
    [DefaultExecutionOrder(20)]
    public sealed class CannonHands : MonoBehaviour
    {
        AdvancedPlayerController player;
        PlayerInventory inventory;
        DirectShipControls controls;
        Cannonball held;
        SimpleCannon aimed;
        float distance;
        float nextHeldSync, nextLoadRequest;
        GameObject selectedVisual;
        PirateSlop.Networking.InventoryItem visibleItem = PirateSlop.Networking.InventoryItem.None;
        public bool HasHeldBall => held != null;
        public Cannonball HeldBall => held;
        public bool CanPickUpBall()
        {
            if (player == null || player.PlayerCamera == null) return false;
            RaycastHit nearest = default; float distance = 4f;
            var camera = player.PlayerCamera;
            foreach(var hit in Physics.RaycastAll(camera.transform.position,camera.transform.forward,4f,~0,QueryTriggerInteraction.Ignore))
                if(!hit.transform.IsChildOf(transform) && hit.distance < distance) { nearest = hit; distance = hit.distance; }
            var ball = nearest.collider != null ? nearest.collider.GetComponent<Cannonball>() : null;
            return ball != null && !ball.Loaded;
        }
        void Awake() { if (GetComponent<CannonDismantle>() == null) gameObject.AddComponent<CannonDismantle>(); player = GetComponent<AdvancedPlayerController>(); inventory = GetComponent<PlayerInventory>(); controls = GetComponent<DirectShipControls>(); }
        void OnDisable() { Drop(); if (selectedVisual != null) selectedVisual.SetActive(false); }
        void OnDestroy() { if (selectedVisual != null) Destroy(selectedVisual); }
        void UpdateSelectedVisual()
        {
            bool show = inventory != null && inventory.BallSelected && !inventory.HandsOccupied && held == null &&
                !player.IsDead && !player.IsSwimming && !player.IsClimbing && !player.LocomotionLocked && (controls == null || !controls.IsDragging);
            if (!show) { if (selectedVisual != null) selectedVisual.SetActive(false); return; }
            var item = inventory.BallItem(inventory.SelectedSlot);
            if (selectedVisual == null || visibleItem != item)
            {
                if (selectedVisual != null) Destroy(selectedVisual);
                var network = GetComponent<PirateSlop.Networking.NetworkWeapon>();
                int prefabIndex = (int)PirateSlop.Networking.InventoryItem.Cannonball;
                if (network == null || network.DropPrefabs == null || network.DropPrefabs.Length <= prefabIndex || network.DropPrefabs[prefabIndex] == null) return;
                var template = network.DropPrefabs[prefabIndex].GetComponent<Cannonball>();
                int index = item == PirateSlop.Networking.InventoryItem.Cannonball ? 0 : (int)item - 7;
                if (template == null || template.AmmoModels == null || index >= template.AmmoModels.Length || template.AmmoModels[index] == null) return;
                selectedVisual = Instantiate(template.AmmoModels[index]);
                selectedVisual.name = "SelectedCannonball";
                foreach (var collider in selectedVisual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                visibleItem = item;
            }
            selectedVisual.SetActive(true);
            var camera = player.PlayerCamera;
            if (player.InputActive && !player.IsThirdPerson && camera != null)
                selectedVisual.transform.SetPositionAndRotation(camera.transform.TransformPoint(new Vector3(.22f, -.24f, .65f)), camera.transform.rotation);
            else
                selectedVisual.transform.SetPositionAndRotation(transform.TransformPoint(new Vector3(.22f, 1.05f, .45f)), transform.rotation);
        }
        void Drop()
        {
            if (held != null)
            {
                held.Held = false;
                held.GetComponent<Collider>().enabled = true;
                var loose = held.GetComponent<PirateSlop.Networking.NetworkLooseCannonball>();
                if (loose != null) loose.RequestHold(false, held.transform.position);
                else if (held.Network != null && held.Network.IsClientInitialized)
                    held.Network.RequestBall(false, held.transform.position);
                else
                {
                    held.Release();

                }
            }
            held = null;
        }
        void LateUpdate()
        {
            UpdateSelectedVisual();
            aimed = null;
            if (inventory != null && (inventory.RodSelected || inventory.HandsOccupied)) { Drop(); return; }
            if (held != null && !held.Held) held = null;
            var mouse = Mouse.current;
            if (!player.InputActive || (player.IsSwimming || player.IsClimbing) || player.LocomotionLocked || mouse == null || (controls != null && controls.IsDragging) || (inventory != null && (inventory.Placing || inventory.InteractionUsed))) { Drop(); return; }
            var camera = player.PlayerCamera;
            var ray = new Ray(camera.transform.position, camera.transform.forward);
            // Ignore the local player's body, including when using F1.
            RaycastHit nearest = default;
            float best = 4f;
            foreach (var hit in Physics.RaycastAll(ray, 4f, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) && (held == null || !hit.transform.IsChildOf(held.transform)) && hit.distance < best)
                { nearest = hit; best = hit.distance; }
            if (nearest.collider != null) aimed = nearest.collider.GetComponentInParent<SimpleCannon>();
            if (held == null && inventory != null && inventory.BallSelected && selectedVisual != null && aimed != null &&
                !aimed.IsLoaded && aimed.Network != null && aimed.Network.IsClientInitialized &&
                Time.unscaledTime >= nextLoadRequest && aimed.CanLoadFrom(selectedVisual.transform.position))
            {
                nextLoadRequest = Time.unscaledTime + .25f;
                GetComponent<PirateSlop.Networking.NetworkWeapon>().LoadBall(aimed.Network.NetworkObject, aimed.Index);
            }
            if (held == null && mouse.leftButton.wasPressedThisFrame && nearest.collider != null)
            {
                var ball = nearest.collider.GetComponent<Cannonball>();
                if (ball != null && !ball.Loaded)
                {
                    if (ball.Network != null && ball.Network.IsClientInitialized && ball.Network.Crate != null && ball.Network.Crate.Supply == ball)
                    {
                        GetComponent<PirateSlop.Networking.NetworkWeapon>().StoreBall(ball.Network.NetworkObject);
                        return;
                    }
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
                    var loose = ball.GetComponent<PirateSlop.Networking.NetworkLooseCannonball>();
                    if (loose != null) loose.RequestHold(true, ball.transform.position);
                    else if (ball.Network != null && ball.Network.IsClientInitialized) ball.Network.RequestBall(true, ball.transform.position);
                }
            }
            if (held != null)
            {
                if (!mouse.leftButton.isPressed) { Drop(); return; }
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > .001f) distance = Mathf.Clamp(distance + Mathf.Sign(scroll) * .3f, .7f, 4f);
                held.transform.position = ray.GetPoint(Mathf.Min(distance, Mathf.Max(.2f, best - .26f)));
                if (held.Network != null && held.Network.IsClientInitialized && Time.unscaledTime >= nextHeldSync)
                { nextHeldSync = Time.unscaledTime + .05f; held.Network.RequestBall(true, held.transform.position); }
                var loose = held.GetComponent<PirateSlop.Networking.NetworkLooseCannonball>();
                if (loose != null && Time.unscaledTime >= nextHeldSync)
                { nextHeldSync = Time.unscaledTime + .05f; loose.RequestHold(true, held.transform.position); }
                foreach (var cannon in FindObjectsByType<SimpleCannon>(FindObjectsSortMode.None))
                    if (Time.unscaledTime >= nextLoadRequest && cannon.CanLoadFrom(held.transform.position) && !cannon.IsLoaded)
                    {
                        var network = cannon.GetComponentInParent<PirateSlop.Networking.NetworkCannon>();
                        if (network != null && network.IsClientInitialized)
                        {
                            if (loose != null) { nextLoadRequest = Time.unscaledTime + .25f; loose.Load(network.NetworkObject, cannon.Index); break; }
                            if (held.Network != network) continue;
                            nextLoadRequest = Time.unscaledTime + .25f;
                            network.RequestLoad(cannon.Index, network.transform.InverseTransformPoint(held.transform.position));
                            break;
                        }
                        else if (!cannon.TryLoad(held)) continue;
                        held = null; break;
                    }
            }
            
        }
        void OnGUI()
        {
            if (player == null || !player.InputActive || player.LocomotionLocked || (controls != null && controls.IsDragging) || (inventory != null && inventory.Placing)) return;
            GUI.Label(new Rect(Screen.width / 2f - 4, Screen.height / 2f - 10, 20, 20), "+");
            string text = held != null ? "E — в инвентарь • Поднеси ядро к дулу • Колесо — ближе/дальше • Отпусти ЛКМ — бросить" : aimed != null ? (aimed.IsIgnited ? "Фитиль горит…" : aimed.IsLoaded ? "E — выстрел · удерживать E 7 с — снять" : "Поднеси ядро к дулу · удерживать E 7 с — снять") : "";
            if (text.Length > 0) GUI.Box(new Rect(Screen.width / 2f - 310, Screen.height - 125, 620, 28), text);
        }
    }
}
