using UnityEngine;

namespace PirateSlop
{
    public static class HelmCenterMark
    {
        public static void Add(Transform wheel)
        {
            if (wheel == null || wheel.Find("CenterMark") != null) return;
            var material = Resources.Load<Material>("HelmCenterMark");
            if (material == null) return;
            Bounds bounds = default;
            bool found = false;
            foreach (var filter in wheel.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                var mesh = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = mesh.center + Vector3.Scale(mesh.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 point = wheel.InverseTransformPoint(filter.transform.TransformPoint(corner));
                    if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                    else bounds.Encapsulate(point);
                }
            }
            if (!found || bounds.size.y < .01f) return;
            var root = new GameObject("CenterMark"); root.transform.SetParent(wheel, false);
            float radius = bounds.extents.y;
            for (int side = 0; side < 2; side++)
            {
                var face = new GameObject(side == 0 ? "Front" : "Back"); face.transform.SetParent(root.transform, false);
                var line = face.AddComponent<LineRenderer>();
                line.useWorldSpace = false; line.positionCount = 2;
                line.sharedMaterial = material;
                line.alignment = LineAlignment.TransformZ;
                line.startWidth = line.endWidth = radius * .065f;
                float z = side == 0 ? bounds.max.z + radius * .01f : bounds.min.z - radius * .01f;
                line.SetPosition(0, new Vector3(bounds.center.x, bounds.center.y + radius * .78f, z));
                line.SetPosition(1, new Vector3(bounds.center.x, bounds.center.y + radius * .96f, z));
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
            }
        }
    }
}
