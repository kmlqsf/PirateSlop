using UnityEngine;
using UnityEngine.InputSystem;
using PirateSlop.Networking;

namespace PirateSlop
{
    [DefaultExecutionOrder(10)]
    public sealed class PlayerInventory : MonoBehaviour
    {
        public SimpleCannon CannonPrefab;
        public Material PreviewMaterial;
        public int SelectedSlot { get; private set; }
        public int CannonSlots { get; private set; }
        public bool PistolSelected => SelectedSlot == 0;
        public bool Placing => HasCannon(SelectedSlot);
        public bool InteractionUsed { get; private set; }
        AdvancedPlayerController motor;
        NetworkWeapon network;
        CannonHands hands;
        GameObject preview;
        Material previewMaterial;
        CannonPickup pickup;
        float rotation;
        bool cancelled;
        bool valid;
        bool Networked => network != null && (network.IsClientInitialized || network.IsServerInitialized);
        public bool HasCannon(int slot) => slot > 0 && slot < 6 && (CannonSlots & (1 << slot)) != 0;
        public int EmptySlot() { for (int i = 1; i < 6; i++) if (!HasCannon(i)) return i; return -1; }
        public void SetContents(int mask) => CannonSlots = mask;
        public void SetSelection(int slot) { SelectedSlot = Mathf.Clamp(slot, 0, 5); cancelled = false; }
        public bool AimingAtPickup()
        {
            if (motor == null || motor.PlayerCamera == null) return false;
            var camera = motor.PlayerCamera;
            Collider nearest = null;
            float distance = 5f;
            foreach (var hit in Physics.RaycastAll(camera.transform.position, camera.transform.forward, distance, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) && hit.distance < distance) { nearest = hit.collider; distance = hit.distance; }
            return nearest != null && nearest.GetComponentInParent<CannonPickup>() != null;
        }

        void Awake()
        {
            motor = GetComponent<AdvancedPlayerController>();
            network = GetComponent<NetworkWeapon>();
            hands = GetComponent<CannonHands>();
        }

        void Update()
        {
            InteractionUsed = false; pickup = null; valid = false;
            if (preview != null) preview.SetActive(false);
            if (!motor.InputActive || motor.LocomotionLocked) return;
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || mouse == null) return;
            for (int i = 0; i < 6; i++)
                if (keyboard[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame)
                {
                    SetSelection(i); rotation = 0;
                    if (Networked) network.SelectSlot(i);
                }
            if (hands != null && hands.HasHeldBall) return;
            var camera = motor.PlayerCamera;
            RaycastHit nearest = default;
            float distance = 5f;
            foreach (var hit in Physics.RaycastAll(camera.transform.position, camera.transform.forward, distance, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) && hit.distance < distance) { nearest = hit; distance = hit.distance; }
            if (nearest.collider != null) pickup = nearest.collider.GetComponentInParent<CannonPickup>();
            if (pickup != null && pickup.Crate.KitAvailable && keyboard.eKey.wasPressedThisFrame)
            {
                InteractionUsed = true;
                int slot = EmptySlot();
                if (slot >= 0)
                {
                    if (Networked) network.TakeCannon(pickup.Crate.Network.NetworkObject);
                    else { pickup.Crate.Kit.SetActive(false); CannonSlots |= 1 << slot; }
                }
                return;
            }
            if (!Placing) return;
            if (mouse.rightButton.wasPressedThisFrame) cancelled = true;
            if (cancelled) return;
            rotation += mouse.scroll.ReadValue().y * .125f;
            if (keyboard.rKey.wasPressedThisFrame) rotation += 15f;
            var ship = nearest.collider != null ? nearest.collider.GetComponentInParent<ShipController>() : null;
            var crate = ship != null ? ship.GetComponentInChildren<CannonballCrate>() : null;
            Quaternion orientation = ship != null ? ship.transform.rotation * Quaternion.Euler(0, rotation, 0) : Quaternion.Euler(0, camera.transform.eulerAngles.y + rotation, 0);
            Vector3 position = nearest.collider != null ? nearest.point : camera.transform.position + camera.transform.forward * 4f;
            if (crate != null)
                valid = CanPlace(crate, ship.transform.InverseTransformPoint(position), rotation, motor);
            if (preview == null) CreatePreview();
            preview.SetActive(true);
            preview.transform.SetPositionAndRotation(position, orientation);
            previewMaterial.SetColor("_BaseColor", valid ? new Color(.15f, .9f, .45f, .55f) : new Color(1f, .2f, .15f, .55f));
            if (!valid || !mouse.leftButton.wasPressedThisFrame) return;
            Vector3 localPosition = ship.transform.InverseTransformPoint(position);
            if (Networked) network.PlaceCannon(crate.Network.NetworkObject, SelectedSlot, localPosition, rotation);
            else { crate.AddCannon(localPosition, rotation); CannonSlots &= ~(1 << SelectedSlot); }
        }

        void CreatePreview()
        {
            preview = Instantiate(CannonPrefab.gameObject);
            preview.name = "CannonPlacementPreview";
            foreach (var collider in preview.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var behaviour in preview.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
            previewMaterial = new Material(PreviewMaterial);
            foreach (var renderer in preview.GetComponentsInChildren<Renderer>(true))
            {
                var materials = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < materials.Length; i++) materials[i] = previewMaterial;
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        public static bool CanPlace(CannonballCrate crate, Vector3 localPosition, float yaw, AdvancedPlayerController player)
        {
            if (crate == null || player == null || player.IsDead || player.LocomotionLocked || !float.IsFinite(localPosition.sqrMagnitude) || !float.IsFinite(yaw)) return false;
            var ship = crate.Ship;
            Vector3 position = ship.transform.TransformPoint(localPosition);
            if (Vector3.Distance(player.transform.position, position) > 6f) return false;
            Quaternion orientation = ship.transform.rotation * Quaternion.Euler(0, yaw, 0);
            Vector3 up = ship.transform.up;
            var body = ship.GetComponent<Rigidbody>();
            foreach (var offset in new[] { Vector3.zero, new Vector3(-.65f, 0, -.7f), new Vector3(.65f, 0, -.7f), new Vector3(-.65f, 0, .7f), new Vector3(.65f, 0, .7f) })
            {
                Vector3 point = position + orientation * offset;
                if (!Physics.Raycast(point + up * .15f, -up, out var hit, .3f, ~0, QueryTriggerInteraction.Ignore) || hit.rigidbody != body || Vector3.Dot(hit.normal, up) < .95f || Mathf.Abs(Vector3.Dot(hit.point - point, up)) > .08f) return false;
                if (hit.collider.GetComponentInParent<SimpleCannon>() != null || hit.collider.GetComponentInParent<CannonballCrate>() != null || hit.collider.GetComponentInParent<CannonPickup>() != null) return false;
            }
            foreach (var box in crate.CannonPrefab.GetComponents<BoxCollider>())
            {
                Vector3 size = box.size;
                Vector3 center = box.center;
                float bottom = center.y - size.y * .5f;
                if (bottom < .1f) { size.y -= .1f - bottom; center.y += (.1f - bottom) * .5f; }
                if (Physics.OverlapBox(position + orientation * center, size * .48f, orientation, ~0, QueryTriggerInteraction.Ignore).Length > 0) return false;
            }
            return true;
        }

        void OnDisable() { if (preview != null) preview.SetActive(false); }
        void OnDestroy() { if (preview != null) Destroy(preview); if (previewMaterial != null) Destroy(previewMaterial); }
        void OnGUI()
        {
            if (motor == null || motor.PlayerCamera == null || !motor.PlayerCamera.enabled || SessionController.MenuOpen) return;
            Color old = GUI.color;
            float width = Mathf.Min(66f, (Screen.width - 20f) / 6f);
            for (int i = 0; i < 6; i++)
            {
                GUI.color = i == SelectedSlot ? new Color(1f, .8f, .35f) : Color.white;
                GUI.Box(new Rect(Screen.width * .5f - width * 3 + width * i, Screen.height - 76, width - 4, 62), (i + 1) + "\n" + (i == 0 ? "Пистолет" : HasCannon(i) ? "Пушка" : ""));
            }
            GUI.color = old;
            string hint = pickup != null ? (EmptySlot() >= 0 ? "E — взять разобранную пушку" : "Инвентарь заполнен") : Placing && !cancelled ? "ЛКМ — установить · R / колесо — повернуть · ПКМ — отменить" : "";
            if (hint.Length > 0) GUI.Box(new Rect(Screen.width * .5f - 290, Screen.height - 165, 580, 28), hint);
        }
    }
}
