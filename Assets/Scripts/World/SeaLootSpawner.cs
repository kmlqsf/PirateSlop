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
            if (EnvironmentTestGallery.IsTest(world.Layout))
            {
                for (int i = 0; i < 7; i++) SpawnOne(world, manager, catalog, i, EnvironmentEventTestRow.Point(i, world.Layout.SeaLevel));
                NetworkBalanceEvent.SpawnEvent(catalog, manager, EnvironmentEventTestRow.Point(7, world.Layout.SeaLevel), world.gameObject.scene);
                NetworkRiggingEvent.SpawnEvent(catalog, manager, EnvironmentEventTestRow.Point(8, world.Layout.SeaLevel), world.gameObject.scene);
                NetworkSternCargoEvent.SpawnEvent(catalog, manager, EnvironmentEventTestRow.Point(9, world.Layout.SeaLevel), world.gameObject.scene, true);
                NetworkCannonBreachEvent.SpawnEvent(catalog, manager, EnvironmentEventTestRow.Point(10, world.Layout.SeaLevel), world.gameObject.scene);
                NetworkSeagullEvent.SpawnEvent(catalog, manager, EnvironmentEventTestRow.Point(11, world.Layout.SeaLevel), world.gameObject.scene);
                return;
            }
            var random = new MapRandom((uint)world.Layout.Seed ^ 0x79c526abu);
            var occupied = new List<Vector3>();
            var eventTypes = new List<int> { 0, 1, 2, 3 };
            if (catalog.SkullEventPrefab != null) eventTypes.Add(4);
            if (catalog.WhaleEventPrefab != null) eventTypes.Add(5);
            if (catalog.ClamEventPrefab != null) eventTypes.Add(6);
            if (catalog.RiggingEventPrefab != null) eventTypes.Add(8);
            if (catalog.SternCargoEventPrefab != null) eventTypes.Add(9);
            if (catalog.CannonBreachEventPrefab != null) eventTypes.Add(10);
            if (catalog.SeagullEventPrefab != null) eventTypes.Add(11);
            int types = eventTypes.Count;
            int count = Mathf.Clamp(catalog.SeaEventsPerType, 1, 20) * types;
            float separation = Mathf.Max(160, catalog.CaptureRadius * 2.5f);
            for (int i = 0; i < count; i++)
            {
                int eventIndex = eventTypes[i % types];
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
                        Vector3 sample = point + new Vector3(Mathf.Cos(bearing), 0, Mathf.Sin(bearing)) * (eventIndex == 10 ? Mathf.Max(100, catalog.CaptureRadius) : catalog.CaptureRadius);
                        if (!world.CanSail(sample, side * 45)) { clear = false; break; }
                    }
                    if (!clear || !world.CanSail(point, 0) || world.GroundHeight(point) > point.y - catalog.SunkenDepth - 2) continue;
                    if (eventIndex >= 5)
                    {
                        SpawnOne(world, manager, catalog, eventIndex, point);
                        occupied.Add(point);
                        break;
                    }
                    if (eventIndex == 4)
                    {
                        NetworkSkullEvent.SpawnEvent(catalog, manager, point, world.gameObject.scene);
                        occupied.Add(point);
                        break;
                    }
                    var chest = Object.Instantiate(catalog.ChestPrefab, point, Quaternion.identity);
                    SceneManager.MoveGameObjectToScene(chest.gameObject, world.gameObject.scene);
                    chest.Catalog = catalog;
                    var lootRandom = new MapRandom(unchecked((uint)world.Layout.Seed ^ 0xa35f19c7u ^ ((uint)i * 2654435761u)));
                    chest.Fill(ref lootRandom);
                    chest.ConfigureOcean((SeaLootKind)(1 + eventIndex), point);
                    manager.ServerManager.Spawn(chest.NetworkObject);
                    occupied.Add(point);
                    break;
                }
            }
        }

        static void SpawnOne(ProceduralWorld world, NetworkManager manager, LootCatalog catalog, int index, Vector3 point)
        {
            if (index == 11) { NetworkSeagullEvent.SpawnEvent(catalog, manager, point, world.gameObject.scene); return; }
            if (index == 10) { NetworkCannonBreachEvent.SpawnEvent(catalog, manager, point, world.gameObject.scene); return; }
            if (index == 9) { NetworkSternCargoEvent.SpawnEvent(catalog, manager, point, world.gameObject.scene, false); return; }
            if (index == 8) { NetworkRiggingEvent.SpawnEvent(catalog, manager, point, world.gameObject.scene); return; }
            if (index == 6) { NetworkClamEvent.SpawnEvent(catalog, manager, point, world.gameObject.scene); return; }
            if (index == 4) { if (catalog.SkullEventPrefab != null) NetworkSkullEvent.SpawnEvent(catalog, manager, point, world.gameObject.scene); return; }
            if (index == 5)
            {
                if (catalog.WhaleEventPrefab == null) return;
                var whale = Object.Instantiate(catalog.WhaleEventPrefab, point + Vector3.up * catalog.WhaleEventPrefab.SurfaceOffset, Quaternion.identity);
                SceneManager.MoveGameObjectToScene(whale.gameObject, world.gameObject.scene);
                whale.Catalog = catalog;
                manager.ServerManager.Spawn(whale.NetworkObject);
                return;
            }
            var chest = Object.Instantiate(catalog.ChestPrefab, point, Quaternion.identity);
            SceneManager.MoveGameObjectToScene(chest.gameObject, world.gameObject.scene);
            chest.Catalog = catalog;
            var random = new MapRandom(unchecked((uint)world.Layout.Seed ^ 0xa35f19c7u ^ ((uint)index * 2654435761u)));
            chest.Fill(ref random);
            chest.ConfigureOcean((SeaLootKind)(1 + index), point);
            manager.ServerManager.Spawn(chest.NetworkObject);
        }
    }
}
