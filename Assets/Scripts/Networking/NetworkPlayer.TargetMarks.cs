using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace PirateSlop.Networking
{
    public struct TargetMarkState
    {
        public int Id;
        public Vector3 Position;
        public float Distance;
        public float SecondsLeft;
    }

    public sealed partial class NetworkPlayer
    {
        public TargetMarkState[] TargetMarks { get; private set; } = System.Array.Empty<TargetMarkState>();
        public float TargetMarksReceivedAt { get; private set; }
        float nextTargetPublish, nextTargetRequest;

        public void MarkSpyglassTarget(Vector3 direction) => MarkSpyglassTargetServerRpc(direction);

        [ServerRpc]
        void MarkSpyglassTargetServerRpc(Vector3 direction)
        {
            if (Time.time < nextTargetRequest) return;
            nextTargetRequest = Time.time + .3f;
            if (Ship == null || Ship.IsSinking || motor.IsDead || motor.IsSwimming || motor.IsClimbing || motor.LocomotionLocked ||
                !float.IsFinite(direction.x) || !float.IsFinite(direction.y) || !float.IsFinite(direction.z) || direction.sqrMagnitude < .9f || direction.sqrMagnitude > 1.1f) return;
            var station = Ship.GetComponentInChildren<ShipSpyglass>();
            if (station == null || station.Viewpoint == null || Vector3.Distance(transform.position, station.transform.position) > 3.5f) return;
            var view = GetComponent<ShipSpyglassView>();
            Vector3 origin = station.Viewpoint.position + station.transform.up * (view != null ? view.ViewpointLift : 1.25f);
            RaycastHit nearest = default;
            float distance = 3000f;
            foreach (var hit in Physics.RaycastAll(origin, direction.normalized, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(Ship.transform) || hit.transform.IsChildOf(transform) || hit.distance >= distance) continue;
                nearest = hit;
                distance = hit.distance;
            }
            if (nearest.collider != null) Ship.AddTargetMark(nearest);
        }

        void PublishTargetMarks()
        {
            if (!IsServerInitialized || IsBot.Value || Owner == null || !Owner.IsActive || Time.time < nextTargetPublish) return;
            nextTargetPublish = Time.time + .1f;
            var marks = Ship != null && Ship.TeamId.Value == TeamId.Value ? Ship.GetTargetMarks() : System.Array.Empty<TargetMarkState>();
            ReceiveTargetMarks(Owner, marks);
        }

        [TargetRpc]
        void ReceiveTargetMarks(NetworkConnection recipient, TargetMarkState[] marks)
        {
            TargetMarks = marks;
            TargetMarksReceivedAt = Time.unscaledTime;
        }
    }
}
