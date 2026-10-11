using FishNet.Object;
using FishNet.Connection;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkShip
    {
        KrakenEncounterManager krakenManager;
        int krakenSeed;
        byte krakenDefeated;
        readonly double[] krakenDefeatedAt=new double[5];
        bool krakenRetreating;
        double krakenAppearedAt, krakenSankAt;
        public KrakenEncounterManager KrakenManager => krakenManager != null ? krakenManager : (krakenManager = GetComponentInChildren<KrakenEncounterManager>(true));
        public double KrakenClock => TimeManager != null && IsSpawned ? TimeManager.TicksToTime(TimeManager.Tick) : Time.timeAsDouble;

        void InitializeKraken()
        {
            if (KrakenManager == null) krakenManager=gameObject.AddComponent<KrakenEncounterManager>();
        }

        public void TriggerKrakenFromNetwork()
        {
            if (!IsServerInitialized || krakenSeed!=0) return;
            InitializeKraken();
            krakenSeed=Random.Range(1,int.MaxValue);
            krakenDefeated=0; krakenRetreating=false;
            System.Array.Clear(krakenDefeatedAt,0,krakenDefeatedAt.Length);
            krakenAppearedAt=KrakenClock; krakenSankAt=0;
            ApplyKrakenSnapshot(krakenSeed,krakenDefeated,krakenDefeatedAt,false,krakenAppearedAt,0);
            PublishKrakenSnapshot();
        }

        void PublishKrakenSnapshot() => KrakenStateObserversRpc(krakenSeed,krakenDefeated,krakenDefeatedAt,krakenRetreating,krakenAppearedAt,krakenSankAt);

        [ObserversRpc(BufferLast=true)]
        void KrakenStateObserversRpc(int seed,byte defeated,double[] defeatedAt,bool retreating,double appearedAt,double sankAt)
        {
            if (IsServerInitialized) return;
            ApplyKrakenSnapshot(seed,defeated,defeatedAt,retreating,appearedAt,sankAt);
        }

        void ApplyKrakenSnapshot(int seed,byte defeated,double[] defeatedAt,bool retreating,double appearedAt,double sankAt)
        {
            InitializeKraken();
            if(seed==0) { KrakenManager.StopEncounterLocal(); return; }
            KrakenManager.StartEncounterLocal(seed,appearedAt);
            var encounter=KrakenManager.ActiveEncounter;
            if(encounter==null) return;
            encounter.ApplyDefeatedMask(defeated,defeatedAt);
            if(retreating) encounter.TriggerSinkLocal(Mathf.Max(0,(float)(KrakenClock-sankAt)));
        }

        public void KrakenAttackTentacle(int tentacleIndex,Vector3 targetPos)
        {
            if (!IsServerInitialized || krakenSeed==0) return;
            var tentacles=KrakenManager.ActiveEncounter.Tentacles;
            if(tentacleIndex<0 || tentacleIndex>=tentacles.Count) return;
            KrakenAttackTentacleObserversRpc(krakenSeed,tentacleIndex,targetPos,tentacles[tentacleIndex].Animator.AttackStartedAt);
        }

        [ObserversRpc]
        void KrakenAttackTentacleObserversRpc(int seed,int tentacleIndex,Vector3 targetPos,double startedAt)
        {
            if(IsServerInitialized) return;
            var encounter=KrakenManager!=null ? KrakenManager.ActiveEncounter : null;
            if(encounter!=null && encounter.Seed==seed) encounter.ExecuteTentacleAttack(tentacleIndex,targetPos,startedAt);
        }

        public override void OnSpawnServer(NetworkConnection connection)
        {
            base.OnSpawnServer(connection);
            var encounter=KrakenManager!=null ? KrakenManager.ActiveEncounter : null;
            if(krakenSeed==0 || krakenRetreating || encounter==null) return;
            for(int i=0;i<encounter.Tentacles.Count;i++)
            {
                var animator=encounter.Tentacles[i].Animator;
                if(animator!=null && animator.IsAttacking)
                    RestoreKrakenAttackTargetRpc(connection,krakenSeed,krakenDefeated,krakenDefeatedAt,krakenAppearedAt,i,animator.AttackTarget,animator.AttackStartedAt);
            }
        }

        [TargetRpc]
        void RestoreKrakenAttackTargetRpc(NetworkConnection connection,int seed,byte defeated,double[] defeatedAt,double appearedAt,int index,Vector3 target,double startedAt)
        {
            if(IsServerInitialized) return;
            ApplyKrakenSnapshot(seed,defeated,defeatedAt,false,appearedAt,0);
            KrakenManager.ActiveEncounter?.ExecuteTentacleAttack(index,target,startedAt);
        }

        public void KrakenDefeatTentacle(int index)
        {
            if(!IsServerInitialized || krakenSeed==0 || index<0 || index>=5) return;
            krakenDefeated|=(byte)(1<<index);
            krakenDefeatedAt[index]=KrakenClock;
            KrakenManager.ActiveEncounter?.ApplyDefeatedMask(krakenDefeated,krakenDefeatedAt);
            if(krakenDefeated==31) KrakenSink(); else PublishKrakenSnapshot();
        }

        public void KrakenTentacleImpact(int tentacleIndex,Vector3 strikePoint)
        {
            if(IsServerInitialized) KrakenTentacleImpactObserversRpc(tentacleIndex,strikePoint);
        }

        [ObserversRpc]
        void KrakenTentacleImpactObserversRpc(int tentacleIndex,Vector3 strikePoint)
        {
            GameAudio.Play(SoundCue.Splash,strikePoint);
            GameAudio.Play(GameAudio.ResolveCue(SoundCue.KrakenSlam,SoundCue.ShipCollision),strikePoint);
            CombatVfx.Splash(strikePoint);
            CombatVfx.Impact(strikePoint,Vector3.up,true,true);
        }

        public void KrakenSink()
        {
            if(!IsServerInitialized || krakenSeed==0 || krakenRetreating) return;
            krakenRetreating=true; krakenSankAt=KrakenClock;
            KrakenManager.ActiveEncounter?.TriggerSinkLocal();
            PublishKrakenSnapshot();
        }

        public void ClearKrakenEncounter()
        {
            if(!IsServerInitialized || krakenSeed==0) return;
            krakenSeed=0; krakenDefeated=0; krakenRetreating=false;
            PublishKrakenSnapshot();
        }

        void StopKraken()
        {
            KrakenManager?.StopEncounterLocal();
            krakenSeed=0; krakenDefeated=0; krakenRetreating=false;
        }
    }
}
