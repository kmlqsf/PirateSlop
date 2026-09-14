using System.Collections.Generic;
using UnityEngine;
using PirateSlop.Networking;

namespace PirateSlop
{
    public sealed class CrewPresentation : MonoBehaviour
    {
        readonly List<NetworkPlayer> visible = new();
        NetworkPlayer owner;
        float sampleAt, respawnAt = -10f;
        bool wasDead;
        void Awake() => owner = GetComponent<NetworkPlayer>();
        void Update()
        {
            if (!owner.IsOwner || owner.Motor == null) return;
            if (wasDead && !owner.Motor.IsDead) respawnAt = Time.unscaledTime;
            wasDead = owner.Motor.IsDead;
            if (Time.unscaledTime < sampleAt) return;
            sampleAt = Time.unscaledTime + .2f; visible.Clear();
            if (wasDead || !owner.Motor.InputActive) return;
            var camera = owner.Motor.PlayerCamera;
            foreach (var member in FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None))
            {
                if (member == owner || member.TeamId.Value != owner.TeamId.Value || member.Motor == null || member.Motor.IsDead) continue;
                Vector3 head = member.transform.position + Vector3.up * 1.5f;
                if ((head - camera.transform.position).sqrMagnitude > 1600f) continue;
                if (FirearmTrace.Cast(gameObject, camera.transform.position, head, out var hit) && hit.collider.GetComponentInParent<NetworkPlayer>() != member) continue;
                visible.Add(member);
            }
        }
        void OnGUI()
        {
            if (!owner.IsOwner || owner.Motor == null || SessionController.MenuOpen) return;
            if (Time.unscaledTime - respawnAt < .65f)
                PirateHudStyle.Fill(new Rect(0,0,Screen.width,Screen.height), new Color(.04f,.08f,.1f,.7f * (1f-(Time.unscaledTime-respawnAt)/.65f)));
            if (!owner.Motor.InputActive) return;
            var camera = owner.Motor.PlayerCamera;
            foreach (var member in visible)
            {
                if (member == null || member.Motor.IsDead) continue;
                var screen = camera.WorldToScreenPoint(member.transform.position + Vector3.up * 2.05f);
                if (screen.z <= 0f || screen.x < 25f || screen.x > Screen.width-25f || screen.y < 25f || screen.y > Screen.height-25f) continue;
                PirateHudStyle.Diamond(new Vector2(screen.x,Screen.height-screen.y),7, new Color(.45f,.9f,.7f));
                PirateHudStyle.Label(new Rect(screen.x-90,Screen.height-screen.y-30,180,24), member.IsBot.Value ? "СОЮЗНИК · БОТ" : "СОЮЗНИК " + member.ParticipantId.Value, PirateHudStyle.Paper);
            }
        }
    }
}
