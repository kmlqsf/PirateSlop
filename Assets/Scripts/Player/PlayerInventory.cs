using UnityEngine;
using UnityEngine.InputSystem;
using PirateSlop.Networking;

namespace PirateSlop
{
    [DefaultExecutionOrder(10)]
    public sealed class PlayerInventory : MonoBehaviour
    {
        public InventoryIcons Icons;
        public int SabreSlots { get; set; } = 1 << 2;
        public bool HasSabre(int slot) => slot >= 2 && slot < 6 && (SabreSlots & (1 << slot)) != 0;
        public bool SabreSelected => HasSabre(SelectedSlot) && !HandsOccupied;
        static PlayerInventory lootOwner;
        public static bool LootWindowOpen => lootOwner != null && lootOwner.lootWindow;
        bool lootWindow;
        NetworkLootChest openChest;
        Cannonball aimedBall;
        SimpleCannon aimedCannon;
        readonly int[] ballCounts = new int[6];
        readonly InventoryItem[] ballItems = new InventoryItem[6];
        public InventoryItem BallItem(int slot) => BallCount(slot) > 0 ? ballItems[slot] : InventoryItem.Cannonball;
        public int BallCount(int slot) => slot >= 0 && slot < 6 ? ballCounts[slot] : 0;
        public void SetBallCount(int slot, int count, InventoryItem item = InventoryItem.Cannonball) { ballCounts[slot] = count; ballItems[slot] = item; }
        public bool BallSelected => BallCount(SelectedSlot) > 0;
        readonly int[] plankCounts = new int[6];
        int malletSlots;
        public int PlankCount(int slot) => slot >= 0 && slot < 6 ? plankCounts[slot] : 0;
        public bool HasMallet(int slot) => slot >= 2 && slot < 6 && (malletSlots & (1 << slot)) != 0;
        public bool MalletSelected => HasMallet(SelectedSlot);
        public int TotalPlanks { get { int total = 0; foreach (int count in plankCounts) total += count; return total; } }
        public void SetRepairItems(int mallets, int slot, int count) { malletSlots = mallets; plankCounts[slot] = count; }
        public InventoryItem ItemAt(int slot) => HasSabre(slot) ? InventoryItem.Sabre : slot == 0 && HasPistol ? InventoryItem.Pistol : slot == 1 && HasRod ? InventoryItem.Rod : HasCannon(slot) ? InventoryItem.Cannon : FishCount(slot) > 0 ? InventoryItem.Fish : BallCount(slot) > 0 ? BallItem(slot) : HasMallet(slot) ? InventoryItem.Mallet : PlankCount(slot) > 0 ? InventoryItem.Plank : InventoryItem.None;
        public void OpenLoot(NetworkLootChest target)
        {
            if (target == null || !network.IsOwner || motor.IsDead || Vector3.Distance(transform.position, target.transform.position) > 5f) return;
            openChest = target; lootWindow = true; lootOwner = this;
            AdvancedPlayerController.SetCursor(false);
        }
        void CloseLoot(bool restoreCursor)
        {
            if (!lootWindow) return;
            lootWindow = false; openChest = null;
            if (lootOwner == this) lootOwner = null;
            if (restoreCursor) AdvancedPlayerController.SetCursor(true);
        }
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
        NetworkLootChest chest;
        float rotation, tilt, roll;
        bool cancelled;
        bool valid;
        bool Networked => network != null && (network.IsClientInitialized || network.IsServerInitialized);
        public bool HasCannon(int slot) => slot > 1 && slot < 6 && (CannonSlots & (1 << slot)) != 0;
        public int EmptySlot() { for (int i = 2; i < 6; i++) if (!HasSabre(i) && !HasCannon(i) && FishCount(i) == 0 && BallCount(i) == 0 && !HasMallet(i) && PlankCount(i) == 0) return i; return -1; }
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
            InteractionUsed = false; pickup = null; chest = null; aimedBall = null; aimedCannon = null; valid = false;
            if (preview != null) preview.SetActive(false);
            if (lootWindow)
            {
                InteractionUsed = true;
                if (openChest == null || !openChest.IsSpawned || motor.IsDead || !Networked || Vector3.Distance(transform.position, openChest.transform.position) > 5f)
                    CloseLoot(Networked && network.IsOwner && !motor.IsDead);
                else if (Keyboard.current != null && (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame)) CloseLoot(true);
                return;
            }
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
            if (hands != null && hands.HasHeldBall)
            {
                var loose = hands.HeldBall.GetComponent<NetworkLooseCannonball>();
                if (keyboard.eKey.wasPressedThisFrame && Networked && loose != null) loose.Store();
                else if (keyboard.eKey.wasPressedThisFrame && Networked && hands.HeldBall.Network != null)
                    network.StoreBall(hands.HeldBall.Network.NetworkObject);
                return;
            }
            if (HandsOccupied) return;
            var camera = motor.PlayerCamera;
            RaycastHit nearest = default;
            float distance = 5f;
            foreach (var hit in Physics.RaycastAll(camera.transform.position, camera.transform.forward, distance, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) && hit.distance < distance) { nearest = hit; distance = hit.distance; }
            if (nearest.collider != null)
            {
                aimedBall = nearest.collider.GetComponent<Cannonball>();
                aimedCannon = nearest.collider.GetComponentInParent<SimpleCannon>();
                pickup = nearest.collider.GetComponentInParent<CannonPickup>();
            }
            if (Networked && aimedBall != null && !aimedBall.Loaded && aimedBall.Network != null &&
                (keyboard.eKey.wasPressedThisFrame || mouse.leftButton.wasPressedThisFrame))
            { InteractionUsed = true; network.StoreBall(aimedBall.Network.NetworkObject); return; }
            if (Networked && keyboard.eKey.wasPressedThisFrame)
            {
                if (BallSelected && aimedCannon != null && !aimedCannon.IsLoaded && aimedCannon.Network != null)
                { InteractionUsed = true; network.LoadBall(aimedCannon.Network.NetworkObject, aimedCannon.Index); return; }
            }
            if (nearest.collider != null) chest = nearest.collider.GetComponentInParent<NetworkLootChest>();
            if (chest != null && Networked && (Fishing == null || (!Fishing.IsFishing && !Fishing.IsEating)))
            {
                if (keyboard.eKey.wasPressedThisFrame)
                {
                    InteractionUsed = true;
                    network.UseChest(chest.NetworkObject);
                }
                return;
            }
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

        void OnDisable() { CloseLoot(false); if (preview != null) preview.SetActive(false); }
        void OnDestroy() { if (preview != null) Destroy(preview); if (previewMaterial != null) Destroy(previewMaterial); }
        void OnGUI()
        {
            if (motor == null || motor.PlayerCamera == null || !motor.PlayerCamera.enabled || SessionController.MenuOpen) return;
            Color old = GUI.color;
            float width = Mathf.Min(66f, (Screen.width - 20f) / 6f);
            for (int i = 0; i < 6; i++)
            {
                GUI.color = i == SelectedSlot ? new Color(1f, .8f, .35f) : Color.white;
                var rect = new Rect(Screen.width * .5f - width * 3 + width * i, Screen.height - 86, width - 4, 72);
                if (Icons != null) Icons.DrawSlot(rect, ItemAt(i), Mathf.Max(FishCount(i), Mathf.Max(BallCount(i), PlankCount(i))), (i + 1).ToString(), false);
                else GUI.Box(rect, (i + 1) + "\n" + InventoryIcons.ItemName(ItemAt(i)));
            }
            GUI.color = old;
            if (lootWindow && openChest != null)
            {
                int slots = Mathf.Max(6, openChest.SlotCount), rows = Mathf.CeilToInt(slots / 6f);
                float panelWidth = width * 6 + 24, panelHeight = 75 + rows * 78;
                var panel = new Rect((Screen.width - panelWidth) * .5f, (Screen.height - panelHeight) * .5f, panelWidth, panelHeight);
                GUI.Box(panel, "Сундук · нажмите на предмет, чтобы забрать");
                for (int i = 0; i < slots; i++)
                {
                    var item = openChest.ItemAt(i);
                    var rect = new Rect(panel.x + 12 + i % 6 * width, panel.y + 30 + i / 6 * 78, width - 4, 72);
                    GUI.enabled = item != InventoryItem.None;
                    bool take = Icons != null ? Icons.DrawSlot(rect, item, 1, "", true) : GUI.Button(rect, InventoryIcons.ItemName(item));
                    if (take) network.UseChest(openChest.NetworkObject, i);
                }
                GUI.enabled = true;
                GUI.Label(new Rect(panel.x + 12, panel.yMax - 32, panelWidth - 110, 24), EmptySlot() < 0 ? "Инвентарь заполнен" : "Ваш инвентарь — внизу экрана");
                if (GUI.Button(new Rect(panel.xMax - 92, panel.yMax - 32, 80, 24), "Закрыть")) CloseLoot(true);
                return;
            }
            GUI.Label(new Rect(Screen.width * .5f - 130, Screen.height - 108, 300, 22), "G — выбросить предмет из выбранного слота");
            string hint = aimedBall != null && aimedBall.Network != null ? "ЛКМ / E — взять 3 ядра" : BallSelected && aimedCannon != null && !aimedCannon.IsLoaded ? "E — зарядить ядро из рук" : chest != null ? chest.Hint(this) : pickup != null ? (EmptySlot() >= 0 ? "E — взять разобранную пушку" : "Инвентарь заполнен") : Placing && !cancelled ? "ЛКМ — поставить · R/колесо — поворот · Q/E — наклон\nShift+Q/E — крен · ПКМ — отменить" : "";
            if (hint.Length > 0) GUI.Box(new Rect(Screen.width * .5f - 290, Screen.height - 165, 580, 44), hint);
        }
    }
}

