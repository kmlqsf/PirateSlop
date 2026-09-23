using System.Collections.Generic;
using UnityEngine;

namespace PirateSlop
{
    public sealed class ShipLadder : MonoBehaviour
    {
        public static readonly List<ShipLadder> Active = new List<ShipLadder>();
        public float Height = 5, Speed = 2.8f, ExitDepth = 1.2f;
        public float HalfWidth = .7f;
        public bool RopeClimb;
        public bool BoardingAccess;
        public float ExitClearance = 1.35f;
        public float TopLean;
        public Vector3 ExitPoint;
        public float RopeDepth(float height) => TopLean * Mathf.Clamp01(height / Height);
        public bool CanGrab(Vector3 feet, float yaw, float pitch)
        {
            var p = transform.InverseTransformPoint(feet + Vector3.up * 1.4f);
            if (p.z > 2.0f || p.z < -0.5f || Mathf.Abs(p.x) > HalfWidth + 0.6f || p.y < -1f || p.y > Height + 1f) return false;
            var target = transform.TransformPoint(new Vector3(Mathf.Clamp(p.x, -.6f, .6f), Mathf.Clamp(p.y, 0, Height + 1f), RopeDepth(p.y)));
            var origin = feet + Vector3.up * 1.4f;
            var delta = target - origin;
            if (delta.magnitude > (BoardingAccess ? 2.5f : 1.85f) || Vector3.Dot(Quaternion.Euler(pitch, yaw, 0) * Vector3.forward, delta.normalized) < (BoardingAccess ? .15f : .55f)) return false;
            return true;
        }
        public Rigidbody Body { get; private set; }
        void Awake()
        {
            Body = GetComponentInParent<Rigidbody>();
            var col = gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0, Height / 2 - 0.6f, 0f);
            col.size = new Vector3(HalfWidth * 2, Height - 1.2f, 0.2f);
            col.isTrigger = false;
        }
        void OnEnable() { Active.Add(this); }
        void OnDisable() { Active.Remove(this); }
        public bool Contains(Vector3 feet, bool climbing)
        {
            var p = transform.InverseTransformPoint(feet);
            float margin = climbing ? 1.5f : 0f;
            if (BoardingAccess)
                return Mathf.Abs(p.x) <= HalfWidth + margin && p.y >= -1f && p.y <= Height + ExitClearance + .5f
                    && p.z <= 2.1f + margin && p.z >= (p.y > Height - 1.5f ? -ExitDepth - .5f : .15f - margin);
            if (RopeClimb)
            {
                if (p.y < -.35f || p.y > Height + .6f) return false;
                if (p.y > Height - .7f && (p - ExitPoint).sqrMagnitude < 3f) return true;
                return Mathf.Abs(p.x) < .9f + margin && Mathf.Abs(p.z - RopeDepth(p.y) - .45f) < .85f + margin;
            }
            if (Mathf.Abs(p.x) > HalfWidth + margin || p.y < -.35f - margin || p.y > Height + .45f + margin) return false;
            return p.z < 1.25f + margin && p.z > (p.y > Height - 1.5f ? -ExitDepth - .2f : .15f - margin);
        }
    }
}
