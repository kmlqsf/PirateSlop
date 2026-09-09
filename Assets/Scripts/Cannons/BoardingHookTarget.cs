using UnityEngine;
using PirateSlop.Networking;

namespace PirateSlop
{
    public sealed class BoardingHookTarget : MonoBehaviour
    {
        public NetworkCannon Source;
        public int Slot;
        public LineRenderer Rope;
        float struckAt=-10;
        public void Strike(GameObject attacker)
        {
            if(Source==null || !Source.IsServerInitialized || attacker==null || Time.time-struckAt<.55f) return;
            var health=attacker.GetComponent<CombatHealth>();
            if(health==null || health.IsDead || Vector3.Distance(attacker.transform.position,transform.position)>3) return;
            struckAt=Time.time;
            Source.StrikeBoarding(Slot);
            GameAudio.Play(SoundCue.Load,transform.position);
        }
    }
}
