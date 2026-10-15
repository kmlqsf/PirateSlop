using UnityEngine;

namespace PirateSlop.World
{
    public sealed class EnvironmentEventTestRow : MonoBehaviour
    {
        public float SeaLevel;
        static readonly string[] titles = { "1 · Захват круга", "2 · Плот / взлом", "3 · Затонувший сундук", "4 · Акулы", "5 · Огненный череп", "6 · Раненый кит", "7 · Раковина с сундуком", "8 · Остров с весами", "9 · Сундук в такелаже", "10 · Груз за кормой", "11 · Пушечный пролом", "12 · Чайка с ключом", "13 · Лестница контрабандистов" };
        GUIStyle style;
        public static Vector3 Point(int index, float seaLevel) => new(-600 + index * 350, seaLevel, 370);
        void OnGUI()
        {
            if (PirateSlop.Performance.PerformanceBenchmarkController.HideTestLabels) return;
            if (RoguelikeUpgradeUI.WindowOpen || Camera.main == null) return;
            style ??= new GUIStyle(GUI.skin.box) { fontSize = 18, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1, .88f, .55f) } };
            for (int i = 0; i < titles.Length; i++)
            {
                var p = Camera.main.WorldToScreenPoint(Point(i, SeaLevel) + Vector3.up * (i == 4 ? 65 : 18));
                if (p.z <= 0 || p.x < 0 || p.x > Screen.width || p.y < 0 || p.y > Screen.height) continue;
                float width = style.CalcSize(new GUIContent(titles[i])).x + 24;
                GUI.Box(new Rect(p.x - width * .5f, Screen.height - p.y - 17, width, 34), titles[i], style);
            }
        }
    }
}
