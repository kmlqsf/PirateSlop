using System.Collections.Generic;
using UnityEngine;
using PirateSlop.Networking;

namespace PirateSlop
{
    public sealed class CannonballCrate : MonoBehaviour
    {
        public Transform SpawnPoint;
        public Cannonball Supply;
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
            Supply.Body.isKinematic = true;
            Supply.transform.SetParent(Ship.transform, true);
            Supply.transform.SetPositionAndRotation(SpawnPoint.position, SpawnPoint.rotation);
            Supply.GetComponent<Collider>().enabled = true;
            Supply.AttachToPlatform(Ship.GetComponent<Rigidbody>());
            Supply.gameObject.SetActive(true);
        }

        public SimpleCannon AddCannon(Vector3 localPosition, float yaw)
        {
            var cannon = Instantiate(CannonPrefab, Ship.transform);
            cannon.transform.localPosition = localPosition;
            cannon.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            cannon.Crate = this;
            cannon.Network = Network;
            cannon.Index = Cannons.Count;
            cannon.InitializeSupply(Supply);
            Cannons.Add(cannon);
            return cannon;
        }

        void Update()
        {
            if (Supply == null || Supply.Loaded || Supply.Held || (Network != null && !Network.IsServerInitialized)) return;
            if (Supply.transform.position.y < Ship.transform.position.y - 15f) ResetSupply();
        }

        void OnDestroy()
        {
            if (Supply != null && !Supply.transform.IsChildOf(transform)) Destroy(Supply.gameObject);
        }
    }
}
