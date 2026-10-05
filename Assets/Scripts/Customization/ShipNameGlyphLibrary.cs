using System;
using System.Collections.Generic;
using UnityEngine;

namespace PirateSlop.Customization
{
    public sealed class ShipNameGlyphLibrary : ScriptableObject
    {
        [Serializable]
        public struct Glyph
        {
            public int Codepoint;
            public Mesh Mesh;
            public float Advance;
        }

        [Serializable]
        public struct Kerning
        {
            public int Left, Right;
            public float Offset;
        }

        public Glyph[] Glyphs;
        public Kerning[] Pairs;
        public Material[] Materials;
        public float SpaceAdvance = .35f;
        Dictionary<int, Glyph> glyphs;
        Dictionary<long, float> kerning;

        void Initialize()
        {
            if (glyphs != null) return;
            glyphs = new Dictionary<int, Glyph>();
            kerning = new Dictionary<long, float>();
            if (Glyphs != null) foreach (var glyph in Glyphs) glyphs[glyph.Codepoint] = glyph;
            if (Pairs != null) foreach (var pair in Pairs) kerning[((long)pair.Left << 32) | (uint)pair.Right] = pair.Offset;
        }

        public bool TryGetGlyph(int codepoint, out Glyph glyph)
        {
            Initialize();
            if (codepoint >= 0x2010 && codepoint <= 0x2014) codepoint = '-';
            if (codepoint == 0x2018 || codepoint == 0x2019) codepoint = '\'';
            if (codepoint == 0x201c || codepoint == 0x201d || codepoint == 0xab || codepoint == 0xbb) codepoint = '"';
            return glyphs.TryGetValue(codepoint, out glyph) || glyphs.TryGetValue('?', out glyph);
        }

        public float PairOffset(int left, int right)
        {
            Initialize();
            return kerning.TryGetValue(((long)left << 32) | (uint)right, out float offset) ? offset : 0f;
        }
    }
}
