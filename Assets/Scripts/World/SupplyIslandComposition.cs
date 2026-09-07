using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PirateSlop.World
{
    public sealed class SupplyIslandComposition : MonoBehaviour
    {
        public GameObject[] Palms, Rocks;
        public GameObject Bush, Fern, Awning, Barrel, Crate;
        public GameObject[] SideWalls, CacheAwnings;
        readonly List<Vector3> occupied = new List<Vector3>();
        readonly List<float> radii = new List<float>();
        readonly List<Vector3> caches = new List<Vector3>();
        LocationRecord terrain;
        ProceduralWorld world;
        BinaryWriter checksum;
        MapRandom random;
        bool built;

        public void Build(LocationRecord location, ProceduralWorld owner, BinaryWriter writer)
        {
            if (built) throw new InvalidOperationException("Supply composition already built.");
            built = true; world = owner; terrain = location; checksum = writer;
            random = new MapRandom(location.Seed ^ 0xa511e9b3u);
            int style = (int)(random.Next() % 3);
            checksum.Write("SupplyComposition1"); checksum.Write(location.Id); checksum.Write(style);
            foreach (var wall in SideWalls)
                wall.SetActive(style == 0 || (style == 1 && wall.transform.localPosition.x < 0));
            foreach (var awning in CacheAwnings)
            {
                bool covered = random.Value() > .35f;
                awning.SetActive(covered); checksum.Write(covered);
                var p = awning.transform.localPosition; p.y = 0; caches.Add(p);
            }
            for (int i = 0; i < 2; i++)
                for (int attempt = 0; attempt < 100; attempt++)
                {
                    var p = Candidate(35, 59);
                    if (!Available(p, 5) || !Surface(p, 4, .65f, out float height)) continue;
                    float yaw = random.Range(0, 360);
                    var rotation = Quaternion.Euler(0, yaw, 0);
                    p.y = height;
                    Place(Awning, p, yaw, 1);
                    var a = p + rotation * new Vector3(-1.5f, 0, .8f);
                    a.y = Ground(a); Place(Crate, a, yaw, 1);
                    var b = p + rotation * new Vector3(1.5f, 0, .8f);
                    b.y = Ground(b); Place(Barrel, b, yaw, 1);
                    Reserve(p, 5); break;
                }
            Scatter(Palms, 28, 2.6f, .2f);
            Scatter(Rocks, 16, 3.5f, .3f);
            Scatter(new[] { Bush }, 38, 1.3f, .12f);
            Scatter(new[] { Fern }, 32, .7f, .02f);
        }

        void Scatter(GameObject[] choices, int count, float radius, float sink)
        {
            for (int attempt = 0, added = 0; attempt < count * 60 && added < count; attempt++)
            {
                var p = Candidate(16, 66);
                float scale = random.Range(.8f, 1.2f);
                if (!Available(p, radius * scale) || !Surface(p, radius * scale, 1.1f, out float height)) continue;
                p.y = height - sink;
                Place(choices[(int)(random.Next() % (uint)choices.Length)], p, random.Range(0, 360), scale);
                Reserve(p, radius * scale); added++;
            }
        }

        Vector3 Candidate(float min, float max)
        {
            float angle = random.Range(0, Mathf.PI * 2);
            float radius = Mathf.Sqrt(random.Range(min * min, max * max));
            return new Vector3(Mathf.Sin(angle) * radius, 0, Mathf.Cos(angle) * radius);
        }

        bool Available(Vector3 p, float radius)
        {
            if (new Vector2(p.x, p.z).magnitude < 13 + radius) return false;
            if (WorldGenerator.ApproachDistance(new Vector2(p.x, p.z), terrain.Type.PierCount) < 7 + radius) return false;
            foreach (var cache in caches)
            {
                if (Horizontal(p - cache) < 7 + radius) return false;
                float t = Mathf.Clamp01(Vector3.Dot(p, cache) / cache.sqrMagnitude);
                if (Horizontal(p - cache * t) < 3 + radius) return false;
            }
            for (int i = 0; i < occupied.Count; i++)
                if (Horizontal(p - occupied[i]) < radius + radii[i]) return false;
            return true;
        }

        bool Surface(Vector3 p, float radius, float maxDelta, out float height)
        {
            height = Ground(p);
            float min = height, max = height;
            for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++)
            {
                float h = Ground(p + new Vector3(x * radius, 0, z * radius));
                min = Mathf.Min(min, h); max = Mathf.Max(max, h);
            }
            return max - min <= maxDelta;
        }

        float Ground(Vector3 local)
        {
            return world.GroundHeight(transform.TransformPoint(new Vector3(local.x, 0, local.z))) - transform.position.y;
        }

        void Reserve(Vector3 p, float radius) { p.y = 0; occupied.Add(p); radii.Add(radius); }
        static float Horizontal(Vector3 p) => new Vector2(p.x, p.z).magnitude;

        void Place(GameObject prefab, Vector3 p, float yaw, float scale)
        {
            var part = Instantiate(prefab, transform);
            part.transform.localPosition = p;
            part.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            part.transform.localScale = Vector3.one * scale;
            checksum.Write(prefab.name);
            checksum.Write(Mathf.RoundToInt(p.x * 1000)); checksum.Write(Mathf.RoundToInt(p.y * 1000)); checksum.Write(Mathf.RoundToInt(p.z * 1000));
            checksum.Write(Mathf.RoundToInt(yaw * 1000)); checksum.Write(Mathf.RoundToInt(scale * 1000));
        }
    }
}
