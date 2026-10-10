using FishNet.Object;
using UnityEngine;
using PirateSlop.World;

namespace PirateSlop.Networking
{
    public sealed class ExperimentalShipEquipment : NetworkBehaviour
    {
        public bool Enabled = true;
        public NetworkFish[] Prefabs;
        public InventoryItem[] Items = System.Array.Empty<InventoryItem>();
        public Transform[] SpawnPoints = System.Array.Empty<Transform>();
        NetworkFish[] spawned;
        float[] bottleRespawnAt;
        float nextSpawn;
        readonly RaycastHit[] deckHits = new RaycastHit[64];
        NetworkShip ship;
        public override void OnStartServer()
        {
            spawned = new NetworkFish[Prefabs.Length];
            bottleRespawnAt = new float[Prefabs.Length];
            ship = GetComponent<NetworkShip>();
            nextSpawn = Time.time;
            if (!EnvironmentTestGallery.IsTest(ProceduralWorld.Instance != null ? ProceduralWorld.Instance.Layout : null)) SpawnItems();
        }
        void Update()
        {
            if (!IsServerInitialized || !Enabled) return;
            bool restock = Time.time >= nextSpawn;
            SpawnItems(!restock);
        }
        void SpawnItems(bool bottlesOnly = false)
        {
            if (!bottlesOnly) nextSpawn = Time.time + 20;
            if (!Enabled) return;
            bool testItems = EnvironmentTestGallery.IsTest(ProceduralWorld.Instance != null ? ProceduralWorld.Instance.Layout : null);
            if (testItems && (ship == null || !ship.IsSpawned)) return;
            bool synced = false;
            if (bottleRespawnAt == null)
            {
                bottleRespawnAt = new float[Prefabs.Length];
                for (int i = 0; i < bottleRespawnAt.Length; i++) bottleRespawnAt[i] = float.PositiveInfinity;
            }
            for(int i=0;i<Prefabs.Length;i++)
            {
                if (bottlesOnly && (Prefabs[i] == null ||
                    (Prefabs[i].Item != InventoryItem.FogBottle && Prefabs[i].Item != InventoryItem.VortexBottle &&
                     Prefabs[i].Item != InventoryItem.Musket && Prefabs[i].Item != InventoryItem.DoubleBarrel &&
                     Prefabs[i].Item != InventoryItem.Barricade))) continue;
                if(Prefabs[i]==null || (spawned[i]!=null && spawned[i].IsSpawned)) continue;
                bool bottle = Prefabs[i].Item == InventoryItem.FogBottle || Prefabs[i].Item == InventoryItem.VortexBottle;
                if (bottle)
                {
                    if (float.IsPositiveInfinity(bottleRespawnAt[i])) bottleRespawnAt[i] = Time.time + 5f;
                    if (Time.time < bottleRespawnAt[i]) continue;
                }
                Vector3 point=i<SpawnPoints.Length && SpawnPoints[i]!=null ? SpawnPoints[i].position : transform.TransformPoint(new Vector3(-3.4f+(i%5)*1.7f,4.6f,(i/5)*1.2f));
                if (testItems && !synced) { Physics.SyncTransforms(); synced = true; }
                float nearest = float.PositiveInfinity;
                int count = Physics.RaycastNonAlloc(point + transform.up * 2, -transform.up, deckHits, 4, ~0, QueryTriggerInteraction.Ignore);
                for (int hitIndex = 0; hitIndex < count; hitIndex++)
                {
                    var hit = deckHits[hitIndex];
                    if (hit.collider.GetComponentInParent<NetworkShip>() != ship || hit.distance >= nearest || Vector3.Dot(hit.normal, transform.up) <= .7f) continue;
                    nearest = hit.distance; point = hit.point;
                }
                if (testItems && float.IsPositiveInfinity(nearest)) { nextSpawn = Mathf.Min(nextSpawn, Time.time + .25f); continue; }
                var shape=Prefabs[i].GetComponent<BoxCollider>();
                var sphere=Prefabs[i].GetComponent<SphereCollider>();
                point+=transform.up*((shape!=null ? shape.size.y*.5f-shape.center.y : sphere!=null ? sphere.radius-sphere.center.y : 0f)+.025f);
                var item=Instantiate(Prefabs[i],point,transform.rotation);
                if(i<Items.Length && CannonAmmo.IsBall(Items[i])) item.SetAmmoItem(Items[i]);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(item.gameObject,gameObject.scene);
                item.Place(NetworkObject,point,transform.rotation);
                ServerManager.Spawn(item.NetworkObject); spawned[i]=item;
                if (bottle) bottleRespawnAt[i] = float.PositiveInfinity;
            }
        }
        public override void OnStopServer()
        {
            if(spawned==null) return;
            foreach(var item in spawned) if(item!=null && item.IsSpawned) ServerManager.Despawn(item.NetworkObject);
        }
    }
}
