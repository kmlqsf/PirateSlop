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
        public NetworkFishing Fishing { get; private set; }
        public bool HandsOccupied => Fishing != null && Fishing.HasFish;
        public bool HasPistol { get; set; } = true;
        public bool HasRod { get; set; } = true;
        readonly int[] fishCounts = new int[6];
        public int FishCount(int slot) => slot >= 0 && slot < 6 ? fishCounts[slot] : 0;
        public void SetFishCount(int slot, int count) => fishCounts[slot] = count;
        public bool FishSelected => FishCount(SelectedSlot) > 0;
        public bool RodSelected => SelectedSlot == 1 && HasRod;
        public bool PistolSelected => SelectedSlot == 0 && HasPistol && !HandsOccupied;
        public bool Placing => HasCannon(SelectedSlot);
        public bool InteractionUsed { get; private set; }
        AdvancedPlayerController motor;
        NetworkWeapon network;
        CannonHands hands;
        GameObject preview;
        Material previewMaterial;
        CannonPickup pickup;
        float rotation, tilt, roll;
        bool cancelled;
        bool valid;
        bool Networked => network != null && (network.IsClientInitialized || network.IsServerInitialized);
        public bool HasCannon(int slot) => slot > 1 && slot < 6 && (CannonSlots & (1 << slot)) != 0;
        public int EmptySlot() { for (int i = 2; i < 6; i++) if (!HasCannon(i) && FishCount(i) == 0) return i; return -1; }
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
            Fishing = GetComponent<NetworkFishing>();
        }

        void Update()
        {
            InteractionUsed = false; pickup = null; valid = false;
            if (preview != null) preview.SetActive(false);
            if (!motor.InputActive || motor.LocomotionLocked || (motor.IsSwimming || motor.IsClimbing)) return;
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || mouse == null) return;
            for (int i = 0; i < 6; i++)
                if (keyboard[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame)
                {
                    SetSelection(i); rotation = tilt = roll = 0;
                    if (Networked) network.SelectSlot(i);
                }
            if (keyboard.gKey.wasPressedThisFrame && Networked && (Fishing == null || !Fishing.CarryingCatch))
            { network.DropSelected(); InteractionUsed = true; return; }
            if (HandsOccupied || (hands != null && hands.HasHeldBall)) return;
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
            float lean = ((keyboard.eKey.isPressed ? 1f : 0f) - (keyboard.qKey.isPressed ? 1f : 0f)) * 60f * Time.deltaTime;
            if (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed) roll += lean;
            else tilt += lean;
            var ship = nearest.collider != null ? nearest.collider.GetComponentInParent<ShipController>() : null;
            var crate = ship != null ? ship.GetComponentInChildren<CannonballCrate>() : null;
            Quaternion orientation = ship != null ? Quaternion.FromToRotation(ship.transform.up, nearest.normal) * ship.transform.rotation * Quaternion.Euler(tilt, rotation, roll) : Quaternion.Euler(tilt, camera.transform.eulerAngles.y + rotation, roll);
            Vector3 position = nearest.collider != null ? nearest.point + nearest.normal * .03f : camera.transform.position + camera.transform.forward * 4f;
            Quaternion localRotation = ship != null ? Quaternion.Inverse(ship.transform.rotation) * orientation : orientation;
            if (crate != null)
                valid = CanPlace(crate, ship.transform.InverseTransformPoint(position), localRotation, motor);
            if (preview == null) CreatePreview();
            preview.SetActive(true);
            preview.transform.SetPositionAndRotation(position, orientation);
            previewMaterial.SetColor("_BaseColor", valid ? new Color(.15f, .9f, .45f, .55f) : new Color(1f, .2f, .15f, .55f));
            if (!valid || !mouse.leftButton.wasPressedThisFrame) return;
            Vector3 localPosition = ship.transform.InverseTransformPoint(position);
            if (Networked) network.PlaceCannon(crate.Network.NetworkObject, SelectedSlot, localPosition, localRotation);
            else { crate.AddCannon(localPosition, localRotation); CannonSlots &= ~(1 << SelectedSlot); }
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

        public static bool CanPlace(CannonballCrate crate, Vector3 localPosition, Quaternion rotation, AdvancedPlayerController player)
        {
            float length = Quaternion.Dot(rotation, rotation);
            if (crate == null || player == null || player.IsDead || player.LocomotionLocked || !float.IsFinite(localPosition.sqrMagnitude) || !float.IsFinite(length) || length < .5f || length > 1.5f) return false;
            var ship = crate.Ship;
            Vector3 position = ship.transform.TransformPoint(localPosition);
            if (Vector3.Distance(player.transform.position, position) > 6f) return false;
            var body = ship.GetComponent<Rigidbody>();
            foreach (var collider in Physics.OverlapSphere(position, .12f, ~0, QueryTriggerInteraction.Ignore))
                if (collider.attachedRigidbody == body) return true;
            return false;
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
                GUI.Box(new Rect(Screen.width * .5f - width * 3 + width * i, Screen.height - 76, width - 4, 62), (i + 1) + "\n" + (i == 0 && HasPistol ? "Пистолет" : i == 1 && HasRod ? "Удочка" : HasCannon(i) ? "Пушка" : FishCount(i) > 0 ? "Рыба ×" + FishCount(i) : ""));
            }
            GUI.color = old;
            GUI.Label(new Rect(Screen.width * .5f - 130, Screen.height - 98, 300, 22), "G — выбросить предмет из выбранного слота");
            string hint = pickup != null ? (EmptySlot() >= 0 ? "E — взять разобранную пушку" : "Инвентарь заполнен") : Placing && !cancelled ? "ЛКМ — поставить · R/колесо — поворот · Q/E — наклон\nShift+Q/E — крен · ПКМ — отменить" : "";
            if (hint.Length > 0) GUI.Box(new Rect(Screen.width * .5f - 290, Screen.height - 165, 580, 44), hint);
        }
    }
}
