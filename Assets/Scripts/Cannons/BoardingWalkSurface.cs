using System.Collections.Generic;
using PirateSlop.Networking;
using UnityEngine;

namespace PirateSlop
{
    public sealed class BoardingWalkSurface : MonoBehaviour
    {
        static readonly List<BoardingWalkSurface> active = new();
        public static IReadOnlyList<BoardingWalkSurface> Active => active;
        readonly List<BoxCollider> surfaces = new();
        readonly List<Vector3> vertices = new();
        readonly List<Vector2> uv = new();
        readonly List<int> triangles = new();
        SimpleCannon cannon;
        BoardingCable first;
        BoardingCable? second;
        Mesh rungs;
        MeshRenderer renderer;
        Vector3 lastStart, lastEnd, lastOtherEnd;
        float lastLength = -1f;
        bool wasLadder;
        public Rigidbody Body { get; private set; }
        public bool HasLadder => second.HasValue;
        public bool Ready => cannon != null && first.Target != null && first.Target.IsSpawned && isActiveAndEnabled;

        void OnEnable() { active.Add(this); }
        void OnDisable() { active.Remove(this); }
        void OnDestroy() { if (rungs != null) Destroy(rungs); }

        public void Configure(SimpleCannon gun, BoardingCable a, BoardingCable? b)
        {
            bool changed = rungs != null && wasLadder != b.HasValue;
            cannon = gun; first = a; second = b; Body = GetComponentInParent<ShipController>().GetComponent<Rigidbody>();
            if (rungs == null)
            {
                rungs = new Mesh { name = "BoardingRopeRungs" }; rungs.MarkDynamic();
                gameObject.AddComponent<MeshFilter>().sharedMesh = rungs;
                renderer = gameObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = Resources.Load<Material>("HookRope");
            }
            Vector3 start = StartPoint(first), end = EndPoint(first);
            Vector3 otherEnd = b.HasValue ? EndPoint(b.Value) : end;
            float length = b.HasValue ? a.Length + b.Value.Length : a.Length;
            if ((start - lastStart).sqrMagnitude < .000004f && (end - lastEnd).sqrMagnitude < .000004f &&
                (otherEnd - lastOtherEnd).sqrMagnitude < .000004f && Mathf.Abs(length - lastLength) < .001f && wasLadder == HasLadder) return;
            lastStart = start; lastEnd = end; lastOtherEnd = otherEnd; lastLength = length; wasLadder = HasLadder;
            RefreshGeometry();
            if (changed)
                foreach (var passenger in ShipDeckPassenger.Active) passenger.UpdateBoardingSupport(this);
        }

        Vector3 StartPoint(BoardingCable cable) => cannon.Muzzle.position;
        public static Vector3 EndPoint(BoardingCable cable) => cable.Target.transform.TransformPoint(cable.Point) + cable.Target.transform.TransformDirection(cable.Normal).normalized * .64f;
        Vector3 RopePoint(BoardingCable cable, float t)
        {
            Vector3 start = StartPoint(cable), end = EndPoint(cable);
            float sag = Mathf.Min(8f, Mathf.Max(0f, cable.Length - Vector3.Distance(start, end)) * .35f + .12f);
            return Vector3.Lerp(start, end, t) + Vector3.down * (Mathf.Sin(t * Mathf.PI) * sag);
        }
        Vector3 Center(float t) => HasLadder ? (RopePoint(first, t) + RopePoint(second.Value, t)) * .5f : RopePoint(first, t);
        Vector3 Side(float t)
        {
            Vector3 tangent = Center(Mathf.Min(1f, t + .005f)) - Center(Mathf.Max(0f, t - .005f));
            return Vector3.Cross(Vector3.up, tangent.normalized).normalized;
        }
        public Vector3 Point(float t, float lateral) => Center(t) + Side(t) * lateral;
        float Width(float t) => HasLadder ? Mathf.Clamp(Vector3.Distance(RopePoint(first, t), RopePoint(second.Value, t)), .24f, 1.5f) : .24f;

        public bool Locate(Vector3 position, out float t, out float lateral, out Vector3 anchor)
        {
            t = lateral = 0f; anchor = position;
            if (!Ready) return false;
            float best = float.MaxValue;
            int count = Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(StartPoint(first), EndPoint(first))), 8, 128);
            for (int i = 0; i < count; i++)
            {
                Vector3 a = Center((float)i / count), b = Center((i + 1f) / count), delta = b - a;
                float along = Mathf.Clamp01(Vector3.Dot(position - a, delta) / Mathf.Max(.0001f, delta.sqrMagnitude));
                float distance = (position - Vector3.Lerp(a, b, along)).sqrMagnitude;
                if (distance >= best) continue;
                best = distance; t = (i + along) / count;
            }
            lateral = Vector3.Dot(position - Center(t), Side(t));
            anchor = Point(t, lateral);
            float vertical = position.y - anchor.y;
            return Mathf.Abs(lateral) < Width(t) * .5f + .3f && vertical > -.35f && vertical < 4f;
        }

        void RefreshGeometry()
        {
            float length = Vector3.Distance(StartPoint(first), EndPoint(first));
            int count = Mathf.Clamp(Mathf.CeilToInt(length / .65f), 4, 192);
            while (surfaces.Count < count)
            {
                var go = new GameObject("WalkSegment"); go.transform.SetParent(transform, false);
                surfaces.Add(go.AddComponent<BoxCollider>());
            }
            for (int i = 0; i < surfaces.Count; i++)
            {
                var box = surfaces[i]; box.gameObject.SetActive(i < count);
                if (i >= count) continue;
                float t = (i + .5f) / count;
                Vector3 a = Center((float)i / count), b = Center((i + 1f) / count);
                Vector3 direction = b - a;
                Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);
                box.transform.SetPositionAndRotation((a + b) * .5f - rotation * Vector3.up * .035f, rotation);
                box.size = new Vector3(Width(t), .12f, direction.magnitude + .05f);
            }
            renderer.enabled = HasLadder;
            rungs.Clear();
            if (!HasLadder) return;
            vertices.Clear(); uv.Clear(); triangles.Clear();
            int steps = Mathf.Clamp(Mathf.CeilToInt(length / .4f), 4, 320);
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                AddRung(RopePoint(first, t), RopePoint(second.Value, t));
            }
            rungs.SetVertices(vertices); rungs.SetUVs(0, uv); rungs.SetTriangles(triangles, 0);
            rungs.RecalculateNormals(); rungs.RecalculateBounds();
        }

        void AddRung(Vector3 a, Vector3 b)
        {
            Vector3 direction = (b - a).normalized;
            Vector3 up = Vector3.Cross(direction, Vector3.forward).normalized;
            if (up.sqrMagnitude < .01f) up = Vector3.Cross(direction, Vector3.up).normalized;
            Vector3 side = Vector3.Cross(direction, up);
            int offset = vertices.Count;
            for (int end = 0; end < 2; end++)
                for (int j = 0; j <= 6; j++)
                {
                    float angle = j * Mathf.PI / 3f;
                    Vector3 radial = (up * Mathf.Cos(angle) + side * Mathf.Sin(angle)) * .035f;
                    vertices.Add(transform.InverseTransformPoint((end == 0 ? a : b) + radial));
                    uv.Add(new Vector2(end * Vector3.Distance(a, b) * 4f, (float)j / 6f));
                }
            for (int j = 0; j < 6; j++)
            {
                int next = j + 1;
                triangles.Add(offset + j); triangles.Add(offset + next); triangles.Add(offset + j + 7);
                triangles.Add(offset + next); triangles.Add(offset + next + 7); triangles.Add(offset + j + 7);
            }
        }
    }
}
