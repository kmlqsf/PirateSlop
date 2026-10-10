using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace PirateSlop
{
    [DefaultExecutionOrder(60)]
    public sealed class WeaponArmRig : MonoBehaviour
    {
        public Transform ViewArms, BodyRig;
        public Transform[] PistolFingers = new Transform[0];
        public Quaternion[] PistolFingerGrip = new Quaternion[0];
        Transform pistolWorldGrip, pistolViewGrip;
        PirateWeapon weapon;
        AdvancedPlayerController motor;
        Renderer[] renderers;
        Arm right, left, viewRight, viewLeft;
        Quaternion gripRotation;
        float weight;
        PirateSlop.Networking.NetworkCrewBell bell;
        Transform[] triggerFingers;
        Quaternion[] triggerRest;
        PlayerInventory inventory;
        SabreAnimation sabreAnimation;
        PirateSlop.Networking.NetworkEquipment equipment;
        PirateSlop.Networking.NetworkHullRepair repair;
        sealed class Arm
        {
            public Transform Upper, Fore, Hand;
            public bool Mixamo;
            public bool Valid => Upper != null && Fore != null && Hand != null;
            public float Reach => Valid ? Vector3.Distance(Upper.position, Fore.position) + Vector3.Distance(Fore.position, Hand.position) - .005f : 0;
            public Vector3 Palm => Mixamo ? new Vector3(-.005f, .075f, .025f) : new Vector3(0, .065f, .025f);
            public Quaternion wristRest;
            public Arm(Transform root, string side)
            {
                if (root == null) return;
                var bones = root.GetComponentsInChildren<Transform>(true);
                string prefix = root.name == "WeaponViewArms" ? "View_" : "";
                string sideFull = side == "R" ? "Right" : "Left";
                Upper = bones.FirstOrDefault(t => t.name == prefix + "UpperArm." + side)
                     ?? bones.FirstOrDefault(t => t.name == "mixamorig:" + sideFull + "Arm");
                Fore = bones.FirstOrDefault(t => t.name == prefix + "Forearm." + side)
                    ?? bones.FirstOrDefault(t => t.name == "mixamorig:" + sideFull + "ForeArm");
                Hand = bones.FirstOrDefault(t => t.name == prefix + "Hand." + side)
                    ?? bones.FirstOrDefault(t => t.name == "mixamorig:" + sideFull + "Hand");
                Mixamo = Hand != null && Hand.name.StartsWith("mixamorig:");
                if (Fore != null && Hand != null)
                    wristRest = Quaternion.Inverse(Fore.rotation) * Hand.rotation;
            }
            public void Solve(Vector3 point, Quaternion rotation, Vector3 pole, float blend, bool orientWrist = false)
            {
                if (Upper == null || Fore == null || Hand == null) return;
                Vector3 origin = Upper.position;
                float a = Vector3.Distance(origin, Fore.position), b = Vector3.Distance(Fore.position, Hand.position);
                Vector3 delta = point - origin;
                float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(a - b) + .001f, a + b - .001f);
                Vector3 forward = delta.normalized;
                if (forward.sqrMagnitude < .001f) return;
                if (orientWrist) point = origin + forward * distance;
                Vector3 bend = Vector3.ProjectOnPlane(pole - origin, forward).normalized;
                if (bend.sqrMagnitude < .001f) bend = Vector3.Cross(forward, Vector3.up).normalized;
                float along = (a * a + distance * distance - b * b) / (2 * distance);
                Vector3 elbow = origin + forward * along + bend * Mathf.Sqrt(Mathf.Max(0, a * a - along * along));
                Quaternion upper = Quaternion.FromToRotation(Fore.position - origin, elbow - origin) * Upper.rotation;
                Upper.rotation = Quaternion.Slerp(Upper.rotation, upper, blend);
                Quaternion fore = Quaternion.FromToRotation(Hand.position - Fore.position, point - Fore.position) * Fore.rotation;
                Fore.rotation = Quaternion.Slerp(Fore.rotation, fore, blend);
                Hand.rotation = Quaternion.Slerp(Hand.rotation, orientWrist ? rotation : Fore.rotation * wristRest, blend);
            }
        }
        void Awake()
        {
            weapon = GetComponent<PirateWeapon>(); motor = GetComponent<AdvancedPlayerController>();
            bell = GetComponent<PirateSlop.Networking.NetworkCrewBell>();
            right = new Arm(BodyRig, "R"); left = new Arm(BodyRig, "L");
            viewRight = new Arm(ViewArms, "R"); viewLeft = new Arm(ViewArms, "L");
            gripRotation = Quaternion.Euler(90, 0, 0);
            renderers = ViewArms.GetComponentsInChildren<Renderer>(true);
            inventory = GetComponent<PlayerInventory>();
            sabreAnimation = GetComponent<SabreAnimation>();
            equipment = GetComponent<PirateSlop.Networking.NetworkEquipment>();
            repair = GetComponent<PirateSlop.Networking.NetworkHullRepair>();
            triggerFingers = ViewArms.GetComponentsInChildren<Transform>(true).Where(t => t.name == "View_Index1.R" || t.name == "View_Index2.R" || t.name == "View_Index3.R").OrderBy(t => t.name).ToArray();
            triggerRest = triggerFingers.Select(t => t.localRotation).ToArray();
            pistolWorldGrip = weapon.WorldPivot.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "GripSocket_Firearm");
            pistolViewGrip = weapon.ViewPivot.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "GripSocket_Firearm");
        }
        void OnEnable() { RenderPipelineManager.beginCameraRendering += Before; RenderPipelineManager.endCameraRendering += After; }
        void OnDisable() { RenderPipelineManager.beginCameraRendering -= Before; RenderPipelineManager.endCameraRendering -= After; }
        void Before(ScriptableRenderContext context, Camera camera)
        {
            if (renderers == null) return;
            bool visible = ((bell != null && bell.IsPulling) || weapon.AnimationEquipped || (equipment != null && equipment.Active && !equipment.Scoped)) && camera == motor.PlayerCamera && camera.enabled && !motor.IsThirdPerson;
            if (motor.SailPullLocked && camera == motor.PlayerCamera && camera.enabled && !motor.IsThirdPerson && !motor.IsDead)
                visible |= inventory.PistolSelected || inventory.SabreSelected || equipment != null && equipment.Item >= PirateSlop.Networking.InventoryItem.Wine;
            bool plankVisible = repair != null && repair.FirstPersonPlank && camera == motor.PlayerCamera && camera.enabled;
            foreach (var r in renderers) r.forceRenderingOff = !plankVisible;
        }
        void After(ScriptableRenderContext context, Camera camera) { if (renderers != null) foreach (var r in renderers) r.forceRenderingOff = true; }
        void LateUpdate()
        {
            if (bell != null && bell.IsPulling)
            {
                var point = bell.HandPoint;
                right.Solve(point, transform.rotation * gripRotation, transform.TransformPoint(new Vector3(.6f, 1.2f, .3f)), 1);
                left.Solve(point + Vector3.up * .12f, transform.rotation * gripRotation, transform.TransformPoint(new Vector3(-.6f, 1.2f, .3f)), 1);
                viewRight.Solve(point, motor.PlayerCamera.transform.rotation * gripRotation, motor.PlayerCamera.transform.TransformPoint(new Vector3(.55f, -.35f, .3f)), 1);
                viewLeft.Solve(point + Vector3.up * .12f, motor.PlayerCamera.transform.rotation * gripRotation, motor.PlayerCamera.transform.TransformPoint(new Vector3(-.55f, -.35f, .3f)), 1);
                return;
            }
            if (GetComponent<PirateSlop.Networking.NetworkFishing>()?.IsPickingUp == true) return;
            if (GetComponent<CharacterActions>() is { ControlsEquipment: true }) return;
            if (repair != null && repair.HeldPlank != null)
            {
                SolvePlank(repair.HeldPlank, right, left, transform);
                SolvePlank(repair.HeldPlank, viewRight, viewLeft, motor.PlayerCamera.transform);
                return;
            }
            if (equipment != null && equipment.Active && equipment.View != null && equipment.World != null)
            {
                SolveEquipment(equipment.View, viewRight, viewLeft, motor.PlayerCamera.transform);
                SolveEquipment(equipment.World, right, left, transform);
                return;
            }
            if (sabreAnimation != null && sabreAnimation.Active) return;
            weight = Mathf.MoveTowards(weight, weapon.AnimationEquipped ? 1 : 0, Time.deltaTime * 8);
            if (weight <= 0) return;
            for (int i = 0; i < triggerFingers.Length; i++)
                triggerFingers[i].localRotation = triggerRest[i] * Quaternion.Euler(inventory.PistolSelected ? (i == 0 ? 30 : i == 1 ? 20 : -10) : 0, 0, 0);
            var view = weapon.ActiveView;
            var world = weapon.ActiveWorld;
            if (inventory.PistolSelected && pistolWorldGrip != null && pistolViewGrip != null)
            {
                for (int i = 0; i < PistolFingers.Length && i < PistolFingerGrip.Length; i++)
                    if (PistolFingers[i] != null) PistolFingers[i].localRotation = PistolFingerGrip[i];
                var palmRotation = Quaternion.AngleAxis(32, Vector3.right) * Quaternion.LookRotation(Vector3.left, Vector3.forward);
                var bodyRotation = transform.rotation * weapon.BodyWeaponRotation;
                var wristRotation = bodyRotation * palmRotation;
                var gripPoint = transform.TransformPoint(weapon.BodyWeaponPosition);
                right.Solve(gripPoint - wristRotation * right.Palm, wristRotation, transform.TransformPoint(new Vector3(.6f, 1.0f, .1f)), weight, true);
                world.SetPositionAndRotation(right.Hand.TransformPoint(right.Palm), bodyRotation);
                world.position += right.Hand.TransformPoint(right.Palm) - pistolWorldGrip.position;
                var viewRotation = view.rotation * palmRotation;
                viewRight.Solve(pistolViewGrip.position - viewRotation * viewRight.Palm, viewRotation, motor.PlayerCamera.transform.TransformPoint(new Vector3(.55f, -.5f, .15f)), 1, true);
                Vector3 otherHand = weapon.Reloading ? weapon.ReloadHandPoint : motor.PlayerCamera.transform.TransformPoint(new Vector3(-.25f, -.4f, .32f));
                viewLeft.Solve(otherHand, motor.PlayerCamera.transform.rotation * gripRotation, motor.PlayerCamera.transform.TransformPoint(new Vector3(-.6f, -.5f, .15f)), 1);
                if (weapon.Reloading) left.Solve(world.TransformPoint(weapon.ReloadHandOffset), transform.rotation * gripRotation, transform.TransformPoint(new Vector3(-.65f, 1.05f, .1f)), weight);
                return;
            }
            Quaternion viewHand = view.rotation * gripRotation;
            viewRight.Solve(view.position - viewHand * Vector3.up * .075f, viewHand, motor.PlayerCamera.transform.TransformPoint(new Vector3(.55f, -.5f, .15f)), 1);
            if (!inventory.PistolSelected) view.position = viewRight.Hand.TransformPoint(new Vector3(0, .065f, .025f));
            Vector3 viewLeftTarget = weapon.Reloading ? weapon.ReloadHandPoint : motor.PlayerCamera.transform.TransformPoint(new Vector3(-.25f, -.4f, .32f));
            viewLeft.Solve(viewLeftTarget, motor.PlayerCamera.transform.rotation * gripRotation, motor.PlayerCamera.transform.TransformPoint(new Vector3(-.6f, -.5f, .15f)), 1);
            Vector3 position = transform.TransformPoint(weapon.BodyWeaponPosition);
            Quaternion rotation = transform.rotation * weapon.BodyWeaponRotation;
            Quaternion handRotation = rotation * gripRotation;
            right.Solve(position - handRotation * Vector3.up * .075f, handRotation, transform.TransformPoint(new Vector3(.6f, 1.0f, .1f)), weight);
            world.SetPositionAndRotation(right.Hand.TransformPoint(new Vector3(0, .065f, .025f)), rotation);
            if (weapon.Reloading)
            {
                Vector3 point = position + rotation * weapon.ReloadHandOffset;
                left.Solve(point, transform.rotation * gripRotation, transform.TransformPoint(new Vector3(-.65f, 1.05f, .1f)), weight);
            }
        }
        void SolveEquipment(Transform item, Arm main, Arm support, Transform basis)
        {
            if (equipment.CanonicalFirearm)
            {
                if (!main.Valid || !support.Valid) return;
                Quaternion mainRotation = item.rotation * Quaternion.LookRotation(Vector3.left, Vector3.forward);
                Quaternion supportRotation = item.rotation * Quaternion.LookRotation(Vector3.up, equipment.ShoulderSupported ? Vector3.forward : Vector3.right);
                bool shouldered = item == equipment.World && equipment.ShoulderSupported && !equipment.IsReloading;
                if (shouldered)
                {
                    Vector3 shoulder = main.Upper.position - basis.right * .035f + basis.up * .045f + basis.forward * .015f;
                    item.position = shoulder + basis.TransformVector(equipment.WorldPoseOffset) - item.rotation * equipment.ShoulderOffset;
                }
                else if (item == equipment.World)
                    for (int i = 0; i < 6; i++)
                    {
                        FitGrip(item, main, item.TransformPoint(equipment.GripOffset) - mainRotation * main.Palm);
                        FitGrip(item, support, item.TransformPoint(equipment.SupportOffset) - supportRotation * support.Palm);
                    }
                Vector3 rightPole = main.Upper.position + basis.right * .4f - basis.up * .45f;
                Vector3 leftPole = support.Upper.position - basis.right * .4f - basis.up * .45f;
                main.Solve(item.TransformPoint(equipment.GripOffset) - mainRotation * main.Palm, mainRotation, rightPole, 1, true);
                Vector3 supportPoint = item.TransformPoint(equipment.SupportOffset) - supportRotation * support.Palm;
                if (shouldered)
                {
                    Vector3 start = item.TransformPoint(equipment.GripOffset) - supportRotation * support.Palm;
                    Vector3 along = supportPoint - start;
                    Vector3 axis = along.normalized;
                    Vector3 offset = start - support.Upper.position;
                    float projection = Vector3.Dot(offset, axis);
                    float discriminant = projection * projection - offset.sqrMagnitude + support.Reach * support.Reach;
                    if (discriminant >= 0)
                    {
                        float distance = -projection + Mathf.Sqrt(discriminant);
                        supportPoint = start + axis * Mathf.Clamp(distance, 0, along.magnitude);
                    }
                }
                support.Solve(supportPoint, supportRotation, leftPole, 1, true);
                return;
            }
            Vector3 pole = main.Upper.position + basis.right * .5f - basis.up * .4f;
            main.Solve(item.TransformPoint(equipment.GripOffset), item.rotation, pole, 1);
            if (equipment.Item == PirateSlop.Networking.InventoryItem.Lantern) return;
            Vector3 otherPole = support.Upper.position - basis.right * .5f - basis.up * .4f;
            support.Solve(item.TransformPoint(equipment.SupportOffset), item.rotation, otherPole, 1);
        }
        static void SolvePlank(Transform item, Arm main, Arm support, Transform basis)
        {
            if (!main.Valid || !support.Valid) return;
            Quaternion rightRotation = item.rotation * Quaternion.Euler(90f, 0f, -90f);
            Quaternion leftRotation = item.rotation * Quaternion.Euler(90f, 0f, 90f);
            main.Solve(item.TransformPoint(new Vector3(.3f, -.028f, 0f)) - rightRotation * main.Palm, rightRotation, main.Upper.position + basis.right * .4f - basis.up * .4f, 1f, true);
            support.Solve(item.TransformPoint(new Vector3(-.3f, -.028f, 0f)) - leftRotation * support.Palm, leftRotation, support.Upper.position - basis.right * .4f - basis.up * .4f, 1f, true);
        }
        static void FitGrip(Transform item, Arm arm, Vector3 point)
        {
            Vector3 delta = point - arm.Upper.position;
            if (delta.magnitude > arm.Reach) item.position += delta.normalized * arm.Reach - delta;
        }
    }
}
