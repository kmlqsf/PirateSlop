using System.Collections.Generic;
using PirateSlop.Networking;
using UnityEngine;

namespace PirateSlop
{
    public sealed class GunnersEyeView : MonoBehaviour
    {
        NetworkPlayer player;
        Material material;
        readonly List<LineRenderer> arcs = new(), markers = new();
        readonly List<Vector3> points = new(1000);
        SimpleCannon previous;
        float nextPreview;
        void Awake() => player = GetComponent<NetworkPlayer>();
        void LateUpdate()
        {
            var cannon = player != null && player.Motor != null ? player.Motor.ActiveCannon : null;
            bool visible = player != null && player.IsOwner && player.IsClientInitialized && player.HasUpgrade(UpgradeEffect.GunnersEye) && !player.Motor.IsDead && cannon != null && !RoguelikeUpgradeUI.BlocksInput && !SessionController.MenuOpen && !DeveloperMenu.IsOpen;
            if (!visible) { Hide(); previous = null; return; }
            if (previous == cannon && Time.unscaledTime < nextPreview) return;
            previous = cannon; nextPreview = Time.unscaledTime + .1f;
            bool split = player.HasUpgrade(UpgradeEffect.SplitVolley) && cannon.LoadedAmmo != InventoryItem.BoardingHook;
            EnsureLines(split ? 2 : 1);
            for (int i = 0; i < arcs.Count; i++)
            {
                bool active = i < (split ? 2 : 1);
                arcs[i].enabled = active;
                if (!active) { markers[i].enabled = false; continue; }
                float angle = split ? (i == 0 ? -1f : 1f) * RoguelikeTuning.Current.splitAngle : 0f;
                Preview(cannon, cannon.UpgradeShotVelocity(player, angle), arcs[i], markers[i]);
            }
        }
        void EnsureLines(int count)
        {
            if (material == null) material = new Material(Resources.Load<Material>("FirearmGlow"));
            while (arcs.Count < count)
            {
                arcs.Add(CreateLine("GunnersEyeTrajectory", .045f));
                markers.Add(CreateLine("GunnersEyeImpact", .055f));
            }
        }
        LineRenderer CreateLine(string name, float width)
        {
            var go = new GameObject(name);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, gameObject.scene);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material; line.useWorldSpace = true; line.widthMultiplier = width;
            line.startColor = line.endColor = new Color(.93f, .74f, .36f, .8f);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false; line.numCapVertices = 2;
            return line;
        }
        void Preview(SimpleCannon cannon, Vector3 velocity, LineRenderer arc, LineRenderer marker)
        {
            Vector3 position = cannon.ShotPosition, launch = position, initial = velocity;
            Transform source = cannon.GetComponentInParent<ShipController>().transform;
            Vector3 normal = Vector3.up;
            bool impact = false, returning = !cannon.IsMortar && cannon.LoadedAmmo == InventoryItem.BoomerangCannonball;
            points.Clear(); points.Add(position);
            float dt = Time.fixedDeltaTime;
            for (float t = 0f; t < (returning ? 5f : 19f) && points.Count < 1000; t += dt)
            {
                Vector3 nextVelocity = CannonShotDamage.StepVelocity(velocity, dt);
                Vector3 delta = (velocity + nextVelocity) * (.5f * dt);
                if (returning)
                {
                    float age = t + dt;
                    Vector3 next = age <= 2f ? launch + initial * age : CannonShotDamage.ReturnCurve(launch + initial * 2f, launch + initial * 2f + initial.normalized * 8f + Vector3.up * 8f, launch, Vector3.Cross(Vector3.up, initial.normalized).normalized, 16f, 8f, Mathf.Clamp01((age - 2f) / 3f));
                    delta = next - position;
                }
                if (MortarTrajectory.Trace(position, delta, cannon.ProjectileRadius, source, out var point, out var hitNormal, out _))
                { position = point; normal = hitNormal; impact = true; points.Add(position); break; }
                position += delta; velocity = nextVelocity; points.Add(position);
            }
            arc.positionCount = points.Count; arc.SetPositions(points.ToArray());
            marker.enabled = impact;
            if (!impact) return;
            Vector3 right = Vector3.Cross(normal, Vector3.up).normalized;
            if (right.sqrMagnitude < .1f) right = Vector3.right;
            Vector3 up = Vector3.Cross(normal, right).normalized;
            marker.positionCount = 33;
            for (int i = 0; i < 33; i++)
            {
                float a = i / 32f * Mathf.PI * 2f;
                marker.SetPosition(i, position + normal * .025f + (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * .25f);
            }
        }
        void Hide() { foreach (var line in arcs) line.enabled = false; foreach (var line in markers) line.enabled = false; }
        void OnDisable() => Hide();
        void OnDestroy()
        {
            foreach (var line in arcs) if (line != null) Destroy(line.gameObject);
            foreach (var line in markers) if (line != null) Destroy(line.gameObject);
            if (material != null) Destroy(material);
        }
    }
}
