using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using PirateSlop.Networking;

namespace PirateSlop
{
    [DefaultExecutionOrder(25)]
    public sealed class MalletHands : MonoBehaviour
    {
        public Transform ViewPivot, WorldPivot;
        PlayerInventory inventory;
        AdvancedPlayerController motor;
        CannonHands cannonHands;
        DirectShipControls controls;
        NetworkWeapon network;
        Renderer[] viewRenderers, worldRenderers;
        Quaternion viewRest, worldRest;
        float nextInput, nextAuthority, swing;
        ShipRepair target;
        int targetId, strikes;
        Vector3 targetPoint;
        bool Equipped => inventory != null && inventory.MalletSelected && !inventory.HandsOccupied && !motor.IsDead && !motor.IsSwimming && !motor.IsClimbing && !motor.LocomotionLocked && !cannonHands.HasHeldBall;
        void Awake()
        {
            inventory = GetComponent<PlayerInventory>(); motor = GetComponent<AdvancedPlayerController>();
            cannonHands = GetComponent<CannonHands>(); controls = GetComponent<DirectShipControls>(); network = GetComponent<NetworkWeapon>();
            viewRest = ViewPivot.localRotation; worldRest = WorldPivot.localRotation;
            viewRenderers = ViewPivot.GetComponentsInChildren<Renderer>(true); worldRenderers = WorldPivot.GetComponentsInChildren<Renderer>(true);
        }
        void OnEnable() { RenderPipelineManager.beginCameraRendering += BeforeCamera; }
        void OnDisable() { RenderPipelineManager.beginCameraRendering -= BeforeCamera; }
        void BeforeCamera(ScriptableRenderContext context, Camera camera)
        {
            bool firstPerson = camera == motor.PlayerCamera && !motor.IsThirdPerson;
            foreach (var renderer in viewRenderers) renderer.forceRenderingOff = !Equipped || !firstPerson || !motor.InputActive;
            foreach (var renderer in worldRenderers) renderer.forceRenderingOff = !Equipped || firstPerson;
        }
        public bool AcceptAuthorityStrike()
        {
            if (Time.time < nextAuthority) return false;
            nextAuthority = Time.time + .65f; return true;
        }
        public void PlayStrike() { swing = 1f; }
        void Update()
        {
            target = null;
            if (!Equipped || !motor.InputActive || PlayerInventory.LootWindowOpen || (controls != null && controls.BlocksPrimary)) return;
            var camera = motor.PlayerCamera;
            RaycastHit nearest = default; float distance = 3.3f;
            foreach (var hit in Physics.RaycastAll(camera.transform.position, camera.transform.forward, distance, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) && hit.distance < distance) { nearest = hit; distance = hit.distance; }
            if (nearest.collider == null) return;
            target = nearest.collider.GetComponentInParent<ShipRepair>();
            if (target == null || !target.FindDamage(nearest.point, out targetId, out strikes)) { target = null; return; }
            targetPoint = nearest.point;
            if (Mouse.current == null || !Mouse.current.leftButton.isPressed || Time.time < nextInput) return;
            nextInput = Time.time + .7f;
            if (strikes == 0 && inventory.TotalPlanks == 0) return;
            if (network.IsOwner) target.Strike(targetId, targetPoint);
        }
        void LateUpdate()
        {
            swing = Mathf.MoveTowards(swing, 0f, Time.deltaTime * 2.5f);
            float angle = Mathf.Sin(swing * Mathf.PI) * -65f;
            ViewPivot.localRotation = viewRest * Quaternion.Euler(angle, 0, 0);
            WorldPivot.localRotation = worldRest * Quaternion.Euler(angle, 0, 0);
        }
        void OnGUI()
        {
            if (!Equipped || !motor.InputActive || PlayerInventory.LootWindowOpen) return;
            string hint = target == null ? "Наведите киянку на повреждение корабля" : strikes == 0 && inventory.TotalPlanks == 0 ? "Для ремонта нужна доска" : "ЛКМ — ремонт · Удары " + strikes + "/3" + (strikes == 0 ? " · 1 доска" : "");
            GUI.Box(new Rect(Screen.width * .5f - 260, Screen.height - 195, 520, 30), hint);
        }
    }
}
