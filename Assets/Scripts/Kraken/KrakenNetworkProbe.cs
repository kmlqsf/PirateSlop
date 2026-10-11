#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using UnityEngine;
using PirateSlop.Networking;

namespace PirateSlop
{
    public sealed class KrakenNetworkProbe : MonoBehaviour
    {
        public static bool IsActive => Environment.GetCommandLineArgs().Contains("-krakentest");
        NetworkShip ship;
        int stage, lastSeed;
        double stageAt, nextAttack;
        float nextLog;
        string lastState;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if(!IsActive) return;
            var root=new GameObject("KrakenNetworkProbe");
            DontDestroyOnLoad(root);
            root.AddComponent<KrakenNetworkProbe>();
        }

        void Update()
        {
            if(ship==null) ship=NetworkShip.ActiveShips.OrderBy(s=>s.ParticipantId.Value).FirstOrDefault();
            if(ship==null) return;
            var encounter=ship.KrakenManager?.ActiveEncounter;
            if(ship.IsServerInitialized)
            {
                KrakenEncounterManager.AutoEncounterEnabled=false;
                if(stage==0)
                {
                    ship.TriggerKrakenFromNetwork();
                    encounter=ship.KrakenManager.ActiveEncounter;
                    encounter.GetComponent<KrakenAttackSystem>().enabled=false;
                    stage=1; stageAt=ship.KrakenClock;
                    Debug.Log("KRAKEN_PROBE APPEARED");
                }
                if(stage==1 && SessionController.Instance.AllPlayers.Count()>=2 && ship.KrakenClock-stageAt>=3)
                {
                    for(int i=2;i<=3;i++)
                    {
                        Vector3 target=ship.transform.TransformPoint(new Vector3(3.5f,4.3f,(i-2)*8));
                        encounter.Tentacles[i].Attack(target,()=>Debug.Log("KRAKEN_PROBE IMPACT callback=1"));
                        ship.KrakenAttackTentacle(i,target);
                    }
                    stage=2; stageAt=ship.KrakenClock;
                    Debug.Log("KRAKEN_PROBE WAVE");
                }
                if(stage==2 && ship.KrakenClock-stageAt>=5)
                {
                    encounter.Tentacles[0].TakeDamage(200);
                    stage=3; stageAt=ship.KrakenClock;
                    Debug.Log("KRAKEN_PROBE DEFEATED_0");
                }
                if(stage==3 && encounter!=null && ship.KrakenClock>=nextAttack)
                {
                    var tentacle=encounter.Tentacles[1];
                    Vector3 target=ship.transform.TransformPoint(new Vector3(-3.5f,4.3f,-8));
                    tentacle.Attack(target,()=>Debug.Log("KRAKEN_PROBE IMPACT callback=1"));
                    ship.KrakenAttackTentacle(1,target);
                    nextAttack=ship.KrakenClock+2;
                }
                if(stage==3 && SessionController.Instance.AllPlayers.Count()>=3 && ship.KrakenClock-stageAt>=8)
                {
                    ship.KrakenSink(); stage=4; stageAt=ship.KrakenClock;
                    Debug.Log("KRAKEN_PROBE SINK");
                }
                if(stage==4 && ship.KrakenClock-stageAt>=6)
                {
                    stage=5;
                    Debug.Log("KRAKEN_PROBE DONE");
                }
            }
            if(encounter!=null && encounter.Seed!=lastSeed)
            {
                lastSeed=encounter.Seed;
                var variants=encounter.Tentacles.Select(t=>new
                {
                    scale=t.transform.localScale.ToString("F4"),
                    mesh=t.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh.bounds.size.ToString("F4"),
                    bones=t.GetComponentInChildren<SkinnedMeshRenderer>().bones.Length
                });
                Debug.Log("KRAKEN_PROBE VARIANTS role="+(ship.IsServerInitialized?"host":"client")+" seed="+lastSeed+" "+string.Join(";",variants));
            }
            if(Time.unscaledTime<nextLog) return;
            nextLog=Time.unscaledTime+.1f;
            string state=encounter==null ? "none" : "seed="+encounter.Seed+" count="+encounter.Tentacles.Count+" alive="+encounter.Tentacles.Count(t=>t.gameObject.activeInHierarchy)+" defeated="+string.Join(",",encounter.Tentacles.Select(t=>t.IsDefeated?"1":"0"))+" attacks="+string.Join(",",encounter.Tentacles.Select(t=>t.IsAttacking?t.Animator.AttackStartedAt.ToString("F4"):"-"));
            if(state==lastState) return;
            lastState=state;
            Debug.Log("KRAKEN_PROBE STATE role="+(ship.IsServerInitialized?"host":"client")+" tick="+ship.KrakenClock.ToString("F4")+" "+state);
        }
    }
}
#endif
