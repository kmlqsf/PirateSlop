using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PirateSlop.World
{
    [Serializable]
    public struct CoastalShoreContour
    {
        public Vector3[] Points;
        public bool Closed;
    }

    public sealed class CoastalRockVisual : MonoBehaviour
    {
        public CoastalShoreContour[] Contours = Array.Empty<CoastalShoreContour>();
        Coroutine pendingRegistration;

        void OnEnable()
        {
            QueueRegistration();
        }

        void OnDisable()
        {
            if (pendingRegistration != null) StopCoroutine(pendingRegistration);
            pendingRegistration = null;
            if (Application.isPlaying) CoastalShoreFoamVfx.UnregisterContours(transform);
        }

        void QueueRegistration()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || pendingRegistration != null) return;
            pendingRegistration = StartCoroutine(RegisterNextFrame());
        }

        IEnumerator RegisterNextFrame()
        {
            yield return null;
            pendingRegistration = null;
            if (Contours != null && Contours.Length > 0) CoastalShoreFoamVfx.RegisterContours(transform, Contours);
            else CoastalShoreFoamVfx.UnregisterContours(transform);
        }

        public void SetTerrainContour(Mesh mesh)
        {
            var contours = new List<CoastalShoreContour>();
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 first = Vector3.zero, second = Vector3.zero;
                int count = 0;
                for (int edge = 0; edge < 3; edge++)
                {
                    var a = vertices[triangles[i + edge]];
                    var b = vertices[triangles[i + (edge + 1) % 3]];
                    if ((a.y <= 0 && b.y > 0) || (b.y <= 0 && a.y > 0))
                    {
                        var point = Vector3.Lerp(a, b, -a.y / (b.y - a.y));
                        if (count == 0) first = point; else second = point;
                        count++;
                    }
                }
                if (count == 2 && Vector3.Distance(first, second) > .03f)
                    contours.Add(new CoastalShoreContour { Points = new[] { first, second } });
            }
            Contours = contours.ToArray();
            QueueRegistration();
        }
    }
}
