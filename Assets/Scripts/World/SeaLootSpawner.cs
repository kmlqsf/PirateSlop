using System.Collections.Generic;
using FishNet.Managing;
using PirateSlop.Networking;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PirateSlop.World
{
    public static class SeaLootSpawner
    {
        public static void Spawn(ProceduralWorld world, NetworkManager manager, LootCatalog catalog)
        {
            if (world == null || !world.Ready || catalog == null || catalog.ChestPrefab == null) return;
            var random = new MapRandom((uint)world.Layout.Seed ^ 0x79c526abu);
            var occupied = new List<Vector3>();
            int count = Mathf.Clamp(catalog.SeaEventsPerType, 1, 20) * 3;
            float separation = Mathf.Max(160, catalog.CaptureRadius * 2.5f);
            for (int i = 0; i < count; i++)
            {
                for (int attempt = 0; attempt < 512; attempt++)
                {
                    float angle = random.Range(0, Mathf.PI * 2);
                    float radius = Mathf.Sqrt(random.Range(.025f, .64f)) * world.Layout.Radius;
                    Vector3 point = new Vector3(Mathf.Cos(angle) * radius, world.Layout.SeaLevel, Mathf.Sin(angle) * radius);
                    if (occupied.Exists(p => (p - point).sqrMagnitude < separation * separation)) continue;
                    bool clear = true;
                    for (int side = 0; side < 8; side++)
                    {
                        float bearing = side * Mathf.PI / 4;
                        Vector3 sample = point + new Vector3(Mathf.Cos(bearing), 0, Mathf.Sin(bearing)) * catalog.CaptureRadius;
                        if (!world.CanSail(sample, side * 45)) { clear = false; break; }
                    }
                    if (!clear || !world.CanSail(point, 0) || world.GroundHeight(point) > point.y - catalog.SunkenDepth - 2) continue;
                    var chest = Object.Instantiate(catalog.ChestPrefab, point, Quaternion.identity);
                    SceneManager.MoveGameObjectToScene(chest.gameObject, world.gameObject.scene);
                    chest.Catalog = catalog;
                    chest.Fill(ref random);
                    chest.ConfigureOcean((SeaLootKind)(1 + i % 3), point);
                    manager.ServerManager.Spawn(chest.NetworkObject);
                    occupied.Add(point);
                    break;
                }
            }
        }
    }
}
