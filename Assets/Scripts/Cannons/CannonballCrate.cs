using System.Collections.Generic;
using UnityEngine;
using PirateSlop.Networking;

namespace PirateSlop
{
    public sealed class CannonballCrate : MonoBehaviour
    {
        public Transform SpawnPoint;
        public Transform DeckSupplyPoint;
        public Cannonball Supply;
        public NetworkFish SpecialSupplyPrefab;
        public float SpecialSupplySpacing = .65f;
        readonly NetworkFish[] specialSupplies = new NetworkFish[5];
        static readonly InventoryItem[] specialAmmo = {
            InventoryItem.Cannonball, InventoryItem.FireCannonball, InventoryItem.IceCannonball,
            InventoryItem.PushCannonball, InventoryItem.BoomerangCannonball
        };
        public GameObject Kit;
        public SimpleCannon CannonPrefab;
        public readonly List<SimpleCannon> Cannons = new();
        public ShipController Ship => GetComponentInParent<ShipController>();
        public NetworkCannon Network => Ship.GetComponent<NetworkCannon>();
        public bool KitAvailable => Kit != null && Kit.activeSelf;

        void Start()
        {
            if (Network == null) ResetSupply();
        }

        public void ResetSupply()
        {
            Supply.Loaded = Supply.Held = false;
            Supply.Ammo = InventoryItem.Cannonball;
            Supply.Body.isKinematic = true;
            Supply.transform.SetParent(Ship.transform, true);
            Supply.transform.SetPositionAndRotation(SpawnPoint.position, SpawnPoint.rotation);
            Supply.GetComponent<Collider>().enabled = true;
            Supply.AttachToPlatform(Ship.GetComponent<Rigidbody>());
            Supply.gameObject.SetActive(true);
        }

        public SimpleCannon AddCannon(Vector3 localPosition, Quaternion localRotation)
        {
            var cannon = Instantiate(CannonPrefab, Ship.transform);
            cannon.transform.localPosition = localPosition;
            cannon.transform.localRotation = localRotation;
            cannon.Crate = this;
            cannon.Network = Network;
            cannon.Index = Cannons.Count;
            cannon.InitializeSupply(Supply);
            Cannons.Add(cannon);
            GameAudio.Play(SoundCue.Place, cannon.transform.position);
            return cannon;
        }

        void Update()
        {
            UpdateSpecialSupply();
            if (Supply == null || Supply.Loaded || Supply.Held || (Network != null && !Network.IsServerInitialized)) return;
            if (Supply.transform.position.y < Ship.transform.position.y - 15f) ResetSupply();
        }

        void UpdateSpecialSupply()
        {
            var network = Network;
            if (network == null || !network.IsServerInitialized || !network.IsSpawned || SpecialSupplyPrefab == null) return;
            for (int i = 0; i < specialSupplies.Length; i++)
            {
                var item = specialSupplies[i];
                if (item != null && item.IsSpawned)
                {
                    var loose = item.GetComponent<NetworkLooseCannonball>();
                    if (loose.IsHeld || item.transform.position.y >= Ship.transform.position.y - 15f) continue;
                    network.ServerManager.Despawn(item.NetworkObject);
                }
                var anchor = DeckSupplyPoint != null ? DeckSupplyPoint : SpawnPoint;
                Vector3 point = anchor.position + Ship.transform.forward * (SpecialSupplySpacing * i);
                float distance = 3f;
                foreach (var hit in Physics.RaycastAll(point + Vector3.up, Vector3.down, distance, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.attachedRigidbody != Ship.GetComponent<Rigidbody>() || hit.normal.y < .5f || hit.distance >= distance) continue;
                    distance = hit.distance;
                    point.y = hit.point.y + SpecialSupplyPrefab.GetComponent<SphereCollider>().radius + .02f;
                }
                item = Instantiate(SpecialSupplyPrefab, point, SpawnPoint.rotation);
                item.name = specialAmmo[i].ToString() + "Supply";
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(item.gameObject, gameObject.scene);
                item.SetAmmoItem(specialAmmo[i]);
                item.Place(network.NetworkObject, point, SpawnPoint.rotation);
                network.ServerManager.Spawn(item.NetworkObject);
                var ball = item.GetComponent<Cannonball>();
                ball.Ammo = specialAmmo[i];
                ball.Body.isKinematic = true;
                ball.AttachToPlatform(Ship.GetComponent<Rigidbody>());
                specialSupplies[i] = item;
            }
        }

        public void ClearSpecialSupply()
        {
            var network = Network;
            for (int i = 0; i < specialSupplies.Length; i++)
            {
                var item = specialSupplies[i];
                if (network != null && network.ServerManager.Started && item != null && item.IsSpawned)
                    network.ServerManager.Despawn(item.NetworkObject);
                specialSupplies[i] = null;
            }
        }

        void OnDestroy()
        {
            if (Supply != null && !Supply.transform.IsChildOf(transform)) Destroy(Supply.gameObject);
        }
    }
}
