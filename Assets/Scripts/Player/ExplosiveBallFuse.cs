using PirateSlop.Networking;
using UnityEngine;

namespace PirateSlop
{
    public sealed class ExplosiveBallFuse : MonoBehaviour
    {
        public LineRenderer Wick;
        public Transform Ember;
        public Vector3 Socket = new(0, .115f, 0);
        public Vector3 Tip = new(.025f, .18f, -.01f);
        NetworkHandMortarBall projectile;
        float nextSpark;

        void Awake() => projectile = GetComponentInParent<NetworkHandMortarBall>();

        void LateUpdate()
        {
            bool burning = projectile != null && projectile.IsSpawned;
            float fraction = burning ? Mathf.Clamp01(projectile.RemainingFuseFraction) : 1f;
            Vector3 tip = Vector3.Lerp(Socket, Tip, Mathf.Max(.04f, fraction));
            Wick.SetPosition(0, Socket); Wick.SetPosition(1, tip);
            Ember.localPosition = tip;
            Ember.gameObject.SetActive(burning);
            if (!burning) return;
            Ember.localScale = Vector3.one * (.01f + .002f * Mathf.Abs(Mathf.Sin(Time.time * 43f)));
            if (Time.time < nextSpark) return;
            nextSpark = Time.time + .35f;
            FirearmVfx.FlintlockStrike(Ember.position, transform.up, .22f);
        }
    }
}
