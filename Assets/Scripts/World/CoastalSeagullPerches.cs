using System;
using UnityEngine;

namespace PirateSlop.World
{
    public sealed class CoastalSeagullPerches : MonoBehaviour
    {
        public Vector3[] Points = Array.Empty<Vector3>();
        public Vector3[] Normals = Array.Empty<Vector3>();
        public Bounds RockBounds;
    }
}
