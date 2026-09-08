using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace PirateSlop
{
    [DefaultExecutionOrder(60)]
    public sealed class WeaponArmRig : MonoBehaviour
    {
        public Transform ViewArms, BodyRig;
        PirateWeapon weapon;
        AdvancedPlayerController motor;
        Renderer[] renderers;
        Arm right, left, viewRight, viewLeft;
        Quaternion gripRotation;
        float weight;
        sealed class Arm
        {
            public Transform Upper, Fore, Hand;
            Quaternion wristRest;
            public Arm(Transform root, string side)
            {
                var bones = root.GetComponentsInChildren<Transform>(true);
                string prefix = root.name == "WeaponViewArms" ? "View_" : "";
                Upper = bones.First(t => t.name == prefix + "UpperArm." + side);
                Fore = bones.First(t => t.name == prefix + "Forearm." + side);
                Hand = bones.First(t => t.name == prefix + "Hand." + side);
                wristRest = Quaternion.Inverse(Fore.rotation) * Hand.rotation;
            }
            public void Solve(Vector3 point, Quaternion rotation, Vector3 pole, float blend)
            {
                Vector3 origin = Upper.position;
                float a = Vector3.Distance(origin, Fore.position), b = Vector3.Distance(Fore.position, Hand.position);
                Vector3 delta = point - origin;
                float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(a - b) + .001f, a + b - .001f);
                Vector3 forward = delta.normalized;
                Vector3 bend = Vector3.ProjectOnPlane(pole - origin, forward).normalized;
                float along = (a * a + distance * distance - b * b) / (2 * distance);
                Vector3 elbow = origin + forward * along + bend * Mathf.Sqrt(Mathf.Max(0, a * a - along * along));
                Quaternion upper = Quaternion.FromToRotation(Fore.position - origin, elbow - origin) * Upper.rotation;
                Upper.rotation = Quaternion.Slerp(Upper.rotation, upper, blend);
                Quaternion fore = Quaternion.FromToRotation(Hand.position - Fore.position, point - Fore.position) * Fore.rotation;
                Fore.rotation = Quaternion.Slerp(Fore.rotation, fore, blend);
                Hand.rotation = Quaternion.Slerp(Hand.rotation, Fore.rotation * wristRest, blend);
            }
        }
        void Awake()
        {
            weapon = GetComponent<PirateWeapon>(); motor = GetComponent<AdvancedPlayerController>();
            right = new Arm(BodyRig, "R"); left = new Arm(BodyRig, "L");
            viewRight = new Arm(ViewArms, "R"); viewLeft = new Arm(ViewArms, "L");
            gripRotation = Quaternion.Euler(90, 0, 0);
            renderers = ViewArms.GetComponentsInChildren<Renderer>(true);
        }
        void OnEnable() { RenderPipelineManager.beginCameraRendering += Before; RenderPipelineManager.endCameraRendering += After; }
        void OnDisable() { RenderPipelineManager.beginCameraRendering -= Before; RenderPipelineManager.endCameraRendering -= After; }
        void Before(ScriptableRenderContext context, Camera camera)
        {
            if (renderers == null) return;
            bool visible = weapon.AnimationEquipped && camera == motor.PlayerCamera && camera.enabled && !motor.IsThirdPerson;
            foreach (var r in renderers) r.forceRenderingOff = !visible;
        }
        void After(ScriptableRenderContext context, Camera camera) { if (renderers != null) foreach (var r in renderers) r.forceRenderingOff = true; }
        void LateUpdate()
        {
            weight = Mathf.MoveTowards(weight, weapon.AnimationEquipped ? 1 : 0, Time.deltaTime * 8);
            if (weight <= 0) return;
            var view = weapon.ActiveView;
            var world = weapon.ActiveWorld;
            Quaternion viewHand = view.rotation * gripRotation;
            viewRight.Solve(view.position - viewHand * Vector3.up * .075f, viewHand, motor.PlayerCamera.transform.TransformPoint(new Vector3(.55f, -.5f, .15f)), 1);
            Vector3 viewLeftTarget = weapon.Reloading ? weapon.ReloadHandPoint : motor.PlayerCamera.transform.TransformPoint(new Vector3(-.25f, -.4f, .32f));
            viewLeft.Solve(viewLeftTarget, motor.PlayerCamera.transform.rotation * gripRotation, motor.PlayerCamera.transform.TransformPoint(new Vector3(-.6f, -.5f, .15f)), 1);
            Vector3 position = transform.TransformPoint(weapon.BodyWeaponPosition);
            Quaternion rotation = transform.rotation * weapon.BodyWeaponRotation;
            Quaternion handRotation = rotation * gripRotation;
            right.Solve(position - handRotation * Vector3.up * .075f, handRotation, transform.TransformPoint(new Vector3(.6f, 1.0f, .1f)), weight);
            world.SetPositionAndRotation(position, rotation);
            if (weapon.Reloading)
            {
                Vector3 point = position + rotation * weapon.ReloadHandOffset;
                left.Solve(point, transform.rotation * gripRotation, transform.TransformPoint(new Vector3(-.65f, 1.05f, .1f)), weight);
            }
        }
    }
}
