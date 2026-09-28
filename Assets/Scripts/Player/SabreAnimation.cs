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
        PirateWeapon weapon;
        PlayerInventory inventory;
        PirateSlop.Networking.NetworkFishing fishing;
        Transform[] bodyBones, viewBones;
        int layer;
        public bool Active => weapon != null && inventory != null && inventory.SabreSelected && weapon.AnimationEquipped && (fishing == null || !fishing.IsPickingUp);

        void Awake()
        {
            weapon = GetComponent<PirateWeapon>();
            inventory = GetComponent<PlayerInventory>();
            fishing = GetComponent<PirateSlop.Networking.NetworkFishing>();
            layer = BodyAnimator.GetLayerIndex("SabreCombat");
            var rig = GetComponent<WeaponArmRig>();
            if (rig != null && rig.BodyRig != null && rig.ViewArms != null)
            {
                var body = rig.BodyRig.GetComponentsInChildren<Transform>(true);
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
                Mount(weapon.SabreViewPivot, rig.ViewArms, "View_Hand.R");
            }
        }

        void Mount(Transform pivot, Transform rig, params string[] handNames)
        {
            if (pivot == null || rig == null) return;
            var hand = rig.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => handNames.Contains(t.name));
            if (hand == null) return;
            pivot.SetParent(hand, false);
            pivot.localPosition = GripPosition;
            pivot.localRotation = GripRotation;
        }

        void Update()
        {
            if (layer < 0) return;
            bool attacking = Time.time - lastSlashTime < 1.3f;
            float targetWeight = Active && attacking ? 1f : 0f;
            float speed = attacking ? 14f : 4f;
            BodyAnimator.SetLayerWeight(layer, Mathf.MoveTowards(BodyAnimator.GetLayerWeight(layer), targetWeight, Time.deltaTime * speed));
        }

        int comboStep;
        float lastSlashTime = -10f;

        public void PlaySlash()
        {
            if (layer < 0) return;
            if (Time.time - lastSlashTime > 1.4f) comboStep = 0;
            string state = comboStep == 0 ? "SabreCombat.Slash" : "SabreCombat.Slash2";
            BodyAnimator.CrossFadeInFixedTime(state, 0.12f, layer, 0f);
            comboStep = (comboStep + 1) % 2;
            lastSlashTime = Time.time;
        }

        void LateUpdate()
        {
            if (!Active || bodyBones == null || viewBones == null) return;
            for (int i = 0; i < bodyBones.Length; i++)
                viewBones[i].localRotation = bodyBones[i].localRotation;
        }
    }
}
