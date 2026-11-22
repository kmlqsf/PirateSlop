using System;
using System.Collections.Generic;
using UnityEngine;

namespace PirateSlop
{
    public readonly struct QueryResults<T>
    {
        static readonly Stack<T[]> pool = new();
        const int Capacity = 256;
        readonly T[] items;
        readonly int count;
        readonly bool pooled;

        QueryResults(T[] items, int count, bool pooled) { this.items = items; this.count = count; this.pooled = pooled; }

        internal static T[] Rent() => pool.Count > 0 ? pool.Pop() : new T[Capacity];
        internal static void Return(T[] buffer) => pool.Push(buffer);
        internal static QueryResults<T> Pooled(T[] buffer, int count) => new(buffer, count, true);
        internal static QueryResults<T> Unpooled(T[] all) => new(all, all.Length, false);

        public Enumerator GetEnumerator() => new(items, count, pooled);

        public struct Enumerator : IDisposable
        {
            T[] items;
            readonly int count;
            readonly bool pooled;
            int index;

            internal Enumerator(T[] items, int count, bool pooled) { this.items = items; this.count = count; this.pooled = pooled; index = -1; }
            public T Current => items[index];
            public bool MoveNext() => ++index < count;
            public void Dispose()
            {
                if (items == null) return;
                if (pooled)
                {
                    Array.Clear(items, 0, count);
                    pool.Push(items);
                }
                items = null;
            }
        }
    }

    public static class PhysicsQuery
    {
        public static QueryResults<RaycastHit> RaycastAll(Vector3 origin, Vector3 direction, float maxDistance = Mathf.Infinity, int layerMask = Physics.DefaultRaycastLayers, QueryTriggerInteraction query = QueryTriggerInteraction.UseGlobal)
        {
            var buffer = QueryResults<RaycastHit>.Rent();
            int count = Physics.RaycastNonAlloc(origin, direction, buffer, maxDistance, layerMask, query);
            if (count < buffer.Length) return QueryResults<RaycastHit>.Pooled(buffer, count);
            QueryResults<RaycastHit>.Return(buffer);
            return QueryResults<RaycastHit>.Unpooled(Physics.RaycastAll(origin, direction, maxDistance, layerMask, query));
        }

        public static QueryResults<RaycastHit> RaycastAll(Ray ray, float maxDistance = Mathf.Infinity, int layerMask = Physics.DefaultRaycastLayers, QueryTriggerInteraction query = QueryTriggerInteraction.UseGlobal)
            => RaycastAll(ray.origin, ray.direction, maxDistance, layerMask, query);

        public static QueryResults<RaycastHit> SphereCastAll(Vector3 origin, float radius, Vector3 direction, float maxDistance = Mathf.Infinity, int layerMask = Physics.DefaultRaycastLayers, QueryTriggerInteraction query = QueryTriggerInteraction.UseGlobal)
        {
            var buffer = QueryResults<RaycastHit>.Rent();
            int count = Physics.SphereCastNonAlloc(origin, radius, direction, buffer, maxDistance, layerMask, query);
            if (count < buffer.Length) return QueryResults<RaycastHit>.Pooled(buffer, count);
            QueryResults<RaycastHit>.Return(buffer);
            return QueryResults<RaycastHit>.Unpooled(Physics.SphereCastAll(origin, radius, direction, maxDistance, layerMask, query));
        }

        public static QueryResults<RaycastHit> SphereCastAll(Ray ray, float radius, float maxDistance = Mathf.Infinity, int layerMask = Physics.DefaultRaycastLayers, QueryTriggerInteraction query = QueryTriggerInteraction.UseGlobal)
            => SphereCastAll(ray.origin, radius, ray.direction, maxDistance, layerMask, query);

        public static QueryResults<RaycastHit> CapsuleCastAll(Vector3 point1, Vector3 point2, float radius, Vector3 direction, float maxDistance = Mathf.Infinity, int layerMask = Physics.DefaultRaycastLayers, QueryTriggerInteraction query = QueryTriggerInteraction.UseGlobal)
        {
            var buffer = QueryResults<RaycastHit>.Rent();
            int count = Physics.CapsuleCastNonAlloc(point1, point2, radius, direction, buffer, maxDistance, layerMask, query);
            if (count < buffer.Length) return QueryResults<RaycastHit>.Pooled(buffer, count);
            QueryResults<RaycastHit>.Return(buffer);
            return QueryResults<RaycastHit>.Unpooled(Physics.CapsuleCastAll(point1, point2, radius, direction, maxDistance, layerMask, query));
        }

        public static QueryResults<RaycastHit> BoxCastAll(Vector3 center, Vector3 halfExtents, Vector3 direction, Quaternion orientation, float maxDistance = Mathf.Infinity, int layerMask = Physics.DefaultRaycastLayers, QueryTriggerInteraction query = QueryTriggerInteraction.UseGlobal)
        {
            var buffer = QueryResults<RaycastHit>.Rent();
            int count = Physics.BoxCastNonAlloc(center, halfExtents, direction, buffer, orientation, maxDistance, layerMask, query);
            if (count < buffer.Length) return QueryResults<RaycastHit>.Pooled(buffer, count);
            QueryResults<RaycastHit>.Return(buffer);
            return QueryResults<RaycastHit>.Unpooled(Physics.BoxCastAll(center, halfExtents, direction, orientation, maxDistance, layerMask, query));
        }

        public static QueryResults<Collider> OverlapSphere(Vector3 position, float radius, int layerMask = Physics.AllLayers, QueryTriggerInteraction query = QueryTriggerInteraction.UseGlobal)
        {
            var buffer = QueryResults<Collider>.Rent();
            int count = Physics.OverlapSphereNonAlloc(position, radius, buffer, layerMask, query);
            if (count < buffer.Length) return QueryResults<Collider>.Pooled(buffer, count);
            QueryResults<Collider>.Return(buffer);
            return QueryResults<Collider>.Unpooled(Physics.OverlapSphere(position, radius, layerMask, query));
        }

        public static QueryResults<Collider> OverlapBox(Vector3 center, Vector3 halfExtents, Quaternion orientation, int layerMask = Physics.AllLayers, QueryTriggerInteraction query = QueryTriggerInteraction.UseGlobal)
        {
            var buffer = QueryResults<Collider>.Rent();
            int count = Physics.OverlapBoxNonAlloc(center, halfExtents, buffer, orientation, layerMask, query);
            if (count < buffer.Length) return QueryResults<Collider>.Pooled(buffer, count);
            QueryResults<Collider>.Return(buffer);
            return QueryResults<Collider>.Unpooled(Physics.OverlapBox(center, halfExtents, orientation, layerMask, query));
        }

        public static QueryResults<Collider> OverlapBox(Vector3 center, Vector3 halfExtents) => OverlapBox(center, halfExtents, Quaternion.identity);

        public static QueryResults<Collider> OverlapCapsule(Vector3 point0, Vector3 point1, float radius, int layerMask = Physics.AllLayers, QueryTriggerInteraction query = QueryTriggerInteraction.UseGlobal)
        {
            var buffer = QueryResults<Collider>.Rent();
            int count = Physics.OverlapCapsuleNonAlloc(point0, point1, radius, buffer, layerMask, query);
            if (count < buffer.Length) return QueryResults<Collider>.Pooled(buffer, count);
            QueryResults<Collider>.Return(buffer);
            return QueryResults<Collider>.Unpooled(Physics.OverlapCapsule(point0, point1, radius, layerMask, query));
        }
    }
}
