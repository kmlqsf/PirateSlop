using System.Collections.Generic;
using UnityEngine;

namespace PirateSlop
{
    public sealed partial class PirateWeapon
    {
        public Transform SabreWorldPivot, SabreViewPivot;
        Renderer[] sabreWorld, sabreView;
        Quaternion sabreWorldRest, sabreViewRest;
        Vector3 sabreViewPosition;
        readonly List<Transform> hammers = new(), triggers = new();
        readonly List<Quaternion> hammerRest = new(), triggerRest = new();
        readonly List<Transform> ramrods = new();
        readonly List<Vector3> ramrodRest = new();
        float reloadVisualStarted;
        bool wasReloading;
        readonly HashSet<CombatHealth> struck = new();
        float attackStarted = -10f, visualAttackStarted = -10f;
        Vector3 attackDirection;
        bool SabreEquipped => inventory != null && inventory.SabreSelected;

        void SetupSeparateWeapons()
        {
            if (SabreWorldPivot != null)
            {
                sabreWorld = SabreWorldPivot.GetComponentsInChildren<Renderer>(true);
                sabreView = SabreViewPivot.GetComponentsInChildren<Renderer>(true);
                sabreWorldRest = SabreWorldPivot.localRotation;
                sabreViewRest = SabreViewPivot.localRotation;
                sabreViewPosition = SabreViewPivot.localPosition;
                AlignSabre(SabreViewPivot);
                AlignSabre(SabreWorldPivot);
                sabreViewRest = Quaternion.identity;
                sabreWorldRest = Quaternion.identity;
            }
            foreach (var pivot in new[] { WorldPivot, ViewPivot })
                foreach (var t in pivot.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "Hammer") { hammers.Add(t); hammerRest.Add(t.localRotation); }
                    if (t.name == "Trigger") { triggers.Add(t); triggerRest.Add(t.localRotation); }
                    if (t.name == "Ramrod" || t.name == "RamrodTip") { ramrods.Add(t); ramrodRest.Add(t.localPosition); }
                }
        }
        void ShowSeparateWeapons(Camera camera, bool show, bool hideWorld)
        {
            foreach (var r in viewRenderers) r.forceRenderingOff |= SabreEquipped;
            foreach (var r in worldRenderers) r.forceRenderingOff |= SabreEquipped;
            if (sabreView == null) return;
            foreach (var r in sabreView) r.forceRenderingOff = !show || !SabreEquipped;
            foreach (var r in sabreWorld) r.forceRenderingOff = hideWorld || !SabreEquipped;
        }
        static void AlignSabre(Transform pivot)
        {
            Transform grip = null, tip = null;
            foreach (var t in pivot.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "GripSocket_Cutlass") grip = t;
                if (t.name == "BladeTipSocket") tip = t;
            }
            if (grip == null || tip == null || pivot.childCount == 0) return;
            var model = pivot.GetChild(0);
            Transform collar = null;
            foreach (var t in pivot.GetComponentsInChildren<Transform>(true))
                if (t.name == "BladeCollar") collar = t;
            Vector3 axis = pivot.InverseTransformVector((collar != null ? collar.position : tip.position) - grip.position);
            if (axis.sqrMagnitude < .0001f) return;
            Transform guard = null;
            foreach (var t in pivot.GetComponentsInChildren<Transform>(true))
                if (t.name == "KnuckleGuard") guard = t;
            var renderer = guard != null ? guard.GetComponent<Renderer>() : null;
            Vector3 edge = renderer != null ? Vector3.ProjectOnPlane(pivot.InverseTransformVector(renderer.bounds.center - grip.position), axis).normalized : Vector3.forward;
            if (edge.sqrMagnitude < .001f) return;
            model.localRotation = Quaternion.Inverse(Quaternion.LookRotation(edge, axis.normalized)) * model.localRotation;
            model.position += pivot.position - grip.position;
        }
        void HideSeparateView()
        {
            foreach (var r in worldRenderers) r.forceRenderingOff |= SabreEquipped;
            if (sabreView == null) return;
            foreach (var r in sabreView) r.forceRenderingOff = true;
            foreach (var r in sabreWorld) r.forceRenderingOff = !Equipped || !SabreEquipped;
        }
        void BeginSabre(Vector3 direction)
        {
            attackStarted = Time.time; attackDirection = direction; struck.Clear();
        }
        void TickSabre()
        {
            float elapsed = Time.time - attackStarted;
            if (!Equipped || !SabreEquipped || motor.IsDead || motor.LocomotionLocked || (hands != null && hands.HasHeldBall)) { attackStarted = -10; return; }
            if (elapsed < .30f || elapsed > .48f) return;
            Vector3 origin = transform.position + Vector3.up * (motor.IsCrouched ? .7f : 1.4f);
            foreach (var collider in Physics.OverlapSphere(origin, 2.4f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (collider.transform.IsChildOf(transform)) continue;
                var health = collider.GetComponentInParent<CombatHealth>();
                if (health == null || struck.Contains(health)) continue;
                Vector3 point = collider.ClosestPoint(origin + attackDirection), delta = point - origin;
                if (Vector3.Angle(attackDirection, delta) > 75f) continue;
                bool blocked = false;
                foreach (var hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                    if (!hit.transform.IsChildOf(transform) && hit.collider.GetComponentInParent<CombatHealth>() != health) { blocked = true; break; }
                if (!blocked) { struck.Add(health); health.ReceiveWeaponHit(25, gameObject); }
            }
        }
        public bool AnimationEquipped => Equipped && !motor.LocomotionLocked && (hands == null || !hands.HasHeldBall);
        public Transform ActiveView => SabreEquipped ? SabreViewPivot : ViewPivot;
        public Transform ActiveWorld => SabreEquipped ? SabreWorldPivot : WorldPivot;
        public Vector3 BodyWeaponPosition { get; private set; }
        public Quaternion BodyWeaponRotation { get; private set; }
        public Vector3 ReloadHandOffset { get; private set; }
        public Vector3 ReloadHandPoint => ViewPivot.TransformPoint(ReloadHandOffset);
        float fireStarted = -10;
        float drawBlend;
        bool lastSabre;
        static Vector3 Pose(float time, float[] times, Vector3[] points)
        {
            for (int i = 1; i < times.Length; i++)
                if (time <= times[i]) return Vector3.LerpUnclamped(points[i - 1], points[i], Mathf.SmoothStep(0, 1, Mathf.InverseLerp(times[i - 1], times[i], time)));
            return points[points.Length - 1];
        }
        static readonly float[] slashTimes = { 0, .12f, .20f, .31f, .43f, .65f };
        static readonly Vector3[] slashAngles = { Vector3.zero, new(-32,-12,15), new(-12,-6,6), new(28,8,-18), new(20,6,-12), Vector3.zero };
        static readonly Vector3[] slashPositions = { Vector3.zero, new(.04f,.065f,-.04f), new(.01f,.025f,.11f), new(-.24f,-.10f,.12f), new(-.18f,-.07f,.04f), Vector3.zero };
        static readonly float[] reloadTimes = { 0, .22f, .55f, 1.0f, 1.45f, 1.95f, 2.4f, 2.7f, 3f };
        static readonly Vector3[] reloadAngles = { Vector3.zero, new(-25,0,-25), new(-50,5,-30), new(-60,4,-20), new(-60,4,-20), new(-60,4,-20), new(-38,0,-18), new(-18,0,-8), Vector3.zero };
        static readonly Vector3[] reloadPositions = { Vector3.zero, new(-.06f,-.035f,-.03f), new(-.08f,-.04f,-.015f), new(-.08f,-.04f,.015f), new(-.08f,-.04f,.015f), new(-.08f,-.04f,.015f), new(-.055f,-.025f,0), new(-.025f,-.015f,0), Vector3.zero };
        static readonly Vector3[] reloadHands = { new(-.25f,-.15f,0), new(-.04f,-.06f,.04f), new(-.04f,.03f,.10f), new(-.025f,.04f,.22f), new(-.025f,.03f,.20f), new(-.025f,.03f,.22f), new(-.04f,.025f,.02f), new(-.07f,-.04f,0), new(-.25f,-.15f,0) };
        void AnimateSeparateWeapons()
        {
            if (reloading && !wasReloading) reloadVisualStarted = Time.time;
            if (lastSabre != SabreEquipped) { drawBlend = 0; visualAttackStarted = -10; }
            lastSabre = SabreEquipped;
            wasReloading = reloading;
            drawBlend = Mathf.MoveTowards(drawBlend, AnimationEquipped ? 1 : 0, Time.deltaTime * 5);
            float reloadTime = reloading ? Mathf.Clamp((Time.time - reloadVisualStarted)*3/Firearm.ReloadDuration, 0, 3) : 3;
            float shot = Time.time - fireStarted;
            float kick = shot < .025f ? Mathf.SmoothStep(0, 1, shot / .025f) : 1 - Mathf.SmoothStep(0, 1, (shot - .025f) / .20f);
            Vector3 angles = Pose(reloadTime, reloadTimes, reloadAngles) + new Vector3(-15, 1.5f, -3) * kick;
            Vector3 position = Pose(reloadTime, reloadTimes, reloadPositions) + new Vector3(0,.02f,-.085f) * kick;
            Vector3 draw = new Vector3(.03f, -.2f, -.08f) * (1 - Mathf.SmoothStep(0, 1, drawBlend));
            Vector3 idle = AnimationEquipped && !reloading ? new Vector3(Mathf.Sin(Time.time * 1.8f) * .0015f, Mathf.Sin(Time.time * 2.2f) * .002f, 0) : Vector3.zero;
            if(!SabreEquipped && handling!=null && handling.Local)
            {
                angles=Pose(reloadTime,reloadTimes,reloadAngles)+handling.PoseRotation;
                position=Pose(reloadTime,reloadTimes,reloadPositions)+handling.PosePosition;
                if(!reloading) position+=Vector3.Lerp(handling.Pistol.HipPosition-viewPosition,handling.Pistol.AimPosition-viewPosition,handling.AimBlend);
            }
            ViewPivot.localRotation = viewRest * Quaternion.Euler(angles);
            ViewPivot.localPosition = viewPosition + position + draw + idle;
            WorldPivot.localRotation = worldRest;
            WorldPivot.localPosition = worldPosition;
            float hammer = loaded ? -32 : 0;
            if (reloading) hammer = -32 * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(2.35f, 2.7f, reloadTime));
            for (int i = 0; i < hammers.Count; i++) hammers[i].localRotation = hammerRest[i] * Quaternion.Euler(hammer, 0, 0);
            for (int i = 0; i < triggers.Count; i++) triggers[i].localRotation = triggerRest[i] * Quaternion.Euler(kick * 15, 0, 0);
            for (int i = 0; i < ramrods.Count; i++) ramrods[i].localPosition = ramrodRest[i];
            ReloadHandOffset = Pose(reloadTime, reloadTimes, reloadHands);
            BodyWeaponPosition = new Vector3(.22f, 1.38f, .38f) + position;
            BodyWeaponRotation = Quaternion.Euler(angles);
            if (SabreViewPivot == null || GetComponent<SabreAnimation>() != null) return;
            float slash = Time.time - visualAttackStarted;
            Vector3 slashAngle = SabreEquipped ? Pose(slash, slashTimes, slashAngles) : Vector3.zero;
            Vector3 slashPosition = SabreEquipped ? Pose(slash, slashTimes, slashPositions) : Vector3.zero;
            SabreViewPivot.localRotation = sabreViewRest * Quaternion.Euler(slashAngle);
            SabreViewPivot.localPosition = sabreViewPosition + slashPosition + draw + idle;
            SabreWorldPivot.localRotation = sabreWorldRest;
            if (SabreEquipped)
            {
                BodyWeaponPosition = new Vector3(.23f, 1.30f, .36f) + slashPosition;
                BodyWeaponRotation = Quaternion.Euler(slashAngle);
            }
        }
    }
}
