using System.Linq;
using UnityEngine;

namespace PirateSlop
{
    [DefaultExecutionOrder(10000)]
    public sealed class PlayerKnockdown : MonoBehaviour
    {
        AdvancedPlayerController motor;
        GameObject ragdoll;
        Transform hips, fallenHips;
        Renderer[] hidden;
        Rigidbody[] bodies;
        Vector3 savedHipsPosition;
        Quaternion savedHipsRotation;
        bool active, recoveringPose;

        void Awake() => motor = GetComponent<AdvancedPlayerController>();

        void Update()
        {
            if (!recoveringPose || hips == null) return;
            hips.SetLocalPositionAndRotation(savedHipsPosition, savedHipsRotation);
            recoveringPose = false;
        }

        void LateUpdate()
        {
            if (motor == null) return;
            if (motor.IsDead) { Stop(); return; }
            if (!motor.IsDowned) { if (active) Stop(); return; }
            if (!active)
            {
                var graphics = transform.Find("PlayerGraphics");
                if (graphics == null) return;
                var arms = GetComponent<WeaponArmRig>()?.ViewArms;
                hips = graphics.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Hips" && (arms == null || !t.IsChildOf(arms)));
                ragdoll = DeathRagdoll.Create(transform, transform.position, motor.KnockdownVelocity, new Vector3(2f, 0f, 3f), 6f);
                if (ragdoll == null) return;
                ragdoll.name = "PirateKnockdown";
                fallenHips = ragdoll.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "Hips");
                bodies = ragdoll.GetComponentsInChildren<Rigidbody>();
                if (motor.IsLocal && !motor.IsThirdPerson) ragdoll.AddComponent<FirstPersonModelVisibility>().Configure(motor.PlayerCamera);
                hidden = GetComponentsInChildren<Renderer>(true).Where(r => r.enabled).ToArray();
                active = true;
            }
            if (ragdoll == null) { Stop(); return; }
            if (fallenHips != null)
            {
                Vector3 delta = Vector3.ProjectOnPlane(transform.position - fallenHips.position, Vector3.up);
                foreach (var body in bodies) if (body != null) body.position += delta;
            }
            bool gettingUp = motor.KnockdownTime < .45f;
            foreach (var renderer in hidden) if (renderer != null) renderer.enabled = gettingUp;
            foreach (var renderer in ragdoll.GetComponentsInChildren<Renderer>()) renderer.enabled = !gettingUp;
            if (gettingUp && hips != null)
            {
                savedHipsPosition = hips.localPosition; savedHipsRotation = hips.localRotation; recoveringPose = true;
                float blend = Mathf.SmoothStep(0f, 1f, motor.KnockdownTime / .45f);
                hips.localPosition = savedHipsPosition + Vector3.down * (.5f * blend);
                hips.localRotation = savedHipsRotation * Quaternion.Euler(55f * blend, 0f, 0f);
            }
            var viewArms = GetComponent<WeaponArmRig>()?.ViewArms;
            if (viewArms != null) foreach (var renderer in viewArms.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
        }

        public void Stop()
        {
            if (recoveringPose && hips != null) hips.SetLocalPositionAndRotation(savedHipsPosition, savedHipsRotation);
            recoveringPose = false;
            if (hidden != null) foreach (var renderer in hidden) if (renderer != null) renderer.enabled = true;
            hidden = null; active = false;
            if (ragdoll != null) Destroy(ragdoll);
            ragdoll = null;
        }

        void OnDisable() => Stop();
    }
}
