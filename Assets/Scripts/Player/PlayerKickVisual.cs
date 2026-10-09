using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using PirateSlop.Networking;

namespace PirateSlop
{
    [DefaultExecutionOrder(150)]
    public sealed class PlayerKickVisual : MonoBehaviour
    {
        NetworkPlayer player;
        Transform thigh, calf, foot;
        Quaternion thighPose, calfPose, footPose;
        bool restore;
        float started = -10f;
        SkinnedMeshRenderer leg;

        void Awake() => player = GetComponent<NetworkPlayer>();
        public void Play() => started = Time.time;

        void Update()
        {
            if (!restore || thigh == null) return;
            thigh.localRotation = thighPose;
            calf.localRotation = calfPose;
            foot.localRotation = footPose;
            restore = false;
        }

        void Initialize()
        {
            var rig = GetComponent<WeaponArmRig>();
            if (rig == null || rig.BodyRig == null) return;
            var bones = rig.BodyRig.GetComponentsInChildren<Transform>(true);
            thigh = bones.FirstOrDefault(t => t.name == "mixamorig:RightUpLeg");
            calf = bones.FirstOrDefault(t => t.name == "mixamorig:RightLeg");
            foot = bones.FirstOrDefault(t => t.name == "mixamorig:RightFoot");
            if (thigh == null || calf == null || foot == null) { thigh = null; return; }
            var source = rig.BodyRig.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault();
            var mesh = Resources.Load<Mesh>("KickLeg");
            if (source == null || mesh == null) return;
            var go = new GameObject("KickLegView");
            go.transform.SetParent(source.transform, false);
            leg = go.AddComponent<SkinnedMeshRenderer>();
            leg.sharedMesh = mesh;
            leg.sharedMaterials = source.sharedMaterials;
            leg.bones = source.bones;
            leg.rootBone = source.rootBone;
            leg.localBounds = new Bounds(Vector3.zero, Vector3.one * 5f);
            leg.updateWhenOffscreen = true;
            leg.shadowCastingMode = ShadowCastingMode.Off;
            leg.enabled = false;
        }

        void LateUpdate()
        {
            float age = Time.time - started;
            bool active = player != null && age >= 0f && age < .5f && !player.Motor.IsDead && !player.Motor.IsDowned;
            if (!active) { if (leg != null) leg.enabled = false; return; }
            if (thigh == null) Initialize();
            if (thigh == null) return;
            thighPose = thigh.localRotation; calfPose = calf.localRotation; footPose = foot.localRotation; restore = true;
            float lift = Mathf.SmoothStep(0f, 1f, age / .12f);
            float extend = Mathf.SmoothStep(0f, 1f, (age - .1f) / .08f);
            float returnBlend = Mathf.SmoothStep(0f, 1f, (age - .25f) / .25f);
            Vector3 hip = thigh.position;
            float upper = Vector3.Distance(hip, calf.position), lower = Vector3.Distance(calf.position, foot.position);
            Vector3 heading = player.transform.forward;
            Vector3 target = hip + heading * Mathf.Lerp(.35f, (upper + lower) * .93f, extend) + Vector3.up * Mathf.Lerp(-.2f, .12f, extend);
            target = Vector3.Lerp(foot.position, target, lift * (1f - returnBlend));
            Vector3 direction = target - hip;
            float distance = Mathf.Clamp(direction.magnitude, Mathf.Abs(upper - lower) + .01f, upper + lower - .01f);
            direction.Normalize();
            Vector3 bend = Vector3.ProjectOnPlane(Vector3.up + heading * .2f, direction).normalized;
            float along = (upper * upper - lower * lower + distance * distance) / (2f * distance);
            Vector3 knee = hip + direction * along + bend * Mathf.Sqrt(Mathf.Max(0f, upper * upper - along * along));
            thigh.rotation = Quaternion.FromToRotation(calf.position - hip, knee - hip) * thigh.rotation;
            calf.rotation = Quaternion.FromToRotation(foot.position - calf.position, target - calf.position) * calf.rotation;
            if (leg != null) leg.enabled = player.IsOwner && !player.Motor.IsThirdPerson && player.Motor.InputActive;
        }

        void OnDisable()
        {
            if (restore && thigh != null)
            {
                thigh.localRotation = thighPose; calf.localRotation = calfPose; foot.localRotation = footPose;
            }
            restore = false;
            if (leg != null) leg.enabled = false;
        }
    }
}
