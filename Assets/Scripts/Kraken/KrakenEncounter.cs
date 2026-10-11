using System.Collections.Generic;
using UnityEngine;
using PirateSlop.Networking;

namespace PirateSlop
{
    public sealed class KrakenEncounter : MonoBehaviour
    {
        [SerializeField] GameObject tentaclePrefab;
        [SerializeField] float chaseSpeed=5.5f, escapeDistance=20f, sinkSpeed=8f;
        [SerializeField] KrakenAttackSystem attackSystem;
        static readonly Vector3[] formationOffsets={new(-13,0,9),new(-14,0,-9),new(13,0,13),new(15,0,0),new(13,0,-13)};
        readonly List<KrakenTentacle> tentacles=new();
        Transform targetShip;
        NetworkShip ship;
        bool isSinking;
        double appearedAt,sankAt;
        Vector3[] sinkingPositions;
        readonly float[] verticalVelocities=new float[formationOffsets.Length];
        Bounds hullBounds;
        public int Seed { get; private set; }
        public IReadOnlyList<KrakenTentacle> Tentacles => tentacles;
        double Clock => ship!=null ? ship.KrakenClock : Time.timeAsDouble;

        public void Initialize(Transform target,int seed=1,double startedAt=-1)
        {
            targetShip=target; ship=target.GetComponent<NetworkShip>();
            var destruction=target.GetComponent<ShipDestruction>();
            hullBounds=destruction!=null ? destruction.KrakenHullBounds : new Bounds(new Vector3(0,-3,0),new Vector3(13,6,40));
            destruction?.PrepareKrakenEncounter();
            Seed=seed; appearedAt=startedAt>=0 ? startedAt : Clock;
            transform.position=target.position;
            if(attackSystem==null) attackSystem=GetComponent<KrakenAttackSystem>() ?? gameObject.AddComponent<KrakenAttackSystem>();
            if(tentaclePrefab==null) tentaclePrefab=Resources.Load<GameObject>("KrakenTentacle");
            for(int i=0;i<formationOffsets.Length;i++)
            {
                Vector3 point=FormationPosition(i);
                Vector3 direction=target.position-point; direction.y=0;
                Quaternion rotation=Quaternion.LookRotation(direction.normalized,Vector3.up);
                var instance=Instantiate(tentaclePrefab,point,rotation);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(instance,gameObject.scene);
                var tentacle=instance.GetComponent<KrakenTentacle>();
                tentacle.Configure(ship,i,seed);
                tentacles.Add(tentacle);
            }
            attackSystem.Initialize(target,tentacles);
            GameAudio.Play(SoundCue.KrakenAppear,target.position);
        }

        Vector3 FormationPosition(int index)
        {
            Vector3 point=targetShip.TransformPoint(formationOffsets[index]);
            var matrix=targetShip.localToWorldMatrix;
            var center=matrix.MultiplyPoint3x4(hullBounds.center);
            var extent=hullBounds.extents;
            float lowest=center.y-Mathf.Abs(matrix.m10)*extent.x-Mathf.Abs(matrix.m11)*extent.y-Mathf.Abs(matrix.m12)*extent.z;
            float rise=Mathf.SmoothStep(0,1,Mathf.Clamp01((float)(Clock-appearedAt)/2));
            point.y=lowest-10f-(1-rise)*28;
            return point;
        }

        public void ExecuteTentacleAttack(int index,Vector3 targetPos,double startedAt=-1)
        {
            if(isSinking || index<0 || index>=tentacles.Count || tentacles[index].IsDefeated) return;
            tentacles[index].Attack(targetPos,null,startedAt);
        }

        public void ApplyDefeatedMask(byte mask,double[] defeatedAt=null)
        {
            for(int i=0;i<tentacles.Count;i++)
                if((mask&(1<<i))!=0)
                    tentacles[i].DefeatLocal(defeatedAt!=null && i<defeatedAt.Length ? Mathf.Max(0,(float)(Clock-defeatedAt[i])) : 0);
        }

        public void TriggerSinkLocal(float elapsed=0)
        {
            if(isSinking) return;
            isSinking=true; sankAt=Clock-elapsed;
            sinkingPositions=new Vector3[tentacles.Count];
            attackSystem.enabled=false;
            for(int i=0;i<tentacles.Count;i++)
            {
                sinkingPositions[i]=tentacles[i].transform.position;
                tentacles[i].Animator.CancelAttack();
            }
        }

        void Update()
        {
            if(isSinking)
            {
                float elapsed=Mathf.Max(0,(float)(Clock-sankAt));
                for(int i=0;i<tentacles.Count;i++)
                    if(tentacles[i]!=null && !tentacles[i].IsDefeated) tentacles[i].transform.position=sinkingPositions[i]+Vector3.down*(sinkSpeed*elapsed);
                if(elapsed>=4) Destroy(gameObject);
                return;
            }
            if(targetShip==null) { TriggerSinkLocal(); return; }
            if(ship==null || ship.IsServerInitialized)
                if(Vector3.Distance(transform.position,targetShip.position)>escapeDistance)
                {
                    if(ship!=null) ship.KrakenSink(); else TriggerSinkLocal();
                    return;
                }
            transform.position=Vector3.MoveTowards(transform.position,targetShip.position,chaseSpeed*Time.deltaTime);
            for(int i=0;i<tentacles.Count;i++)
            {
                var tentacle=tentacles[i];
                if(tentacle==null || tentacle.IsDefeated) continue;
                Vector3 destination=FormationPosition(i);
                Vector3 next=Vector3.MoveTowards(tentacle.transform.position,destination,chaseSpeed*Time.deltaTime);
                next.y=Mathf.SmoothDamp(tentacle.transform.position.y,destination.y,ref verticalVelocities[i],.3f,40f,Time.deltaTime);
                tentacle.transform.position=next;
                Vector3 direction=targetShip.position-next; direction.y=0;
                if(direction.sqrMagnitude>.01f) tentacle.transform.rotation=Quaternion.Slerp(tentacle.transform.rotation,Quaternion.LookRotation(direction.normalized,Vector3.up),Time.deltaTime*4);
            }
        }

        void OnDestroy()
        {
            foreach(var tentacle in tentacles) if(tentacle!=null) Destroy(tentacle.gameObject);
            if(targetShip!=null) targetShip.GetComponent<KrakenEncounterManager>()?.ClearEncounter(this);
        }
    }
}
