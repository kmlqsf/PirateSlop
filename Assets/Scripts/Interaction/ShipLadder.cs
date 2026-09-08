using System.Collections.Generic;
using UnityEngine;

namespace PirateSlop
{
    public sealed class ShipLadder : MonoBehaviour
    {
        public static readonly List<ShipLadder> Active = new List<ShipLadder>();
        public float Height = 5, Speed = 2.8f, ExitDepth = 1.2f;
        public bool RopeClimb;
        public float TopLean;
        public Vector3 ExitPoint;
        public float RopeDepth(float height) => TopLean * Mathf.Clamp01(height / Height);
        public bool CanGrab(Vector3 feet, float yaw, float pitch)
        {
            if (!RopeClimb || !Contains(feet, false)) return false;
            var p = transform.InverseTransformPoint(feet + Vector3.up * 1.4f);
            var target = transform.TransformPoint(new Vector3(Mathf.Clamp(p.x, -.6f, .6f), Mathf.Clamp(p.y, 0, Height + 1f), RopeDepth(p.y)));
            var origin = feet + Vector3.up * 1.4f;
            var delta = target - origin;
            if (delta.magnitude > 1.65f || Vector3.Dot(Quaternion.Euler(pitch, yaw, 0) * Vector3.forward, delta.normalized) < .65f) return false;
            foreach (var hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (hit.collider.attachedRigidbody != null && hit.collider.attachedRigidbody != Body || hit.collider.GetComponentInParent<AdvancedPlayerController>() != null) continue;
                else if (!hit.transform.IsChildOf(transform)) return false;
            return true;
        }
        public Rigidbody Body { get; private set; }
        void Awake() { Body = GetComponentInParent<Rigidbody>(); }
        void OnEnable() { Active.Add(this); }
        void OnDisable() { Active.Remove(this); }
        public bool Contains(Vector3 feet, bool climbing)
        {
            var p = transform.InverseTransformPoint(feet);
            float margin = climbing ? .35f : 0f;
            if (RopeClimb)
            {
                if (p.y < -.35f || p.y > Height + .6f) return false;
                if (p.y > Height - .7f && (p - ExitPoint).sqrMagnitude < 3f) return true;
                return Mathf.Abs(p.x) < .9f + margin && Mathf.Abs(p.z - RopeDepth(p.y) - .45f) < .85f + margin;
            }
            if (Mathf.Abs(p.x) > .7f + margin || p.y < -.35f - margin || p.y > Height + .45f + margin) return false;
            return p.z < 1.25f + margin && p.z > (p.y > Height - 1.5f ? -ExitDepth - .2f : .15f - margin);
        }
    }
}
