using UnityEngine;

namespace PirateSlop
{
    [DefaultExecutionOrder(60)]
    public sealed class CannonWheelVisual : MonoBehaviour
    {
        public Transform[] Wheels = System.Array.Empty<Transform>();
        public float Radius = .26f;
        Transform support;
        Vector3 previous;
        Quaternion[] rest;
        float angle;
        void OnEnable()
        {
            support = transform.parent;
            previous = support != null ? support.InverseTransformPoint(transform.position) : transform.position;
            rest = new Quaternion[Wheels.Length];
            for (int i = 0; i < Wheels.Length; i++) rest[i] = Wheels[i].localRotation;
            angle = 0f;
        }
        void LateUpdate()
        {
            if (transform.parent != support) { OnEnable(); return; }
            Vector3 current = support != null ? support.InverseTransformPoint(transform.position) : transform.position;
            Vector3 delta = current - previous;
            previous = current;
            if (delta.sqrMagnitude > 1f) return;
            Vector3 forward = support != null ? support.InverseTransformDirection(transform.forward) : transform.forward;
            angle = Mathf.Repeat(angle + Vector3.Dot(delta, forward) / Mathf.Max(.01f, Radius) * Mathf.Rad2Deg, 360f);
            for (int i = 0; i < Wheels.Length; i++) if (Wheels[i] != null) Wheels[i].localRotation = Quaternion.AngleAxis(angle, Vector3.right) * rest[i];
        }
    }
}
