using System.Linq;
using UnityEngine;

namespace PirateSlop
{
    [DefaultExecutionOrder(80)]
    public sealed class SabreAnimation : MonoBehaviour
    {
        public Animator BodyAnimator;
        public Vector3 GripPosition = new Vector3(0, .055f, .02f);
        public Quaternion GripRotation = Quaternion.FromToRotation(Vector3.up, Vector3.left);
        PirateWeapon weapon;
        PlayerInventory inventory;
        Transform[] bodyBones, viewBones;
        int layer;
        public bool Active => weapon != null && inventory != null && inventory.SabreSelected && weapon.AnimationEquipped;

        void Awake()
        {
            weapon = GetComponent<PirateWeapon>();
            inventory = GetComponent<PlayerInventory>();
            layer = BodyAnimator.GetLayerIndex("SabreCombat");
            var rig = GetComponent<WeaponArmRig>();
            var body = rig.BodyRig.GetComponentsInChildren<Transform>(true);
            var view = rig.ViewArms.GetComponentsInChildren<Transform>(true);
            var spine = body.First(t => t.name == "Spine");
            bodyBones = spine.GetComponentsInChildren<Transform>(true).Where(b => view.Any(t => t.name == "View_" + b.name)).ToArray();
            viewBones = bodyBones.Select(b => view.First(t => t.name == "View_" + b.name)).ToArray();
        }

        void Start()
        {
            var rig = GetComponent<WeaponArmRig>();
            Mount(weapon.SabreWorldPivot, rig.BodyRig, "Hand.R");
            Mount(weapon.SabreViewPivot, rig.ViewArms, "View_Hand.R");
        }

        void Mount(Transform pivot, Transform rig, string handName)
        {
            var hand = rig.GetComponentsInChildren<Transform>(true).First(t => t.name == handName);
            pivot.SetParent(hand, false);
            pivot.localPosition = GripPosition;
            pivot.localRotation = GripRotation;
        }

        void Update()
        {
            if (layer < 0) return;
            BodyAnimator.SetLayerWeight(layer, Mathf.MoveTowards(BodyAnimator.GetLayerWeight(layer), Active ? 1 : 0, Time.deltaTime * 10));
        }

        public void PlaySlash()
        {
            if (layer < 0) return;
            BodyAnimator.SetLayerWeight(layer, 1);
            BodyAnimator.Play("SabreCombat.Slash", layer, 0);
        }

        void LateUpdate()
        {
            if (!Active) return;
            for (int i = 0; i < bodyBones.Length; i++)
                viewBones[i].localRotation = bodyBones[i].localRotation;
        }
    }
}
