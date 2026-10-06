using System;
using System.Collections.Generic;
using PirateSlop.Networking;
using UnityEngine;

namespace PirateSlop
{
    public sealed class GhostSabreWave : MonoBehaviour
    {
        GameObject source;
        Vector3 forward;
        float remaining, damage;
        bool authority;
        LineRenderer arc;
        Material material;
        readonly HashSet<CombatHealth> struck = new();
        public void Initialize(GameObject attacker, Vector3 origin, Vector3 direction, bool authoritative, float amount)
        {
            source = attacker; forward = direction.normalized; authority = authoritative; damage = amount;
            remaining = RoguelikeTuning.Current.ghostRange;
            transform.position = origin;
            if (Application.isBatchMode) return;
            GameAudio.Play(SoundCue.GhostWave, origin);
            material = new Material(Resources.Load<Material>("FirearmGlow"));
            arc = gameObject.AddComponent<LineRenderer>();
            arc.sharedMaterial = material; arc.useWorldSpace = true; arc.positionCount = 17;
            arc.widthMultiplier = .045f; arc.numCapVertices = 2;
            arc.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            arc.receiveShadows = false;
        }
        void FixedUpdate()
        {
            if (source == null || remaining <= 0) { Destroy(gameObject); return; }
            float distance = Mathf.Min(remaining, RoguelikeTuning.Current.ghostSpeed * Time.fixedDeltaTime);
            var hits = Physics.SphereCastAll(transform.position, RoguelikeTuning.Current.ghostRadius, forward, distance, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.transform.IsChildOf(source.transform) || hit.collider.GetComponentInParent<GhostSabreWave>() != null || !PlayerHitbox.IsTarget(hit.collider)) continue;
                var health = hit.collider.GetComponentInParent<CombatHealth>();
                if (health != null)
                {
                    if (authority && struck.Add(health))
                    {
                        float before = health.Current;
                        health.ReceiveWeaponHit(damage, source);
                        UpgradeCombat.AfterHit(health, before, source, false, forward);
                    }
                    continue;
                }
                distance = hit.distance; remaining = 0f; break;
            }
            transform.position += forward * distance;
            remaining -= distance;
        }
        void LateUpdate()
        {
            if (arc == null) return;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            if (right.sqrMagnitude < .1f) right = Vector3.right;
            float fade = Mathf.Clamp01(remaining / (RoguelikeTuning.Current.ghostRange * .25f));
            var tint = new Color(.68f, .9f, .86f, fade * .8f);
            arc.startColor = arc.endColor = tint;
            for (int i = 0; i < 17; i++)
            {
                float x = i / 16f * 2f - 1f;
                arc.SetPosition(i, transform.position + right * x * .75f + forward * (1f - x * x) * .2f);
            }
        }
        void OnDestroy() { if (material != null) Destroy(material); }
    }
}
