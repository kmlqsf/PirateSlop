using System.Linq;
using UnityEngine;
using PirateSlop.Networking;

namespace PirateSlop
{
    [DefaultExecutionOrder(75)]
    public sealed class CharacterActions : MonoBehaviour
    {
        public Animator Animator;
        public Transform BodyProp, ViewProp;
        NetworkEquipment equipment;
        AdvancedPlayerController motor;
        Transform[] body, view;
        Transform hips, chest;
        Vector3 hipsRest;
        Quaternion hipsRestRotation;
        Transform viewRoot;
        Vector3 viewRest;
        Vector3 viewShift;
        int layer;
        string state;
        public bool ControlsEquipment => isActiveAndEnabled && layer >= 0 && equipment != null && equipment.Active && (equipment.Item == InventoryItem.Musket || equipment.Item == InventoryItem.DoubleBarrel);

        void Awake()
        {
            equipment = GetComponent<NetworkEquipment>();
            motor = GetComponent<AdvancedPlayerController>();
            layer = Animator.GetLayerIndex("ItemActions");
            var rig = GetComponent<WeaponArmRig>();
            viewRoot = rig.ViewArms;
            viewRest = viewRoot.localPosition;
            var views = rig.ViewArms.GetComponentsInChildren<Transform>(true).ToDictionary(t => t.name);
            hips = rig.BodyRig.GetComponentsInChildren<Transform>(true).First(t => t.name == "Hips");
            hipsRest = hips.localPosition;
            hipsRestRotation = hips.localRotation;
            body = hips.GetComponentsInChildren<Transform>(true).Where(t => views.ContainsKey("View_" + t.name)).ToArray();
            view = body.Select(t => views["View_" + t.name]).ToArray();
            chest = body.First(t => t.name == "Chest");
        }

        void Update()
        {
            if (layer < 0) return;
            bool active = ControlsEquipment;
            Animator.SetLayerWeight(layer, Mathf.MoveTowards(Animator.GetLayerWeight(layer), active ? 1 : 0, Time.deltaTime * 10));
            if (!active) { state = null; return; }
            string prefix = equipment.Item == InventoryItem.DoubleBarrel ? "Shotgun" : "Musket";
            string next = prefix + (equipment.IsReloading ? "Reload" : equipment.ShotAge < .38f ? "Fire" : equipment.AnimationAiming ? "Aim" : "Ready");
            if (next != state)
            {
                Animator.CrossFadeInFixedTime(next, next.EndsWith("Fire") ? .02f : .12f, layer);
                state = next;
            }
        }

        void OnDisable()
        {
            if (viewRoot != null) viewRoot.localPosition = viewRest;
            viewShift = Vector3.zero;
            if (Animator != null && layer >= 0) Animator.SetLayerWeight(layer, 0);
            state = null;
        }

        void LateUpdate()
        {
            var targetShift = ControlsEquipment ? equipment.AnimationAiming && !equipment.IsReloading ? new Vector3(-.18f, -.13f, .25f) : new Vector3(0, -.13f, .12f) : Vector3.zero;
            viewShift = Vector3.Lerp(viewShift, targetShift, 1 - Mathf.Exp(-20 * Time.deltaTime));
            if (viewRoot != null) viewRoot.localPosition = viewRest + viewShift;
            if (!ControlsEquipment || equipment.World == null || equipment.View == null) return;
            var hipRotation = hips.rotation * Quaternion.Inverse(hips.parent.rotation * hipsRestRotation);
            BodyProp.SetPositionAndRotation(hips.position + hipRotation * (BodyProp.position - hips.parent.TransformPoint(hipsRest)), hipRotation * BodyProp.rotation);
            for (int i = 0; i < body.Length; i++)
                view[i].SetLocalPositionAndRotation(body[i].localPosition, body[i].localRotation);
            ViewProp.SetLocalPositionAndRotation(BodyProp.localPosition, BodyProp.localRotation);
            ViewProp.localScale = BodyProp.localScale;
            if (equipment.AnimationAiming && !equipment.IsReloading)
            {
                float pitch = motor.InputActive ? motor.AimEuler.x : Mathf.Asin(Mathf.Clamp(-equipment.AnimationDirection.y, -1, 1)) * Mathf.Rad2Deg;
                var rotation = Quaternion.AngleAxis(pitch, transform.right);
                var origin = chest.position;
                chest.rotation = rotation * chest.rotation;
                BodyProp.SetPositionAndRotation(origin + rotation * (BodyProp.position - origin), rotation * BodyProp.rotation);
            }
            ApplyProp(equipment.World, BodyProp, BodyProp);
            ApplyProp(equipment.View, ViewProp, BodyProp);
        }

        static void ApplyProp(Transform target, Transform source, Transform parts)
        {
            target.SetPositionAndRotation(source.position, source.rotation);
            var parentScale = target.parent.lossyScale;
            var scale = source.lossyScale;
            target.localScale = new Vector3(scale.x / parentScale.x, scale.y / parentScale.y, scale.z / parentScale.z);
            if (target.childCount == 0) return;
            Transform model = null;
            foreach (Transform child in target)
                if (child.name != "Muzzle") { model = child; break; }
            if (model == null) return;
            model.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            model.localScale = Vector3.one;
            var muzzle = target.Find("Muzzle");
            var authoredMuzzle = model.Find("ActionMuzzle");
            if (muzzle != null && authoredMuzzle != null)
                muzzle.SetPositionAndRotation(authoredMuzzle.position, authoredMuzzle.rotation);
            foreach (Transform part in parts)
            {
                var moving = model.Find(part.name);
                if (moving == null) continue;
                moving.SetLocalPositionAndRotation(part.localPosition, part.localRotation);
                moving.localScale = part.localScale;
            }
        }
    }
}
