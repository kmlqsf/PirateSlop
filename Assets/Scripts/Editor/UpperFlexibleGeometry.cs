using System.Collections.Generic;
using UnityEngine;

namespace PirateSlop.EditorTools
{
    static class UpperFlexibleGeometry
    {
        public static Mesh Fold(Mesh source, Matrix4x4 matrix, Vector3 pivot, bool aroundMast)
        {
            var mesh = Object.Instantiate(source);
            var points = source.vertices;
            var normals = source.normals;
            var parent = new int[points.Length];
            var weld = new Dictionary<Vector3Int, int>();
            int Root(int index) { while (parent[index] != index) { parent[index] = parent[parent[index]]; index = parent[index]; } return index; }
            void Join(int a, int b) { a = Root(a); b = Root(b); if (a != b) parent[b] = a; }
            for (int i = 0; i < points.Length; i++)
            {
                parent[i] = i; points[i] = matrix.MultiplyPoint3x4(points[i]);
                var key = Vector3Int.RoundToInt(points[i] * 1000f);
                if (weld.TryGetValue(key, out int other)) Join(i, other); else weld[key] = i;
            }
            foreach (int sub in System.Linq.Enumerable.Range(0, source.subMeshCount))
            {
                var triangles = source.GetTriangles(sub);
                for (int at = 0; at < triangles.Length; at += 3) { Join(triangles[at], triangles[at + 1]); Join(triangles[at], triangles[at + 2]); }
            }
            var islands = new Dictionary<int, List<int>>();
            for (int i = 0; i < points.Length; i++)
            {
                int key = Root(i);
                if (!islands.TryGetValue(key, out var island)) islands[key] = island = new List<int>();
                island.Add(i);
            }
            var inverse = matrix.inverse;
            var normalToShip = matrix.inverse.transpose;
            var normalFromShip = matrix.transpose;
            foreach (var island in islands.Values)
            {
                var bounds = new Bounds(points[island[0]], Vector3.zero);
                foreach (int i in island) bounds.Encapsulate(points[i]);
                var center = bounds.center;
                float height = bounds.size.y;
                float phase = Mathf.Atan2(center.z - pivot.z, center.x - pivot.x);
                if (height < .4f)
                {
                    var destination = aroundMast ? new Vector3(pivot.x + Mathf.Cos(phase) * .42f, center.y, pivot.z + Mathf.Sin(phase) * .42f) : center;
                    var turn = aroundMast ? Quaternion.Euler(0, 0, 50f * Mathf.Sin(phase)) : Quaternion.identity;
                    foreach (int i in island)
                    {
                        points[i] = inverse.MultiplyPoint3x4(destination + turn * (points[i] - center));
                        if (normals.Length > i) normals[i] = normalFromShip.MultiplyVector(turn * normalToShip.MultiplyVector(normals[i])).normalized;
                    }
                    continue;
                }
                const int bins = 32;
                var centers = new Vector3[bins]; var counts = new int[bins];
                foreach (int i in island)
                {
                    int bin = Mathf.Clamp(Mathf.FloorToInt((points[i].y - bounds.min.y) / height * bins), 0, bins - 1);
                    centers[bin] += points[i]; counts[bin]++;
                }
                for (int bin = 0; bin < bins; bin++) if (counts[bin] > 0) centers[bin] /= counts[bin];
                for (int bin = 0; bin < bins; bin++)
                {
                    if (counts[bin] > 0) continue;
                    int nearest = 0; float best = float.PositiveInfinity;
                    for (int other = 0; other < bins; other++) if (counts[other] > 0 && Mathf.Abs(other - bin) < best) { best = Mathf.Abs(other - bin); nearest = other; }
                    centers[bin] = centers[nearest];
                }
                foreach (int i in island)
                {
                    float t = Mathf.Clamp01((points[i].y - bounds.min.y) / height);
                    float sample = Mathf.Clamp(t * bins - .5f, 0, bins - 1);
                    int low = Mathf.FloorToInt(sample), high = Mathf.Min(low + 1, bins - 1);
                    var spine = Vector3.Lerp(centers[low], centers[high], sample - low);
                    var offset = points[i] - spine; offset.y = 0;
                    Vector3 destination; Quaternion turn;
                    if (aroundMast)
                    {
                        float radius = .4f + .025f * Mathf.Sin(points[i].y * 1.8f + phase);
                        float angle = phase + offset.x / radius;
                        turn = Quaternion.AngleAxis(-angle * Mathf.Rad2Deg, Vector3.up);
                        destination = new Vector3(pivot.x + Mathf.Sin(angle) * (radius + offset.z), points[i].y, pivot.z + Mathf.Cos(angle) * (radius + offset.z));
                    }
                    else
                    {
                        float distance = t * height;
                        float radius = Mathf.Clamp(.22f + distance * .025f, .22f, .6f);
                        float angle = phase + distance / radius;
                        var tangent = new Vector3(Mathf.Cos(angle), .025f, -Mathf.Sin(angle)).normalized;
                        turn = Quaternion.FromToRotation(Vector3.up, tangent);
                        destination = new Vector3(center.x + Mathf.Sin(angle) * radius, bounds.min.y + .03f + distance * .025f, center.z + Mathf.Cos(angle) * radius) + turn * offset;
                    }
                    points[i] = inverse.MultiplyPoint3x4(destination);
                    if (normals.Length > i) normals[i] = normalFromShip.MultiplyVector(turn * normalToShip.MultiplyVector(normals[i])).normalized;
                }
            }
            mesh.vertices = points; mesh.normals = normals; mesh.RecalculateBounds();
            return mesh;
        }
    }
}
