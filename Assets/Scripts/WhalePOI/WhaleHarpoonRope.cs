using UnityEngine;
using PirateSlop.World;

namespace PirateSlop
{
    [DefaultExecutionOrder(60)]
    public sealed class WhaleHarpoonRope : MonoBehaviour
    {
        public WhaleHarpoonPin A, B;
        public LineRenderer Line;
        public Vector3[] Route;
        public float RestLength;
        Vector3[] points, taut;
        bool detached, splashA, splashB;
        double carriedAt, removedAtA, removedAtB, clockSample, localSample;
        Vector3 releaseA, releaseB, driftOrigin, driftAxis, driftDirection;
        Quaternion rotationA, rotationB;
        float seaLevel;
        void Awake() { points = new Vector3[Mathf.Max(48, Mathf.CeilToInt(RestLength / .12f))]; taut = new Vector3[Route.Length]; }

        void LateUpdate()
        {
            var owner = A.Owner;
            double clock = owner != null ? owner.Clock : clockSample + Time.timeAsDouble - localSample;
            if (owner != null) { clockSample = clock; localSample = Time.timeAsDouble; seaLevel = owner.SeaLevel; }
            bool a = detached || A.IsRemoved, b = detached || B.IsRemoved;
            if (!a && !b)
            {
                for (int i = 0; i < taut.Length; i++)
                {
                    taut[i] = owner.transform.TransformPoint(Route[i]);
                }
                taut[0] = A.RopePoint; taut[^1] = B.RopePoint;
                Line.positionCount = taut.Length; Line.SetPositions(taut); return;
            }
            else
            {
                if (a && !splashA) { var state = owner.Work(A.Index); releaseA = state.ReleasePoint; rotationA = state.ReleaseRotation; removedAtA = state.RemovedAt; splashA = true; }
                if (b && !splashB) { var state = owner.Work(B.Index); releaseB = state.ReleasePoint; rotationB = state.ReleaseRotation; removedAtB = state.RemovedAt; splashB = true; }
                if (a && b && !detached)
                {
                    carriedAt = System.Math.Max(owner.Work(A.Index).RemovedAt, owner.Work(B.Index).RemovedAt);
                    driftOrigin = (releaseA + releaseB) * .5f;
                    driftOrigin.y = seaLevel - .25f;
                    driftAxis = Vector3.ProjectOnPlane(releaseB + rotationB * Vector3.forward * 2.2f - releaseA - rotationA * Vector3.forward * 2.2f, Vector3.up) * .5f;
                    driftDirection = owner.transform.forward * 1.2f + owner.transform.right * .12f;
                    transform.SetParent(null, true); A.transform.SetParent(transform, true); B.transform.SetParent(transform, true);
                    detached = true;
                }
                if (detached)
                {
                    float age = Mathf.Max(0, (float)(clock - carriedAt));
                    var center = driftOrigin + driftDirection * age;
                    var axis = Quaternion.Euler(0, Mathf.Sin(age * .35f) * 3, 0) * driftAxis;
                    PlaceFloating(A, DepartingPoint(releaseA, rotationA, (float)(clock - removedAtA), center - axis, age), rotationA, age, 0);
                    PlaceFloating(B, DepartingPoint(releaseB, rotationB, (float)(clock - removedAtB), center + axis, age), rotationB, age, 1);
                    if (age >= 15) { A.gameObject.SetActive(false); B.gameObject.SetActive(false); Line.enabled = false; var tube = GetComponent<RopeTubeVisual>(); if (tube != null) tube.enabled = false; foreach (var renderer in GetComponentsInChildren<Renderer>()) renderer.enabled = false; if (owner == null) Destroy(gameObject); return; }
                }
                else
                {
                    var pin = a ? A : B;
                    var release = a ? releaseA : releaseB; var rotation = a ? rotationA : rotationB;
                    float age = Mathf.Max(0, (float)(clock - owner.Work(pin.Index).RemovedAt));
                    Vector3 water = pin.EmbeddedPoint + pin.Outward * 3f;
                    water.y = seaLevel - .3f;
                    if (OceanSurface.Instance != null) water.y = OceanSurface.Instance.Height(water) - .3f;
                    var fall = release + pin.Outward * (Mathf.Min(age, 1.2f) * 2.2f) + Vector3.down * (4.9f * age * age);
                    float blend = Mathf.SmoothStep(0, 1, age / 1.3f);
                    var target = Vector3.Lerp(fall, water, blend);
                    target.y = Mathf.Max(water.y - .15f, target.y);
                    PlaceFloating(pin, target, rotation, age, a ? 0 : 1);
                }
                Vector3 start = A.RopePoint, end = B.RopePoint;
                float distance = Vector3.Distance(start, end), sag = Mathf.Min(3.5f, Mathf.Sqrt(Mathf.Max(0, RestLength * RestLength - distance * distance)) * .3f);
                for (int i = 0; i < points.Length; i++)
                {
                    float t = i / (float)(points.Length - 1), curve = Mathf.Sin(t * Mathf.PI);
                    if (!detached)
                    {
                        float f = t * (Route.Length - 1); int n = Mathf.Min((int)f, Route.Length - 2);
                        var guide = owner.transform.TransformPoint(Vector3.Lerp(Route[n], Route[n + 1], f - n));
                        var shift = a ? (start - owner.transform.TransformPoint(Route[0])) * Mathf.Pow(1 - t, 6) : (end - owner.transform.TransformPoint(Route[^1])) * Mathf.Pow(t, 6);
                        points[i] = guide + shift + owner.transform.forward * (curve * 1.5f * (a ? -1f : 1f));
                        var body = owner.WhaleModel.GetComponent<MeshCollider>();
                        if (i > 0 && i < points.Length - 1 && body != null && body.Raycast(new Ray(points[i] + owner.transform.up * 25f, -owner.transform.up), out var hit, 35f)) points[i] = hit.point + hit.normal * .049f;
                    }
                    else
                    {
                        points[i] = Vector3.Lerp(start, end, t) + Vector3.down * (sag * curve) + new Vector3(Mathf.Sin((float)clock * 1.3f + t * 7) * .18f, 0, Mathf.Cos((float)clock + t * 6) * .18f) * curve;
                        if (owner != null && i > 0 && i < points.Length - 1)
                        {
                            var body = owner.WhaleModel.GetComponent<MeshCollider>();
                            if (body != null && body.Raycast(new Ray(points[i] + Vector3.up * 25, Vector3.down), out var hit, 30) && hit.point.y > points[i].y) points[i].y = hit.point.y + .1f;
                        }
                    }
                }
                points[0] = start; points[^1] = end;
            }
            Line.positionCount = points.Length; Line.SetPositions(points);
        }

        Vector3 DepartingPoint(Vector3 release, Quaternion rotation, float fallAge, Vector3 destination, float driftAge)
        {
            var fall = release + rotation * Vector3.forward * (Mathf.Min(fallAge, 1.2f) * 2.2f) + Vector3.down * (4.9f * fallAge * fallAge);
            fall.y = Mathf.Max(destination.y, fall.y);
            return Vector3.Lerp(fall, destination, Mathf.SmoothStep(0, 1, driftAge / 1.4f));
        }

        void PlaceFloating(WhaleHarpoonPin pin, Vector3 point, Quaternion rotation, float age, int phase)
        {
            float surface = OceanSurface.Instance != null ? OceanSurface.Instance.Height(point) : seaLevel;
            point.y = Mathf.Lerp(point.y, surface - .25f, Mathf.SmoothStep(0, 1, (age - .45f) / .85f));
            point.y += Mathf.Sin((float)clockSample * 1.7f + phase * 2) * .12f * Mathf.Clamp01(age);
            pin.transform.SetPositionAndRotation(point, Quaternion.Slerp(rotation, Quaternion.Euler(0, rotation.eulerAngles.y + age * 9, 80 + Mathf.Sin(age * 1.8f + phase) * 5), Mathf.SmoothStep(0, 1, age / 1.4f)));
        }
    }
}
