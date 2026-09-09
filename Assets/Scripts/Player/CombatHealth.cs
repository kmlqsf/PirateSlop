using UnityEngine;
using PirateSlop.Networking;
namespace PirateSlop
{
    public sealed class CombatHealth : MonoBehaviour, IWeaponTarget
    {

        public float MaxHealth = 100f;
        public float BarHeight = 2.2f;
        [Min(0f)] public float RespawnDelay = 5f;
        public float Current { get; private set; }
        public bool IsDead => Current <= 0f;
        NetworkHealth network;
        CharacterController controller;
        Renderer[] hiddenRenderers;
        Collider[] disabledColliders;
        Vector3 spawnPosition;
        float spawnYaw;
        Vector3 localSpawnPosition;
        float localSpawnYaw;
        Transform spawnPlatform;
        float respawnAt;
        public float RespawnRemaining => Mathf.Max(0, respawnAt - Time.time);
        void Awake() { Current = MaxHealth; network = GetComponent<NetworkHealth>(); controller = GetComponent<CharacterController>(); if (controller != null && GetComponent<DamageFeedback>() == null) gameObject.AddComponent<DamageFeedback>(); }
        void Start()
        {
            spawnPosition = transform.position; spawnYaw = transform.eulerAngles.y;
            var passenger = GetComponent<ShipDeckPassenger>();
            if (passenger != null && passenger.Ship != null)
            {
                spawnPlatform = passenger.Ship.transform;
                localSpawnPosition = spawnPlatform.InverseTransformPoint(spawnPosition);
                localSpawnYaw = spawnYaw - spawnPlatform.eulerAngles.y;
            }
        }
        public Vector3 SpawnPosition => spawnPlatform != null && spawnPlatform.gameObject.activeInHierarchy ? spawnPlatform.TransformPoint(localSpawnPosition) : spawnPosition;
        public float SpawnYaw => spawnPlatform != null && spawnPlatform.gameObject.activeInHierarchy ? spawnPlatform.eulerAngles.y + localSpawnYaw : spawnYaw;
        public void ApplySnapshot(float value, bool playAudio = true)
        {
            bool wasDead = IsDead;
            float previous = Current;
            Current = Mathf.Clamp(value, 0, MaxHealth);
            if (playAudio && Current < previous) GameAudio.Play(IsDead ? SoundCue.Death : SoundCue.Hurt, transform.position);
            if (wasDead == IsDead) return;
            if (IsDead)
            {
                respawnAt = Time.time + RespawnDelay;
                var motor = GetComponent<AdvancedPlayerController>();
                foreach (var helm in FindObjectsByType<HelmInteraction>(FindObjectsSortMode.None))
                    if (motor != null && helm.IsControlledBy(motor)) helm.ReleaseControl();
                GetComponent<ShipDeckPassenger>()?.Attach(null);
                hiddenRenderers = System.Array.FindAll(GetComponentsInChildren<Renderer>(true), r => r.enabled);
                disabledColliders = System.Array.FindAll(GetComponentsInChildren<Collider>(true), c => c.enabled);
                foreach (var renderer in hiddenRenderers) renderer.enabled = false;
                foreach (var collider in disabledColliders) collider.enabled = false;
            }
            else
            {
                if (hiddenRenderers != null) foreach (var renderer in hiddenRenderers) if (renderer != null) renderer.enabled = true;
                if (disabledColliders != null) foreach (var collider in disabledColliders) if (collider != null) collider.enabled = true;
            }
        }
        void Update()
        {
            if (!IsDead || Time.time < respawnAt || (network != null && !network.IsServerInitialized)) return;
            var player = GetComponent<NetworkPlayer>();
            var ship = player != null ? player.Ship : null;
            if (player != null && (ship == null || !ship.ConsumeRespawnRum())) return;
            Vector3 position = ship != null ? ship.transform.TransformPoint(SessionController.Instance.Config.PlayerLocalSpawn) : SpawnPosition;
            float yaw = ship != null ? ship.transform.eulerAngles.y : SpawnYaw;
            if (network != null) network.Respawn(position, yaw);
            else Respawn(position, yaw);
        }
        public void Respawn(Vector3 position, float yaw)
        {
            ApplySnapshot(MaxHealth);
            var state = new PlayerState { Position = position, Yaw = yaw };
            var player = GetComponent<NetworkPlayer>();
            if (player != null) player.Teleport(state);
            else
            {
                GetComponent<AdvancedPlayerController>()?.Restore(state);
                GetComponent<ShipDeckPassenger>()?.Attach(null);
            }
            GameAudio.Play(SoundCue.Respawn, position);
        }
        public void Heal(float amount)
        {
            if (network != null && !network.IsServerInitialized) return;
            if (IsDead || !float.IsFinite(amount) || amount <= 0) return;
            ApplySnapshot(Current + amount, false);
            if (network != null) network.Publish(Current);
        }
        public void Damage(float amount, GameObject attacker = null)
        {
            if (attacker != null && attacker != gameObject)
            {
                var source = attacker.GetComponent<NetworkPlayer>();
                var target = GetComponent<NetworkPlayer>();
                if (source != null && target != null && source.TeamId.Value > 0 && source.TeamId.Value == target.TeamId.Value) return;
            }
            if (network != null && !network.IsServerInitialized) return;
            if (IsDead || amount <= 0 || float.IsNaN(amount) || float.IsInfinity(amount)) return;
            float dealt = Mathf.Min(Current, amount);
            ApplySnapshot(Current - amount, false);
            if (network != null) network.DamageFeedback(dealt, IsDead);
            else GetComponent<DamageFeedback>()?.ReceiveDamage(dealt, IsDead);
            if (attacker != null && attacker != gameObject)
            {
                var source = attacker.GetComponent<NetworkHealth>();
                if (source != null) source.ConfirmHit();
                else attacker.GetComponent<DamageFeedback>()?.ConfirmHit();
            }
            if (network != null) network.Publish(Current);
        }
        public void ReceiveWeaponHit(float damage, GameObject attacker) { Damage(damage, attacker); }
        public void ReceiveFirearmHit(float distance, Vector3 point, GameObject attacker, FirearmSettings settings)
        {

            bool head = controller != null && transform.InverseTransformPoint(point).y >= controller.center.y + controller.height * .5f - .3f;
            Damage(Mathf.Lerp(head ? settings.NearHeadDamage : settings.NearDamage,
                head ? settings.FarHeadDamage : settings.FarDamage,
                Mathf.InverseLerp(settings.FalloffStart, settings.FalloffEnd, distance)), attacker);
        }
        void OnGUI()
        {
            var owner = GetComponent<NetworkPlayer>();
            if (owner != null && owner.IsOwner) return;
            if (IsDead) return;
            var camera = Camera.main;
            if (camera == null || !camera.isActiveAndEnabled) return;
            Vector3 screen = camera.WorldToScreenPoint(transform.position + Vector3.up * (controller != null ? controller.center.y + controller.height * .5f + .3f : BarHeight));
            if (screen.z <= 0) return;
            float width = 80f;
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
