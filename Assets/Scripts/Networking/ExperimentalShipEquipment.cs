using FishNet.Object;
using UnityEngine;

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
        public override void OnStartServer()
        {
            spawned = new NetworkFish[Prefabs.Length];
            bottleRespawnAt = new float[Prefabs.Length];
            SpawnItems();
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
            if (bottleRespawnAt == null)
            {
                bottleRespawnAt = new float[Prefabs.Length];
                for (int i = 0; i < bottleRespawnAt.Length; i++) bottleRespawnAt[i] = float.PositiveInfinity;
            }
            for(int i=0;i<Prefabs.Length;i++)
            {
                if (bottlesOnly && (Prefabs[i] == null ||
                    (Prefabs[i].Item != InventoryItem.FogBottle && Prefabs[i].Item != InventoryItem.VortexBottle &&
                     Prefabs[i].Item != InventoryItem.Musket && Prefabs[i].Item != InventoryItem.DoubleBarrel))) continue;
                if(Prefabs[i]==null || (spawned[i]!=null && spawned[i].IsSpawned)) continue;
                bool bottle = Prefabs[i].Item == InventoryItem.FogBottle || Prefabs[i].Item == InventoryItem.VortexBottle;
                if (bottle)
                {
                    if (float.IsPositiveInfinity(bottleRespawnAt[i])) bottleRespawnAt[i] = Time.time + 5f;
                    if (Time.time < bottleRespawnAt[i]) continue;
                }
                Vector3 point=i<SpawnPoints.Length && SpawnPoints[i]!=null ? SpawnPoints[i].position : transform.TransformPoint(new Vector3(-3.4f+(i%5)*1.7f,4.6f,(i/5)*1.2f));
                float nearest = 4f;
                foreach(var hit in Physics.RaycastAll(point+transform.up*2,-transform.up,4,~0,QueryTriggerInteraction.Ignore))
                    if(hit.collider.GetComponentInParent<NetworkShip>() == GetComponent<NetworkShip>() && hit.distance < nearest && Vector3.Dot(hit.normal,transform.up)>.7f)
                    { nearest=hit.distance; point=hit.point; }
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
