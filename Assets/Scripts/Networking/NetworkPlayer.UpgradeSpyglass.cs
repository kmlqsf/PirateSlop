using FishNet.Object;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkPlayer
    {
        Transform upgradeObservedTarget;
        float upgradeObservedSeconds, lastUpgradeObservation, nextUpgradeObserveRequest;
        public void ObserveUpgradeSpyglass(Vector3 direction, bool portable)
        { if (IsOwner && IsClientInitialized) ObserveUpgradeSpyglassServerRpc(direction, portable); }
        [ServerRpc]
        void ObserveUpgradeSpyglassServerRpc(Vector3 direction, bool portable)
        {
            if (Time.time < nextUpgradeObserveRequest) return;
            nextUpgradeObserveRequest = Time.time + .08f;
            if (!HasUpgrade(UpgradeEffect.SharpEye) || Ship == null || Ship.IsSinking || motor.IsDead || motor.IsSwimming || motor.IsClimbing || !float.IsFinite(direction.sqrMagnitude) || Mathf.Abs(direction.sqrMagnitude - 1f) > .05f) return;
            Vector3 origin;
            if (portable)
            {
                var inventory = GetComponent<PlayerInventory>();
                var equipment = GetComponent<NetworkEquipment>();
                if (inventory.ItemAt(inventory.SelectedSlot) != InventoryItem.Spyglass || !equipment.Active) return;
                origin = transform.position + Vector3.up * (motor.IsCrouched ? .8f : 1.6f);
            }
            else
            {
                var station = Ship.GetComponentInChildren<ShipSpyglass>();
                if (station == null || station.Viewpoint == null || Vector3.Distance(transform.position, station.transform.position) > 3.5f) return;
                origin = station.Viewpoint.position + station.transform.up * station.ViewpointLift;
            }
            RaycastHit nearest = default;
            float distance = 3000f;
            foreach (var hit in Physics.RaycastAll(origin, direction, distance, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(Ship.transform) && !hit.transform.IsChildOf(transform) && hit.distance < distance) { nearest = hit; distance = hit.distance; }
            Transform target = null;
            if (nearest.collider != null)
            {
                var enemy = nearest.collider.GetComponentInParent<NetworkPlayer>();
                var enemyShip = nearest.collider.GetComponentInParent<NetworkShip>();
                if (IsEnemy(enemy)) target = enemy.transform;
                else if (enemyShip != null && enemyShip.TeamId.Value != TeamId.Value && !enemyShip.IsSinking) target = enemyShip.transform;
            }
            if (target == null || target != upgradeObservedTarget || Time.time - lastUpgradeObservation > .4f) upgradeObservedSeconds = 0f;
            else upgradeObservedSeconds += Mathf.Min(.25f, Time.time - lastUpgradeObservation);
            upgradeObservedTarget = target; lastUpgradeObservation = Time.time;
            if (target != null && upgradeObservedSeconds >= RoguelikeTuning.Current.spyglassObserveSeconds)
            {
                Ship.AddTargetMark(nearest, RoguelikeTuning.Current.spyglassMarkSeconds);
                upgradeObservedSeconds = 0f;
            }
        }
    }
}
