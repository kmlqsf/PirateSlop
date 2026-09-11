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
        public void DeveloperCommand(byte command, int count = 1) => DeveloperCommandServerRpc(command, count);
        [ServerRpc]
        void DeveloperCommandServerRpc(byte command, int count)
        {
            if (!DeveloperMenu.Available || (!IsOwner && !DeveloperMenu.AllowRemote)) { DeveloperResultTargetRpc(Owner, "Нужно разрешение хоста."); return; }
            if (Time.time < nextDeveloperCommand) return;
            nextDeveloperCommand = Time.time + .3f;
            var session = SessionController.Instance;
            var health = GetComponent<CombatHealth>();
            if (command == 6) { health.Heal(health.MaxHealth); DeveloperResultTargetRpc(Owner, "Здоровье восстановлено."); return; }
            if (command == 12) { health.Damage(10f); DeveloperResultTargetRpc(Owner, "Нанесено 10 урона."); return; }
            if (command >= 32 && command < 64)
            {
                var item = (InventoryItem)(command - 32);
                int added = 0, requested = Mathf.Clamp(count, 1, 20);
                while (added < requested && AddItem(item)) added++;
                DeveloperResultTargetRpc(Owner, "Добавлено: " + added + " / " + requested + (added < requested ? " · Инвентарь заполнен или предмет недоступен." : "")); return;
            }
            if (command == 8) { DeveloperResultTargetRpc(Owner, AddItem(InventoryItem.Cannon) ? "Пушка добавлена." : "Инвентарь заполнен."); return; }
            if (command == 9)
            {
                foreach (var item in developerObjects) if (item != null && item.IsSpawned) ServerManager.Despawn(item);
                developerObjects.Clear(); DeveloperResultTargetRpc(Owner, "Тестовые объекты удалены."); return;
            }
            developerObjects.RemoveAll(o => o == null || !o.IsSpawned);
            if (developerObjects.Count >= 20) { DeveloperResultTargetRpc(Owner, "Удалите тестовые объекты: достигнут лимит 20."); return; }
            if (command == 0)
            {
                if (session == null) return;
                var spawned = session.SpawnDeveloperShip(transform.position, transform.eulerAngles.y);
                if (spawned != null) developerObjects.Add(spawned);
                DeveloperResultTargetRpc(Owner, spawned != null ? "Корабль создан рядом." : "Рядом нет свободного места на воде."); return;
            }
            var itemType = command >= 64 ? (InventoryItem)(command - 64) : command == 2 ? InventoryItem.Cannon : command == 3 ? InventoryItem.Pistol : command == 4 ? InventoryItem.Sabre : InventoryItem.Cannonball;
            if (command > 5 && command < 64 && command != 13) return;
            if (itemType < InventoryItem.Fish || itemType > InventoryItem.BoomerangCannonball || itemType == InventoryItem.Mallet || itemType == InventoryItem.Plank) return;
            int prefabIndex = CannonAmmo.IsBall(itemType) ? (int)InventoryItem.Cannonball : (int)itemType;
            if (command != 1 && command != 13 && (DropPrefabs == null || prefabIndex >= DropPrefabs.Length || DropPrefabs[prefabIndex] == null))
            { DeveloperResultTargetRpc(Owner, "Префаб предмета не назначен."); return; }
            var prefab = command == 13 ? session?.PlayerPrefab : command == 1 ? DeveloperTargetPrefab : DropPrefabs[prefabIndex].NetworkObject;
            if (prefab == null) { DeveloperResultTargetRpc(Owner, "Префаб не назначен."); return; }
            Vector3 origin = transform.position + transform.forward * 3 + Vector3.up * 3;
            RaycastHit floor = default; float distance = 10;
            foreach (var hit in Physics.RaycastAll(origin, Vector3.down, distance, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) && hit.normal.y > .5f && hit.distance < distance) { floor = hit; distance = hit.distance; }
            if (floor.collider == null) { DeveloperResultTargetRpc(Owner, "Направьте взгляд на свободную палубу или землю."); return; }
            var bounds = prefab.GetComponent<Collider>().bounds;
            var box = prefab.GetComponent<BoxCollider>();
            float height = command == 13 ? 0f : command == 1 ? 1f : box != null ? box.size.y * .5f - box.center.y : .18f;
            Vector3 position = floor.point + Vector3.up * (height + .03f);
            if (command == 13 && Physics.CheckCapsule(position + Vector3.up * .35f, position + Vector3.up * 1.5f, .3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            { DeveloperResultTargetRpc(Owner, "Перед вами недостаточно места для манекена."); return; }
            var obj = Instantiate(prefab, position, Quaternion.identity);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(obj.gameObject, gameObject.scene);
            var support = floor.collider.GetComponentInParent<NetworkShip>();
            if (command == 13)
            {
                obj.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(transform.position - position, Vector3.up));
                var dummy = obj.GetComponent<NetworkPlayer>();
                dummy.IsTrainingDummy = true;
                dummy.TeamId.Value = int.MaxValue;
                dummy.Passenger.Attach(support != null ? support.Body : null);
                ServerManager.Spawn(obj);
                dummy.ParticipantId.Value = -(obj.ObjectId + 1);
                developerObjects.Add(obj);
                DeveloperResultTargetRpc(Owner, "Враг-манекен создан."); return;
            }
            var drop = obj.GetComponent<NetworkFish>();
            if (drop != null && CannonAmmo.IsBall(itemType)) drop.SetAmmoItem(itemType);
            if (drop != null) drop.Place(support != null ? support.NetworkObject : null, position, Quaternion.identity);
            var target = obj.GetComponent<DeveloperTarget>();
            if (target != null) target.Place(support, position);
            ServerManager.Spawn(obj); developerObjects.Add(obj);
            DeveloperResultTargetRpc(Owner, "Объект создан.");
        }
        [TargetRpc] void DeveloperResultTargetRpc(NetworkConnection connection, string message) => GetComponent<DeveloperMenu>().Report(message);
    }

}
