using PirateSlop.Networking;
using PirateSlop.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PirateSlop
{
    [DefaultExecutionOrder(-81)]
    public sealed class BalanceStonePlayer : MonoBehaviour
    {
        NetworkWeapon network;
        AdvancedPlayerController motor;
        BalanceEventTarget hovered;
        int consumedFrame = -1;
        float requestAt = -1;
        string message;
        float messageUntil;
        public void Notify(string text) { message = text; messageUntil = Time.unscaledTime + 2.5f; }
        public bool ConsumedInput => consumedFrame == Time.frameCount;
        public bool BlocksInventory => network != null && network.CarryingBalanceStone || Time.unscaledTime < requestAt + .25f;
        public Transform HeldVisual => network != null && network.BalanceEvent != null ? network.BalanceEvent.CarriedVisual(network) : null;
        public float GripWeight
        {
            get
            {
                if(network == null || network.BalanceEvent == null) return 0;
                var state=network.BalanceEvent.Stone(network.BalanceStoneIndex);
                return state.Phase!=2 ? 1 : 1-Mathf.SmoothStep(0,1,((float)(network.BalanceEvent.Clock-state.Started)-NetworkBalanceEvent.DepositSeconds*NetworkBalanceEvent.ReleaseFraction)/.22f);
            }
        }
        public Vector3 HandPoint
        {
            get
            {
                var state=network.BalanceEvent.Stone(network.BalanceStoneIndex);
                return state.Phase==2 && network.BalanceEvent.Clock>=state.Started+NetworkBalanceEvent.DepositSeconds*NetworkBalanceEvent.ReleaseFraction ? state.ReleasePoint : HeldVisual.position;
            }
        }
        readonly RaycastHit[] hits = new RaycastHit[48];
        void Awake() { network = GetComponent<NetworkWeapon>(); motor = GetComponent<AdvancedPlayerController>(); }
        void Update()
        {
            hovered = null;
            if (network == null || !network.IsOwner || !network.IsClientInitialized || motor.IsDead || motor.PlayerCamera == null) return;
            var keyboard = Keyboard.current;
            if (network.CarryingBalanceStone && keyboard != null && keyboard.gKey.wasPressedThisFrame && motor.InputActive)
            { consumedFrame = Time.frameCount; network.DropBalanceStone(); return; }
            if (!motor.InputActive || motor.LocomotionLocked) return;
            var camera = motor.PlayerCamera.transform;
            int count = Physics.SphereCastNonAlloc(camera.position,.1f,camera.forward,hits,2.8f,~0,QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            for (int i=0;i<count;i++)
            {
                if (hits[i].collider.transform.IsChildOf(transform) || hits[i].distance >= nearest) continue;
                var candidate = BalanceEventTarget.FromCollider(hits[i].collider);
                if (network.CarryingBalanceStone && candidate != null && candidate.StoneIndex == network.BalanceStoneIndex && candidate.Owner == network.BalanceEvent) continue;
                nearest=hits[i].distance; hovered=candidate;
            }
            if (hovered == null || hovered.Owner == null) return;
            bool usable = hovered.StoneIndex < 0 ? hovered.Owner.CanDeposit(network) : hovered.Owner.CanTake(network,hovered.StoneIndex);
            if (keyboard == null || !keyboard.eKey.wasPressedThisFrame || Time.unscaledTime < requestAt + .25f) return;
            if (!usable)
            {
                if (hovered.StoneIndex < 0 && network.CarryingBalanceStone)
                {
                    consumedFrame = Time.frameCount;
                    Notify(hovered.Owner.DepositBlockReason(network));
                }
                return;
            }
            consumedFrame=Time.frameCount; requestAt=Time.unscaledTime;
            if (hovered.StoneIndex < 0) network.DepositBalanceStone(); else network.TakeBalanceStone(hovered.Owner.NetworkObject,hovered.StoneIndex);
        }
        void OnGUI()
        {
            if (network == null || !network.IsOwner || motor.IsDead || RoguelikeUpgradeUI.WindowOpen) return;
            string hint=null;
            if (network.CarryingBalanceStone)
            {
                var state=network.BalanceEvent.Stone(network.BalanceStoneIndex);
                hint=state.Phase==2 ? "Кладём камень на чашу…" : $"Камень в руках · G — бросить · Весы: {network.BalanceEvent.Deposited}/5";
                if (hovered != null && hovered.StoneIndex<0 && state.Phase != 2)
                {
                    var reason = hovered.Owner.DepositBlockReason(network);
                    hint = reason == null ? "E — положить камень на чашу · G — бросить" : reason + " · G — бросить";
                }
            }
            else if (hovered != null && hovered.Owner != null && hovered.StoneIndex>=0 && hovered.Owner.CanTake(network,hovered.StoneIndex)) hint="E — поднять камень";
            else if (hovered != null && hovered.StoneIndex<0) hint=$"Весы: {hovered.Owner.Deposited}/5 · найдите камни на острове";
            if (Time.unscaledTime < messageUntil && !string.IsNullOrEmpty(message)) hint = message;
            if (hint != null) PirateHudStyle.Panel(new Rect(Screen.width*.5f-330,Screen.height*.72f,660,34),hint);
        }
    }
}
