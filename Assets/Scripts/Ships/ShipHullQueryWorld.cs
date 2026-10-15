using System;
using System.Collections.Generic;
using PirateSlop.Performance;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PirateSlop.Ships
{
    public sealed class ShipHullQueryWorld : MonoBehaviour
    {
        const int Capacity = 512;
        sealed class Proxy
        {
            public Collider Source, Shape;
            public int Kind;
            public bool Active;
        }
        static ShipHullQueryWorld instance;
        static double nextSetupIssue;
        readonly Dictionary<Collider, Proxy> bySource = new();
        readonly Dictionary<Collider, Proxy> byShape = new();
        readonly List<Proxy> pool = new();
        readonly List<Proxy> active = new(), pending = new();
        readonly HashSet<ShipController> owners = new();
        Scene scene;
        PhysicsScene physicsScene;
        double nextIssue;

        public static bool Prepare(ShipController owner, Collider[] sources, int count)
        {
            using var timing = PerformanceCpuTimings.Measure(PerformanceCpuSection.ShipHullProxySetup);
            if (count < 0 || count > Capacity)
            {
                if (PerformanceCapture.Active != null && Time.realtimeSinceStartupAsDouble >= nextSetupIssue)
                {
                    nextSetupIssue = Time.realtimeSinceStartupAsDouble + 1;
                    PerformanceCapture.Event("HullQueryFallback:candidate-capacity", owner, value: count);
                }
                return false;
            }
            if (instance == null)
            {
                var host = new GameObject("ShipHullQueryWorld") { hideFlags = HideFlags.HideAndDontSave };
                if (Application.isPlaying) DontDestroyOnLoad(host);
                try
                {
                    instance = host.AddComponent<ShipHullQueryWorld>();
                    instance.scene = SceneManager.CreateScene("ShipHullQueries", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
                    instance.physicsScene = instance.scene.GetPhysicsScene();
                }
                catch (Exception) { instance = null; Destroy(host); return false; }
            }
            return instance.Configure(owner, sources, count);
        }

        static int Kind(Collider source) => source is BoxCollider ? 1 : source is SphereCollider ? 2 : source is CapsuleCollider ? 3 : source is MeshCollider ? 4 : source is TerrainCollider ? 5 : 0;

        static bool Representable(Collider source)
        {
            if (source == null || Kind(source) == 0) return false;
            var t = source.transform;
            var scale = t.lossyScale;
            if (!float.IsFinite(scale.sqrMagnitude) || Mathf.Abs(scale.x * scale.y * scale.z) < .000001f) return false;
            var original = t.localToWorldMatrix;
            var copy = Matrix4x4.TRS(t.position, t.rotation, scale);
            for (int r = 0; r < 3; r++) for (int c = 0; c < 3; c++)
                if (Mathf.Abs(original[r, c] - copy[r, c]) > .00001f * (1f + Mathf.Abs(original[r, c]))) return false;
            return true;
        }

        void Issue(string reason, Collider source)
        {
            if (PerformanceCapture.Active == null || Time.realtimeSinceStartupAsDouble < nextIssue) return;
            nextIssue = Time.realtimeSinceStartupAsDouble + 1;
            PerformanceCapture.Event("HullQueryFallback:" + reason, source);
        }

        Proxy Get(Collider source)
        {
            if (bySource.TryGetValue(source, out var existing)) return existing;
            int kind = Kind(source);
            foreach (var candidate in pool)
            {
                if (candidate.Active || candidate.Kind != kind || pending.Contains(candidate)) continue;
                if (!ReferenceEquals(candidate.Source, null)) bySource.Remove(candidate.Source);
                candidate.Source = source; bySource.Add(source, candidate);
                return candidate;
            }
            if (pool.Count >= Capacity)
                for (int i = 0; i < pool.Count; i++)
                {
                    var old = pool[i];
                    if (old.Active || pending.Contains(old)) continue;
                    if (!ReferenceEquals(old.Source, null)) bySource.Remove(old.Source);
                    byShape.Remove(old.Shape); Destroy(old.Shape.gameObject); pool.RemoveAt(i); break;
                }
            if (pool.Count >= Capacity) return null;
            var root = new GameObject("HullQuerySurface") { hideFlags = HideFlags.HideAndDontSave };
            root.SetActive(false);
            SceneManager.MoveGameObjectToScene(root, scene);
            Collider shape = kind switch
            {
                1 => root.AddComponent<BoxCollider>(),
                2 => root.AddComponent<SphereCollider>(),
                3 => root.AddComponent<CapsuleCollider>(),
                4 => root.AddComponent<MeshCollider>(),
                5 => root.AddComponent<TerrainCollider>(),
                _ => null
            };
            shape.enabled = false; root.SetActive(true);
            var proxy = new Proxy { Source = source, Shape = shape, Kind = kind };
            pool.Add(proxy); bySource.Add(source, proxy); byShape.Add(shape, proxy);
            return proxy;
        }

        static void Copy(Proxy proxy)
        {
            var source = proxy.Source; var target = proxy.Shape;
            var t = source.transform; var copy = target.transform;
            if (!copy.position.Equals(t.position) || !copy.rotation.Equals(t.rotation)) copy.SetPositionAndRotation(t.position, t.rotation);
            if (!copy.localScale.Equals(t.lossyScale)) copy.localScale = t.lossyScale;
            if (target.contactOffset != source.contactOffset) target.contactOffset = source.contactOffset;
            switch (source)
            {
                case BoxCollider box:
                    var b = (BoxCollider)target;
                    if (!b.center.Equals(box.center)) b.center = box.center;
                    if (!b.size.Equals(box.size)) b.size = box.size;
                    break;
                case SphereCollider sphere:
                    var s = (SphereCollider)target;
                    if (!s.center.Equals(sphere.center)) s.center = sphere.center;
                    if (s.radius != sphere.radius) s.radius = sphere.radius;
                    break;
                case CapsuleCollider capsule:
                    var c = (CapsuleCollider)target;
                    if (!c.center.Equals(capsule.center)) c.center = capsule.center;
                    if (c.radius != capsule.radius) c.radius = capsule.radius;
                    if (c.height != capsule.height) c.height = capsule.height;
                    if (c.direction != capsule.direction) c.direction = capsule.direction;
                    break;
                case MeshCollider mesh:
                    var m = (MeshCollider)target;
                    if (m.cookingOptions != mesh.cookingOptions || m.convex != mesh.convex || m.sharedMesh != mesh.sharedMesh)
                    {
                        m.sharedMesh = null;
                        m.cookingOptions = mesh.cookingOptions;
                        m.convex = mesh.convex;
                    }
                    m.sharedMesh = mesh.sharedMesh;
                    break;
                case TerrainCollider terrain:
                    var ground = (TerrainCollider)target;
                    if (ground.terrainData != terrain.terrainData) ground.terrainData = terrain.terrainData;
                    break;
            }
            if (!proxy.Active) { target.enabled = true; proxy.Active = true; }
        }

        void DisableAll()
        {
            foreach (var proxy in pool)
                if (proxy.Active) { proxy.Shape.enabled = false; proxy.Active = false; }
            active.Clear(); pending.Clear();
        }

        bool Configure(ShipController owner, Collider[] sources, int count)
        {
            owners.Add(owner);
            pending.Clear();
            Collider copying = null;
            try
            {
                for (int i = 0; i < count; i++)
                {
                    copying = sources[i];
                    if (!Representable(sources[i])) { Issue("unsupported-shape-or-shear", sources[i]); DisableAll(); return false; }
                    var proxy = Get(sources[i]);
                    if (proxy == null) { Issue("proxy-capacity", sources[i]); DisableAll(); return false; }
                    Copy(proxy); pending.Add(proxy);
                }
                foreach (var old in active)
                    if (!pending.Contains(old)) { old.Shape.enabled = false; old.Active = false; }
                active.Clear(); active.AddRange(pending); pending.Clear();
                Physics.SyncTransforms();
                return physicsScene.IsValid();
            }
            catch (Exception error)
            {
                Issue("copy-failed-" + error.GetType().Name, copying); DisableAll(); return false;
            }
        }

        public static int Cast(Vector3 center, Vector3 extents, Vector3 direction, RaycastHit[] hits, Quaternion rotation, float distance)
            => instance.physicsScene.BoxCast(center, extents, direction, hits, rotation, distance, ~0, QueryTriggerInteraction.Ignore);

        public static Collider Source(Collider shape) => instance != null && shape != null && instance.byShape.TryGetValue(shape, out var proxy) ? proxy.Source : shape;

        public static void Release(ShipController owner)
        {
            if (instance == null) return;
            instance.owners.Remove(owner);
            if (instance.owners.Count == 0) { var host = instance.gameObject; instance = null; Destroy(host); }
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
            foreach (var proxy in pool)
                if (proxy.Shape != null)
                {
                    if (Application.isPlaying) Destroy(proxy.Shape.gameObject);
                    else DestroyImmediate(proxy.Shape.gameObject);
                }
            pool.Clear(); active.Clear(); pending.Clear(); bySource.Clear(); byShape.Clear(); owners.Clear();
            if (Application.isPlaying && scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
        }
    }
}
