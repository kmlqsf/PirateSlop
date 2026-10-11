using System.Collections.Generic;
using UnityEngine;
using PirateSlop.Networking;

namespace PirateSlop
{
    public sealed class KrakenAttackSystem : MonoBehaviour
    {
        public const float FirstStrikeDamage = 50f;
        [SerializeField] float attackInterval = 20f;

        Transform targetShip;
        readonly List<KrakenTentacle> tentacles = new();
        float nextAttackTime;
        int globalAttackCount;
        bool crewDamagedInCurrentWave;
        bool shipDamagedInCurrentWave;

        public void Initialize(Transform ship, List<KrakenTentacle> activeTentacles)
        {
            targetShip = ship;
            tentacles.Clear();
            if (activeTentacles != null) tentacles.AddRange(activeTentacles);
            nextAttackTime = Time.time + attackInterval;
            globalAttackCount = 0;
        }

        void Update()
        {
            if (targetShip == null || tentacles.Count == 0) return;

            var netShip = targetShip.GetComponent<PirateSlop.Networking.NetworkShip>();
            if (netShip != null && !netShip.IsServerInitialized) return;

            if (Time.time >= nextAttackTime)
            {
                nextAttackTime = Time.time + attackInterval;
                TriggerAttackWave();
            }
        }

        void TriggerAttackWave()
        {
            var available = tentacles.FindAll(t => t != null && t.gameObject.activeInHierarchy && !t.IsDefeated && !t.IsAttacking);
            if (available.Count == 0) return;

            globalAttackCount++;
            float crewDamage = globalAttackCount == 1 ? FirstStrikeDamage : 100f;
            crewDamagedInCurrentWave = false;
            shipDamagedInCurrentWave = false;

            if (available.Count == 1)
            {
                LaunchTentacleAttack(available[0], crewDamage);
            }
            else
            {
                int first = Random.Range(0, available.Count);
                int second = (first + Random.Range(1, available.Count)) % available.Count;

                LaunchTentacleAttack(available[first], crewDamage);
                LaunchTentacleAttack(available[second], crewDamage);
            }
        }

        void LaunchTentacleAttack(KrakenTentacle tentacle, float crewDamage)
        {
            int index = tentacles.IndexOf(tentacle);
            var netShip = targetShip != null ? targetShip.GetComponent<PirateSlop.Networking.NetworkShip>() : null;
            Vector3 local = targetShip.InverseTransformPoint(tentacle.transform.position);
            Vector3 target = targetShip.TransformPoint(new Vector3(Mathf.Sign(local.x) * 3.5f, 4.3f, Mathf.Clamp(local.z, -8f, 8f)));
            tentacle.Attack(target, () => OnTentacleImpact(tentacle, crewDamage));
            if (netShip != null && netShip.IsServerInitialized && index >= 0)
            {
                netShip.KrakenAttackTentacle(index, target);
            }
        }

        void OnTentacleImpact(KrakenTentacle tentacle, float crewDamage)
        {
            if (tentacle == null) return;
            Vector3 strikePoint = tentacle.TipPosition;

            int index = tentacles.IndexOf(tentacle);
            var netShip = targetShip != null ? targetShip.GetComponent<PirateSlop.Networking.NetworkShip>() : null;
            if (netShip != null && netShip.IsServerInitialized && index >= 0)
            {
                netShip.KrakenTentacleImpact(index, strikePoint);
            }
            else
            {
                GameAudio.Play(SoundCue.Splash, strikePoint);
                GameAudio.Play(GameAudio.ResolveCue(SoundCue.KrakenSlam, SoundCue.ShipCollision), strikePoint);
                CombatVfx.Splash(strikePoint);
                CombatVfx.Impact(strikePoint, Vector3.up, true, true);
            }

            if (!shipDamagedInCurrentWave)
            {
                shipDamagedInCurrentWave = true;
                targetShip.GetComponent<ShipDestruction>()?.KrakenStrike(strikePoint, globalAttackCount);
            }

            if (!crewDamagedInCurrentWave)
            {
                crewDamagedInCurrentWave = true;
                DamageCrew(targetShip,crewDamage,gameObject);
            }
        }

        public static void DamageCrew(Transform targetShip,float damage,GameObject attacker)
        {
            if (targetShip == null) return;

            var crew = new HashSet<CombatHealth>();
            var shipRb = targetShip.GetComponent<Rigidbody>();
            var netShip = targetShip.GetComponent<NetworkShip>();

            var passengers = ShipDeckPassenger.Active;
            for (int i = 0; i < passengers.Count; i++)
            {
                var p = passengers[i];
                if (p != null && (p.Ship == shipRb || p.transform.IsChildOf(targetShip)))
                {
                    var h = p.GetComponent<CombatHealth>();
                    if (h != null && !h.IsDead) crew.Add(h);
                }
            }

            var players = NetworkPlayer.Active;
            for (int i = 0; i < players.Count; i++)
            {
                var np = players[i];
                if (np != null && np.Ship == netShip)
                {
                    var h = np.GetComponent<CombatHealth>();
                    if (h != null && !h.IsDead) crew.Add(h);
                }
            }

            var allHealth = CombatHealth.Active;
            for (int i = 0; i < allHealth.Count; i++)
            {
                var h = allHealth[i];
                if (h == null || h.IsDead) continue;
                if (Vector3.Distance(h.transform.position, targetShip.position) <= 25f)
                {
                    var motor = h.GetComponent<AdvancedPlayerController>();
                    if (motor == null || !motor.IsSwimming) crew.Add(h);
                }
            }

            foreach (var member in crew)
            {
                member.Damage(damage, attacker);
            }
        }
    }
}
