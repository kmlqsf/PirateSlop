using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using UnityEngine;

namespace PirateSlop.Networking
{
    public struct BoardingCable
    {
        public NetworkObject Target;
        public int Cannon;
        public Vector3 Point, Normal;
        public float Length, Desired;
        public int Hits;
    }
    public sealed partial class NetworkCannon
    {
        readonly SyncList<BoardingCable> cables = new();
        readonly System.Collections.Generic.Dictionary<int, BoardingHookTarget> hookViews = new();
        float nextWinch, nextCableSync;
        public bool HasBoarding(int index)
        {
            foreach (var cable in cables) if (cable.Cannon == index && cable.Target != null && cable.Target.IsSpawned) return true;
            return false;
        }
        public void AttachBoarding(int index, NetworkShip target, Vector3 point, Vector3 normal)
        {
            var cannon=Cannon(index);
            if (!IsServerInitialized || cannon == null || target == null || !target.IsSpawned || target.gameObject == gameObject || HasBoarding(index)) return;
            float length=Vector3.Distance(cannon.Muzzle.position,point);
            if (length>60 || length<2) return;
            var cable=new BoardingCable { Target=target.NetworkObject, Cannon=index, Point=target.transform.InverseTransformPoint(point+normal*.18f), Normal=target.transform.InverseTransformDirection(normal), Length=Mathf.Max(4,length+.5f), Desired=Mathf.Max(4,length+.5f) };
            for(int i=0;i<cables.Count;i++) if(cables[i].Target==null) { cables[i]=cable; return; }
            cables.Add(cable);
        }
        public void Winch(int index, float direction) => WinchServerRpc(index,direction);
        [ServerRpc(RequireOwnership=false)]
        void WinchServerRpc(int index, float direction, NetworkConnection sender=null)
        {
            var player=sender != null ? SessionController.Instance.GetPlayer(sender.ClientId) : null;
            var cannon=Cannon(index);
            if (player==null || player.Motor.IsDead || player.Motor.IsClimbing || player.Motor.LocomotionLocked || cannon==null || !float.IsFinite(direction) || Time.time<nextWinch || !player.GetComponent<NetworkWeapon>().CanReachCannon(cannon)) return;
            if (player.Ship != GetComponent<NetworkShip>()) return;
            for(int i=0;i<cables.Count;i++)
            {
                var cable=cables[i];
                if(cable.Cannon!=index || cable.Target==null) continue;
                cable.Desired=Mathf.Clamp(cable.Desired-Mathf.Clamp(direction,-1,1)*2,4,80);
                cables[i]=cable; nextWinch=Time.time+.08f; return;
            }
        }
        public void StrikeBoarding(int slot)
        {
            if (!IsServerInitialized || slot<0 || slot>=cables.Count) return;
            var cable=cables[slot];
            if(cable.Target==null) return;
            cable.Hits++;
            if(cable.Hits>=2) cable.Target=null;
            cables[slot]=cable;
        }
        void UpdateBoarding()
        {
            if (!IsSpawned) return;
            bool sync=IsServerInitialized && Time.time>=nextCableSync;
            if(sync) nextCableSync=Time.time+.05f;
            for(int i=0;i<cables.Count;i++)
            {
                var cable=cables[i];
                var cannon=Cannon(cable.Cannon);
                bool valid=cable.Target!=null && cable.Target.IsSpawned && cannon!=null;
                if(!valid)
                {
                    if(hookViews.TryGetValue(i,out var old)) { if(old!=null) Destroy(old.gameObject); hookViews.Remove(i); }
                    if(IsServerInitialized && cable.Target!=null) { cable.Target=null; cables[i]=cable; }
                    continue;
                }
                Vector3 end=cable.Target.transform.TransformPoint(cable.Point);
                if(sync)
                {
                    if(Vector3.Distance(cannon.Muzzle.position,end)>120) { cable.Target=null; cables[i]=cable; continue; }
                    cable.Length=Mathf.MoveTowards(cable.Length,cable.Desired,.125f);
                    cables[i]=cable;
                }
                if(!hookViews.TryGetValue(i,out var hook) || hook==null)
                {
                    var visual=Instantiate(Resources.Load<GameObject>("BoardingHookVisual"));
                    visual.transform.SetParent(cable.Target.transform,false);
                    hook=visual.AddComponent<BoardingHookTarget>();
                    hook.Source=this; hook.Slot=i;
                    hook.Rope=visual.AddComponent<LineRenderer>();
                    hook.Rope.sharedMaterial=Resources.Load<Material>("HookRope");
                    hook.Rope.widthMultiplier=.055f; hook.Rope.positionCount=25;
                    hook.Rope.generateLightingData=true; hook.Rope.numCapVertices=3;
                    hookViews[i]=hook;
                }
                if (hook.transform.parent != cable.Target.transform) hook.transform.SetParent(cable.Target.transform, false);
                hook.transform.localPosition=cable.Point;
                hook.transform.localRotation=Quaternion.LookRotation(cable.Normal);
                Vector3 start=cannon.Muzzle.position;
                float sag=Mathf.Min(8,Mathf.Max(0,cable.Length-Vector3.Distance(start,end))*.35f+.12f);
                for(int p=0;p<25;p++) { float t=p/24f; hook.Rope.SetPosition(p,Vector3.Lerp(start,end,t)+Vector3.down*(Mathf.Sin(t*Mathf.PI)*sag)); }
            }
        }
        void OnDestroy()
        {
            foreach(var hook in hookViews.Values) if(hook!=null) Destroy(hook.gameObject);
        }
        public static void ConstrainBoarding(ShipController ship, ref Vector3 position, Quaternion rotation)
        {
            for(int pass=0;pass<2;pass++) foreach(var source in NetworkShip.ActiveShips)
            {
                if(source==null) continue;
                var network=source.GetComponent<NetworkCannon>();
                if(network==null) continue;
                foreach(var cable in network.cables)
                {
                    if(cable.Target==null || !cable.Target.IsSpawned) continue;
                    var cannon=network.Cannon(cable.Cannon);
                    if(cannon==null) continue;
                    Vector3 local, other;
                    if(ship.gameObject==source.gameObject)
                    { local=source.transform.InverseTransformPoint(cannon.Muzzle.position); other=cable.Target.transform.TransformPoint(cable.Point); }
                    else if(ship.gameObject==cable.Target.gameObject)
                    { local=cable.Point; other=cannon.Muzzle.position; }
                    else continue;
                    Vector3 anchor=position+rotation*local;
                    Vector3 flat=Vector3.ProjectOnPlane(anchor-other,Vector3.up);
                    float height=anchor.y-other.y;
                    float limit=Mathf.Sqrt(Mathf.Max(1,cable.Length*cable.Length-height*height));
                    if(flat.magnitude>limit) position-=flat.normalized*(flat.magnitude-limit);
                }
            }
        }
    }
}
