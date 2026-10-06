using FishNet.Object;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkWeapon
    {
        public NetworkBarricade BarricadePrefab;
        NetworkShip buildingShip;
        NetworkBarricade collectingBarricade;
        Vector3 buildingPoint;
        Quaternion buildingRotation;
        int buildingSlot = -1;
        float barricadeStarted, barricadeLast;
        public void HoldBarricade(NetworkObject ship, int slot, Vector3 point, Quaternion rotation, Vector3 eyeOffset, Vector3 forward, bool holding)
            => BuildBarricadeServerRpc(ship, slot, point, rotation, eyeOffset, forward, holding);
        public void HoldBarricadeCollection(NetworkObject barricade, Vector3 eyeOffset, Vector3 forward, bool holding)
            => CollectBarricadeServerRpc(barricade, eyeOffset, forward, holding);
        void ResetBarricadeWork()
        {
            buildingShip = null; collectingBarricade = null; buildingSlot = -1;
            barricadeStarted = barricadeLast = 0f;
        }
        bool BarricadeHandsFree => CanHandleBall() && !GetComponent<CannonHands>().HasHeldBall;
        bool BarricadeLook(Vector3 offset, Vector3 forward, out RaycastHit nearest)
        {
            nearest = default;
            if (!float.IsFinite(offset.sqrMagnitude) || !float.IsFinite(forward.sqrMagnitude) || offset.sqrMagnitude > 25f || forward.sqrMagnitude < .5f) return false;
            float distance = 5f;
            foreach (var hit in Physics.RaycastAll(transform.position + offset, forward.normalized, distance, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) && hit.distance < distance) { nearest = hit; distance = hit.distance; }
            return nearest.collider != null;
        }
        [ServerRpc]
        void BuildBarricadeServerRpc(NetworkObject target, int slot, Vector3 point, Quaternion rotation, Vector3 eyeOffset, Vector3 forward, bool holding)
        {
            var ship = target != null ? target.GetComponent<NetworkShip>() : null;
            bool valid = holding && BarricadePrefab != null && BarricadeHandsFree && inventory.IsNormalSlot(slot) &&
                selectedSlot.Value == slot && inventory.EquipmentAt(slot) == InventoryItem.Barricade && ship != null && ship.IsSpawned &&
                PlayerInventory.CanPlaceBarricade(ship, point, rotation, GetComponent<AdvancedPlayerController>()) &&
                BarricadeLook(eyeOffset, forward, out var aimed) && aimed.collider.GetComponentInParent<NetworkShip>() == ship &&
                Vector3.Distance(ship.transform.InverseTransformPoint(aimed.point), point) <= .38f;
            if (!valid)
            {
                ResetBarricadeWork(); BarricadeProgressTargetRpc(Owner, 0f, false); return;
            }
            if (buildingShip != ship || buildingSlot != slot || collectingBarricade != null || Time.time - barricadeLast > .5f ||
                Vector3.Distance(buildingPoint, point) > .05f || Quaternion.Angle(buildingRotation, rotation) > 1f)
            {
                ResetBarricadeWork(); buildingShip = ship; buildingSlot = slot;
                buildingPoint = point; buildingRotation = rotation; barricadeStarted = Time.time;
            }
            barricadeLast = Time.time;
            float progress = Mathf.Clamp01((Time.time - barricadeStarted) / 3f);
            if (progress < 1f) { BarricadeProgressTargetRpc(Owner, progress, false); return; }
            var placed = Instantiate(BarricadePrefab);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(placed.gameObject, ship.gameObject.scene);
            placed.Place(ship, buildingPoint, buildingRotation);
            ServerManager.Spawn(placed.NetworkObject);
            ConsumeEquipment(slot, InventoryItem.Barricade);
            DropSoundObserversRpc(placed.transform.position);
            ResetBarricadeWork(); BarricadeProgressTargetRpc(Owner, 1f, true);
        }
        [ServerRpc]
        void CollectBarricadeServerRpc(NetworkObject target, Vector3 eyeOffset, Vector3 forward, bool holding)
        {
            var barricade = target != null ? target.GetComponent<NetworkBarricade>() : null;
            bool valid = holding && BarricadeHandsFree && barricade != null && barricade.Available && CanAddItem(InventoryItem.Barricade) &&
                BarricadeLook(eyeOffset, forward, out var aimed) && aimed.collider.GetComponentInParent<NetworkBarricade>() == barricade &&
                CanReach(aimed.point, barricade.transform);
            if (!valid) { ResetBarricadeWork(); BarricadeProgressTargetRpc(Owner, 0f, false); return; }
            if (collectingBarricade != barricade || buildingShip != null || Time.time - barricadeLast > .5f)
            { ResetBarricadeWork(); collectingBarricade = barricade; barricadeStarted = Time.time; }
            barricadeLast = Time.time;
            float progress = Mathf.Clamp01((Time.time - barricadeStarted) / 7f);
            if (progress < 1f) { BarricadeProgressTargetRpc(Owner, progress, false); return; }
            bool completed = barricade.Collect(this);
            ResetBarricadeWork(); BarricadeProgressTargetRpc(Owner, completed ? 1f : 0f, completed);
        }
        [TargetRpc]
        void BarricadeProgressTargetRpc(FishNet.Connection.NetworkConnection connection, float progress, bool completed)
            => inventory.ReportBarricadeWork(progress, completed);
    }
}
