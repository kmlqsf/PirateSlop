using UnityEngine;

namespace PirateSlop.World
{
    public class WhirlpoolTest : MonoBehaviour
    {
        public Vector3 Center;
        public float Radius = 250f;
        public float Depth = 80f; // Reduced from 120 so it doesn't clip below sea floor

        WhirlpoolVFX vfx;

        void Start()
        {
            var vfxObj = new GameObject("WhirlpoolVFX_Test");
            vfxObj.transform.position = Center;
            vfx = vfxObj.AddComponent<WhirlpoolVFX>();
        }

        void Update()
        {
            if (OceanSurface.Instance != null)
            {
                OceanSurface.Instance.WhirlpoolCenter = Center;
                OceanSurface.Instance.WhirlpoolRadius = Radius;
                OceanSurface.Instance.WhirlpoolDepth = Depth;
                OceanSurface.Instance.WhirlpoolTwist = 2f;
            }
        }

        void OnGUI()
        {
            var camera = Camera.main;
            if (camera == null) return;
            var point = camera.WorldToScreenPoint(Center + Vector3.up * 50f);
            if (point.z > 0)
            {
                var style = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 24,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.yellow }
                };
                var content = new GUIContent("WHIRLPOOL (ВОДОВОРОТ)");
                var size = style.CalcSize(content);
                var rect = new Rect(point.x - size.x * .5f - 10, Screen.height - point.y - 18, size.x + 20, 36);
                GUI.Box(rect, GUIContent.none);
                GUI.Label(rect, content, style);
            }
        }
    }
}
