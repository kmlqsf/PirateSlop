using System.Collections.Generic;
using PirateSlop.Networking;
using UnityEngine;

namespace PirateSlop.Ships
{
    public sealed partial class ShipMonkey
    {
        public Transform Neck, Head, LeftEye, RightEye;
        public Vector3 HeadForward = Vector3.forward;
        public Vector3 HeadUp = Vector3.up;
        public Vector3 LeftEyeForward = Vector3.forward;
        public Vector3 RightEyeForward = Vector3.forward;
        [Min(.5f)] public float NoticeRadius = 3.2f;
        [Range(0f, 1f)] public float NoticeChance = .45f;
        [Range(0f, 80f)] public float HeadYawLimit = 55f;
        [Range(0f, 50f)] public float HeadPitchLimit = 32f;
        [Range(0f, 20f)] public float EyeYawLimit = 12f;
        [Range(0f, 15f)] public float EyePitchLimit = 8f;
        readonly HashSet<NetworkPlayer> nearbyPlayers = new();
        readonly HashSet<NetworkPlayer> nextNearbyPlayers = new();
        NetworkPlayer watchedPlayer;
        bool looking;
        Vector3 lookPoint;
        float attentionScan, attentionRemaining, attentionCooldown;
        Vector2 headAngles, eyeAngles;

        void ResetLook()
        {
            nearbyPlayers.Clear(); nextNearbyPlayers.Clear(); watchedPlayer = null;
            looking = false; attentionScan = attentionRemaining = 0f;
            attentionCooldown = 1.5f; headAngles = eyeAngles = Vector2.zero;
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

        void UpdateAttention(float delta)
        {
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
                if (Mathf.Abs(yaw) <= 85f) wanted = new Vector2(Mathf.Clamp(pitch, -HeadPitchLimit, HeadPitchLimit), Mathf.Clamp(yaw, -HeadYawLimit, HeadYawLimit));
            }
            float blend = 1f - Mathf.Exp(-Time.deltaTime * 6f);
            headAngles = Vector2.Lerp(headAngles, wanted, blend);
            Quaternion headOffset = frame * Quaternion.Euler(headAngles.x, headAngles.y, 0f) * Quaternion.Inverse(frame);
            if (Neck != null)
            {
                Quaternion neckOffset = Quaternion.Slerp(Quaternion.identity, headOffset, .22f);
                Neck.rotation = neckOffset * Neck.rotation;
                Head.rotation = Quaternion.Slerp(Quaternion.identity, headOffset, .78f) * Head.rotation;
            }
            else Head.rotation = headOffset * Head.rotation;
            Vector2 eyeWanted = Vector2.zero;
            if (looking)
            {
                Quaternion eyeFrame = Quaternion.LookRotation(Head.TransformDirection(HeadForward), Head.TransformDirection(HeadUp));
                Vector3 direction = Quaternion.Inverse(eyeFrame) * (targetPoint - Head.position);
                if (direction.z > 0f)
                    eyeWanted = new Vector2(Mathf.Clamp(-Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude) * Mathf.Rad2Deg, -EyePitchLimit, EyePitchLimit), Mathf.Clamp(Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg, -EyeYawLimit, EyeYawLimit));
            }
            eyeAngles = Vector2.Lerp(eyeAngles, eyeWanted, 1f - Mathf.Exp(-Time.deltaTime * 14f));
            AimEye(LeftEye, LeftEyeForward);
            AimEye(RightEye, RightEyeForward);
        }

        void AimEye(Transform eye, Vector3 localForward)
        {
            if (eye == null) return;
            Quaternion frame = Quaternion.LookRotation(eye.TransformDirection(localForward), Head.TransformDirection(HeadUp));
            eye.rotation = frame * Quaternion.Euler(eyeAngles.x, eyeAngles.y, 0f) * Quaternion.Inverse(frame) * eye.rotation;
        }
    }
}
