using System.Linq;
using UnityEngine;

namespace PirateSlop
{
    [DefaultExecutionOrder(80)]
    public sealed class SabreAnimation : MonoBehaviour
    {
        public Animator BodyAnimator;
        public Vector3 GripPosition = new Vector3(0f, 0.055f, 0.015f);
        public Quaternion GripRotation = Quaternion.FromToRotation(Vector3.up, Vector3.left);
        public Vector3 MixamoGripPosition = new Vector3(-0.009597f, 0.086534f, 0.021396f);
        public Quaternion MixamoGripRotation = Quaternion.LookRotation(new Vector3(-0.081345f, 0.918127f, 0.387847f), new Vector3(-0.980509f, -0.143544f, 0.134156f));
        public Vector3 MixamoViewOffset = new Vector3(-0.04f, 0.10f, 0.28f);
        PirateWeapon weapon;
        PlayerInventory inventory;
        PirateSlop.Networking.NetworkFishing fishing;
        Transform[] bodyBones, viewBones;
        Transform viewCamera;
        bool mixamoBody, wasActive;
        int slashFrame = -1;
        int layer;
        static readonly int slashSpeed = Animator.StringToHash("SabreSpeed");
        bool supportsSlashSpeed;
        public bool Active => weapon != null && inventory != null && inventory.SabreSelected && weapon.AnimationEquipped && (fishing == null || !fishing.IsPickingUp);

        void Awake()
        {
            weapon = GetComponent<PirateWeapon>();
            inventory = GetComponent<PlayerInventory>();
            fishing = GetComponent<PirateSlop.Networking.NetworkFishing>();
            layer = BodyAnimator != null ? BodyAnimator.GetLayerIndex("SabreCombat") : -1;
            supportsSlashSpeed = BodyAnimator != null && BodyAnimator.parameters.Any(parameter => parameter.nameHash == slashSpeed && parameter.type == AnimatorControllerParameterType.Float);
            var rig = GetComponent<WeaponArmRig>();
            if (rig != null && rig.BodyRig != null && rig.ViewArms != null)
            {
                var body = rig.BodyRig.GetComponentsInChildren<Transform>(true);
                mixamoBody = body.Any(t => t.name == "mixamorig:RightHand");
                var view = rig.ViewArms.GetComponentsInChildren<Transform>(true);
                var spine = body.FirstOrDefault(t => t.name == "Spine" || t.name == "mixamorig:Spine");
                if (spine != null)
                {
                    bodyBones = spine.GetComponentsInChildren<Transform>(true).Where(b => view.Any(t => t.name == "View_" + b.name)).ToArray();
                    viewBones = bodyBones.Select(b => view.First(t => t.name == "View_" + b.name)).ToArray();
                }
            }
            if (bodyBones == null) { bodyBones = new Transform[0]; viewBones = new Transform[0]; }
        }

        void Start()
        {
            var rig = GetComponent<WeaponArmRig>();
            if (rig != null && weapon != null)
            {
                Mount(weapon.SabreWorldPivot, rig.BodyRig, "Hand.R", "mixamorig:RightHand");
                var motor = GetComponent<AdvancedPlayerController>();
                if (mixamoBody && motor != null && motor.PlayerCamera != null && weapon.SabreViewPivot != null)
                {
                    viewCamera = motor.PlayerCamera.transform;
                    weapon.SabreViewPivot.SetParent(viewCamera, false);
                }
                else
                    Mount(weapon.SabreViewPivot, rig.ViewArms, "View_Hand.R");
            }
        }

        void Mount(Transform pivot, Transform rig, params string[] handNames)
        {
            if (pivot == null || rig == null) return;
            var hand = rig.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => handNames.Contains(t.name));
            if (hand == null) return;
            pivot.SetParent(hand, false);
            bool mixamoHand = hand.name == "mixamorig:RightHand";
            pivot.localPosition = mixamoHand ? MixamoGripPosition : GripPosition;
            pivot.localRotation = mixamoHand ? MixamoGripRotation : GripRotation;
        }

        void Update()
        {
            if (layer < 0) return;
            if (supportsSlashSpeed) BodyAnimator.SetFloat(slashSpeed, weapon != null ? weapon.SabreSpeed : 1f);
            bool active = Active;
            if (active && !wasActive && slashFrame != Time.frameCount)
                BodyAnimator.CrossFadeInFixedTime("SabreCombat.Ready", 0.12f, layer, 0f);
            wasActive = active;
            float targetWeight = active ? 1f : 0f;
            float speed = active ? 14f : 4f;
            BodyAnimator.SetLayerWeight(layer, Mathf.MoveTowards(BodyAnimator.GetLayerWeight(layer), targetWeight, Time.deltaTime * speed));
        }

        int comboStep;
        float lastSlashTime = -10f;

        public void PlaySlash()
        {
            if (layer < 0) return;
            if (Time.time - lastSlashTime > 1.4f) comboStep = 0;
            string state = comboStep == 0 ? "SabreCombat.Slash" : "SabreCombat.Slash2";
            float speed = weapon != null ? weapon.SabreSpeed : 1f;
            if (supportsSlashSpeed) BodyAnimator.SetFloat(slashSpeed, speed);
            BodyAnimator.CrossFadeInFixedTime(state, 0.12f / speed, layer, 0f);
            comboStep = (comboStep + 1) % 2;
            lastSlashTime = Time.time;
            slashFrame = Time.frameCount;
        }

        void LateUpdate()
        {
            if (!Active) return;
            if (mixamoBody && viewCamera != null && weapon.SabreWorldPivot != null && weapon.SabreViewPivot != null)
            {
                var source = weapon.SabreWorldPivot;
                var target = weapon.SabreViewPivot;
                var basis = BodyAnimator.transform.parent != null ? BodyAnimator.transform.parent : transform;
                target.localPosition = basis.InverseTransformPoint(source.position)
                    - basis.InverseTransformPoint(viewCamera.position) + MixamoViewOffset;
                target.localRotation = Quaternion.Inverse(basis.rotation) * source.rotation;
                return;
            }
            if (bodyBones == null || viewBones == null) return;
            for (int i = 0; i < bodyBones.Length; i++)
                viewBones[i].localRotation = bodyBones[i].localRotation;
        }
    }
}
