using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace PirateSlop
{
    public sealed class ShipDebrisPool : MonoBehaviour
    {
        sealed class Chunk
        {
            public GameObject Object;
            public Rigidbody Body;
            public MeshFilter Mesh;
            public MeshRenderer Renderer;
            public BoxCollider Collider;
            public float Expires;
            public bool Splashed;
            public bool Small;
            public bool OwnsMesh;
        }
        ObjectPool<Chunk> pool;
        readonly List<Chunk> active = new();
        ShipDestruction owner;
        Transform root;
        int available;
        public void Initialize(ShipDestruction value)
        {
            if (pool != null) return;
            owner = value;
            root = new GameObject("ShipDebrisPool").transform;
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root.gameObject, gameObject.scene);
            int capacity = owner.Profile.PhysicalLimit + owner.Profile.CosmeticLimit;
            pool = new ObjectPool<Chunk>(Create, null, c => c.Object.SetActive(false), c => { if (c.Object != null) Destroy(c.Object); }, true, capacity, capacity);
            StartCoroutine(Prewarm());
        }
        Chunk Create()
        {
            var go = new GameObject("ShipChunk"); go.SetActive(false); go.transform.SetParent(root, false);
            go.layer = LayerMask.NameToLayer(owner.Profile.DebrisLayer);
            var chunk = new Chunk { Object = go, Mesh = go.AddComponent<MeshFilter>(), Renderer = go.AddComponent<MeshRenderer>(), Collider = go.AddComponent<BoxCollider>(), Body = go.AddComponent<Rigidbody>() };
            chunk.Body.isKinematic = true;
            return chunk;
        }
        IEnumerator Prewarm()
        {
            var warming = new List<Chunk>();
            for (int i = 0; i < owner.Profile.PhysicalLimit + owner.Profile.CosmeticLimit; i++)
            {
                warming.Add(pool.Get());
                if ((i + 1) % Mathf.Max(1, owner.Profile.PrewarmPerFrame) == 0) yield return null;
            }
            foreach (var chunk in warming) pool.Release(chunk);
            available = warming.Count;
        }
        public void Spawn(ShipDamageSection section, ShipDestructionEvent impact)
        {
            if (pool == null || available == 0) return;
            var definition = owner.Definition(section.SectionId);
            var random = new System.Random(impact.Seed);
            var sources = new List<GameObject>();
            if (section.Fragments.Length > 0)
            {
                for (int i = 0; i < section.Fragments.Length; i++) if ((impact.DetachedFragments & (1UL << i)) != 0) sources.Add(section.Fragments[i]);
            }
            else sources.AddRange(section.Debris);
            bool cluster = impact.Reason == ShipDamageReason.SupportLost && sources.Count > 1;
            int count = cluster ? 1 : Mathf.Min(sources.Count, definition.MaxDebris);
            for (int i = 0; i < count; i++)
            {
                var source = sources[i];
                if (source == null || !source.TryGetComponent<MeshFilter>(out var filter)) continue;
                MakeRoom(false);
                var chunk = pool.Get();
                chunk.Small = false; chunk.Collider.enabled = true;
                chunk.OwnsMesh = cluster;
                if (cluster)
                {
                    chunk.Mesh.sharedMesh = Combine(sources, section.transform, out var materials);
                    chunk.Renderer.sharedMaterials = materials;
                }
                else
                {
                    chunk.Mesh.sharedMesh = filter.sharedMesh;
                    chunk.Renderer.sharedMaterials = source.GetComponent<MeshRenderer>().sharedMaterials;
                }
                var origin = cluster ? section.transform : source.transform;
                chunk.Object.transform.SetPositionAndRotation(origin.position, origin.rotation);
                chunk.Object.transform.localScale = origin.lossyScale;
                var bounds = chunk.Mesh.sharedMesh.bounds;
                chunk.Collider.center = bounds.center; chunk.Collider.size = Vector3.Max(bounds.size, Vector3.one * .015f);
                chunk.Body.mass = Mathf.Max(1f, definition.DebrisMass / Mathf.Max(1, count));
                chunk.Body.linearDamping = owner.Profile.LinearDamping; chunk.Body.angularDamping = owner.Profile.AngularDamping;
                chunk.Object.SetActive(true); chunk.Body.isKinematic = false;
                chunk.Body.linearVelocity = impact.PointVelocity + Vector3.Cross(impact.AngularVelocity, chunk.Body.worldCenterOfMass - section.transform.position) + owner.transform.TransformDirection(impact.LocalVelocity.normalized) * definition.DebrisImpulse * impact.Impulse;
                chunk.Body.angularVelocity = new Vector3((float)random.NextDouble() * 2f - 1f, (float)random.NextDouble() * 2f - 1f, (float)random.NextDouble() * 2f - 1f) * definition.AngularSpeed;
                chunk.Expires = Time.time + definition.DebrisLifetime; chunk.Splashed = false;
                active.Add(chunk);
            }
        }
        static Mesh Combine(List<GameObject> sources, Transform origin, out Material[] materials)
        {
            var groups = new Dictionary<Material, List<CombineInstance>>();
            foreach (var source in sources)
            {
                var mesh = source.GetComponent<MeshFilter>().sharedMesh;
                var slots = source.GetComponent<MeshRenderer>().sharedMaterials;
                for (int i = 0; i < mesh.subMeshCount; i++)
                {
                    if (!groups.TryGetValue(slots[i], out var instances)) groups[slots[i]] = instances = new List<CombineInstance>();
                    instances.Add(new CombineInstance { mesh = mesh, subMeshIndex = i, transform = origin.worldToLocalMatrix * source.transform.localToWorldMatrix });
                }
            }
            var combined = new List<CombineInstance>();
            var slotsList = new List<Material>();
            foreach (var group in groups)
            {
                var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.CombineMeshes(group.Value.ToArray(), true, true);
                combined.Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.identity }); slotsList.Add(group.Key);
            }
            var result = new Mesh { name = "DetachedShipAssembly", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            result.CombineMeshes(combined.ToArray(), false, false);
            foreach (var instance in combined) Destroy(instance.mesh);
            materials = slotsList.ToArray();
            return result;
        }
        void MakeRoom(bool small)
        {
            int count = 0, oldest = -1;
            for (int i = 0; i < active.Count; i++) if (active[i].Small == small) { count++; if (oldest < 0) oldest = i; }
            int limit = small ? owner.Profile.CosmeticLimit : owner.Profile.PhysicalLimit;
            if (count >= limit && oldest >= 0) Release(oldest);
            if (active.Count >= available) Release(0);
        }
        public void SpawnSplinters(ShipDestructionEvent impact)
        {
            if (pool == null || available == 0 || owner.Profile.SplinterMeshes.Length == 0) return;
            var random = new System.Random(impact.Seed);
            Vector3 point = owner.transform.TransformPoint(impact.LocalPoint);
            Vector3 normal = owner.transform.TransformDirection(impact.LocalNormal).normalized;
            for (int i = 0; i < owner.Profile.SplintersPerHit; i++)
            {
                MakeRoom(true);
                var chunk = pool.Get();chunk.Small = true;chunk.Collider.enabled = false;
                chunk.Mesh.sharedMesh = owner.Profile.SplinterMeshes[random.Next(owner.Profile.SplinterMeshes.Length)];
                chunk.Renderer.sharedMaterials = new[] { owner.Profile.SplinterMaterial };
                Vector3 spread = new Vector3((float)random.NextDouble()*2f-1f,(float)random.NextDouble()*2f-1f,(float)random.NextDouble()*2f-1f);
                chunk.Object.transform.SetPositionAndRotation(point + normal * .08f + spread * .12f, Quaternion.Euler(spread * 180f));
                chunk.Object.transform.localScale = Vector3.one * Mathf.Lerp(.6f,1.3f,(float)random.NextDouble());
                chunk.Body.mass = .08f;chunk.Body.linearDamping = .15f;chunk.Body.angularDamping = .3f;
                chunk.Object.SetActive(true);chunk.Body.isKinematic = false;
                chunk.Body.linearVelocity = impact.PointVelocity + normal * Mathf.Lerp(2f,6f,(float)random.NextDouble()) + spread * 4f + Vector3.up * 2f;
                chunk.Body.angularVelocity = spread * 12f;
                chunk.Expires = Time.time + 4f + (float)random.NextDouble()*2f;chunk.Splashed = true;
                active.Add(chunk);
            }
        }
        void Update()
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var chunk = active[i]; var point = chunk.Object.transform.position;
                if (Time.time >= chunk.Expires || (Camera.main != null && Vector3.Distance(Camera.main.transform.position, point) > owner.Profile.DebrisDistance)) { Release(i); continue; }
                var ocean = OceanSurface.Instance;
                if (!chunk.Splashed && ocean != null && point.y <= ocean.Height(point))
                {
                    chunk.Splashed = true; point.y = ocean.Height(point);
                    CombatVfx.Splash(point); GameAudio.Play(SoundCue.Splash, point);
                    chunk.Body.linearDamping = 2f;
                }
            }
        }
        void Release(int index)
        {
            var chunk = active[index]; active.RemoveAt(index);
            if (chunk.OwnsMesh)
            {
                Destroy(chunk.Mesh.sharedMesh); chunk.Mesh.sharedMesh = null; chunk.OwnsMesh = false;
            }
            chunk.Body.linearVelocity = Vector3.zero; chunk.Body.angularVelocity = Vector3.zero; chunk.Body.isKinematic = true;
            pool.Release(chunk);
        }
        public void Clear()
        {
            StopAllCoroutines();
            while (active.Count > 0) Release(active.Count - 1);
            pool?.Clear(); pool = null; available = 0;
            if (root != null) Destroy(root.gameObject);
        }
        void OnDestroy() => Clear();
    }
}
