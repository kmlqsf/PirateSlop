using UnityEngine;

namespace PirateSlop.Customization
{
    public sealed class ShipNameplate : MonoBehaviour
    {
        public TextMesh Label;
        public float TextWidth = 4.1f;
        public float TextHeight = .56f;
        string displayed;

        public void SetName(string value)
        {
            if (Label == null) return;
            string name = ShipCustomizationData.NormalizeName(value);
            if (displayed == name) return;
            displayed = name;
            Label.text = name;
            Label.anchor = TextAnchor.MiddleCenter;
            Label.alignment = TextAlignment.Center;
            Label.richText = false;
            Label.transform.localScale = Vector3.one;
            if (name.Length == 0) return;
            var renderer = Label.GetComponent<MeshRenderer>();
            if (renderer == null) return;
            var size = renderer.localBounds.size;
            float scale = Mathf.Min(1f, TextWidth / Mathf.Max(.001f, size.x), TextHeight / Mathf.Max(.001f, size.y));
            Label.transform.localScale = Vector3.one * scale;
        }
    }
}
