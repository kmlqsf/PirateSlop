using FishNet.Object;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed class ExperimentalShipEquipment : NetworkBehaviour
    {
        public bool Enabled = true;
        public NetworkFish[] Prefabs;
        NetworkFish[] spawned;
        float nextSpawn;
        public override void OnStartServer()
        {
            spawned = new NetworkFish[Prefabs.Length];
            SpawnItems();
        }
        void Update() { if(IsServerInitialized && Enabled && Time.time >= nextSpawn) SpawnItems(); }
        void SpawnItems()
        {
            nextSpawn = Time.time + 20;
            if (!Enabled) return;
            for(int i=0;i<Prefabs.Length;i++)
            {
                if(Prefabs[i]==null || (spawned[i]!=null && spawned[i].IsSpawned)) continue;
                Vector3 point=transform.TransformPoint(new Vector3(-2.8f+i*1.7f,4.6f,-6));
                float nearest = 4f;
                foreach(var hit in Physics.RaycastAll(point+transform.up*2,-transform.up,4,~0,QueryTriggerInteraction.Ignore))
                    if(hit.collider.GetComponentInParent<NetworkShip>() == GetComponent<NetworkShip>() && hit.distance < nearest && Vector3.Dot(hit.normal,transform.up)>.7f)
                    { nearest=hit.distance; point=hit.point; }
                var shape=Prefabs[i].GetComponent<BoxCollider>();
                point+=transform.up*(shape.size.y*.5f-shape.center.y+.025f);
                var item=Instantiate(Prefabs[i],point,transform.rotation);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(item.gameObject,gameObject.scene);
                item.Place(NetworkObject,point,transform.rotation);
                ServerManager.Spawn(item.NetworkObject); spawned[i]=item;
            }
        }
        public override void OnStopServer()
        {
            if(spawned==null) return;
            foreach(var item in spawned) if(item!=null && item.IsSpawned) ServerManager.Despawn(item.NetworkObject);
        }
    }
}
