using UnityEngine;
using PirateSlop.Networking;
namespace PirateSlop
{
    public sealed class CombatHealth : MonoBehaviour, IWeaponTarget
    {
        public bool IsShip;
        public float MaxHealth = 100f;
        public float BarHeight = 2.2f;
        public float Current { get; private set; }
        NetworkHealth network;
        CharacterController controller;
        void Awake() { Current = MaxHealth; network = GetComponent<NetworkHealth>(); controller = GetComponent<CharacterController>(); }
        public void ApplySnapshot(float value) => Current = Mathf.Clamp(value, 0, MaxHealth);
        public void Damage(float amount)
        {
            if (network != null && !network.IsServerInitialized) return;
            if (amount <= 0 || float.IsNaN(amount) || float.IsInfinity(amount)) return;
            ApplySnapshot(Current - amount);
            if (network != null) network.Publish(Current);
        }
        public void ReceiveWeaponHit(float damage, GameObject attacker) { if (!IsShip) Damage(damage); }
        public void ReceivePistolHit(float distance, Vector3 point, GameObject attacker)
        {
            if (IsShip) return;
            bool head = controller != null && transform.InverseTransformPoint(point).y >= controller.center.y + controller.height * .5f - .3f;
            Damage(Mathf.Lerp(head ? 70f : 45f, head ? 40f : 25f, Mathf.InverseLerp(15f, 35f, distance)));
        }
        void OnGUI()
        {
            var camera = Camera.main;
            if (camera == null || !camera.isActiveAndEnabled) return;
            Vector3 screen = camera.WorldToScreenPoint(transform.position + Vector3.up * (controller != null ? controller.center.y + controller.height * .5f + .3f : BarHeight));
            if (screen.z <= 0) return;
            float width = IsShip ? 120f : 80f;
            Rect bar = new Rect(screen.x - width / 2, Screen.height - screen.y, width, 10);
            Color old = GUI.color;
            GUI.color = Color.black; GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = Color.Lerp(Color.red, Color.green, Current / MaxHealth);
            GUI.DrawTexture(new Rect(bar.x + 1, bar.y + 1, (width - 2) * Current / MaxHealth, 8), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(bar.x, bar.y - 21, width, 22), Mathf.CeilToInt(Current) + " / " + Mathf.CeilToInt(MaxHealth));
            GUI.color = old;
        }
    }
}
