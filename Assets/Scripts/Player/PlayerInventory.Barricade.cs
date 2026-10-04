using PirateSlop.Networking;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PirateSlop
{
    public sealed partial class PlayerInventory
    {
        public GameObject BarricadeVisual;
        public Material BarricadeConstructionMaterial;
        public bool BarricadeSelected => EquipmentAt(SelectedSlot) == InventoryItem.Barricade;
        GameObject barricadePreview;
        Material barricadePreviewMaterial;
        NetworkShip barricadeShip;
        NetworkBarricade aimedBarricade, collectingTarget;
        Vector3 barricadePoint;
        Quaternion barricadeRotation;
        int barricadeSlot = -1;
        float barricadeProgress, barricadeNextSend, barricadeReportedAt;
        bool barricadeBuilding;
        public void ReportBarricadeWork(float progress, bool completed)
        {
            if (!barricadeBuilding && collectingTarget == null) return;
            barricadeProgress = progress;
            barricadeReportedAt = Time.unscaledTime;
            if (completed)
            {
                bool installed = barricadeBuilding;
                barricadeBuilding = false; collectingTarget = null;
                barricadeProgress = 0f; barricadeShip = null;
                if (barricadePreview != null) barricadePreview.SetActive(false);
                ShowMessage("Баррикада " + (installed ? "установлена" : "в инвентаре"));
            }
        }
        void BeginBarricadeFrame()
        {
            aimedBarricade = null;
            if (barricadePreview != null) barricadePreview.SetActive(false);
            if (!Networked || !network.IsOwner || !motor.InputActive || motor.IsDead || motor.LocomotionLocked || motor.IsSwimming || motor.IsClimbing ||
                HandsOccupied || ControlFocused || lootWindow || hands != null && hands.HasHeldBall ||
                barricadeBuilding && (!BarricadeSelected || SelectedSlot != barricadeSlot)) CancelBarricadeWork();
            var keys = Keyboard.current;
            if (collectingTarget != null && (keys == null || !keys.eKey.isPressed || !(keys.leftShiftKey.isPressed || keys.rightShiftKey.isPressed))) CancelBarricadeWork();
        }
        void CancelBarricadeWork()
        {
            if (network != null && network.IsOwner && network.IsSpawned)
            {
                if (barricadeBuilding) network.HoldBarricade(null, -1, default, Quaternion.identity, default, Vector3.forward, false);
                if (collectingTarget != null) network.HoldBarricadeCollection(null, default, Vector3.forward, false);
            }
            barricadeBuilding = false; collectingTarget = null; barricadeShip = null;
            barricadeProgress = 0f; barricadeSlot = -1;
            if (barricadePreview != null) barricadePreview.SetActive(false);
        }
        bool HandleBarricadeInteraction(RaycastHit hit)
        {
            aimedBarricade = hit.collider != null ? hit.collider.GetComponentInParent<NetworkBarricade>() : null;
            if (collectingTarget != null && aimedBarricade != collectingTarget) CancelBarricadeWork();
            if (aimedBarricade == null || !Networked || !network.IsOwner) return false;
            var keys = Keyboard.current;
            bool held = keys != null && keys.eKey.isPressed && (keys.leftShiftKey.isPressed || keys.rightShiftKey.isPressed);
            if (held && CanFitItem(InventoryItem.Barricade))
            {
                if (collectingTarget != aimedBarricade)
                { CancelBarricadeWork(); collectingTarget = aimedBarricade; barricadeNextSend = 0f; }
                InteractionUsed = true;
                if (Time.unscaledTime >= barricadeNextSend)
                {
                    barricadeNextSend = Time.unscaledTime + .12f;
                    network.HoldBarricadeCollection(collectingTarget.NetworkObject, motor.PlayerCamera.transform.position - transform.position, motor.PlayerCamera.transform.forward, true);
                }
            }
            return true;
        }
        float DisplayBarricadeProgress(float duration) => barricadeProgress <= 0f ? 0f : Mathf.Min(.99f, barricadeProgress + Mathf.Min(.15f, Time.unscaledTime - barricadeReportedAt) / duration);
        void PresentBarricadePlacement()
        {
            if (!placementPending || !motor.InputActive || !Networked || !network.IsOwner || HandsOccupied || ControlFocused || motor.LocomotionLocked || motor.IsSwimming || motor.IsClimbing || aimedBarricade != null)
            { if (barricadeBuilding) CancelBarricadeWork(); return; }
            var keys = Keyboard.current; var mouse = Mouse.current; var camera = motor.PlayerCamera;
            if (keys == null || mouse == null || camera == null || BarricadeVisual == null || BarricadeConstructionMaterial == null) return;
            if (mouse.rightButton.wasPressedThisFrame) { cancelled = true; CancelBarricadeWork(); }
            if (cancelled) return;
            float previousRotation = rotation;
            rotation += mouse.scroll.ReadValue().y * .125f;
            if (keys.rKey.wasPressedThisFrame) rotation += 15f;
            if (barricadeBuilding && (!mouse.leftButton.isPressed || !Mathf.Approximately(rotation, previousRotation))) CancelBarricadeWork();
            RaycastHit nearest = default; float distance = 5f;
            foreach (var hit in Physics.RaycastAll(camera.transform.position, camera.transform.forward, distance, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) && hit.distance < distance) { nearest = hit; distance = hit.distance; }
            var ship = nearest.collider != null ? nearest.collider.GetComponentInParent<NetworkShip>() : null;
            Vector3 position = nearest.collider != null ? nearest.point + nearest.normal * .03f : camera.transform.position + camera.transform.forward * 4f;
            Quaternion localRotation = Quaternion.Euler(0, rotation, 0);
            Vector3 localPoint = ship != null ? ship.transform.InverseTransformPoint(position) : Vector3.zero;
            if (barricadeBuilding && (ship != barricadeShip || Vector3.Distance(localPoint, barricadePoint) > .38f)) CancelBarricadeWork();
            if (barricadeBuilding) { localPoint = barricadePoint; localRotation = barricadeRotation; position = ship.transform.TransformPoint(localPoint); }
            bool canPlace = ship != null && Vector3.Dot(nearest.normal, ship.transform.up) > .7f && CanPlaceBarricade(ship, localPoint, localRotation, motor);
            if (!canPlace && barricadeBuilding) CancelBarricadeWork();
            if (barricadePreview == null)
            {
                barricadePreview = Instantiate(BarricadeVisual);
                barricadePreview.name = "BarricadePlacementPreview";
                foreach (var collider in barricadePreview.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                barricadePreviewMaterial = new Material(BarricadeConstructionMaterial);
                foreach (var renderer in barricadePreview.GetComponentsInChildren<Renderer>(true))
                { renderer.sharedMaterial = barricadePreviewMaterial; renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
            }
            barricadePreview.SetActive(true);
            barricadePreview.transform.SetPositionAndRotation(position, ship != null ? ship.transform.rotation * localRotation : Quaternion.Euler(0, camera.transform.eulerAngles.y + rotation, 0));
            barricadePreviewMaterial.SetMatrix("_ConstructionWorldToLocal", barricadePreview.transform.worldToLocalMatrix);
            barricadePreviewMaterial.SetColor("_ConstructionTint", canPlace ? new Color(.15f, .9f, .45f, .55f) : new Color(1f, .2f, .15f, .55f));
            float progress = barricadeBuilding ? DisplayBarricadeProgress(3f) : 0f;
            barricadePreviewMaterial.SetFloat("_ConstructionProgress", progress);
            if (!canPlace || !mouse.leftButton.isPressed) return;
            if (!barricadeBuilding)
            {
                barricadeBuilding = true; barricadeShip = ship; barricadePoint = localPoint;
                barricadeRotation = localRotation; barricadeSlot = SelectedSlot; barricadeNextSend = 0f; barricadeProgress = 0f;
            }
            if (Time.unscaledTime >= barricadeNextSend)
            {
                barricadeNextSend = Time.unscaledTime + .12f;
                network.HoldBarricade(ship.NetworkObject, barricadeSlot, barricadePoint, barricadeRotation, camera.transform.position - transform.position, camera.transform.forward, true);
            }
        }
        public static bool CanPlaceBarricade(NetworkShip ship, Vector3 point, Quaternion rotation, AdvancedPlayerController player)
        {
            float length = Quaternion.Dot(rotation, rotation);
            if (ship == null || player == null || player.IsDead || player.LocomotionLocked || !float.IsFinite(point.sqrMagnitude) || !float.IsFinite(length) || length < .9f || length > 1.1f) return false;
            if (Vector3.Dot(rotation * Vector3.up, Vector3.up) < .999f) return false;
            Vector3 position = ship.transform.TransformPoint(point);
            if (Vector3.Distance(player.transform.position, position) > 6f) return false;
            bool supported = false;
            foreach (var hit in Physics.RaycastAll(position + ship.transform.up * .15f, -ship.transform.up, .25f, ~0, QueryTriggerInteraction.Ignore))
                if (hit.collider.GetComponentInParent<NetworkShip>() == ship && Vector3.Dot(hit.normal, ship.transform.up) > .7f) { supported = true; break; }
            if (!supported) return false;
            Quaternion worldRotation = ship.transform.rotation * rotation;
            return Physics.OverlapBox(position + worldRotation * new Vector3(0, 1.08f, 0), new Vector3(.68f, 1.015f, .32f), worldRotation, ~0, QueryTriggerInteraction.Ignore).Length == 0;
        }
        void DrawBarricadeWork()
        {
            if (!Networked || !network.IsOwner || !motor.InputActive) return;
            if (aimedBarricade != null) ContextPrompt.Offer(CanFitItem(InventoryItem.Barricade) ? "Shift+E — собрать баррикаду · удерживайте 7 секунд" : CannotFitHint(InventoryItem.Barricade), 45);
            else if (BarricadeSelected && !cancelled) ContextPrompt.Offer("Удерживайте ЛКМ 3 секунды, смотря на место установки\nR/колесо — поворот · ПКМ — отменить", 45);
            if (!barricadeBuilding && collectingTarget == null) return;
            var rect = new Rect(Screen.width * .5f - 160f, Screen.height * .5f + 65f, 320f, 44f);
            PirateHudStyle.Panel(rect, barricadeBuilding ? "Установка баррикады · удерживайте ЛКМ" : "Сбор баррикады · удерживайте Shift+E");
            var previous = GUI.color; GUI.color = new Color(.15f, .9f, .45f);
            GUI.DrawTexture(new Rect(rect.x + 5f, rect.yMax - 12f, (rect.width - 10f) * DisplayBarricadeProgress(barricadeBuilding ? 3f : 7f), 7f), Texture2D.whiteTexture);
            GUI.color = previous;
        }
        void DestroyBarricadePreview()
        {
            if (barricadePreview != null) Destroy(barricadePreview);
            if (barricadePreviewMaterial != null) Destroy(barricadePreviewMaterial);
        }
    }
}
