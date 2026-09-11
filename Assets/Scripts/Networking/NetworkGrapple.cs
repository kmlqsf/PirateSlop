using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkWeapon
    {
        readonly SyncVar<byte> hookPhase = new();
        readonly SyncVar<Vector3> hookPoint = new();
        readonly SyncVar<Vector3> hookNormal = new(Vector3.up);
        readonly SyncVar<NetworkObject> hookAnchor = new();
        readonly SyncVar<NetworkObject> hookVictim = new();
        readonly SyncVar<NetworkObject> hookCaptor = new();
        Vector3 flightDirection;
        float flightDistance, hookExpires, hookCooldown, blockedFor;
        LineRenderer grappleLine;
        Transform hookVisual;
        Vector3 visualPoint;
        bool visualActive;
        public bool GrappleActive => hookPhase.Value == 2 && hookVictim.Value == null;
        public bool BeingHooked => hookCaptor.Value != null && hookCaptor.Value.IsSpawned && hookCaptor.Value.GetComponent<NetworkWeapon>().hookPhase.Value == 2;
        public Vector3 GrapplePoint => hookAnchor.Value != null ? hookAnchor.Value.transform.TransformPoint(hookPoint.Value) : hookPoint.Value;
        public Rigidbody GrappleBody => hookAnchor.Value != null ? hookAnchor.Value.GetComponent<Rigidbody>() : null;
        public Vector3 GrappleNormal => hookAnchor.Value != null ? hookAnchor.Value.transform.TransformDirection(hookNormal.Value) : hookNormal.Value;
        public Vector3 PullPoint => BeingHooked ? hookCaptor.Value.transform.position + Vector3.up : GrapplePoint + GrappleNormal * .55f;
        public void ThrowGrapple(Vector3 direction)
        {
            if (!IsServerInitialized || !CanHandleBall() || inventory.EquipmentAt(inventory.SelectedSlot) != InventoryItem.GrapplingHook || BeingHooked) return;
            if (hookPhase.Value != 0 || Time.time < hookCooldown || !float.IsFinite(direction.sqrMagnitude) || direction.sqrMagnitude < .5f) return;
            flightDirection = direction.normalized;
            hookPoint.Value = transform.position + Vector3.up * 1.55f;
            hookAnchor.Value = null; hookVictim.Value = null;
            flightDistance = 0f; blockedFor = 0f;
            hookExpires = Time.time + 6f; hookPhase.Value = 1;
        }
        public void ReleaseGrapple()
        {
            if (IsServerInitialized) EndHook();
            else if (IsOwner) ReleaseGrappleServerRpc();
        }
        [ServerRpc] void ReleaseGrappleServerRpc() => EndHook();
        void EndHook()
        {
            if (hookVictim.Value != null)
            {
                var victim = hookVictim.Value.GetComponent<NetworkWeapon>();
                if (victim != null && victim.hookCaptor.Value == NetworkObject) victim.hookCaptor.Value = null;
            }
            hookVictim.Value = null; hookAnchor.Value = null; hookPhase.Value = 0;
            hookCooldown = Time.time + .7f;
        }
        public override void OnStopServer()
        {
            EndHook();
            if (hookCaptor.Value != null) hookCaptor.Value.GetComponent<NetworkWeapon>()?.EndHook();
            base.OnStopServer();
        }
        void AdvanceHook()
        {
            if (hookPhase.Value == 0) return;
            var motor = GetComponent<AdvancedPlayerController>();
            if (motor.IsDead || Time.time >= hookExpires || inventory.EquipmentAt(inventory.SelectedSlot) != InventoryItem.GrapplingHook)
            { EndHook(); return; }
            if (hookPhase.Value == 1)
            {
                float step = Mathf.Min(55f * Time.deltaTime, 35f - flightDistance);
                RaycastHit closest = default;
                float distance = step + .001f;
                foreach (var hit in Physics.SphereCastAll(hookPoint.Value, .12f, flightDirection, step, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    if (hit.transform.IsChildOf(transform) || hit.distance >= distance) continue;
                    closest = hit; distance = hit.distance;
                }
                if (closest.collider != null)
                {
                    var victim = closest.collider.GetComponentInParent<NetworkPlayer>();
                    if (victim != null)
                    {
                        var target = victim.GetComponent<NetworkWeapon>();
                        if (victim.Motor.IsDead || target == null || target.BeingHooked) { EndHook(); return; }
                        target.EndHook(); target.hookCaptor.Value = NetworkObject;
                        hookVictim.Value = victim.NetworkObject; hookAnchor.Value = victim.NetworkObject;
                        hookPoint.Value = Vector3.up;
                        victim.Motor.ApplyKnockback(Vector3.zero);
                    }
                    else
                    {
                        var ship = closest.collider.GetComponentInParent<NetworkShip>();
                        if (closest.collider.GetComponentInParent<NetworkFish>() != null || (closest.rigidbody != null && ship == null)) { EndHook(); return; }
                        hookAnchor.Value = ship != null ? ship.NetworkObject : null;
                        hookPoint.Value = ship != null ? ship.transform.InverseTransformPoint(closest.point) : closest.point;
                        hookNormal.Value = ship != null ? ship.transform.InverseTransformDirection(closest.normal) : closest.normal;
                    }
                    hookPhase.Value = 2; return;
                }
                hookPoint.Value += flightDirection * step; flightDistance += step;
                if (flightDistance >= 35f) EndHook();
                return;
            }
            if (hookVictim.Value != null && hookVictim.Value.GetComponent<CombatHealth>().IsDead) { EndHook(); return; }
            Vector3 start = transform.position + Vector3.up, delta = GrapplePoint - start;
            if (delta.magnitude < (hookVictim.Value != null ? 1.5f : 1.1f) || delta.magnitude > 40f) { EndHook(); return; }
            bool blocked = false;
            foreach (var hit in Physics.RaycastAll(start, delta.normalized, Mathf.Max(0f, delta.magnitude - .35f), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform) || (hookVictim.Value != null && hit.transform.IsChildOf(hookVictim.Value.transform))) continue;
                blocked = true; break;
            }
            blockedFor = blocked ? blockedFor + Time.deltaTime : 0f;
            if (blockedFor > .25f) EndHook();
        }
        void UpdateGrapple()
        {
            if (!IsSpawned) return;
            if (IsServerInitialized) AdvanceHook();
            if (!IsClientInitialized) return;
            if (IsOwner && hookPhase.Value != 0 && GetComponent<AdvancedPlayerController>().InputActive && Keyboard.current != null &&
                (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.qKey.wasPressedThisFrame)) ReleaseGrapple();
            if (grappleLine == null && hookPhase.Value != 0)
            {
                var rope = new GameObject("GrappleRope"); rope.transform.SetParent(transform, false);
                grappleLine = rope.AddComponent<LineRenderer>();
                grappleLine.sharedMaterial = Resources.Load<Material>("HookRope");
                grappleLine.positionCount = 24; grappleLine.widthMultiplier = .025f;
                grappleLine.generateLightingData = true; grappleLine.numCapVertices = 3;
                var model = Resources.Load<GameObject>("GrappleHookModel");
                if (model != null)
                {
                    hookVisual = Instantiate(model, rope.transform).transform;
                    foreach (var collider in hookVisual.GetComponentsInChildren<Collider>()) collider.enabled = false;
                }
            }
            if (grappleLine == null) return;
            bool visible = hookPhase.Value != 0;
            grappleLine.enabled = visible;
            if (hookVisual != null) hookVisual.gameObject.SetActive(visible);
            if (!visible) { visualActive = false; return; }
            Vector3 start = transform.position + Vector3.up * 1.25f + transform.right * .2f, end = GrapplePoint;
            if (!visualActive) { visualPoint = start; visualActive = true; }
            visualPoint = hookPhase.Value == 1 ? Vector3.MoveTowards(visualPoint, end, 70f * Time.deltaTime) : end;
            end = visualPoint;
            if (hookVisual != null)
            {
                hookVisual.position = end;
                if ((end - start).sqrMagnitude > .01f) hookVisual.rotation = Quaternion.LookRotation(end - start) * Quaternion.Euler(90f, 0f, 0f);
            }
            float sag = hookPhase.Value == 1 ? .35f : Mathf.Min(.65f, Vector3.Distance(start, end) * .025f);
            for (int i = 0; i < 24; i++)
            {
                float t = i / 23f;
                grappleLine.SetPosition(i, Vector3.Lerp(start, end, t) + Vector3.down * (Mathf.Sin(t * Mathf.PI) * sag));
            }
        }
        void OnGUI()
        {
            if (!IsOwner || hookPhase.Value == 0 || !GetComponent<AdvancedPlayerController>().InputActive) return;
            PirateHudStyle.Panel(new Rect(Screen.width * .5f - 300, Screen.height - 180, 600, 32), "A/D — раскачиваться · Space/Q — отпустить канат");
        }
    }
}
