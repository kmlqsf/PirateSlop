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
    }
}
