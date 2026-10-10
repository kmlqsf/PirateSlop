using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace PirateSlop.Ships
{
    [DefaultExecutionOrder(-200)]
    public sealed class ShipDamagePreparation : MonoBehaviour
    {
        public ShaderVariantCollection Shaders;
        static readonly HashSet<(Mesh, bool, MeshColliderCookingOptions)> prepared = new();
        static readonly HashSet<ShaderVariantCollection> warmed = new();

        struct BakeJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<EntityId> Meshes;
            public bool Convex;
            public MeshColliderCookingOptions Options;
            public void Execute(int index) => Physics.BakeMesh(Meshes[index], Convex, Options);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            prepared.Clear(); warmed.Clear();
        }

        void Awake()
        {
            if (!Application.isPlaying) return;
            var groups = new Dictionary<(bool, MeshColliderCookingOptions), List<EntityId>>();
            void Add(Mesh mesh, bool convex, MeshColliderCookingOptions options)
            {
                if (mesh == null || mesh.vertexCount == 0 || !mesh.isReadable || !prepared.Add((mesh, convex, options))) return;
                if (!groups.TryGetValue((convex, options), out var meshes)) groups[(convex, options)] = meshes = new List<EntityId>();
                meshes.Add(mesh.GetEntityId());
            }
            foreach (var collider in GetComponentsInChildren<MeshCollider>(true)) Add(collider.sharedMesh, collider.convex, collider.cookingOptions);
            var owner = GetComponent<ShipDestruction>();
            const MeshColliderCookingOptions fragmentOptions = MeshColliderCookingOptions.CookForFasterSimulation | MeshColliderCookingOptions.EnableMeshCleaning | MeshColliderCookingOptions.WeldColocatedVertices;
            if (owner != null)
                foreach (var section in owner.Sections)
                {
                    if (section == null || section.Indestructible || !section.LazyFragmentColliders) continue;
                    for (int i = 0; i < section.Fragments.Length; i++)
                    {
                        if ((section.ProtectedFragments & (1UL << i)) != 0) continue;
                        Add(section.Fragments[i].GetComponent<MeshFilter>()?.sharedMesh, false, fragmentOptions);
                    }
                }
            foreach (var group in groups)
            {
                using var meshes = new NativeArray<EntityId>(group.Value.ToArray(), Allocator.TempJob);
                new BakeJob { Meshes = meshes, Convex = group.Key.Item1, Options = group.Key.Item2 }.Schedule(meshes.Length, 8).Complete();
            }
            if (Shaders != null && warmed.Add(Shaders)) Shaders.WarmUp();
        }
    }
}
