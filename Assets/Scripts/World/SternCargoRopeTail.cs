using System.Collections.Generic;
using PirateSlop.Networking;
using UnityEngine;

namespace PirateSlop.World
{
    [DefaultExecutionOrder(65)]
    public sealed class SternCargoRopeTail : MonoBehaviour
    {
        public NetworkTowedCargo Cargo;
        public LineRenderer Loaded, Tail;
        RiggingRopeVisual loadedVisual, tailVisual;
        readonly RaycastHit[] floorHits = new RaycastHit[32];
        readonly List<Vector3> points = new();
        readonly List<float> coils = new();
        float selected, floorLength, flow, layFlow, remaining;
        Vector3 gripLocal, feedLocal;
        uint gripVersion;
        bool ready, feedReady;
        public float Flow => flow;
        public Vector3 GripWorld => Cargo != null && Cargo.Source != null ? Cargo.Source.transform.TransformPoint(gripLocal) : transform.position;
        void Awake()
        {
            loadedVisual = Loaded.GetComponent<RiggingRopeVisual>();
            tailVisual = Tail.GetComponent<RiggingRopeVisual>();
        }
        public Vector3 AboveDeck(Vector3 world,float clearance,bool nearby = false)
        {
            var owner = Cargo.Source;
            var local = owner.transform.InverseTransformPoint(world);
            var start = owner.transform.TransformPoint(new Vector3(local.x,11,local.z));
            int count = Physics.RaycastNonAlloc(start,-owner.transform.up,floorHits,12,~LayerMask.GetMask("Water"),QueryTriggerInteraction.Ignore);
            float floor = float.NegativeInfinity;
            for (int i=0;i<count;i++)
                if (floorHits[i].collider.GetComponentInParent<NetworkShip>() == owner.Ship)
                {
                    float y = owner.transform.InverseTransformPoint(floorHits[i].point).y;
                    if (y<9.4f && (!nearby || y<local.y+.5f)) floor = Mathf.Max(floor,y);
                }
            local.y = Mathf.Max(local.y,floor+clearance);
            return owner.transform.TransformPoint(local);
        }
        public void FollowGrip(Vector3 world)
        {
            var owner = Cargo.Source;
            var target = owner.transform.InverseTransformPoint(world);
            if (!ready) { gripLocal = target; selected = Cargo.SelectedRope; floorLength = Cargo.FloorRopeLength; ready = true; }
            if (Cargo.Operator != null && gripVersion != Cargo.GripVersion)
            { gripLocal = owner.transform.InverseTransformPoint(Cargo.GripStart); gripVersion = Cargo.GripVersion; }
            gripLocal = Vector3.Lerp(gripLocal,target,1-Mathf.Exp(-12*Time.deltaTime));
        }
        void LateUpdate()
        {
            var owner = Cargo != null ? Cargo.Source : null;
            if (owner == null || !Cargo.IsSpawned || !Cargo.KnotUntied || Cargo.FloorDepositCount == 0) { Tail.enabled = false; return; }
            float blend = 1-Mathf.Exp(-10*Time.deltaTime);
            float previous = selected;
            selected = Mathf.Lerp(selected,Cargo.SelectedRope,blend);
            flow = Mathf.Lerp(flow,Mathf.Abs(selected-previous)/Mathf.Max(.001f,Time.deltaTime),blend);
            loadedVisual.LengthOffset = selected;
            float previousFloor = floorLength;
            floorLength = Mathf.Lerp(floorLength,Cargo.FloorRopeLength,blend);
            layFlow = Mathf.Lerp(layFlow,Mathf.Abs(floorLength-previousFloor)/Mathf.Max(.001f,Time.deltaTime),blend);
            int count = Cargo.FloorDepositCount;
            var last = Cargo.FloorDeposit(count-1);
            if (!feedReady) { feedLocal = last.Position; feedReady = true; }
            feedLocal = Vector3.Lerp(feedLocal,last.Position,blend);
            float route = Vector3.Distance(GripWorld,owner.transform.TransformPoint(feedLocal));
            for (int i=1;i<count;i++) route += Vector3.Distance(Cargo.FloorDeposit(i-1).Position,i==count-1 ? feedLocal : Cargo.FloorDeposit(i).Position);
            float spare = Mathf.Max(0,floorLength-route);
            coils.Clear();
            for (int i=0;i<count;i++)
            {
                float length = Mathf.Min(spare,Cargo.FloorDeposit(i).Length);
                coils.Add(length); spare -= length;
            }
            coils[count-1] += spare;
            points.Clear(); remaining = floorLength; points.Add(GripWorld);
            var tip = PilePoint(coils[count-1],feedLocal,true);
            var grip = GripWorld;
            float sag = Mathf.Min(.38f,Vector3.Distance(grip,tip)*.12f);
            for (int i=1;i<=16 && remaining>0;i++)
            {
                float t=i/16f;
                var point=Vector3.Lerp(grip,tip,t)-owner.transform.up*Mathf.Sin(t*Mathf.PI)*sag;
                Append(AboveDeck(point,.02f,true));
            }
            for (int node=count-1;node>=0 && remaining>0;node--)
            {
                var center = node==count-1 ? feedLocal : Cargo.FloorDeposit(node).Position;
                float length = coils[node];
                var entry = PilePoint(length,center,node==count-1);
                if (node<count-1)
                {
                    var start = points[points.Count-1]; int steps = Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(start,entry)/.4f));
                    for (int j=1;j<=steps && remaining>0;j++) Append(AboveDeck(Vector3.Lerp(start,entry,j/(float)steps),.02f,true));
                }
                if (length<.025f) continue;
                int samples = Mathf.Max(2,Mathf.CeilToInt(length/.11f));
                for (int j=1;j<=samples && remaining>0;j++) Append(PilePoint(length*(1-j/(float)samples),center,node==count-1));
            }
            Tail.enabled = points.Count>1;
            Tail.positionCount = points.Count;
            for (int i=0;i<points.Count;i++) Tail.SetPosition(i,points[i]);
            tailVisual.LengthOffset = -selected;
        }
        void Append(Vector3 point)
        {
            var start = points[points.Count-1]; float distance = Vector3.Distance(start,point);
            if (distance<.0001f || remaining<=0) return;
            if (distance>remaining) { points.Add(Vector3.Lerp(start,point,remaining/distance)); remaining = 0; return; }
            remaining -= distance; points.Add(point);
        }
        Vector3 PilePoint(float length,Vector3 center,bool feeding)
        {
            if (length<.025f) return Cargo.Source.transform.TransformPoint(center);
            float seed = Cargo.StationIndex*1.731f+center.x*.71f+center.z*1.13f;
            float angle = length/.275f+seed;
            float radius = Mathf.Min(1,length/.15f)*(.245f+.037f*Mathf.Sin(length*.8f+seed)+.015f*Mathf.Sin(angle*3+seed));
            float fresh = feeding ? Mathf.Clamp01(1-(coils[coils.Count-1]-length)/.65f)*Mathf.Clamp01(layFlow/2) : 0;
            float height = .016f+length*.009f+fresh*(.025f+.012f*Mathf.Sin(Time.time*7-angle));
            return Cargo.Source.transform.TransformPoint(center+new Vector3(Mathf.Cos(angle)*radius,height,Mathf.Sin(angle)*radius*.85f));
        }
    }
}
