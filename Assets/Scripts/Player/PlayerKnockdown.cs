using System.Linq;
using UnityEngine;

namespace PirateSlop
{
    [DefaultExecutionOrder(10000)]
    public sealed class PlayerKnockdown : MonoBehaviour
    {
        AdvancedPlayerController motor;
        GameObject ragdoll;
        Transform fallenHips;
        Transform fallenHead;
        Vector3 cameraOffset;
        Quaternion cameraRotation;
        Rigidbody pelvis;
        Renderer[] hidden;
        bool active;
        bool recovering;
        public bool IsRecovering => recovering;
        float recoveryAge;
        const float RecoverySeconds = .95f;
        Vector3 lastCameraPosition;
        Quaternion lastCameraRotation;
        Transform[] recoveryBones;
        Transform[] recoveryTargets;
        Vector3[] recoveryPositions;
        Quaternion[] recoveryRotations;

        void Awake() => motor = GetComponent<AdvancedPlayerController>();

        public void Begin(Vector3 velocity)
        {
            if (recovering) Stop();
            if (active || motor == null || motor.IsDead) return;
            ragdoll = DeathRagdoll.CreateKnockdown(transform, transform.position, velocity, new Vector3(.5f, 0f, 1f));
            if (ragdoll == null) return;
            ragdoll.name = "PirateKnockdown";
            fallenHips = DeathRagdoll.FindBone(ragdoll.transform, "Hips");
            pelvis = fallenHips != null ? fallenHips.GetComponent<Rigidbody>() : null;
            fallenHead = DeathRagdoll.FindBone(ragdoll.transform, "Head");
            if (motor.IsLocal && motor.PlayerCamera != null && fallenHead != null)
            {
                var camera = motor.PlayerCamera.transform;
                cameraOffset = fallenHead.InverseTransformPoint(fallenHead.position + Vector3.ClampMagnitude(camera.position - fallenHead.position, .2f));
                cameraRotation = Quaternion.Inverse(fallenHead.rotation) * camera.rotation;
            }
            if (motor.IsLocal && !motor.IsThirdPerson) ragdoll.AddComponent<FirstPersonModelVisibility>().Configure(motor.PlayerCamera);
            hidden = GetComponentsInChildren<Renderer>(true).Where(r => r.enabled).ToArray();
            active = true;
        }

        public bool TryGetPhysicsState(bool recovering, out Vector3 position, out Vector3 velocity)
        {
            position = transform.position;
            velocity = Vector3.zero;
            if (!active || fallenHips == null || pelvis == null) return false;
            position = fallenHips.position - Vector3.up * .35f;
            velocity = pelvis.linearVelocity;
            if (recovering)
            {
                float nearest = 1.2f;
                foreach (var hit in Physics.RaycastAll(fallenHips.position + Vector3.up * .2f, Vector3.down, nearest, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.GetComponentInParent<Networking.NetworkPlayer>() != null || hit.normal.y < .55f || hit.distance >= nearest) continue;
                    nearest = hit.distance;
                    position = new Vector3(fallenHips.position.x, hit.point.y + .05f, fallenHips.position.z);
                }
            }
            return true;
        }

        void LateUpdate()
        {
            if (motor == null) return;
            if (motor.IsDead) { if (active) Stop(); return; }
            if (!motor.IsDowned)
            {
                if (active)
                {
                    if (!recovering) BeginRecovery();
                    UpdateRecovery();
                }
                return;
            }
            if (!active) Begin(motor.KnockdownVelocity);
            if (ragdoll == null) { Stop(); return; }
            foreach (var renderer in hidden) if (renderer != null) renderer.enabled = false;
            var viewArms = GetComponent<WeaponArmRig>()?.ViewArms;
            if (viewArms != null) foreach (var renderer in viewArms.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
            PositionCamera();
        }

        public bool PositionCamera()
        {
            if (!active || fallenHead == null || motor == null || !motor.IsLocal || motor.IsThirdPerson || motor.IsDead || !motor.IsDowned || motor.PlayerCamera == null) return false;
            motor.PlayerCamera.transform.SetPositionAndRotation(fallenHead.TransformPoint(cameraOffset), fallenHead.rotation * cameraRotation);
            lastCameraPosition = transform.InverseTransformPoint(motor.PlayerCamera.transform.position);
            lastCameraRotation = Quaternion.Inverse(transform.rotation) * motor.PlayerCamera.transform.rotation;
            return true;
        }

        void BeginRecovery()
        {
            recovering = true;
            recoveryAge = 0f;
            if (ragdoll == null) return;
            foreach (var collider in ragdoll.GetComponentsInChildren<Collider>()) collider.enabled = false;
            foreach (var body in ragdoll.GetComponentsInChildren<Rigidbody>()) body.isKinematic = true;
            var rig = GetComponent<WeaponArmRig>()?.BodyRig ?? transform.Find("PlayerGraphics");
            var targets = rig != null ? rig.GetComponentsInChildren<Transform>(true) : System.Array.Empty<Transform>();
            recoveryBones = ragdoll.GetComponentsInChildren<Transform>(true)
                .Where(bone => targets.Any(target => target.name == bone.name)).ToArray();
            recoveryTargets = recoveryBones.Select(bone => targets.First(target => target.name == bone.name)).ToArray();
            recoveryPositions = recoveryBones.Select(bone => transform.InverseTransformPoint(bone.position)).ToArray();
            recoveryRotations = recoveryBones.Select(bone => Quaternion.Inverse(transform.rotation) * bone.rotation).ToArray();
        }

        void UpdateRecovery()
        {
            if (ragdoll == null) { Stop(); return; }
            recoveryAge += Time.deltaTime;
            float progress = Mathf.Clamp01(recoveryAge / RecoverySeconds);
            float poseBlend = Mathf.SmoothStep(0f, 1f, progress);
            for (int i = 0; recoveryBones != null && i < recoveryBones.Length; i++)
            {
                if (recoveryBones[i] == null || recoveryTargets[i] == null) continue;
                recoveryBones[i].SetPositionAndRotation(
                    Vector3.Lerp(transform.TransformPoint(recoveryPositions[i]), recoveryTargets[i].position, poseBlend),
                    Quaternion.Slerp(transform.rotation * recoveryRotations[i], recoveryTargets[i].rotation, poseBlend));
            }
            if (hidden != null) foreach (var renderer in hidden) if (renderer != null) renderer.enabled = false;
            var viewArms = GetComponent<WeaponArmRig>()?.ViewArms;
            if (viewArms != null) foreach (var renderer in viewArms.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
            if (motor.IsLocal && !motor.IsThirdPerson && motor.PlayerCamera != null)
            {
                var camera = motor.PlayerCamera.transform;
                var origin = transform.TransformPoint(lastCameraPosition);
                var upright = camera.rotation;
                float align = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / .4f));
                float rise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((progress - .25f) / .75f));
                var braced = new Vector3(camera.position.x, Mathf.Lerp(origin.y, camera.position.y, .15f), camera.position.z);
                var position = Vector3.Lerp(Vector3.Lerp(origin, braced, align), camera.position, rise);
                camera.SetPositionAndRotation(position, Quaternion.Slerp(transform.rotation * lastCameraRotation, upright, align));
            }
            if (progress >= 1f) Stop();
        }

        public void Stop()
        {
            if (hidden != null) foreach (var renderer in hidden) if (renderer != null) renderer.enabled = true;
            hidden = null; active = recovering = false;
            recoveryBones = recoveryTargets = null;
            recoveryPositions = null; recoveryRotations = null;
            if (ragdoll != null) Destroy(ragdoll);
            ragdoll = null; fallenHips = null; fallenHead = null; pelvis = null;
        }

        void OnDisable() => Stop();
    }
}
