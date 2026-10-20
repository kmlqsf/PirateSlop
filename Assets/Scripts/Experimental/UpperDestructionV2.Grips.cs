#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
using System.Collections.Generic;
using UnityEngine;

namespace PirateSlop.Experimental
{
    public sealed partial class UpperDestructionV2
    {
        readonly List<CombineInstance> gripParts = new();

        void RefreshGrips()
        {
            for (int index = 0; index < Assemblies.Length; index++)
                foreach (var grip in Assemblies[index].Grips)
                {
                    if (grip.Collider == null || grip.Section == null) continue;
                    bool visible = EffectiveStage(index) < 3;
                    ulong mask = grip.Section.RemovedFragments;
                    if (grip.AppliedMask == mask && grip.AppliedVisible == visible)
                    {
                        if (visible && grip.Mesh != null && grip.Collider.sharedMesh != grip.Mesh)
                        {
                            grip.Collider.sharedMesh = grip.Mesh;
                            grip.Collider.enabled = grip.Mesh.vertexCount > 0;
                        }
                        continue;
                    }
                    grip.AppliedMask = mask; grip.AppliedVisible = visible;
                    grip.Collider.enabled = false;
                    if (!visible) continue;
                    gripParts.Clear();
                    void Add(GameObject source)
                    {
                        var filter = source != null ? source.GetComponent<MeshFilter>() : null;
                        if (filter == null || filter.sharedMesh == null) return;
                        for (int sub = 0; sub < filter.sharedMesh.subMeshCount; sub++)
                            gripParts.Add(new CombineInstance { mesh = filter.sharedMesh, subMeshIndex = sub, transform = grip.Collider.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix });
                    }
                    if (mask == 0) Add(grip.Section.Intact);
                    else for (int bit = 0; bit < grip.Section.Fragments.Length; bit++)
                            if ((mask & (1UL << bit)) == 0) Add(grip.Section.Fragments[bit]);
                    if (grip.Mesh == null) grip.Mesh = new Mesh { name = "UpperGrip", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                    grip.Collider.sharedMesh = null; grip.Mesh.Clear();
                    if (gripParts.Count == 0) continue;
                    grip.Mesh.CombineMeshes(gripParts.ToArray(), true, true);
                    grip.Mesh.UploadMeshData(false);
                    grip.Collider.sharedMesh = grip.Mesh;
                    grip.Collider.enabled = grip.Mesh.vertexCount > 0;
                }
        }

        public bool CanGrabHelm(AdvancedPlayerController player)
        {
            if (player == null || player.PlayerCamera == null) return false;
            var start = player.PlayerCamera.transform.position;
            var end = start + player.PlayerCamera.transform.forward * 3.5f;
            return HelmIntersection(start, end) < 3.5f;
        }

        public bool CanGrabHelmAt(AdvancedPlayerController player, Vector3 point)
        {
            if (player == null || !float.IsFinite(point.sqrMagnitude)) return false;
            var start = player.transform.position + transform.up * 1.5f;
            var delta = point - start;
            float distance = delta.magnitude;
            if (distance > 3.5f || distance < .01f) return false;
            return HelmIntersection(start, point + delta / distance * .08f) <= distance + .08f;
        }

        float HelmIntersection(Vector3 start, Vector3 end)
        {
            float nearest = float.PositiveInfinity;
            foreach (var assembly in Assemblies)
            {
                if (assembly.Kind != UpperAssemblyKind.Helm) continue;
                foreach (var section in assembly.Sections)
                {
                    if (section.RemovedFragments == 0) nearest = Mathf.Min(nearest, IntersectionDistance(section.Intact.transform, start, end));
                    for (int bit = 0; bit < section.Fragments.Length; bit++)
                        if ((section.RemovedFragments & (1UL << bit)) == 0) nearest = Mathf.Min(nearest, IntersectionDistance(section.Fragments[bit].transform, start, end));
                }
            }
            return nearest;
        }
    }
}
#endif
