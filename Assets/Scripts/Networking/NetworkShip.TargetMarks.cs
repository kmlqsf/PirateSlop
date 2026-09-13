using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkShip
    {
        [UnityEngine.SerializeField, Min(1f)] float targetMarkLifetime = 120f;
        [UnityEngine.SerializeField, Range(1, 16)] int maxTargetMarks = 8;
        sealed class TargetMark
        {
            public int Id;
            public Transform Target;
            public Vector3 LocalPoint;
            public float Expires;
        }
        readonly List<TargetMark> targetMarks = new();
        int nextMarkId;

        public void AddTargetMark(RaycastHit hit)
        {
            if (!IsServerInitialized || hit.collider == null) return;
            PruneTargetMarks();
            var networkTarget = hit.collider.GetComponentInParent<NetworkObject>();
            Transform target = networkTarget != null ? networkTarget.transform : hit.collider.transform;
            Vector3 localPoint = target.InverseTransformPoint(hit.point);
            foreach (var mark in targetMarks)
            {
                if (mark.Target != target || (networkTarget == null && Vector3.Distance(mark.LocalPoint, localPoint) > 5f)) continue;
                mark.LocalPoint = localPoint;
                mark.Expires = Time.time + targetMarkLifetime;
                return;
            }
            if (targetMarks.Count >= maxTargetMarks) targetMarks.RemoveAt(0);
            targetMarks.Add(new TargetMark { Id = ++nextMarkId, Target = target, LocalPoint = localPoint, Expires = Time.time + targetMarkLifetime });
        }

        void PruneTargetMarks() => targetMarks.RemoveAll(mark => mark.Target == null || !mark.Target.gameObject.activeInHierarchy || Time.time >= mark.Expires);

        public TargetMarkState[] GetTargetMarks()
        {
            PruneTargetMarks();
            var result = new TargetMarkState[targetMarks.Count];
            for (int i = 0; i < result.Length; i++)
            {
                var mark = targetMarks[i];
                Vector3 position = mark.Target.TransformPoint(mark.LocalPoint);
                result[i] = new TargetMarkState { Id = mark.Id, Position = position, Distance = Vector3.Distance(transform.position, position), SecondsLeft = mark.Expires - Time.time };
            }
            return result;
        }
    }
}
