using System.Collections.Generic;
using UnityEngine;
using PirateSlop.Networking;

namespace PirateSlop
{
    public sealed class KrakenAttackSystem : MonoBehaviour
    {
        [SerializeField] float attackInterval = 20f;
        [SerializeField] float damageRadius = 7f;

        Transform targetShip;
        readonly List<KrakenTentacle> tentacles = new();
        float nextAttackTime;
        int globalAttackCount;
        bool crewDamagedInCurrentWave;

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
            var available = tentacles.FindAll(t => t != null && t.gameObject.activeInHierarchy && !t.IsAttacking);
            if (available.Count == 0) return;

            globalAttackCount++;
            float crewDamage = globalAttackCount == 1 ? 50f : 100f;
            crewDamagedInCurrentWave = false;

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
            if (netShip != null && netShip.IsServerInitialized && index >= 0)
            {
                netShip.KrakenAttackTentacle(index, targetShip.position);
            }

            tentacle.Attack(targetShip.position, () => OnTentacleImpact(tentacle, crewDamage));
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
                GameAudio.Play(SoundCue.ShipCollision, strikePoint);
                CombatVfx.Splash(strikePoint);
                CombatVfx.Impact(strikePoint, Vector3.up, true, true);
            }

            DamageShip(strikePoint);

            if (!crewDamagedInCurrentWave)
            {
                crewDamagedInCurrentWave = true;
                DamageCrew(crewDamage);
            }
        }

        void DamageShip(Vector3 strikePoint)
        {
            var hitSections = new HashSet<ShipDamageSection>();
            var colliders = Physics.OverlapSphere(strikePoint, damageRadius, ~0, QueryTriggerInteraction.Collide);

            for (int i = 0; i < colliders.Length; i++)
            {
                var section = colliders[i].GetComponentInParent<ShipDamageSection>();
                if (section == null || !hitSections.Add(section)) continue;

                if (section.Owner != null)
                {
                    section.Owner.Damage(colliders[i], strikePoint, Vector3.up, Vector3.down * 15f, InventoryItem.Cannonball, gameObject, damageRadius);
                }
                else
                {
                    ulong broken = section.BreakNear(strikePoint, 100f, false);
                    section.Apply(ShipSectionState.Damaged, broken);
                }
            }
        }

        void DamageCrew(float damage)
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
                member.Damage(damage, gameObject);
            }
        }
    }
}
