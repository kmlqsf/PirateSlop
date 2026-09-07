using UnityEngine;
using UnityEngine.InputSystem;

namespace PirateSlop.World
{
    public sealed class WorldMapDisplay : MonoBehaviour
    {
        bool visible;
        void Update() { if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame) visible = !visible; }
        void OnGUI()
        {
            var world = GetComponent<ProceduralWorld>();
            if (!visible || !world.Ready) return;
            var map = world.Layout;
            float size = Mathf.Min(560, Screen.height - 100), x = (Screen.width - size) * .5f, y = 45;
            GUI.Box(new Rect(x - 8, y - 28, size + 16, size + 75), "Map · seed " + map.Seed + " · M close");
            var old = GUI.color; GUI.color = new Color(.05f, .19f, .25f); GUI.DrawTexture(new Rect(x, y, size, size), Texture2D.whiteTexture);
            foreach (var location in map.Locations)
            {
                float radius = Mathf.Max(3, location.Radius / (map.Radius * 2) * size);
                float px = x + (.5f + location.Position.x / (map.Radius * 2)) * size, py = y + (.5f - location.Position.z / (map.Radius * 2)) * size;
                GUI.color = location.Type.Shape == Landform.Reef || location.Type.Shape == Landform.ReefPassage ? Color.cyan : location.Type.Ground;
                GUI.DrawTexture(new Rect(px - radius, py - radius, radius * 2, radius * 2), Texture2D.whiteTexture);
                GUI.color = Color.white; GUI.Label(new Rect(px - 40, py, 110, 20), location.Type.Id);
            }
            foreach (var ship in FindObjectsByType<ShipController>(FindObjectsSortMode.None))
            {
                var p = ship.transform.position; GUI.color = Color.yellow;
                GUI.DrawTexture(new Rect(x + (.5f + p.x / (map.Radius * 2)) * size - 3, y + (.5f - p.z / (map.Radius * 2)) * size - 3, 6, 6), Texture2D.whiteTexture);
            }
            GUI.color = Color.white; GUI.Label(new Rect(x, y + size + 6, size, 35), "Yellow: ships | Cyan: reefs | " + (map.Radius * 2 / 1000).ToString("0.0") + " km across"); GUI.color = old;
        }
    }
}
