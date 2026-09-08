using System.Collections.Generic;
using FishNet.Object;
using FishNet.Connection;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkWeapon
    {
        public NetworkObject DeveloperTargetPrefab;
        readonly List<NetworkObject> developerObjects = new();
        float nextDeveloperCommand;
        public void DeveloperCommand(byte command) => DeveloperCommandServerRpc(command);
        [ServerRpc]
        void DeveloperCommandServerRpc(byte command)
        {
            if (!DeveloperMenu.Available || (!IsOwner && !DeveloperMenu.AllowRemote)) { DeveloperResultTargetRpc(Owner, "Host permission required."); return; }
            if (Time.time < nextDeveloperCommand) return;
            nextDeveloperCommand = Time.time + .3f;
            var session = SessionController.Instance;
            var health = GetComponent<CombatHealth>();
            if (command == 6) { health.Heal(health.MaxHealth); DeveloperResultTargetRpc(Owner, "Player healed."); return; }
            if (command == 7)
            {
                var ship = GetComponent<ShipDeckPassenger>().Ship;
                if (ship != null) ship.GetComponent<CombatHealth>().Heal(1200);
                DeveloperResultTargetRpc(Owner, ship != null ? "Ship healed." : "Stand on a ship."); return;
            }
            if (command == 8) { DeveloperResultTargetRpc(Owner, AddItem(InventoryItem.Cannon) ? "Kit added." : "Inventory full."); return; }
            if (command == 9)
            {
                foreach (var item in developerObjects) if (item != null && item.IsSpawned) ServerManager.Despawn(item);
                developerObjects.Clear(); DeveloperResultTargetRpc(Owner, "Test objects removed."); return;
            }
            developerObjects.RemoveAll(o => o == null || !o.IsSpawned);
            if (developerObjects.Count >= 20) { DeveloperResultTargetRpc(Owner, "Remove test objects first (limit 20)."); return; }
            if (command == 0)
            {
                if (session == null) return;
                var spawned = session.SpawnDeveloperShip(transform.position, transform.eulerAngles.y);
                if (spawned != null) developerObjects.Add(spawned);
                DeveloperResultTargetRpc(Owner, spawned != null ? "Ship spawned nearby." : "No clear water nearby."); return;
            }
            var itemType = command == 2 ? InventoryItem.Cannon : command == 3 ? InventoryItem.Pistol : command == 4 ? InventoryItem.Sabre : command == 10 ? InventoryItem.Mallet : command == 11 ? InventoryItem.Plank : InventoryItem.Cannonball;
            if (command > 5 && command != 10 && command != 11) return;
            if (command != 1 && (DropPrefabs == null || (int)itemType >= DropPrefabs.Length || DropPrefabs[(int)itemType] == null))
            { DeveloperResultTargetRpc(Owner, "Spawn prefab is not configured."); return; }
            var prefab = command == 1 ? DeveloperTargetPrefab : DropPrefabs[(int)itemType].NetworkObject;
            Vector3 origin = transform.position + transform.forward * 3 + Vector3.up * 3;
            RaycastHit floor = default; float distance = 10;
            foreach (var hit in Physics.RaycastAll(origin, Vector3.down, distance, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) && hit.normal.y > .5f && hit.distance < distance) { floor = hit; distance = hit.distance; }
            if (floor.collider == null) { DeveloperResultTargetRpc(Owner, "Face a clear deck or ground."); return; }
            var bounds = prefab.GetComponent<Collider>().bounds;
            var box = prefab.GetComponent<BoxCollider>();
            float height = command == 1 ? 1f : box != null ? box.size.y * .5f - box.center.y : .18f;
            Vector3 position = floor.point + Vector3.up * (height + .03f);
            var obj = Instantiate(prefab, position, Quaternion.identity);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(obj.gameObject, gameObject.scene);
            var support = floor.collider.GetComponentInParent<NetworkShip>();
            var drop = obj.GetComponent<NetworkFish>();
            if (drop != null) drop.Place(support != null ? support.NetworkObject : null, position, Quaternion.identity);
            var target = obj.GetComponent<DeveloperTarget>();
            if (target != null) target.Place(support, position);
            ServerManager.Spawn(obj); developerObjects.Add(obj);
            DeveloperResultTargetRpc(Owner, "Spawned.");
        }
        [TargetRpc] void DeveloperResultTargetRpc(NetworkConnection connection, string message) => GetComponent<DeveloperMenu>().Report(message);
    }

}
