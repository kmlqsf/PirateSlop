using System;
using System.Collections.Generic;
using System.Linq;
using FishNet.Managing;
using PirateSlop.Networking;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PirateSlop.World
{
    public static class IslandLootSpawner
    {
        public static void Spawn(ProceduralWorld world, NetworkManager manager, LootCatalog catalog)
        {
            if (world == null || !world.Ready || catalog == null || catalog.ChestPrefab == null) return;
            Physics.SyncTransforms();
            var occupied = new List<Vector3>();
            foreach (var location in world.Layout.Locations)
            {
                var points = world.Points("loot").Where(p => p.Id.StartsWith(location.Id + "/", StringComparison.Ordinal)).ToList();
                var supplyRandom = new MapRandom(location.Seed ^ 0x13d87b29u);
                SpawnSupplies(world, manager, catalog, location.Position, location.Radius, points.Select(p => p.Position).ToList(), occupied, ref supplyRandom);
                if (points.Count == 0) continue;
                var random = new MapRandom(location.Seed ^ 0x71b59a43u);
                for (int i = points.Count - 1; i > 0; i--)
                {
                    int j = (int)(random.Next() % (uint)(i + 1));
                    (points[i], points[j]) = (points[j], points[i]);
                }
                int wanted = LootCatalog.Count(catalog.ChestsPerIsland, ref random), spawned = 0;
                for (int attempt = 0; attempt < points.Count + 100 && spawned < wanted; attempt++)
                {
                    Vector3 position;
                    if (attempt < points.Count) position = points[attempt].Position;
                    else
                    {
                        float angle = random.Range(0, Mathf.PI * 2), radius = Mathf.Sqrt(random.Value()) * location.Radius * .7f;
                        position = location.Position + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                        position.y = world.GroundHeight(position) + .15f;
                    }
                    if (!Physics.Raycast(position + Vector3.up * .6f, Vector3.down, out var floor, 2f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (floor.normal.y < .94f || floor.point.y < world.Layout.SeaLevel + 1f) continue;
                    position = floor.point + Vector3.up * .03f;
                    if (occupied.Exists(p => (p - position).sqrMagnitude < 9f)) continue;
                    var rotation = Quaternion.Euler(0, random.Range(0, 360), 0);
                    if (Physics.CheckBox(position + Vector3.up * .65f, new Vector3(.7f, .5f, .5f), rotation, ~0, QueryTriggerInteraction.Ignore)) continue;
                    var chest = UnityEngine.Object.Instantiate(catalog.ChestPrefab, position, rotation);
                    SceneManager.MoveGameObjectToScene(chest.gameObject, world.gameObject.scene);
                    chest.Fill(ref random);
                    manager.ServerManager.Spawn(chest.NetworkObject);
                    occupied.Add(position); spawned++;
                }
            }
        }
        static void SpawnSupplies(ProceduralWorld world, NetworkManager manager, LootCatalog catalog, Vector3 center, float radius, List<Vector3> markers, List<Vector3> occupied, ref MapRandom random)
        {
            if (catalog.LoosePrefabs == null || catalog.LoosePrefabs.Length <= (int)InventoryItem.Rum || catalog.LoosePrefabs[(int)InventoryItem.Rum] == null)
                throw new InvalidOperationException("Island loot requires a registered rum prefab.");
            int wanted = 1 + LootCatalog.Count(catalog.LooseItemsPerIsland, ref random);
            int spawned = 0;
            bool dryLand = false;
            for (int attempt = 0; attempt < markers.Count + 2048 && spawned < wanted; attempt++)
            {
                Vector3 point;
                if (attempt < markers.Count) point = markers[attempt];
                else
                {
                    int index = attempt - markers.Count;
                    float angle = index * 2.39996323f;
                    float distance = Mathf.Sqrt((index + .5f) / 2048f) * radius * .94f;
                    point = center + new Vector3(Mathf.Cos(angle) * distance, 0, Mathf.Sin(angle) * distance);
                    point.y = world.GroundHeight(point) + .1f;
                }
                if (point.y < world.Layout.SeaLevel + .7f) continue;
                dryLand = true;
                if (!Physics.Raycast(point + Vector3.up * .8f, Vector3.down, out var floor, 2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                if (floor.normal.y < .8f || floor.point.y < world.Layout.SeaLevel + .6f || floor.collider.GetComponentInParent<NetworkFish>() != null) continue;
                point = floor.point + Vector3.up * .03f;
                if (occupied.Exists(p => (p - point).sqrMagnitude < 1f)) continue;
                InventoryItem item = InventoryItem.Rum;
                if (spawned > 0 && !catalog.Roll(ref random, out item)) continue;
                int prefabIndex = CannonAmmo.IsBall(item) ? (int)InventoryItem.Cannonball : (int)item;
                if (prefabIndex < 0 || prefabIndex >= catalog.LoosePrefabs.Length || catalog.LoosePrefabs[prefabIndex] == null) continue;
                var prefab = catalog.LoosePrefabs[prefabIndex];
                var box = prefab.GetComponent<BoxCollider>();
                Vector3 half = box != null ? box.size * .5f : Vector3.one * .25f;
                Vector3 offset = box != null ? box.center : Vector3.up * .25f;
                point += Vector3.up * Mathf.Max(0, half.y - offset.y);
                Quaternion rotation = Quaternion.Euler(0, random.Range(0,360), 0);
                if (Physics.CheckBox(point + rotation * offset, half * .9f, rotation, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                var loot = UnityEngine.Object.Instantiate(prefab, point, rotation);
                SceneManager.MoveGameObjectToScene(loot.gameObject, world.gameObject.scene);
                loot.Place(null, point, rotation);
                if (CannonAmmo.IsBall(item)) loot.SetAmmoItem(item);
                manager.ServerManager.Spawn(loot.NetworkObject);
                occupied.Add(point);
                spawned++;
            }
            if (dryLand && spawned == 0) throw new InvalidOperationException($"No accessible rum spawn on island at {center}. Check island loot markers.");
        }
    }
}
