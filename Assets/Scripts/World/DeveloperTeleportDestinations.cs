using System;
using System.Collections.Generic;
using System.Linq;
using PirateSlop.Networking;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PirateSlop.World
{
    public struct DeveloperTeleportDestination
    {
        public string Key, Name;
        public int Category;
        public Vector3 Position;
        public float Radius;
    }

    public static class DeveloperTeleportDestinations
    {
        public static DeveloperTeleportDestination[] Collect(Scene scene)
        {
            var places = new List<DeveloperTeleportDestination>();
            var world = ProceduralWorld.Instance;
            if (world == null || !world.Ready || world.gameObject.scene != scene) return places.ToArray();
            foreach (var location in world.Layout.Locations)
            {
                bool rock = location.Type.Shape is Landform.Reef or Landform.SeaStack or Landform.RockPassage or Landform.ReefPassage;
                Add(places, "location/" + location.Id, location.Type.Id + " · " + location.Id, rock ? 0 : 2, location.Position, location.Radius * 1.4f);
            }
            foreach (var point in world.Layout.Points)
            {
                if (point.Tag != WorldDecorationPlacer.Tag || world.Profile.Decorations == null || point.Rule < 0 || point.Rule >= world.Profile.Decorations.Length) continue;
                var decoration = world.Profile.Decorations[point.Rule];
                if (decoration.Prefab != null) Add(places, "decoration/" + point.Id, decoration.Prefab.name + " · " + point.Id, decoration.Prefab.name.IndexOf("island", StringComparison.OrdinalIgnoreCase) >= 0 ? 2 : 0, point.Position, decoration.ClearanceRadius);
            }
            foreach (var gallery in UnityEngine.Object.FindObjectsByType<EnvironmentTestGallery>(FindObjectsSortMode.None))
            {
                if (gallery.gameObject.scene != scene) continue;
                var islands = gallery.transform.Find("SandyIslandTestRow");
                foreach (var label in gallery.Labels)
                {
                    if (label == null || islands != null && label.IsChildOf(islands)) continue;
                    var model = gallery.transform.Find(label.name);
                    if (model == null) continue;
                    Add(places, "gallery/" + label.name, label.name, label.name.IndexOf("island", StringComparison.OrdinalIgnoreCase) >= 0 ? 2 : 0, label.position, Radius(model));
                }
                if (islands != null) foreach (Transform island in islands)
                {
                    if (island.GetComponentInChildren<Terrain>() == null) continue;
                    string title = island.name.StartsWith("SmallSandyIsland", StringComparison.Ordinal) ? "Песчаный остров" : island.name.StartsWith("TwoLandingIsland", StringComparison.Ordinal) ? "Остров с двумя проходами" : "Остров с одним проходом и скальным кольцом";
                    Add(places, "island/" + island.name, title, 2, island.position, Radius(island));
                }
            }
            foreach (var chest in NetworkLootChest.ServerChests)
            {
                if (chest == null || !chest.IsSpawned || chest.gameObject.scene != scene || chest.SupportingShip != null || chest.Carrier != null || chest.OnWhale || chest.InClam || chest.OnBalance || chest.InRigging) continue;
                string title = chest.Kind switch { SeaLootKind.Capture => "Захват зоны", SeaLootKind.Raft => "Плот", SeaLootKind.Sunken => "Подводный сундук", SeaLootKind.Shark => "Стая акул", _ => null };
                if (title != null) Add(places, "chest/" + chest.NetworkObject.ObjectId, title + " · " + chest.NetworkObject.ObjectId, 1, chest.Kind == SeaLootKind.Raft && chest.RaftBody != null ? chest.RaftBody.position : chest.EventPoint, 0);
            }
            foreach (var skull in NetworkSkullEvent.ServerEvents)
                if (skull != null && skull.IsSpawned && skull.gameObject.scene == scene) Add(places, "skull/" + skull.NetworkObject.ObjectId, "Огненный череп · " + skull.NetworkObject.ObjectId, 1, skull.transform.position, skull.PlatformRadius);
            foreach (var clam in NetworkClamEvent.ServerEvents)
                if (clam != null && clam.IsSpawned && clam.gameObject.scene == scene) Add(places, "clam/" + clam.NetworkObject.ObjectId, "Раковина с сундуком · " + clam.NetworkObject.ObjectId, 1, clam.transform.position, clam.PlatformRadius);
            foreach (var balance in NetworkBalanceEvent.ServerEvents)
                if (balance != null && balance.IsSpawned && balance.gameObject.scene == scene) Add(places, "balance/" + balance.NetworkObject.ObjectId, "Остров с весами · " + balance.NetworkObject.ObjectId, 1, balance.transform.position, 40);
            foreach (var rigging in NetworkRiggingEvent.ServerEvents)
                if (rigging != null && rigging.IsSpawned && rigging.gameObject.scene == scene) Add(places, "rigging/" + rigging.NetworkObject.ObjectId, "Сундук в такелаже · " + rigging.NetworkObject.ObjectId, 1, rigging.transform.position, rigging.PlatformRadius);
            foreach (var whale in UnityEngine.Object.FindObjectsByType<WhaleLootPoint>(FindObjectsSortMode.None))
                if (whale.IsSpawned && whale.gameObject.scene == scene) Add(places, "whale/" + whale.NetworkObject.ObjectId, "Раненый кит · " + whale.NetworkObject.ObjectId, 1, whale.transform.position, 18);
            return places.OrderBy(p => p.Category).ThenBy(p => p.Name, StringComparer.Ordinal).ToArray();
        }

        static void Add(List<DeveloperTeleportDestination> places, string key, string name, int category, Vector3 point, float radius)
        {
            places.Add(new DeveloperTeleportDestination { Key = key, Name = name, Category = category, Position = point, Radius = radius });
        }

        static float Radius(Transform root)
        {
            var bounds = new Bounds(root.position, Vector3.zero);
            foreach (var collider in root.GetComponentsInChildren<Collider>()) if (collider.enabled && !collider.isTrigger) bounds.Encapsulate(collider.bounds);
            return Mathf.Max(bounds.extents.x, bounds.extents.z);
        }

        public static bool TryFindArrival(DeveloperTeleportDestination place, NetworkShip ship, out Vector3 position, out float yaw)
        {
            position = default; yaw = ship.transform.eulerAngles.y;
            var world = ProceduralWorld.Instance;
            if (world == null || !world.Ready) return false;
            float height = 4f;
            foreach (var collider in ship.GetComponentsInChildren<Collider>()) if (collider.enabled && !collider.isTrigger) height = Mathf.Max(height, collider.bounds.max.y - ship.transform.position.y + 2f);
            if (place.Key.StartsWith("location/", StringComparison.Ordinal))
            {
                string prefix = place.Key.Substring(9) + "/";
                foreach (var point in world.Points("ship_approach"))
                    if (point.Id.StartsWith(prefix, StringComparison.Ordinal) && Free(point.Position, point.Yaw, ship, world, height)) { position = point.Position; yaw = point.Yaw; return true; }
            }
            float clearance = place.Radius + ship.HullHalfExtents.y + 12f;
            for (int ring = 0; ring < 12; ring++) for (int side = 0; side < 16; side++)
            {
                float angle = side * Mathf.PI * 2f / 16;
                var candidate = place.Position + new Vector3(Mathf.Sin(angle), 0, -Mathf.Cos(angle)) * (clearance + ring * 15f);
                candidate.y = world.Layout.SeaLevel;
                float heading = Mathf.Atan2(place.Position.x - candidate.x, place.Position.z - candidate.z) * Mathf.Rad2Deg;
                if (!Free(candidate, heading, ship, world, height)) continue;
                position = candidate; yaw = heading; return true;
            }
            return false;
        }

        static bool Free(Vector3 point, float yaw, NetworkShip ship, ProceduralWorld world, float height)
        {
            if (!world.CanSail(point, yaw)) return false;
            var rotation = Quaternion.Euler(0, yaw, 0);
            var extents = new Vector3(ship.HullHalfExtents.x + 1f, (height + 3f) * .5f, ship.HullHalfExtents.y + 2f);
            foreach (var collider in Physics.OverlapBox(point + Vector3.up * ((height - 3f) * .5f), extents, rotation, ~LayerMask.GetMask("Water"), QueryTriggerInteraction.Ignore))
            {
                if (collider.transform.IsChildOf(ship.transform) || collider.GetComponentInParent<NetworkPlayer>() != null) continue;
                var fish = collider.GetComponentInParent<NetworkFish>();
                if (fish != null && fish.SupportingShip == ship) continue;
                var ball = collider.GetComponentInParent<Cannonball>();
                if (ball != null && ball.PlatformBody == ship.Body) continue;
                var chest = collider.GetComponentInParent<NetworkLootChest>();
                if (chest != null && chest.SupportingShip == ship) continue;
                return false;
            }
            return true;
        }
    }
}
