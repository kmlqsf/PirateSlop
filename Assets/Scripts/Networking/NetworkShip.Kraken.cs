using FishNet.Object;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkShip
    {
        KrakenEncounterManager krakenManager;
        public KrakenEncounterManager KrakenManager => krakenManager != null ? krakenManager : (krakenManager = GetComponentInChildren<KrakenEncounterManager>());

        public void TriggerKrakenFromNetwork()
        {
            if (!IsServerInitialized) return;
            StartKrakenEncounterObserversRpc();
        }

        [ObserversRpc]
        void StartKrakenEncounterObserversRpc()
        {
            if (KrakenManager != null) KrakenManager.StartEncounterLocal();
        }

        public void KrakenAttackTentacle(int tentacleIndex, Vector3 targetPos)
        {
            if (IsServerInitialized)
                KrakenAttackTentacleObserversRpc(tentacleIndex, targetPos);
        }

        [ObserversRpc]
        void KrakenAttackTentacleObserversRpc(int tentacleIndex, Vector3 targetPos)
        {
            if (KrakenManager != null && KrakenManager.ActiveEncounter != null)
                KrakenManager.ActiveEncounter.ExecuteTentacleAttack(tentacleIndex, targetPos);
        }

        public void KrakenTentacleImpact(int tentacleIndex, Vector3 strikePoint)
        {
            if (IsServerInitialized)
                KrakenTentacleImpactObserversRpc(tentacleIndex, strikePoint);
        }

        [ObserversRpc]
        void KrakenTentacleImpactObserversRpc(int tentacleIndex, Vector3 strikePoint)
        {
            GameAudio.Play(SoundCue.Splash, strikePoint);
            GameAudio.Play(SoundCue.ShipCollision, strikePoint);
            CombatVfx.Splash(strikePoint);
            CombatVfx.Impact(strikePoint, Vector3.up, true, true);
        }

        public void KrakenSink()
        {
            if (IsServerInitialized)
                KrakenSinkObserversRpc();
        }

        [ObserversRpc]
        void KrakenSinkObserversRpc()
        {
            if (KrakenManager != null && KrakenManager.ActiveEncounter != null)
                KrakenManager.ActiveEncounter.TriggerSinkLocal();
        }
    }
}
