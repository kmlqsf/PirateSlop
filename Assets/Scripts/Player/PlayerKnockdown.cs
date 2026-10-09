using System.Linq;
using UnityEngine;

namespace PirateSlop
{
    [DefaultExecutionOrder(10000)]
    public sealed class PlayerKnockdown : MonoBehaviour
    {
        AdvancedPlayerController motor;
        GameObject ragdoll;
        Transform fallenHips;
        Rigidbody pelvis;
        Renderer[] hidden;
        bool active;

        void Awake() => motor = GetComponent<AdvancedPlayerController>();

        public void Begin(Vector3 velocity)
        {
            if (active || motor == null || motor.IsDead) return;
            ragdoll = DeathRagdoll.CreateKnockdown(transform, transform.position, velocity, new Vector3(.5f, 0f, 1f));
            if (ragdoll == null) return;
            ragdoll.name = "PirateKnockdown";
            fallenHips = DeathRagdoll.FindBone(ragdoll.transform, "Hips");
            pelvis = fallenHips != null ? fallenHips.GetComponent<Rigidbody>() : null;
            if (motor.IsLocal && !motor.IsThirdPerson) ragdoll.AddComponent<FirstPersonModelVisibility>().Configure(motor.PlayerCamera);
            hidden = GetComponentsInChildren<Renderer>(true).Where(r => r.enabled).ToArray();
            active = true;
        }

        public bool TryGetPhysicsState(bool recovering, out Vector3 position, out Vector3 velocity)
        {
            position = transform.position;
            velocity = Vector3.zero;
            if (!active || fallenHips == null || pelvis == null) return false;
            position = fallenHips.position - Vector3.up * .35f;
            velocity = pelvis.linearVelocity;
            if (recovering)
            {
                float nearest = 1.2f;
                foreach (var hit in Physics.RaycastAll(fallenHips.position + Vector3.up * .2f, Vector3.down, nearest, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.GetComponentInParent<Networking.NetworkPlayer>() != null || hit.normal.y < .55f || hit.distance >= nearest) continue;
                    nearest = hit.distance;
                    position = new Vector3(fallenHips.position.x, hit.point.y + .05f, fallenHips.position.z);
                }
            }
            return true;
        }

        void LateUpdate()
        {
            if (motor == null) return;
            if (motor.IsDead || !motor.IsDowned) { if (active) Stop(); return; }
            if (!active) Begin(motor.KnockdownVelocity);
            if (ragdoll == null) { Stop(); return; }
            foreach (var renderer in hidden) if (renderer != null) renderer.enabled = false;
            var viewArms = GetComponent<WeaponArmRig>()?.ViewArms;
            if (viewArms != null) foreach (var renderer in viewArms.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
        }

        public void Stop()
        {
            if (hidden != null) foreach (var renderer in hidden) if (renderer != null) renderer.enabled = true;
            hidden = null; active = false;
            if (ragdoll != null) Destroy(ragdoll);
            ragdoll = null; fallenHips = null; pelvis = null;
        }

        void OnDisable() => Stop();
    }
}
