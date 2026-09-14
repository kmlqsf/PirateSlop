using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PirateSlop
{
    [DefaultExecutionOrder(30)]
    public sealed class FirstPersonMotion : MonoBehaviour
    {
        [SerializeField, Range(0f, 2f)] float strength = 1f;
        readonly List<Transform> items = new();
        readonly List<(Transform root, Vector3 position, Quaternion rotation)> saved = new();
        AdvancedPlayerController motor;
        PirateWeapon weapon;
        WeaponArmRig arms;
        Networking.NetworkEquipment equipment;
        FirearmHandling handling;
        Vector3 offset, angles, previousAim;
        Vector2 sway;
        float phase, movement, sprint, landing, previousVertical;
        bool grounded, initialized;
        public float StepPhase => phase;
        public void Register(Transform item) { if (item != null && !items.Contains(item)) items.Add(item); }
        void Awake() => motor = GetComponent<AdvancedPlayerController>();
        void Start()
        {
            weapon = GetComponent<PirateWeapon>(); arms = GetComponent<WeaponArmRig>();
            equipment = GetComponent<Networking.NetworkEquipment>(); handling = GetComponent<FirearmHandling>();
        }
        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += BeforeCamera;
            RenderPipelineManager.endCameraRendering += AfterCamera;
        }
        void OnDisable()
        {
            Restore();
            RenderPipelineManager.beginCameraRendering -= BeforeCamera;
            RenderPipelineManager.endCameraRendering -= AfterCamera;
        }
        void Update()
        {
            bool active = motor.InputActive && !motor.IsThirdPerson && !motor.IsSwimming && !motor.IsClimbing && !motor.LocomotionLocked;
            float dt = Mathf.Min(Time.deltaTime, .05f);
            var aim = motor.AimEuler;
            if (!initialized) { previousAim = aim; grounded = motor.IsGrounded; initialized = true; }
            var turn = new Vector2(Mathf.DeltaAngle(previousAim.y, aim.y), Mathf.DeltaAngle(previousAim.x, aim.x)) / Mathf.Max(.001f, dt);
            previousAim = aim;
            sway = Vector2.Lerp(sway, active ? Vector2.ClampMagnitude(turn / 180f, 1f) : Vector2.zero, 1f - Mathf.Exp(-10f * dt));
            float speed = active && motor.IsGrounded && !motor.IsSliding ? motor.PlanarSpeed : 0f;
            movement = Mathf.Lerp(movement, Mathf.Clamp01(speed / 8f), 1f - Mathf.Exp(-12f * dt));
            phase = Mathf.Repeat(phase + speed * dt * Mathf.PI / 2f, Mathf.PI * 2f);
            float focus = handling != null ? handling.AimBlend : 0f;
            sprint = Mathf.Lerp(sprint, active && motor.IsSprinting && speed > 5f ? 1f - focus : 0f, 1f - Mathf.Exp(-9f * dt));
            if (active && motor.IsGrounded && !grounded) landing = Mathf.Clamp(-previousVertical * .006f, 0f, .065f);
            grounded = motor.IsGrounded; previousVertical = motor.VerticalSpeed;
            landing = Mathf.MoveTowards(landing, 0f, dt * .2f);
            float weight = strength * (1f - focus * .94f);
            var target = new Vector3(Mathf.Sin(phase) * .014f * movement - sway.x * .012f,
                Mathf.Cos(phase * 2f) * .012f * movement - landing - sprint * .065f,
                -sprint * .025f - Mathf.Clamp(motor.VerticalSpeed, -8f, 8f) * (active && !grounded ? .0015f : 0f));
            var rotation = new Vector3(sway.y * 1.2f + sprint * 8f + landing * 60f, -sway.x * 1.8f,
                Mathf.Sin(phase) * movement * 1.2f - motor.LocalMove.x * movement * 1.8f);
            offset = Vector3.Lerp(offset, active ? target * weight : Vector3.zero, 1f - Mathf.Exp(-14f * dt));
            angles = Vector3.Lerp(angles, active ? rotation * weight : Vector3.zero, 1f - Mathf.Exp(-14f * dt));
        }
        void BeforeCamera(ScriptableRenderContext context, Camera camera)
        {
            Restore();
            if (camera != motor.PlayerCamera || !camera.enabled || motor.IsThirdPerson || motor.IsDead || !motor.InputActive) return;
            Register(arms != null ? arms.ViewArms : null);
            Register(weapon != null ? weapon.ViewPivot : null);
            Register(weapon != null ? weapon.SabreViewPivot : null);
            Register(equipment != null ? equipment.View : null);
            items.RemoveAll(item => item == null);
            var basis = camera.transform;
            Quaternion turn = basis.rotation * Quaternion.Euler(angles) * Quaternion.Inverse(basis.rotation);
            foreach (var item in items)
            {
                if (!item.gameObject.activeInHierarchy) continue;
                bool nested = false;
                foreach (var other in items) if (other != item && item.IsChildOf(other)) { nested = true; break; }
                if (nested) continue;
                saved.Add((item, item.localPosition, item.localRotation));
                item.SetPositionAndRotation(basis.position + turn * (item.position - basis.position) + basis.TransformVector(offset), turn * item.rotation);
            }
        }
        void AfterCamera(ScriptableRenderContext context, Camera camera) => Restore();
        void Restore()
        {
            foreach (var state in saved) if (state.root != null) { state.root.localPosition = state.position; state.root.localRotation = state.rotation; }
            saved.Clear();
        }
    }
}
