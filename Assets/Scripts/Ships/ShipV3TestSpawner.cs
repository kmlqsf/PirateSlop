using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Object;
using PirateSlop.World;
using PirateSlop.Networking;
using UnityEngine;

namespace PirateSlop.Ships
{
    public static class ShipV3TestSpawner
    {
        public static void Spawn(NetworkManager manager, WorldLayout layout)
        {
            if (!EnvironmentTestGallery.IsTest(layout) || !manager.ServerManager.Started) return;
            var gallery = Resources.Load<EnvironmentTestGallery>(EnvironmentTestGallery.ResourcePath);
            var registry = manager.SpawnablePrefabs as SinglePrefabObjects;
            if (registry == null)
                throw new System.InvalidOperationException("ShipV3 requires the session SinglePrefabObjects collection.");
            NetworkObject prefab = null;
            foreach (var registered in registry.Prefabs)
            {
                if (registered != null && registered.name == "ShipV3Test")
                {
                    prefab = registered;
                    break;
                }
            }
            if (gallery == null || prefab == null)
                throw new System.InvalidOperationException("ShipV3 test prefab is missing from the session spawnable collection.");
            var ship = Object.Instantiate(prefab, gallery.Spawn + new Vector3(0, layout.SeaLevel, -60f), Quaternion.Euler(0, 90, 0));
            var network = ship.GetComponent<NetworkShip>();
            network.ParticipantId.Value = 1000001;
            network.TeamId.Value = -1;
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(ship.gameObject, UnityEngine.SceneManagement.SceneManager.GetSceneByName("NetworkOcean"));
            manager.ServerManager.Spawn(ship);
        }
    }
}
