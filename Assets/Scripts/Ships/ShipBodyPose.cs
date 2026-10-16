using System.Collections.Generic;
using UnityEngine;

namespace PirateSlop.Ships
{
    public static class ShipBodyPose
    {
        public static bool Enabled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState() => Enabled = false;

        public static void Apply(Rigidbody root, Vector3 position, Quaternion rotation, List<Rigidbody> children, bool contact)
        {
            bool fast = contact && Enabled && root.isKinematic && root.interpolation == RigidbodyInterpolation.None
                && root.gameObject.activeInHierarchy;
            if (!fast)
            {
                root.position = position;
                root.rotation = rotation;
                root.transform.SetPositionAndRotation(position, rotation);
                return;
            }
            using var timing = PirateSlop.Performance.PerformanceCpuTimings.Measure(PirateSlop.Performance.PerformanceCpuSection.ShipBodyPoseFast);
            if (root.GetComponentInChildren<CharacterController>() != null)
            {
                root.position = position;
                root.rotation = rotation;
                root.transform.SetPositionAndRotation(position, rotation);
                return;
            }
            root.GetComponentsInChildren(false, children);
            root.position = position;
            root.rotation = rotation;
            root.PublishTransform();
            foreach (var child in children)
            {
                if (child == null || child == root) continue;
                var transform = child.transform;
                child.position = transform.position;
                child.rotation = transform.rotation;
                child.PublishTransform();
            }
        }
    }
}
