using PirateSlop.Networking;
using System.Collections.Generic;
using UnityEngine;

namespace PirateSlop.Ships
{
    public sealed class ShipBilgePump : MonoBehaviour
    {
        public Transform LeverPivot, LeverGrip;
        public float DownAngle = -82f, ReturnDuration = .32f;
        public float FullStrokeDuration = .68f;
        public float CycleDuration => Mathf.Max(.1f, FullStrokeDuration) + Mathf.Max(.05f, ReturnDuration);
        public float PullSpeed => 1f / Mathf.Max(.1f, FullStrokeDuration);
        public static IReadOnlyList<ShipBilgePump> Active => active;
        static readonly List<ShipBilgePump> active = new();
        readonly RaycastHit[] reachHits = new RaycastHit[64];
        BoxCollider body;
        public NetworkShip Ship { get; private set; }
        Quaternion rest;
        float displayed;
        float prediction = -1f;
        float predictionReturnAt = -1f;
        public float Clock => Ship != null && Ship.TimeManager != null ? (float)Ship.TimeManager.Tick * (float)Ship.TimeManager.TickDelta : 0f;
        void Awake()
        {
            Ship = GetComponentInParent<NetworkShip>();
            body = GetComponentInChildren<BoxCollider>();
            if (LeverPivot != null) rest = LeverPivot.localRotation;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRegistry() => active.Clear();
        void OnEnable() { if (!active.Contains(this)) active.Add(this); }
        public bool AimedAt(Ray ray, out float distance)
        {
            distance = 0f;
            if (body == null || LeverPivot == null || LeverGrip == null) return false;
            var bounds = body.bounds;
            bounds.Encapsulate(LeverPivot.position);
            bounds.Encapsulate(LeverGrip.position);
            bounds.Expand(.4f);
            return bounds.IntersectRay(ray, out distance) && distance <= 3.25f;
        }
        public float Pull(ShipPumpSnapshot state)
        {
            if (state.Holder != 0) return CyclePull(Clock - state.ReleasedAt);
            if (state.ReturnFrom <= 0f) return state.Pull;
            float progress = Mathf.Clamp01((Clock - state.ReleasedAt) / Mathf.Max(.05f, ReturnDuration));
            return state.ReturnFrom * (1f - Mathf.SmoothStep(0f, 1f, progress));
        }
        public float CyclePull(float elapsed)
        {
            float phase = Mathf.Repeat(elapsed, CycleDuration);
            return phase < FullStrokeDuration ? Mathf.SmoothStep(0f, 1f, phase / FullStrokeDuration) :
                1f - Mathf.SmoothStep(0f, 1f, (phase - FullStrokeDuration) / ReturnDuration);
        }
        public void Predict(float value) { prediction = value; predictionReturnAt = -1f; }
        public void PredictReturn() { prediction = displayed; predictionReturnAt = Time.unscaledTime; }
        void LateUpdate()
        {
            if (LeverPivot == null || Ship == null) return;
            float target = prediction >= 0f ? prediction : Pull(Ship.PumpState);
            if (predictionReturnAt >= 0f)
            {
                float phase = Mathf.Clamp01((Time.unscaledTime - predictionReturnAt) / ReturnDuration);
                target = prediction * (1f - Mathf.SmoothStep(0f, 1f, phase));
                if (phase >= 1f) { prediction = -1f; predictionReturnAt = -1f; }
            }
            displayed = Mathf.Lerp(displayed, target, 1f - Mathf.Exp(-30f * Time.deltaTime));
            LeverPivot.localRotation = rest * Quaternion.AngleAxis(DownAngle * displayed, Vector3.right);
        }
        public bool CanReach(NetworkPlayer player, bool holding = false)
        {
            if (player == null || body == null || LeverPivot == null || LeverGrip == null || Ship == null || !Ship.IsSpawned || Ship.IsSinking || player.Eliminated.Value ||
                player.Motor == null || player.Motor.IsDead || player.Motor.IsDowned || player.Motor.IsFrozen ||
                !holding && (player.Motor.IsSwimming && !ShipWaterInterior.Contains(player.transform.position) || player.Motor.IsClimbing)) return false;
            var weapon = player.GetComponent<NetworkWeapon>();
            var hands = player.GetComponent<CannonHands>();
            var fishing = player.GetComponent<NetworkFishing>();
            return weapon != null && !weapon.LootHandsBusy && (hands == null || !hands.HasHeldBall) &&
                (fishing == null || !fishing.CarryingCatch && !fishing.IsFishing && !fishing.IsEating) &&
                Vector3.Distance(Ship.transform.InverseTransformPoint(player.transform.position) + Vector3.up,
                    Ship.transform.InverseTransformPoint(LeverPivot.position)) <= (holding ? 4.5f : 3f) && (holding || ClearReach(player));
        }
        bool ClearReach(NetworkPlayer player)
        {
            Vector3 origin = player.transform.position + Ship.transform.up * 1.5f;
            Vector3 delta = body.ClosestPoint(origin) - origin;
            if (delta.sqrMagnitude < .0001f) return true;
            int count = Physics.RaycastNonAlloc(origin, delta.normalized, reachHits, delta.magnitude,
                ~(1 << LayerMask.NameToLayer("Water")), QueryTriggerInteraction.Ignore);
            if (count == reachHits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                var hit = reachHits[i];
                if (!hit.transform.IsChildOf(player.transform) && !hit.transform.IsChildOf(transform) &&
                    hit.collider.GetComponentInParent<AdvancedPlayerController>() == null && hit.collider.GetComponentInParent<Cannonball>() == null) return false;
            }
            return true;
        }
        void OnDisable() { active.Remove(this); prediction = -1f; predictionReturnAt = -1f; }
    }
}
