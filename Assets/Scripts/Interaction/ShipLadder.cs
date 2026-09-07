using System.Collections.Generic;
using UnityEngine;

namespace PirateSlop
{
    public sealed class ShipLadder : MonoBehaviour
    {
        public static readonly List<ShipLadder> Active = new List<ShipLadder>();
        public float Height = 5, Speed = 2.8f, ExitDepth = 1.2f;
        public Rigidbody Body { get; private set; }
        void Awake() { Body = GetComponentInParent<Rigidbody>(); }
        void OnEnable() { Active.Add(this); }
        void OnDisable() { Active.Remove(this); }
        public bool Contains(Vector3 feet, bool climbing)
        {
            var p = transform.InverseTransformPoint(feet);
            float margin = climbing ? .35f : 0f;
            if (Mathf.Abs(p.x) > .7f + margin || p.y < -.35f - margin || p.y > Height + .45f + margin) return false;
            return p.z < 1.25f + margin && p.z > (p.y > Height - 1.5f ? -ExitDepth - .2f : .15f - margin);
        }
    }
}
