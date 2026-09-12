using PirateSlop.Networking;
using UnityEngine;

namespace PirateSlop.World
{
    public sealed class ShipComparison : MonoBehaviour
    {
        public int Index;
        public Vector3 DeckSpawn;

        public static void Spawn(ProceduralWorld world, SessionConfig config)
        {
            var previous = world.transform.Find("ShipComparison");
            if (previous != null) { previous.gameObject.SetActive(false); Destroy(previous.gameObject); }
            if (!config.ShipComparisonEnabled || config.ComparisonShips == null) return;
            WorldPoint first = world.Layout.Points.Find(p => p.Tag == "ship_spawn");
            if (first == null) return;
            var group = new GameObject("ShipComparison").transform;
            group.SetParent(world.transform, false);
            Vector3 inward = Vector3.ProjectOnPlane(-first.Position, Vector3.up).normalized;
            if (inward.sqrMagnitude < .1f) inward = Vector3.forward;
            Quaternion direction = Quaternion.LookRotation(inward);
            var occupied = new System.Collections.Generic.List<Vector3>();
            for (int i = 0; i < config.ComparisonShips.Length; i++)
            {
                if (config.ComparisonShips[i] == null) continue;
                bool placed = false;
                for (int attempt = 0; attempt < 40; attempt++)
                {
                    Vector3 point = first.Position + direction * new Vector3((i - 1) * 70 + attempt % 5 * 70, 0, 90 + attempt / 5 * 70);
                    point.y = world.Layout.SeaLevel;
                    if (occupied.Exists(p => Vector3.Distance(p, point) < 70)) continue;
                    if (world.Layout.Points.Exists(p => p.Tag == "ship_spawn" && Vector3.Distance(p.Position, point) < 70)) continue;
                    if (!world.CanSail(point, first.Yaw) || world.GroundHeight(point) > point.y - 8) continue;
                    var exhibit = Instantiate(config.ComparisonShips[i], point, Quaternion.Euler(0, first.Yaw, 0), group);
                    exhibit.Index = i;
                    occupied.Add(point);
                    Physics.SyncTransforms();
                    placed = true;
                    break;
                }
                if (!placed) Debug.LogWarning($"No clear position for comparison ship {i + 1}.");
            }
        }

        public static ShipComparison Find(int index)
        {
            var world = ProceduralWorld.Instance;
            var group = world != null ? world.transform.Find("ShipComparison") : null;
            if (group == null) return null;
            foreach (var ship in group.GetComponentsInChildren<ShipComparison>()) if (ship.Index == index) return ship;
            return null;
        }

        void OnGUI()
        {
            if (SessionController.MenuOpen) return;
            var camera = Camera.main;
            if (camera == null) return;
            Vector3 screen = camera.WorldToScreenPoint(transform.TransformPoint(DeckSpawn + Vector3.up * 3));
            if (Index == 0) PirateHudStyle.Panel(new Rect(16, 70, 330, 28), "F2 / F3 / F4: test ships · F5: return");
            if (screen.z <= 0 || screen.z > 600) return;
            PirateHudStyle.Panel(new Rect(screen.x - 140, Screen.height - screen.y, 280, 28), $"Test ship {Index + 1} · F{Index + 2}");
        }
    }
}
