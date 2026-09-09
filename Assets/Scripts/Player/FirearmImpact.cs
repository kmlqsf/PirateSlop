using UnityEngine;
using PirateSlop.Networking;

namespace PirateSlop
{
    public sealed class FirearmImpact : MonoBehaviour
    {
        static FirearmImpact instance;
        const int Capacity=128;
        readonly Transform[] marks=new Transform[Capacity];
        readonly float[] expiry=new float[Capacity];
        MaterialPropertyBlock block;
        Material material;
        int next;
        float nextAudio;
        void Awake()
        {
            block=new MaterialPropertyBlock();
        }
        static FirearmImpact Get()
        {
            if(instance!=null) return instance;
            instance=new GameObject("FirearmImpactPool").AddComponent<FirearmImpact>();
            instance.material=Resources.Load<Material>("BulletMark");
            return instance;
        }
        public static void Present(FirearmShot shot,bool detailed)
        {
            if(Application.isBatchMode || !Application.isPlaying) return;
            ResolvePoint(ref shot);
            if(shot.Water) { if(detailed) CombatVfx.Splash(shot.End,.18f);return; }
            if(!shot.Hit) return;
            var pool=Get();
            if(shot.LeaveMark) pool.Mark(shot);
            if(detailed)
            {
                FirearmVfx.Impact(shot.End,shot.Normal,shot.Surface);
                if(Time.unscaledTime>=pool.nextAudio)
                {
                    pool.nextAudio=Time.unscaledTime+.045f;
                    var cue=shot.Surface==BulletSurfaceKind.Metal?SoundCue.BulletMetal:shot.Surface==BulletSurfaceKind.Wood?SoundCue.BulletWood:shot.Surface==BulletSurfaceKind.Flesh?SoundCue.BulletFlesh:SoundCue.BulletStone;
                    GameAudio.Play(cue,shot.End,.75f);
                }
            }
        }
        public static void ResolvePoint(ref FirearmShot shot)
        {
            if(shot.Anchor==null && shot.ShipId>0)
                foreach(var ship in NetworkShip.ActiveShips) if(ship!=null && ship.ParticipantId.Value==shot.ShipId) { shot.Anchor=ship.NetworkObject;break; }
            if(shot.Anchor!=null)
            { shot.End=shot.Anchor.transform.TransformPoint(shot.LocalEnd);shot.Normal=shot.Anchor.transform.TransformDirection(shot.LocalNormal).normalized; }
        }
        void Mark(FirearmShot shot)
        {
            if(material==null) return;
            if(!Physics.Raycast(shot.End+shot.Normal*.07f,-shot.Normal,out var hit,.15f,~0,QueryTriggerInteraction.Ignore)) return;
            if(Vector3.Dot(hit.normal,shot.Normal)<.8f || hit.collider.GetComponentInParent<CombatHealth>()!=null) return;
            Transform anchor=hit.collider.transform;
            Quaternion rotation=Quaternion.LookRotation(-hit.normal)*Quaternion.Euler(0,0,Random.Range(0,360));
            float size=shot.Surface==BulletSurfaceKind.Wood?.105f:shot.Surface==BulletSurfaceKind.Metal?.065f:.09f;
            if(!Fits(hit.collider,hit.point,hit.normal,rotation,size)) size*=.5f;
            if(!Fits(hit.collider,hit.point,hit.normal,rotation,size)) return;
            int index=next++%Capacity;
            if(marks[index]==null)
            {
                var go=GameObject.CreatePrimitive(PrimitiveType.Quad);go.name="BulletImpactMark";
                var collider=go.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
                var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
                marks[index]=go.transform;
            }
            var mark=marks[index];mark.SetParent(null,false);mark.gameObject.SetActive(true);
            mark.SetPositionAndRotation(hit.point+hit.normal*.006f,rotation);
            mark.localScale=new Vector3(size*(shot.Surface==BulletSurfaceKind.Wood?.7f:1),size,1);
            mark.SetParent(anchor,true);
            block.SetColor("_BaseColor",shot.Surface==BulletSurfaceKind.Metal?new Color(.2f,.23f,.24f,.85f):shot.Surface==BulletSurfaceKind.Wood?new Color(.16f,.09f,.035f,.9f):new Color(.18f,.16f,.13f,.8f));
            mark.GetComponent<Renderer>().SetPropertyBlock(block);
            expiry[index]=Time.unscaledTime+35;
        }
        static bool Fits(Collider collider,Vector3 point,Vector3 normal,Quaternion rotation,float size)
        {
            for(int i=0;i<4;i++)
            {
                Vector3 corner=point+rotation*new Vector3((i%2==0?-1:1)*size*.5f,(i<2?-1:1)*size*.5f,0);
                if(!collider.Raycast(new Ray(corner+normal*.03f,-normal),out var hit,.06f) || Vector3.Dot(hit.normal,normal)<.9f) return false;
            }
            return true;
        }
        void Update()
        {
            for(int i=0;i<Capacity;i++)
                if(marks[i]!=null && marks[i].gameObject.activeSelf && Time.unscaledTime>expiry[i]) marks[i].gameObject.SetActive(false);
        }
        void OnDestroy()
        {
            foreach(var mark in marks) if(mark!=null) Destroy(mark.gameObject);
            if(instance==this) instance=null;
        }
    }
}
