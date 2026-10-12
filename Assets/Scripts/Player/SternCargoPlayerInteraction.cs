using FishNet.Object;
using PirateSlop.Networking;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PirateSlop
{
    [DefaultExecutionOrder(-80)]
    public sealed class SternCargoPlayerInteraction : MonoBehaviour
    {
        NetworkWeapon network;
        AdvancedPlayerController motor;
        NetworkTowedCargo hovered, held, selected;
        NetworkSternCargoEvent nearby;
        int consumedFrame = -1;
        float nextSend, grabAt, messageUntil;
        uint attempt;
        byte lastStage;
        bool confirmed, ownsLock, waitForRelease;
        Vector2 handMotion, pendingMotion;
        Vector3 hoverGrab, grabLocal;
        string message;
        public bool ConsumedInput => consumedFrame == Time.frameCount;
        public NetworkTowedCargo HeldCargo => confirmed ? held : null;
        public Vector2 HandMotion => handMotion;
        void Awake() { network = GetComponent<NetworkWeapon>(); motor = GetComponent<AdvancedPlayerController>(); }
        void Update()
        {
            hovered = null; nearby = null;
            if (network == null || !network.IsOwner || !network.IsClientInitialized) return;
            float nearestShip = 38;
            foreach (var owner in NetworkSternCargoEvent.Active)
            {
                if (owner == null || !owner.IsSpawned) continue;
                float distance = Vector3.Distance(motor.transform.position,owner.transform.position);
                if (distance<nearestShip) { nearestShip = distance; nearby = owner; }
            }
            var keys = Keyboard.current; var mouse = Mouse.current;
            if (mouse != null && !mouse.leftButton.isPressed) waitForRelease = false;
            if (keys == null || mouse == null || motor.PlayerCamera == null || motor.IsDead || motor.IsDowned || motor.IsFrozen || motor.IsSwimming || motor.IsClimbing ||
                SessionController.MenuOpen || DeveloperMenu.IsOpen || RoguelikeUpgradeUI.BlocksInput || network.LootHandsBusy ||
                Cursor.lockState != CursorLockMode.Locked || motor.LocomotionLocked && !ownsLock)
            { Release(); return; }
            if (held != null)
            {
                consumedFrame = Time.frameCount; nearby = held.Source;
                if (!held.CanWork || !held.CanReach(network,true) || !mouse.leftButton.isPressed || keys.qKey.wasPressedThisFrame || keys.escapeKey.wasPressedThisFrame)
                { Release(); return; }
                if (!confirmed && Time.unscaledTime-grabAt>1.5f) { Reject("Сервер не подтвердил захват каната"); return; }
                if (confirmed && held.Operator != network.NetworkObject && Time.unscaledTime-grabAt>.6f) { Reject("Канат отпущен"); return; }
                if (!confirmed) return;
                if (lastStage != held.WorkStage) { handMotion = pendingMotion = Vector2.zero; lastStage = held.WorkStage; }
                motor.CargoRopeWalking = held.WalkingPull;
                var delta = mouse.delta.ReadValue();
                if (!held.KnotUntied)
                {
                    pendingMotion.x += delta.magnitude*.006f;
                    handMotion = Vector2.ClampMagnitude(delta*.0015f,.12f);
                }
                else if (held.WalkingPull) handMotion = new Vector2(0,.35f);
                else
                {
                    float previous = handMotion.y;
                    handMotion.y = Mathf.Clamp01(previous+Mathf.Max(0,-delta.y)*.006f);
                    pendingMotion.y += handMotion.y-previous;
                }
                motor.SetInteractionCameraPull(handMotion.y*.5f);
                if (Time.unscaledTime>=nextSend)
                {
                    network.SternCargoInput(held.NetworkObject,true,pendingMotion,grabLocal,held.WorkStage,attempt);
                    pendingMotion = Vector2.zero; nextSend = Time.unscaledTime+.05f;
                }
                return;
            }
            if (!motor.InputActive || WhalePlayerInteraction.InputBlocked) return;
            var ray = new Ray(motor.PlayerCamera.transform.position,motor.PlayerCamera.transform.forward);
            float nearest = float.PositiveInfinity;
            foreach (var cargo in NetworkTowedCargo.Active)
            {
                if (cargo == null || !cargo.CanWork || !cargo.CanReach(network) || !cargo.AimRope(ray,out float along,out var point) || along>=nearest) continue;
                if (cargo.KnotUntied && !cargo.CanGripPoint(network,point)) continue;
                hovered = cargo; hoverGrab = point; nearest = along;
            }
            if (hovered == null && selected != null && selected.CanWork && selected.KnotUntied && selected.CanReach(network,true) &&
                selected.FindReachableGrip(network,out var nearbyGrip) && selected.CanGripPoint(network,nearbyGrip))
            { hovered = selected; hoverGrab = nearbyGrip; }
            if (hovered == null || !mouse.leftButton.wasPressedThisFrame || waitForRelease) return;
            consumedFrame = Time.frameCount; held = hovered; confirmed = false;
            grabAt = Time.unscaledTime; nextSend = grabAt+.05f; lastStage = held.WorkStage;
            handMotion = pendingMotion = Vector2.zero;
            grabLocal = held.Source.transform.InverseTransformPoint(hoverGrab);
            motor.ShipActivityLocked = ownsLock = true; motor.CargoRopeWalking = held.WalkingPull;
            network.SternCargoInput(held.NetworkObject,true,Vector2.zero,grabLocal,held.WorkStage,++attempt,true);
        }
        public void GripResult(NetworkObject target,uint request,bool accepted,string reason)
        {
            if (held == null || held.NetworkObject != target || attempt != request) return;
            if (accepted) confirmed = true; else Reject(reason);
        }
        void Reject(string reason) { message = reason; messageUntil = Time.unscaledTime+3; waitForRelease = true; Release(); }
        void Release()
        {
            if (held != null)
            {
                if (held.Source != null) selected = held;
                if (network != null && network.IsSpawned) network.SternCargoInput(held.NetworkObject,false,pendingMotion,grabLocal,held.WorkStage,attempt);
            }
            held = null; confirmed = false; handMotion = pendingMotion = Vector2.zero;
            if (ownsLock && motor != null) { motor.ShipActivityLocked = motor.CargoRopeWalking = false; motor.SetInteractionCameraPull(0); }
            ownsLock = false;
        }
        void OnDisable() { Release(); }
        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || network == null || !network.IsOwner || motor.IsDead || motor.IsFrozen || RoguelikeUpgradeUI.WindowOpen || SessionController.MenuOpen) return;
            if (Time.unscaledTime<messageUntil) ContextPrompt.Draw(message);
            else if (held != null && confirmed)
            {
                float x = Screen.width*.5f-350, y = Screen.height*.70f;
                PirateHudStyle.Panel(new Rect(x,y,700,91));
                PirateHudStyle.Label(new Rect(x,y,700,28),held.WorkHint,PirateHudStyle.Paper);
                string action = held.Phase == SternCargoPhase.Hoisting && handMotion.y>.98f ? "Отпустите ЛКМ, затем снова захватите канат" : held.MotionHint;
                PirateHudStyle.Label(new Rect(x,y+28,700,25),action,PirateHudStyle.Gold);
                PirateHudStyle.Bar(new Rect(x+180,y+58,340,6),held.WorkProgress,PirateHudStyle.Gold);
                PirateHudStyle.Label(new Rect(x,y+68,700,22),held.WalkingPull ? "Можно вернуться за новым проходом, не отпуская ЛКМ" : "Захватывайте любой доступный участок каната · Q — выйти",PirateHudStyle.Muted);
            }
            else if (held != null) ContextPrompt.Draw("Захватываем канат…");
            else if (hovered != null) ContextPrompt.Draw($"ЛКМ — взять канат · {hovered.WorkHint}");
            if (nearby != null) PirateHudStyle.Panel(new Rect(Screen.width*.5f-330,Screen.height*.14f,660,34),nearby.Hint);
        }
    }
}
