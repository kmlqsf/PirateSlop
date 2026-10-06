using System.Collections;
using FishNet.Object;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkWeapon
    {
        public void ScheduleUpgradeEcho(FirearmDefinition definition, Vector3 eye, Vector3 muzzle, Vector3 direction, bool aimed, int seed)
        {
            if (IsServerInitialized && GetComponent<NetworkPlayer>().HasUpgrade(UpgradeEffect.ShotEcho))
                StartCoroutine(UpgradeEcho(definition, eye - transform.position, muzzle - eye, direction, aimed, seed, FirearmCombat.Spread(gameObject, definition, aimed)));
        }
        IEnumerator UpgradeEcho(FirearmDefinition definition, Vector3 eyeOffset, Vector3 muzzleOffset, Vector3 direction, bool aimed, int seed, float spread)
        {
            yield return new WaitForSeconds(RoguelikeTuning.Current.echoDelay);
            if (!IsServerInitialized || !IsSpawned || GetComponent<CombatHealth>().IsDead || definition == null) yield break;
            Vector3 eye = transform.position + eyeOffset;
            var shots = FirearmCombat.Resolve(gameObject, definition, eye, eye + muzzleOffset, direction, aimed, seed, true, RoguelikeTuning.Current.echoDamage, spread);
            UpgradeEchoObserversRpc(shots, definition.TracerWidth, definition.TracerSpeed, definition.Sound);
        }
        [ObserversRpc(RunLocally = true)]
        void UpgradeEchoObserversRpc(FirearmShot[] shots, float width, float speed, SoundCue cue)
        {
            if (!IsClientInitialized || shots == null) return;
            foreach (var shot in shots) PistolBullet.Spawn(shot.Start, shot, weapon.EffectMaterial, width, true, speed);
            if (shots.Length > 0) { GameAudio.Play(cue, shots[0].Start); CombatVfx.Fire(shots[0].Start, (shots[0].End - shots[0].Start).normalized, false); }
        }
        public void LaunchUpgradeSabreWave(Vector3 origin, Vector3 direction, float damage)
        { if (IsServerInitialized) UpgradeSabreWaveObserversRpc(origin, direction, damage); }
        [ObserversRpc(RunLocally = true)]
        void UpgradeSabreWaveObserversRpc(Vector3 origin, Vector3 direction, float damage)
        {
            var wave = new GameObject("GhostSabreWave");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(wave, gameObject.scene);
            wave.AddComponent<GhostSabreWave>().Initialize(gameObject, origin, direction, IsServerInitialized, damage);
        }
        public void PublishUpgradeTrace(FirearmShot shot) { if (IsServerInitialized) UpgradeTraceObserversRpc(shot); }
        [ObserversRpc(RunLocally = true)]
        void UpgradeTraceObserversRpc(FirearmShot shot)
        {
            if (IsClientInitialized) PistolBullet.Spawn(shot.Start, shot, weapon.EffectMaterial, .022f, true, 450f);
        }
    }
}
