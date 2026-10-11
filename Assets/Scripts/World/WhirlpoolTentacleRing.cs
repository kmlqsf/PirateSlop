using System.Collections;
using System.Collections.Generic;
using PirateSlop.Networking;
using UnityEngine;

namespace PirateSlop.World
{
    [DefaultExecutionOrder(100)]
    public sealed class WhirlpoolTentacleRing : MonoBehaviour
    {
        public const int Count=8;
        const double AttackInterval=6;
        public static WhirlpoolTentacleRing Instance { get; private set; }
        readonly KrakenTentacle[] tentacles=new KrakenTentacle[Count];
        readonly WhirlpoolTentacleState[] state=new WhirlpoolTentacleState[Count];
        readonly double[] playedAt=new double[Count];
        readonly Dictionary<NetworkShip,double> nextAttack=new();
        readonly Vector3[] previousTips=new Vector3[Count];
        readonly HashSet<CombatHealth>[] struckPlayers=new HashSet<CombatHealth>[Count];
        readonly Collider[] strikeHits=new Collider[48];
        readonly Dictionary<NetworkShip,int> strikes=new();
        Vector3 center;
        float ringRadius;
        double scanAt;
        SessionController session;
        public WhirlpoolTentacleState[] Snapshot => (WhirlpoolTentacleState[])state.Clone();
        double Clock => session!=null ? session.WhirlpoolClock : Time.timeAsDouble;

        void Awake() => Instance=this;

        public void Initialize(Vector3 position,float radius,float depth,float seaLevel,int seed)
        {
            session=SessionController.Instance;
            center=new Vector3(position.x,seaLevel-depth-18f,position.z);
            ringRadius=radius*.06f;
            transform.position=center;
            for(int i=0;i<Count;i++)
            {
                state[i].Health=150f; state[i].ShipId=-1;
                playedAt[i]=double.NegativeInfinity;
                struckPlayers[i]=new HashSet<CombatHealth>();
            }
            StartCoroutine(Spawn(seed));
        }

        IEnumerator Spawn(int seed)
        {
            var prefab=Resources.Load<GameObject>("KrakenTentacle");
            if(prefab==null) yield break;
            for(int i=0;i<Count;i++)
            {
                float angle=i*Mathf.PI*2/Count;
                Vector3 outward=new(Mathf.Cos(angle),0,Mathf.Sin(angle));
                var instance=Instantiate(prefab,center+outward*ringRadius,Quaternion.LookRotation(-outward,Vector3.up),transform);
                var tentacle=instance.GetComponent<KrakenTentacle>();
                tentacle.Configure(null,i,unchecked(seed+18647));
                tentacle.transform.localScale=Vector3.one*tentacle.transform.localScale.y;
                tentacle.Animator.SetIdleMotion(3.2f);
                tentacle.Animator.SetClockSource(()=>Clock);
                tentacles[i]=tentacle;
                previousTips[i]=tentacle.TipPosition;
                tentacle.ApplyRingHealth(state[i].Health,Mathf.Max(0,(float)(Clock-state[i].DefeatedAt)));
                yield return null;
            }
        }

        public void ApplySnapshot(WhirlpoolTentacleState[] snapshot)
        {
            if(snapshot==null || snapshot.Length!=Count) return;
            for(int i=0;i<Count;i++)
            {
                state[i]=snapshot[i];
                if(tentacles[i]!=null)
                {
                    tentacles[i].ApplyRingHealth(state[i].Health,Mathf.Max(0,(float)(Clock-state[i].DefeatedAt)));
                    if(!state[i].Attacking) tentacles[i].Animator.CancelAttack();
                }
            }
        }

        public void DamageTentacle(int index,float damage)
        {
            if(session==null || !session.WhirlpoolServer || index<0 || index>=Count || damage<=0 || !float.IsFinite(damage) || state[index].Health<=0) return;
            state[index].Health=Mathf.Max(0,state[index].Health-damage);
            if(state[index].Health<=0) { state[index].DefeatedAt=Clock; state[index].Attacking=false; }
            var tentacle=tentacles[index];
            if(tentacle!=null)
            {
                GameAudio.Play(SoundCue.BulletFlesh,tentacle.TipPosition);
                CombatVfx.Impact(tentacle.TipPosition,Vector3.up,false,true);
                tentacle.ApplyRingHealth(state[index].Health,0);
            }
            Publish();
        }

        void Publish() => session?.PublishWhirlpoolTentacles(Snapshot);

        static NetworkShip ResolveShip(int id)
        {
            foreach(var ship in NetworkShip.ActiveShips)
                if(ship!=null && ship.IsSpawned && ship.ObjectId==id) return ship;
            return null;
        }

        void Update()
        {
            if(session==null) return;
            double now=Clock;
            bool server=session.WhirlpoolServer, changed=false;
            for(int i=0;i<Count;i++)
            {
                var tentacle=tentacles[i];
                if(tentacle==null || tentacle.IsDefeated || !state[i].Attacking) continue;
                var target=ResolveShip(state[i].ShipId);
                if(target==null || target.IsSinking || now>=state[i].StartedAt+tentacle.Animator.Duration)
                {
                    tentacle.Animator.CancelAttack();
                    if(server) { state[i].Attacking=false; changed=true; }
                    continue;
                }
                if(!server && playedAt[i]!=state[i].StartedAt)
                {
                    tentacle.Animator.CancelAttack();
                    tentacle.Animator.SetTarget(target);
                    tentacle.Attack(target.transform.TransformPoint(state[i].TargetLocal),null,state[i].StartedAt);
                    playedAt[i]=state[i].StartedAt;
                }
            }
            if(changed) Publish();
            if(!server || now<scanAt) return;
            scanAt=now+.25;
            foreach(var ship in NetworkShip.ActiveShips)
            {
                if(ship==null || !ship.IsSpawned || !ship.IsServerInitialized || ship.IsSinking) continue;
                Vector3 offset=ship.transform.position-center; offset.y=0;
                if(offset.sqrMagnitude>(ringRadius+200f)*(ringRadius+200f)) continue;
                ship.GetComponent<ShipDestruction>()?.PrepareKrakenEncounter();
                if(nextAttack.TryGetValue(ship,out double next) && now<next) continue;
                strikes.TryGetValue(ship,out int strike);
                bool lower=(strike&1)!=0;
                int first=-1,second=-1;
                float closest=float.MaxValue,runnerUp=float.MaxValue;
                for(int i=0;i<Count;i++)
                {
                    var tentacle=tentacles[i];
                    if(tentacle==null || tentacle.IsDefeated || tentacle.IsAttacking || state[i].Attacking) continue;
                    Vector3 aim=Aim(ship,tentacle,lower);
                    float distance=(aim-tentacle.transform.position).sqrMagnitude;
                    float reach=tentacle.Animator.Reach*.94f;
                    if(distance>reach*reach) continue;
                    if(distance<closest) { second=first; runnerUp=closest; first=i; closest=distance; }
                    else if(distance<runnerUp) { second=i; runnerUp=distance; }
                }
                if(first<0) continue;
                nextAttack[ship]=now+AttackInterval;
                strikes[ship]=strike+1;
                bool damaged=false;
                void Launch(int index)
                {
                    var tentacle=tentacles[index];
                    Vector3 aim=Aim(ship,tentacle,lower);
                    state[index].ShipId=ship.ObjectId;
                    state[index].StartedAt=now;
                    state[index].TargetLocal=ship.transform.InverseTransformPoint(aim);
                    state[index].Attacking=true;
                    playedAt[index]=now;
                    previousTips[index]=tentacle.TipPosition;
                    struckPlayers[index].Clear();
                    tentacle.Animator.SetTarget(ship);
                    tentacle.Attack(aim,()=>
                    {
                        if(ship==null || !ship.IsSpawned || ship.IsSinking || !session.WhirlpoolServer) return;
                        Vector3 contact=ship.transform.TransformPoint(state[index].TargetLocal);
                        if((tentacle.TipPosition-contact).sqrMagnitude>64f) return;
                        ship.KrakenTentacleImpact(index,tentacle.TipPosition);
                        if(damaged) return;
                        damaged=true;
                        ship.GetComponent<ShipDestruction>()?.WhirlpoolStrike(contact,lower);
                    },now);
                }
                Launch(first);
                if(second>=0) Launch(second);
                Publish();
            }
        }

        static Vector3 Aim(NetworkShip ship,KrakenTentacle tentacle,bool lower)
        {
            var local=ship.transform.InverseTransformPoint(tentacle.transform.position);
            var flood=ship.GetComponent<ShipFlooding>();
            float height=lower && flood!=null ? Mathf.Lerp(flood.EmptyHeight,flood.FullHeight,.25f) : 4.3f;
            return ship.transform.TransformPoint(new Vector3(Mathf.Sign(local.x)*ship.HullHalfExtents.x*.9f,height,Mathf.Clamp(local.z,-8f,8f)));
        }

        void LateUpdate()
        {
            for(int i=0;i<Count;i++)
            {
                var tentacle=tentacles[i];
                if(tentacle==null) continue;
                Vector3 tip=tentacle.TipPosition;
                if(session!=null && session.WhirlpoolServer && tentacle.Animator.IsStriking && !tentacle.IsDefeated)
                {
                    int count=Physics.OverlapCapsuleNonAlloc(previousTips[i],tip,.8f,strikeHits,~0,QueryTriggerInteraction.Ignore);
                    for(int hit=0;hit<count;hit++)
                    {
                        var player=strikeHits[hit].GetComponentInParent<NetworkPlayer>();
                        if(player==null || !player.IsSpawned || !player.IsServerInitialized) continue;
                        var health=player.GetComponent<CombatHealth>();
                        if(health!=null && !health.IsDead && struckPlayers[i].Add(health)) health.Damage(25f,gameObject);
                    }
                }
                previousTips[i]=tip;
            }
        }

        void OnDestroy()
        {
            if(Instance==this) Instance=null;
        }
    }
}
