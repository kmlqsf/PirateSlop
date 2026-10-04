using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkFish
    {
        sealed class BodyMesh
        {
            public MeshFilter Filter;
            public Mesh Source, Mesh;
            public Vector3[] Points, Normals, Vertices, BentNormals;
            public Matrix4x4 ToBody, FromBody;
        }

        readonly List<BodyMesh> bodyMeshes = new();
        ShipLadder escapeNet;
        NetworkShip escapeShip;
        bool escapeCommitted, bodyInitialized, bodyBent;
        float motionStarted = -1f, bodyMinZ, bodyMaxZ;
        bool tailMotion;
        bool LivingFish => Item == InventoryItem.Fish || Item == InventoryItem.Pufferfish || Item == InventoryItem.Swordfish;

        Vector3 EscapePoint(ShipLadder net) => net.transform.TransformPoint(net.ExitPoint);

        Vector3 EscapeDirection()
        {
            var ship = resolvedPlatform.transform;
            if (escapeShip != resolvedPlatform)
            {
                escapeShip = resolvedPlatform;
                escapeNet = null;
                escapeCommitted = false;
            }
            if (escapeNet == null)
            {
                float nearest = float.PositiveInfinity;
                foreach (var net in ShipLadder.Active)
                {
                    if (net == null || !net.BoardingAccess || net.GetComponentInParent<NetworkShip>() != resolvedPlatform) continue;
                    float distance = Vector3.ProjectOnPlane(EscapePoint(net) - transform.position, ship.up).sqrMagnitude;
                    if (distance >= nearest) continue;
                    nearest = distance;
                    escapeNet = net;
                }
            }
            if (escapeNet == null)
                return ship.right * (ship.InverseTransformPoint(transform.position).x < 0f ? -1f : 1f);
            Vector3 toward = Vector3.ProjectOnPlane(EscapePoint(escapeNet) - transform.position, ship.up);
            if (toward.sqrMagnitude < .36f) escapeCommitted = true;
            return escapeCommitted ? -escapeNet.transform.forward : toward.normalized;
        }

        void SimulateFlop(float dt)
        {
            if (projectile != null && projectile.Flying) return;
            if (projectile != null && projectile.Stuck)
            {
                if (Time.time >= nextFlop)
                {
                    BodyMotionObserversRpc(true);
                    nextFlop = Time.time + Random.Range(2.5f, 5.5f);
                }
                return;
            }
            if (!airborne)
            {
                if (resolvedPlatform == null || Time.time < nextFlop) return;
                var ship = resolvedPlatform.transform;
                velocity = ship.InverseTransformDirection(EscapeDirection()) * Random.Range(.85f, 1.25f);
                velocity += Vector3.up * Random.Range(1.8f, 2.4f);
                airborne = true;
                BodyMotionObserversRpc(false);
                FlopSoundObserversRpc(SoundCue.FishDrop, transform.position);
            }
            int steps = Mathf.Max(1, Mathf.CeilToInt(dt / .02f));
            float step = dt / steps;
            float radius = Item == InventoryItem.Swordfish ? .3f : .14f;
            for (int i = 0; i < steps; i++)
            {
                var support = resolvedPlatform != null ? resolvedPlatform.transform : null;
                velocity += (support != null ? support.InverseTransformDirection(Physics.gravity) : Physics.gravity) * step;
                Vector3 worldVelocity = support != null ? support.TransformDirection(velocity) : velocity;
                Vector3 delta = worldVelocity * step;
                RaycastHit nearest = default;
                float distance = delta.magnitude;
                if (distance > .00001f)
                    foreach (var hit in Physics.SphereCastAll(transform.position, radius, delta / distance, distance, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (hit.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<NetworkFish>() != null) continue;
                        if (hit.distance <= distance) { nearest = hit; distance = hit.distance; }
                    }
                if (nearest.collider != null)
                {
                    if (worldVelocity.y <= 0f && nearest.normal.y > .5f)
                    {
                        var ship = nearest.collider.GetComponentInParent<NetworkShip>();
                        Vector3 head = Vector3.ProjectOnPlane(transform.forward, nearest.normal).normalized;
                        if (head.sqrMagnitude < .01f) head = Vector3.ProjectOnPlane(Vector3.forward, nearest.normal).normalized;
                        var facing = Quaternion.AngleAxis(Random.Range(-18f, 18f), nearest.normal) * Quaternion.LookRotation(head, nearest.normal) * Quaternion.Euler(0f, 0f, 90f);
                        Place(ship != null ? ship.NetworkObject : null, nearest.point + nearest.normal * radius, facing);
                        resolvedPlatform = ship;
                        airborne = false;
                        nextFlop = Time.time + Random.Range(3f, 6f);
                        FlopSoundObserversRpc(SoundCue.FishDrop, transform.position);
                        return;
                    }
                    delta = delta.normalized * Mathf.Max(0f, distance - .01f);
                    worldVelocity = Vector3.Reflect(worldVelocity, nearest.normal) * .35f;
                    velocity = support != null ? support.InverseTransformDirection(worldVelocity) : worldVelocity;
                }
                Vector3 point = transform.position + delta;
                position.Value = support != null ? support.InverseTransformPoint(point) : point;
                worldPosition.Value = point;
                transform.position = point;
                if (support != null && escapeNet != null && escapeCommitted &&
                    Vector3.Dot(point - EscapePoint(escapeNet), -escapeNet.transform.forward) > 1.2f)
                {
                    velocity = support.TransformDirection(velocity) + resolvedPlatform.Motor.CannonPointVelocity(point);
                    Place(null, point, transform.rotation);
                    resolvedPlatform = null;
                }
                var ocean = OceanSurface.Instance;
                if (ocean == null || point.y > ocean.Height(point)) continue;
                foreach (var chest in NetworkLootChest.ServerChests)
                    if (chest != null && chest.IsSpawned && chest.Kind == SeaLootKind.Shark &&
                        Vector3.Distance(point, chest.EventPoint) <= 12f)
                        chest.FeedSharks(Item, point);
                FlopSoundObserversRpc(SoundCue.Splash, point);
                ServerManager.Despawn(NetworkObject);
                return;
            }
        }

        [ObserversRpc(RunLocally = true)]
        void BodyMotionObserversRpc(bool tailOnly)
        {
            motionStarted = Time.time;
            tailMotion = tailOnly;
        }

        void InitializeBodyMotion()
        {
            bodyInitialized = true;
            bodyMinZ = float.PositiveInfinity;
            bodyMaxZ = float.NegativeInfinity;
            foreach (var filter in GetComponentsInChildren<MeshFilter>(true))
            {
                var source = filter.sharedMesh;
                if (source == null || !source.isReadable) continue;
                var mesh = Instantiate(source);
                mesh.name = source.name + "_FishMotion";
                mesh.MarkDynamic();
                var toBody = transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var normalToBody = toBody.inverse.transpose;
                var points = source.vertices;
                var normals = source.normals;
                for (int i = 0; i < points.Length; i++)
                {
                    points[i] = toBody.MultiplyPoint3x4(points[i]);
                    bodyMinZ = Mathf.Min(bodyMinZ, points[i].z);
                    bodyMaxZ = Mathf.Max(bodyMaxZ, points[i].z);
                    if (normals.Length == points.Length) normals[i] = normalToBody.MultiplyVector(normals[i]).normalized;
                }
                var bounds = source.bounds;
                bounds.Expand(bounds.size.magnitude * .6f);
                mesh.bounds = bounds;
                filter.sharedMesh = mesh;
                bodyMeshes.Add(new BodyMesh
                {
                    Filter = filter, Source = source, Mesh = mesh, Points = points, Normals = normals,
                    Vertices = new Vector3[points.Length], BentNormals = new Vector3[normals.Length],
                    ToBody = toBody, FromBody = toBody.inverse
                });
            }
        }

        void AnimateBody()
        {
            float age = Time.time - motionStarted;
            if (motionStarted < 0f || age > 1f || (projectile != null && projectile.Flying))
            {
                RestoreBodyMotion();
                return;
            }
            if (!bodyInitialized) InitializeBodyMotion();
            float pivotZ = Mathf.Lerp(bodyMinZ, bodyMaxZ, tailMotion ? .48f : .85f);
            var pivot = new Vector3(0f, 0f, pivotZ);
            float envelope = Mathf.Sin(Mathf.Clamp01(age) * Mathf.PI);
            foreach (var part in bodyMeshes)
            {
                for (int i = 0; i < part.Points.Length; i++)
                {
                    float tail = Mathf.Clamp01((pivotZ - part.Points[i].z) / Mathf.Max(.01f, pivotZ - bodyMinZ));
                    float angle = Mathf.Sin(age * 27f - tail * 3f) * envelope * (tailMotion ? 24f : 32f) * tail * tail;
                    var bend = Quaternion.AngleAxis(angle, Vector3.up);
                    part.Vertices[i] = part.FromBody.MultiplyPoint3x4(pivot + bend * (part.Points[i] - pivot));
                    if (part.Normals.Length == part.Points.Length)
                        part.BentNormals[i] = part.ToBody.transpose.MultiplyVector(bend * part.Normals[i]).normalized;
                }
                part.Mesh.vertices = part.Vertices;
                if (part.Normals.Length == part.Points.Length) part.Mesh.normals = part.BentNormals;
            }
            bodyBent = true;
        }

        void RestoreBodyMotion()
        {
            if (!bodyBent) return;
            foreach (var part in bodyMeshes)
                if (part.Mesh != null && part.Source != null)
                {
                    part.Mesh.vertices = part.Source.vertices;
                    part.Mesh.normals = part.Source.normals;
                }
            bodyBent = false;
        }

        void OnDestroy()
        {
            foreach (var part in bodyMeshes)
            {
                if (part.Filter != null) part.Filter.sharedMesh = part.Source;
                if (part.Mesh != null) Destroy(part.Mesh);
            }
        }
    }
}
