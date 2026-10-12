using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed class NetworkStaffProjectile : NetworkBehaviour
    {
        public StaffSettings Settings;
        public struct FlightState
        {
            public Vector3 Position, Velocity;
        }
        readonly SyncVar<FlightState> flightState = new();
        NetworkPlayer shooter;
        Vector3 forward, desired, inheritedVelocity;
        float age;
        bool spent;
        AudioSource flightAudio;
        float snapshotAt;
        Transform flightVisual;
        Vector3 previousSimulationPosition;

        public void Launch(NetworkPlayer source, Vector3 direction)
        {
            shooter = source;
            forward = desired = direction.sqrMagnitude > .001f ? direction.normalized : source.transform.forward;
            var ship = source.GetComponent<ShipDeckPassenger>()?.Ship;
            inheritedVelocity = ship != null ? ship.GetComponent<ShipController>().CannonPointVelocity(transform.position) : Vector3.zero;
            PublishFlight();
            previousSimulationPosition = transform.position;
            age = 0f;
            spent = false;
        }

        public void Guide(Vector3 direction)
        {
            if (!IsServerInitialized || !float.IsFinite(direction.sqrMagnitude) || direction.sqrMagnitude < .01f) return;
            desired = direction.normalized;
        }

        void PublishFlight() => flightState.Value = new FlightState { Position = transform.position, Velocity = forward * Settings.ProjectileSpeed + inheritedVelocity };

        void ReceiveFlight(FlightState previous, FlightState next, bool asServer)
        {
            if (!asServer) snapshotAt = Time.time;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            flightState.OnChange += ReceiveFlight;
            snapshotAt = Time.time;
            if (!IsServerInitialized) transform.position = flightState.Value.Position;
            if (!Application.isBatchMode && Settings != null)
            {
                StaffVfx.Flight(transform, Settings);
                flightVisual = transform.Find("StaffFlightVfx");
            }
        }

        void Update()
        {
            if (IsClientInitialized && IsSpawned && Settings != null) GameAudio.Loop(ref flightAudio, StaffAudio.Flight(Settings.Item), flightVisual != null ? flightVisual : transform, 1f);
            if (IsSpawned && !IsServerInitialized)
            {
                var state = flightState.Value;
                Vector3 predicted = state.Position + state.Velocity * Mathf.Clamp(Time.time - snapshotAt, 0f, .15f);
                float elapsed = Time.time - snapshotAt;
                if (elapsed <= .15f) transform.position += state.Velocity * Time.deltaTime;
                transform.position = Vector3.Lerp(transform.position, predicted, 1f - Mathf.Exp(-Time.deltaTime * 8f));
            }
        }

        void LateUpdate()
        {
            if (!IsServerInitialized || !IsClientInitialized || spent || flightVisual == null) return;
            float blend = Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime);
            flightVisual.position = Vector3.Lerp(previousSimulationPosition, transform.position, blend);
        }

        public override void OnStopClient()
        {
            flightState.OnChange -= ReceiveFlight;
            StaffAudio.Stop(ref flightAudio);
            foreach (var effect in GetComponentsInChildren<StaffVfxEffect>()) effect.Release();
            flightVisual = null;
            base.OnStopClient();
        }

        void FixedUpdate()
        {
            if (!IsServerInitialized || spent || Settings == null) return;
            previousSimulationPosition = transform.position;
            age += Time.fixedDeltaTime;
            if (shooter == null || !shooter.IsSpawned || age >= Settings.Lifetime) { Finish(false); return; }
            forward = Vector3.RotateTowards(forward, desired, Settings.TurnDegreesPerSecond * Mathf.Deg2Rad * Time.fixedDeltaTime, 0f).normalized;
            Vector3 delta = (forward * Settings.ProjectileSpeed + inheritedVelocity) * Time.fixedDeltaTime;
            Vector3 travelDirection = delta.sqrMagnitude > .000001f ? delta.normalized : forward;
            Vector3 start = transform.position;
            RaycastHit nearest = default;
            float distance = delta.magnitude;
            foreach (var hit in Physics.SphereCastAll(start, Settings.ProjectileRadius, travelDirection, distance, ~0, QueryTriggerInteraction.Collide))
            {
                if (!PlayerHitbox.IsTarget(hit.collider) || hit.collider.gameObject.scene != gameObject.scene || hit.collider.gameObject.layer == LayerMask.NameToLayer("Water")) continue;
                if (hit.transform.IsChildOf(transform) || hit.transform.IsChildOf(shooter.transform) || hit.collider.GetComponentInParent<NetworkStaffProjectile>() != null) continue;
                if (hit.distance <= distance) { nearest = hit; distance = hit.distance; }
            }
            if (ProjectileWaterFlight.IsSubmerged(start, Settings.ProjectileRadius)) { Finish(false, true); return; }
            if (WaterImpactPhysics.Cross(OceanSurface.Instance, start, start + delta, Settings.ProjectileRadius, out var water, out float fraction) && (nearest.collider == null || delta.magnitude * fraction < distance))
            {
                transform.position = water;
                Finish(false, true);
                return;
            }
            if (nearest.collider != null)
            {
                transform.position = start + travelDirection * distance + (nearest.normal.sqrMagnitude > .001f ? nearest.normal.normalized : -travelDirection) * .025f;
                Finish(true);
                return;
            }
            transform.position += delta;
            PublishFlight();
        }

        void Finish(bool damage, bool water = false)
        {
            if (spent) return;
            spent = true;
            if (damage) StaffCombat.Impact(shooter, transform.position, Settings);
            ImpactObserversRpc(transform.position, damage, water);
            ServerManager.Despawn(NetworkObject);
        }

        [ObserversRpc(RunLocally = true)]
        void ImpactObserversRpc(Vector3 point, bool damage, bool water)
        {
            if (IsClientInitialized && Settings != null) { StaffVfx.Impact(point, Settings, damage); GameAudio.Play(damage ? StaffAudio.Impact(Settings.Item) : water ? SoundCue.StaffWater : SoundCue.StaffDissipate, point); }
        }
    }
}
