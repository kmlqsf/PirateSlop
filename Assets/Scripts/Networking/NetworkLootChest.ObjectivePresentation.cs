using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkLootChest
    {
        float floatVelocity;
        float fallVelocity, nextLooseFloorProbe;
        Vector3 looseFloorAnchor, looseFloorLocalPoint;
        Collider looseFloor;
        FishNet.Object.NetworkObject presentedCarrier;
        NetworkPlayer presentedCarrierPlayer;
        static readonly RaycastHit[] looseFloorHits = new RaycastHit[64];
        SeaLootState lastPhase;
        bool phaseKnown;

        void UpdateCarriedChestPose()
        {
            var holder = carrier.Value;
            if (holder == null) return;
            if (presentedCarrier != holder)
            {
                presentedCarrier = holder;
                presentedCarrierPlayer = holder.GetComponent<NetworkPlayer>();
            }
            var position = presentedCarrierPlayer != null ? presentedCarrierPlayer.RenderPosition : holder.transform.position;
            transform.SetPositionAndRotation(position + holder.transform.forward * .9f + holder.transform.right * .35f + Vector3.up * .65f, holder.transform.rotation);
        }

        void ResetLooseChestMotion()
        {
            floatVelocity = fallVelocity = nextLooseFloorProbe = 0f;
            looseFloor = null;
        }

        bool IsLooseChestFloor(RaycastHit hit)
        {
            if (hit.collider == null || hit.normal.y < .45f || hit.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<AdvancedPlayerController>() != null) return false;
            var ship = hit.collider.GetComponentInParent<NetworkShip>();
            return ship == null || !ship.IsSinking;
        }

        void ProbeLooseChestFloor(Vector3 point, float water)
        {
            nextLooseFloorProbe = Time.time + .2f;
            looseFloorAnchor = point;
            looseFloor = null;
            var box = chestCollider as BoxCollider;
            var half = box != null ? Vector3.Scale(box.size * .5f, transform.lossyScale) : new Vector3(.6f, .2f, .32f);
            half = new Vector3(Mathf.Max(.05f, Mathf.Abs(half.x)), .025f, Mathf.Max(.05f, Mathf.Abs(half.z)));
            var rotation = Quaternion.Euler(0, facing.Value.eulerAngles.y, 0);
            float top = Mathf.Max(Mathf.Max(point.y, transform.position.y), water) + .15f;
            var origin = new Vector3(point.x, top + half.y, point.z);
            float distance = top - Mathf.Min(water - 2f, transform.position.y - 2f);
            int count = Physics.BoxCastNonAlloc(origin, half, Vector3.down, looseFloorHits, rotation, distance, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = looseFloorHits[i];
                if (hit.distance >= nearest || !IsLooseChestFloor(hit)) continue;
                nearest = hit.distance;
                looseFloor = hit.collider;
            }
            if (looseFloor != null)
                looseFloorLocalPoint = looseFloor.transform.InverseTransformPoint(new Vector3(point.x, top - nearest + .02f, point.z));
        }

        void UpdateObjectivePresentation()
        {
            if (phaseKnown && lastPhase == SeaLootState.Rising && phase.Value == SeaLootState.Ready && IsClientInitialized)
            {
                GameAudio.Play(SoundCue.WaterSplash, transform.position);
                CombatVfx.Splash(transform.position);
            }
            if (phaseKnown && lastPhase == SeaLootState.Locked && phase.Value == SeaLootState.Rising && IsClientInitialized)
            {
                transform.position = anchor.Value;
                GameAudio.Play(SoundCue.UnderwaterBubbles, transform.position);
            }
            phaseKnown = true;
            lastPhase = phase.Value;
            if (Kind == SeaLootKind.Capture) UpdateCaptureStation();
            if (Kind == SeaLootKind.Sunken)
            {
                UpdateSunkenRope();
                UpdateSunkenGulls();
            }
        }

        void FloatChest(Vector3 point)
        {
            var ocean = OceanSurface.Instance;
            float water = ocean != null ? ocean.Height(point) : eventPoint.Value.y;
            float t = ocean != null ? ocean.WaveTime : Time.time;
            float target = water - .12f + Mathf.Sin(t * 1.7f + eventPoint.Value.x) * .035f;
            if (Time.time >= nextLooseFloorProbe || (looseFloorAnchor - point).sqrMagnitude > .001f)
                ProbeLooseChestFloor(point, water);
            float floor = looseFloor != null && looseFloor.enabled && looseFloor.gameObject.activeInHierarchy
                ? looseFloor.transform.TransformPoint(looseFloorLocalPoint).y : float.NegativeInfinity;
            bool grounded = floor > target;
            if (grounded) target = floor;
            if (transform.position.y > target + (grounded ? .01f : .35f))
            {
                float dt = Mathf.Min(Time.deltaTime, .05f);
                fallVelocity = Mathf.Max(-25f, fallVelocity + Physics.gravity.y * dt);
                point.y = Mathf.Max(target, transform.position.y + fallVelocity * dt);
                floatVelocity = 0f;
            }
            else
            {
                fallVelocity = 0f;
                point.y = grounded ? target : Mathf.SmoothDamp(transform.position.y, target, ref floatVelocity, .32f, 10f, Time.deltaTime);
            }
            if (grounded)
            {
                var groundRotation = Quaternion.Euler(0, facing.Value.eulerAngles.y, 0);
                transform.SetPositionAndRotation(point, groundRotation);
                if (IsServerInitialized && Mathf.Abs(point.y - floor) < .025f)
                {
                    var ship = looseFloor.GetComponentInParent<NetworkShip>();
                    if (ship != null && ship.IsSpawned && !ship.IsSinking)
                    {
                        deck = ship;
                        support.Value = ship.NetworkObject;
                        supportId.Value = ship.ParticipantId.Value;
                        anchor.Value = ship.transform.InverseTransformPoint(point);
                        facing.Value = Quaternion.Inverse(ship.transform.rotation) * groundRotation;
                    }
                }
                return;
            }
            Vector3 normal = Vector3.up;
            if (ocean != null)
            {
                float x = ocean.Height(point - Vector3.right * .6f) - ocean.Height(point + Vector3.right * .6f);
                float z = ocean.Height(point - Vector3.forward * .6f) - ocean.Height(point + Vector3.forward * .6f);
                normal = new Vector3(x, 1.2f, z).normalized;
            }
            var rotation = Quaternion.FromToRotation(Vector3.up, normal) * facing.Value * Quaternion.Euler(Mathf.Sin(t * 1.2f) * 2f, 0, Mathf.Cos(t * 1.5f) * 2f);
            transform.SetPositionAndRotation(point, Quaternion.Slerp(transform.rotation, rotation, 1f - Mathf.Exp(-4f * Time.deltaTime)));
        }
    }
}
