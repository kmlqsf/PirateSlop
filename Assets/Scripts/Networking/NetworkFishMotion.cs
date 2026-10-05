using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
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
        readonly SyncVar<bool> diving = new();
        Bounds movementBounds;
        float hopHeight, swimUntil;
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

        Vector3 BodyCenter => Vector3.Scale(movementBounds.center, transform.lossyScale);
        Vector3 BodyExtents => Vector3.Scale(movementBounds.extents, new Vector3(
            Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y), Mathf.Abs(transform.lossyScale.z)));

        float BodyMinimum(Quaternion facing, Vector3 normal)
        {
            var localNormal = Quaternion.Inverse(facing) * normal;
            return Vector3.Dot(facing * BodyCenter, normal) -
                Vector3.Dot(BodyExtents, new Vector3(Mathf.Abs(localNormal.x), Mathf.Abs(localNormal.y), Mathf.Abs(localNormal.z)));
        }

        bool MotionObstacle(Collider collider) => !collider.transform.IsChildOf(transform) &&
            collider.GetComponentInParent<NetworkFish>() == null &&
            collider.GetComponentInParent<AdvancedPlayerController>() == null;

        bool FindFloor(Vector3 candidate, out RaycastHit floor)
        {
            floor = default;
            float bottom = candidate.y + BodyMinimum(transform.rotation, Vector3.up);
            float previousBottom = transform.position.y + BodyMinimum(transform.rotation, Vector3.up);
            float top = -BodyMinimum(transform.rotation, Vector3.down);
            var extents = BodyExtents;
            var x = transform.right * extents.x;
            var y = transform.up * extents.y;
            var z = transform.forward * extents.z;
            float halfX = Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x);
            float halfZ = Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z);
            float highest = float.NegativeInfinity;
            for (int i = 0; i < 5; i++)
            {
                var offset = i == 0 ? Vector3.zero : new Vector3(
                    ((i - 1) & 1) == 0 ? -halfX * .85f : halfX * .85f, 0f,
                    ((i - 1) & 2) == 0 ? -halfZ * .85f : halfZ * .85f);
                var origin = candidate + offset + Vector3.up * (top + .1f);
                float distance = Mathf.Max(.1f, origin.y - bottom + .06f);
                foreach (var hit in Physics.RaycastAll(origin, Vector3.down, distance, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (!MotionObstacle(hit.collider) || hit.normal.y < .5f ||
                        hit.point.y > previousBottom + .08f || hit.point.y < bottom - .025f ||
                        hit.point.y <= highest) continue;
                    floor = hit;
                    highest = hit.point.y;
                }
            }
            return floor.collider != null;
        }

        void Land(Vector3 point, RaycastHit hit)
        {
            var ship = hit.collider.GetComponentInParent<NetworkShip>();
            var head = Vector3.ProjectOnPlane(transform.forward, hit.normal).normalized;
            if (head.sqrMagnitude < .01f) head = Vector3.ProjectOnPlane(Vector3.forward, hit.normal).normalized;
            var facing = Quaternion.LookRotation(head, hit.normal) * Quaternion.Euler(0f, 0f, 90f);
            point += hit.normal * (.025f - Vector3.Dot(point - hit.point, hit.normal) - BodyMinimum(facing, hit.normal));
            Place(ship != null ? ship.NetworkObject : null, point, facing);
            resolvedPlatform = ship;
            airborne = false;
            nextFlop = Time.time + Random.Range(3f, 6f);
            FlopSoundObserversRpc(SoundCue.FishDrop, point);
        }

        void SimulateFlop(float dt)
        {
            if (diving.Value)
            {
                if (Time.time >= swimUntil) { ServerManager.Despawn(NetworkObject); return; }
                Place(null, transform.position + velocity * dt,
                    Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(velocity), 1f - Mathf.Exp(-8f * dt)));
                return;
            }
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
                hopHeight = position.Value.y;
                airborne = true;
                BodyMotionObserversRpc(false);
                FlopSoundObserversRpc(SoundCue.FishDrop, transform.position);
            }
            int steps = Mathf.Max(1, Mathf.CeilToInt(dt / .02f));
            float step = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                var support = resolvedPlatform != null ? resolvedPlatform.transform : null;
                velocity += (support != null ? support.InverseTransformDirection(Physics.gravity) : Physics.gravity) * step;
                Vector3 worldVelocity = support != null ? support.TransformDirection(velocity) : velocity;
                Vector3 delta = worldVelocity * step;
                RaycastHit nearest = default;
                float distance = delta.magnitude;
                if (distance > .00001f)
                    foreach (var hit in Physics.BoxCastAll(transform.position + transform.rotation * BodyCenter,
                        BodyExtents * .98f, delta / distance, transform.rotation, distance, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (!MotionObstacle(hit.collider)) continue;
                        if (hit.distance <= distance && hit.normal.sqrMagnitude > .1f) { nearest = hit; distance = hit.distance; }
                    }
                Vector3 point = transform.position + (nearest.collider != null ?
                    delta.normalized * Mathf.Max(0f, distance - .005f) : delta);
                if (worldVelocity.y <= 0f)
                {
                    if (nearest.collider != null && nearest.normal.y > .5f)
                    {
                        Land(point, nearest);
                        return;
                    }
                    if (FindFloor(point, out var floor))
                    {
                        Land(point, floor);
                        return;
                    }
                }
                if (nearest.collider != null)
                {
                    worldVelocity = Vector3.Reflect(worldVelocity, nearest.normal) * .35f;
                    velocity = support != null ? support.InverseTransformDirection(worldVelocity) : worldVelocity;
                }
                position.Value = support != null ? support.InverseTransformPoint(point) : point;
                worldPosition.Value = point;
                transform.position = point;
                if (support != null && (position.Value.y < hopHeight - .2f ||
                    escapeNet != null && escapeCommitted &&
                    Vector3.Dot(point - EscapePoint(escapeNet), -escapeNet.transform.forward) > 1.2f))
                {
                    velocity = support.TransformDirection(velocity) + resolvedPlatform.Motor.CannonPointVelocity(point);
                    Place(null, point, transform.rotation);
                    resolvedPlatform = null;
                }
                var ocean = OceanSurface.Instance;
                if (resolvedPlatform == null && ocean != null &&
                    point.y + BodyMinimum(transform.rotation, Vector3.up) <= ocean.Height(point))
                {
                    EnterWater(velocity);
                    return;
                }
            }
        }

        public void EnterWater(Vector3 incomingVelocity)
        {
            if (!IsServerInitialized || diving.Value || !LivingFish) return;
            var ocean = OceanSurface.Instance;
            var point = transform.position;
            var splash = point;
            if (ocean != null) splash.y = ocean.Height(point);
            foreach (var chest in NetworkLootChest.ServerChests)
                if (chest != null && chest.IsSpawned && chest.Kind == SeaLootKind.Shark &&
                    Vector3.Distance(splash, chest.EventPoint) <= 12f)
                    chest.FeedSharks(Item, splash);
            var direction = Vector3.ProjectOnPlane(incomingVelocity, Vector3.up);
            if (escapeNet != null) direction = Vector3.ProjectOnPlane(-escapeNet.transform.forward, Vector3.up);
            if (direction.sqrMagnitude < .01f) direction = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (direction.sqrMagnitude < .01f) direction = Vector3.forward;
            velocity = direction.normalized * 1.8f + Vector3.down * 1.8f;
            Place(null, point, transform.rotation);
            resolvedPlatform = null;
            airborne = false;
            diving.Value = true;
            swimUntil = Time.time + 3f;
            expires = float.PositiveInfinity;
            WaterEntryObserversRpc(splash, incomingVelocity);
        }

        [ObserversRpc(RunLocally = true)]
        void WaterEntryObserversRpc(Vector3 point, Vector3 incoming)
        {
            var impactBody = WaterImpactBody.Ensure(gameObject);
            float radius = Mathf.Clamp(Mathf.Sqrt(movementBounds.extents.x * movementBounds.extents.z), .06f, 1.5f);
            WaterImpactPhysics.Report(point, incoming, impactBody.Mass, radius, WaterImpactKind.Object, gameObject);
            GameAudio.Play(SoundCue.Splash, point);
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
            if (!diving.Value && (motionStarted < 0f || age > 1f || (projectile != null && projectile.Flying)))
            {
                RestoreBodyMotion();
                return;
            }
            if (!bodyInitialized) InitializeBodyMotion();
            float pivotZ = Mathf.Lerp(bodyMinZ, bodyMaxZ, tailMotion || diving.Value ? .48f : .85f);
            var pivot = new Vector3(0f, 0f, pivotZ);
            float envelope = diving.Value ? .65f : Mathf.Sin(Mathf.Clamp01(age) * Mathf.PI);
            if (diving.Value) age = Time.time * .4f;
            foreach (var part in bodyMeshes)
            {
                for (int i = 0; i < part.Points.Length; i++)
                {
                    float tail = Mathf.Clamp01((pivotZ - part.Points[i].z) / Mathf.Max(.01f, pivotZ - bodyMinZ));
                    float angle = Mathf.Sin(age * 27f - tail * 3f) * envelope * (diving.Value ? 18f : tailMotion ? 24f : 32f) * tail * tail;
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
