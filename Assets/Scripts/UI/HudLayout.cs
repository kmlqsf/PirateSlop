using System;
using UnityEngine;

namespace PirateSlop
{
    public sealed class HudLayout
    {
        public static float Width => Screen.width;
        public static float Height => Screen.height;
        public readonly struct Scope : IDisposable
        {
            readonly Matrix4x4 matrix;
            public Scope(bool enabled)
            {
                matrix = GUI.matrix;
                if (enabled) GUI.matrix = Matrix4x4.identity;
            }
            public void Dispose() => GUI.matrix = matrix;
        }
    }
}
