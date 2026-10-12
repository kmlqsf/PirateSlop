using FishNet.Connection;
using FishNet.Object;
using PirateSlop.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PirateSlop.Networking
{
    public struct TargetMarkState
    {
        public int Id;
        public Vector3 Position;
        public float Distance;
        public float SecondsLeft;
        public bool Seagull;
        public NetworkObject SeagullEvent;
        public int BirdIndex;
        public int AuthorId;
        public byte CrewColor;
        public Vector3 TrackedPosition
        {
            get
            {
                var bird = SeagullEvent != null ? SeagullEvent.GetComponent<NetworkSeagullEvent>()?.BirdAt(BirdIndex) : null;
                return bird != null && bird.gameObject.activeInHierarchy ? bird.transform.position : Position;
            }
        }
    }

    public sealed partial class NetworkPlayer
    {
        public TargetMarkState[] TargetMarks { get; private set; } = System.Array.Empty<TargetMarkState>();
        public float TargetMarksReceivedAt { get; private set; }
        float nextTargetPublish, nextTargetRequest;
        float nextSeagullRequest;

        void UpdateSeagullMarkInput()
        {
            if (!IsOwner || !IsClientInitialized || !IsSpawned || motor == null || !motor.InputActive || PlayerInventory.LootWindowOpen || SessionController.MenuOpen) return;
            var mouse = Mouse.current;
            var view = motor.PlayerCamera;
            if (mouse != null && mouse.middleButton.wasPressedThisFrame && view != null && view.enabled)
                MarkSeagullTarget(view.transform.position, view.transform.forward);
        }
        public void MarkSeagullTarget(Vector3 origin, Vector3 direction) => MarkSeagullTargetServerRpc(origin, direction);
        [ServerRpc]
        void MarkSeagullTargetServerRpc(Vector3 origin, Vector3 direction)
        {
            if (Time.time < nextSeagullRequest) return;
            nextSeagullRequest = Time.time + .25f;
            if (!IsSpawned || Ship == null || Ship.IsSinking || TeamId.Value <= 0 || Ship.TeamId.Value != TeamId.Value || motor.IsDead || motor.IsDowned || motor.IsFrozen
                || !float.IsFinite(origin.sqrMagnitude) || (origin - transform.position).sqrMagnitude > 25f
                || !float.IsFinite(direction.sqrMagnitude) || Mathf.Abs(direction.sqrMagnitude - 1f) > .05f) return;
            RaycastHit nearest = default;
            float distance = 300f;
            foreach (var hit in Physics.SphereCastAll(origin, .12f, direction.normalized, distance, ~0, QueryTriggerInteraction.Collide))
            {
                if (hit.transform.IsChildOf(transform) || hit.distance >= distance) continue;
                var bird = hit.collider.GetComponentInParent<SeagullVisual>();
                if (hit.collider.isTrigger && bird == null) continue;
                nearest = hit; distance = hit.distance;
            }
            var target = nearest.collider != null ? nearest.collider.GetComponentInParent<SeagullVisual>() : null;
            if (target != null && !target.Shot) Ship.AddSeagullTargetMark(target, this);
        }

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
            foreach (var hit in Physics.RaycastAll(origin, direction.normalized, distance, ~0, QueryTriggerInteraction.Collide))
            {
                if (hit.transform.IsChildOf(Ship.transform) || hit.transform.IsChildOf(transform) || hit.distance >= distance) continue;
                if (hit.collider.isTrigger && hit.collider.GetComponentInParent<SeagullVisual>() == null) continue;
                nearest = hit;
                distance = hit.distance;
            }
            if (nearest.collider != null) Ship.AddTargetMark(nearest, author: this);
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
