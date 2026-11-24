using System.Collections.Generic;
using PirateSlop.Networking;
using UnityEngine;

namespace PirateSlop.Ships
{
    public sealed partial class ShipMonkey
    {
        public Transform Neck, Head;
        public Vector3 HeadForward = Vector3.forward;
        public Vector3 HeadUp = Vector3.up;
        [Min(.5f)] public float NoticeRadius = 3.2f;
        [Range(0f, 1f)] public float NoticeChance = .45f;
        [Range(0f, 30f)] public float HeadYawLimit = 30f;
        [Range(0f, 15f)] public float HeadPitchLimit = 15f;
        readonly HashSet<NetworkPlayer> nearbyPlayers = new();
        readonly HashSet<NetworkPlayer> nextNearbyPlayers = new();
        NetworkPlayer watchedPlayer;
        bool looking;
        Vector3 lookPoint;
        float attentionScan, attentionRemaining, attentionCooldown;
        Vector2 headAngles;

        void ResetLook()
        {
            nearbyPlayers.Clear(); nextNearbyPlayers.Clear(); watchedPlayer = null;
            looking = false; attentionScan = attentionRemaining = 0f;
            attentionCooldown = 1.5f; headAngles = Vector2.zero;
        }

        bool CanNotice(NetworkPlayer player, out Vector3 point)
        {
            point = default;
            if (player == null || !player.IsSpawned || player.Eliminated.Value || player.Motor == null || player.Motor.IsDead) return false;
            point = player.transform.position + player.transform.up * 1.55f;
            Vector3 origin = Head != null ? Head.position : Visual.position + transform.up * .55f;
            Vector3 offset = point - origin;
            if (offset.sqrMagnitude > NoticeRadius * NoticeRadius) return false;
            Vector3 local = Visual.InverseTransformDirection(offset);
            float yaw = Mathf.Abs(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg);
            float pitch = Mathf.Abs(Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg);
            if (yaw > 80f || pitch > 65f) return false;
            if (Physics.Raycast(origin, offset.normalized, out var hit, offset.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(player.transform)) return false;
            return true;
        }

        bool guiding;
        Vector3 guidePoint, guideLook;
        const float GuideTravelMultiplier = 2.75f;
        bool guidePathDirty;

        public Vector3 StartTutorialGreeting(Vector3 playerPosition)
        {
            var origin = transform.InverseTransformPoint(playerPosition);
            var desired = origin + Vector3.forward * 3.5f;
            int node = current;
            float best = float.PositiveInfinity;
            for (int i = 0; i < Nodes.Length; i++)
            {
                if (Nodes[i].Surface != ShipMonkeySurface.Deck || !Nodes[i].Available) continue;
                var p = Point(i);
                if (Mathf.Abs(p.y - origin.y) > 1.2f || (p - origin).sqrMagnitude < 6.25f) continue;
                float score = (p - desired).sqrMagnitude;
                if (score < best) { best = score; node = i; }
            }
            current = node;
            position = Point(node);
            SetGuideDestination(transform.TransformPoint(position));
            guidePoint = position;
            Show(position, rotation, ShipMonkeyMotion.Idle, 1f);
            return position;
        }

        public void SetGuideDestination(Vector3 destination)
        {
            var local = transform.InverseTransformPoint(destination);
            if (guiding && (local - guidePoint).sqrMagnitude < .01f) return;
            if (!guiding)
            {
                CancelActivities();
                ResetJump();
                target = -1;
                path.Clear();
            }
            else if (target >= 0 && pathIndex + 1 < path.Count)
                path.RemoveRange(pathIndex + 1, path.Count - pathIndex - 1);

            guiding = true;
            guidePoint = local;
            guidePathDirty = true;
            wantsRest = false;
            pause = 0f;
        }

        public void SetGuideFocus(Vector3 focus) => guideLook = transform.InverseTransformPoint(focus);

        void UpdateAttention(float delta)
        {
            if (guiding)
            {
                looking = true;
                lookPoint = guideLook;
                var direction = lookPoint - position;
                direction.y = 0;
                if (target < 0 && direction.sqrMagnitude > .01f)
                    rotation = Quaternion.RotateTowards(rotation, Quaternion.LookRotation(direction), delta * 240f);
                return;
            }
            if (task == TaskKind.SlotWait && activitySlot != null)
            {
                watchedPlayer = null; looking = true;
                lookPoint = transform.InverseTransformPoint(activitySlot.transform.TransformPoint(new Vector3(0, 1.01f, .35f)));
                return;
            }
            attentionCooldown -= delta;
            if (watchedPlayer == null) looking = false;
            bool allowed = Motion == ShipMonkeyMotion.Idle || Motion == ShipMonkeyMotion.Sit || Motion == ShipMonkeyMotion.Walk || Motion == ShipMonkeyMotion.RailWalk;
            if (watchedPlayer != null)
            {
                attentionRemaining -= delta;
                if (allowed && attentionRemaining > 0f && CanNotice(watchedPlayer, out var point))
                {
                    looking = true; lookPoint = transform.InverseTransformPoint(point);
                }
                else { watchedPlayer = null; looking = false; }
            }
            attentionScan -= delta;
            if (attentionScan > 0f) return;
            attentionScan = .25f;
            nextNearbyPlayers.Clear();
            NetworkPlayer candidate = null;
            float closest = float.PositiveInfinity;
            foreach (var player in NetworkPlayer.Active)
            {
                if (player == null || (player.transform.position - Visual.position).sqrMagnitude > NoticeRadius * NoticeRadius || !CanNotice(player, out var point)) continue;
                nextNearbyPlayers.Add(player);
                if (!allowed || watchedPlayer != null || attentionCooldown > 0f || nearbyPlayers.Contains(player)) continue;
                float squared = (point - Visual.position).sqrMagnitude;
                if (squared < closest) { candidate = player; closest = squared; }
            }
            nearbyPlayers.Clear();
            foreach (var player in nextNearbyPlayers) nearbyPlayers.Add(player);
            if (candidate == null) return;
            attentionCooldown = Random.Range(4f, 8f);
            if (Random.value >= NoticeChance) return;
            watchedPlayer = candidate; attentionRemaining = Random.Range(1.3f, 2.8f);
            looking = true;
            lookPoint = transform.InverseTransformPoint(candidate.transform.position + candidate.transform.up * 1.55f);
        }

        void LateUpdate()
        {
            PresentActivities();
            PresentRepair();
            PresentDefense();
            PresentHeadRide();
            if ((!initialized && !remoteInitialized) || Visual == null || !Visual.gameObject.activeInHierarchy || Animator == null || !Animator.isActiveAndEnabled || Head == null) return;
            Vector3 forward = Head.TransformDirection(HeadForward).normalized;
            Vector3 up = Head.TransformDirection(HeadUp).normalized;
            Quaternion frame = Quaternion.LookRotation(forward, up);
            Vector2 wanted = Vector2.zero;
            Vector3 targetPoint = transform.TransformPoint(lookPoint);
            if (looking)
            {
                Vector3 direction = Quaternion.Inverse(frame) * (targetPoint - Head.position);
                float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                float pitch = -Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude) * Mathf.Rad2Deg;
                if (Mathf.Abs(yaw) <= 85f) wanted = new Vector2(Mathf.Clamp(pitch, -Mathf.Clamp(HeadPitchLimit, 0f, 15f), Mathf.Clamp(HeadPitchLimit, 0f, 15f)), Mathf.Clamp(yaw, -Mathf.Clamp(HeadYawLimit, 0f, 30f), Mathf.Clamp(HeadYawLimit, 0f, 30f)));
            }
            float blend = 1f - Mathf.Exp(-Time.deltaTime * 6f);
            headAngles = Vector2.Lerp(headAngles, wanted, blend);
            Quaternion headOffset = frame * Quaternion.Euler(headAngles.x, headAngles.y, 0f) * Quaternion.Inverse(frame);
            if (Neck != null)
            {
                Quaternion neckOffset = Quaternion.Slerp(Quaternion.identity, headOffset, .12f);
                Neck.rotation = neckOffset * Neck.rotation;
                Head.rotation = Quaternion.Slerp(Quaternion.identity, headOffset, .88f) * Head.rotation;
            }
            else Head.rotation = headOffset * Head.rotation;
        }
    }
}
