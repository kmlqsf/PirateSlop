using PirateSlop.Networking;
using UnityEngine;

namespace PirateSlop
{
    [DisallowMultipleComponent]
    public sealed class PlayerFreezeScreen : MonoBehaviour
    {
        NetworkHealth status;
        AdvancedPlayerController motor;
        Material material;
        float appeared;
        bool showing;
        void Awake() { status = GetComponent<NetworkHealth>(); motor = GetComponent<AdvancedPlayerController>(); }
        void Update()
        {
            bool frozen=status!=null && status.IsFrozen;
            if (frozen && !showing) appeared=Time.unscaledTime-Mathf.Max(0f,1f-status.FrozenSeconds);
            showing=frozen;
        }
        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || status == null || !status.IsClientInitialized || !status.IsOwner || !status.IsFrozen || motor.IsDead || !motor.IsLocal) return;
            if (material == null)
            {
                var template = Resources.Load<Material>("VFX/PlayerFreezeScreen");
                if (template == null) return;
                material = new Material(template);
            }
            float remaining = status.FrozenSeconds;
            float opacity = Mathf.SmoothStep(0f, 1f, Mathf.Max(1f-remaining,Time.unscaledTime-appeared) / .1f) * Mathf.SmoothStep(0f, 1f, remaining / .25f);
            material.SetFloat("_FreezeFade", opacity);
            Graphics.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture, material);
        }
        void OnDestroy() { if (material != null) Destroy(material); }
    }
}
