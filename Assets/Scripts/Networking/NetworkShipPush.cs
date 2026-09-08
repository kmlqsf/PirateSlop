using FishNet.Object;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PirateSlop.Networking
{
    public sealed class NetworkShipPush : NetworkBehaviour
    {
        NetworkPlayer player;
        NetworkShip aimed;
        float nextPush;
        void Awake() { player = GetComponent<NetworkPlayer>(); }
        void Update()
        {
            if (!IsOwner) return;
            aimed = null;
            if (!player.Motor.InputActive || player.Motor.IsDead || player.Motor.IsClimbing || player.Motor.LocomotionLocked || player.Passenger.Ship != null) return;
            var camera = player.Motor.PlayerCamera;
            foreach (var hit in Physics.RaycastAll(camera.transform.position,camera.transform.forward,3.5f,~0,QueryTriggerInteraction.Ignore))
            {
                if(hit.transform.IsChildOf(transform)) continue;
                var ship=hit.collider.GetComponentInParent<NetworkShip>();
                if(ship!=null && Outside(ship)) { aimed=ship; break; }
            }
            if (aimed != null && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) PushServerRpc(aimed.NetworkObject);
        }
        bool Outside(NetworkShip ship)
        {
            var p=ship.transform.InverseTransformPoint(transform.position);
            return Mathf.Abs(p.x)>5.5f || Mathf.Abs(p.z)>21f;
        }
        [ServerRpc]
        void PushServerRpc(NetworkObject target)
        {
            if (target == null || Time.time < nextPush || player.Motor.IsDead || player.Motor.IsClimbing || player.Motor.LocomotionLocked || player.Passenger.Ship != null) return;
            var ship=target.GetComponent<NetworkShip>();
            if(ship==null || !Outside(ship)) return;
            Vector3 origin=transform.position+Vector3.up;
            bool reachable=false;
            foreach(var c in ship.GetComponentsInChildren<Collider>())
                if(c.enabled && !c.isTrigger && Vector3.Distance(origin,c.ClosestPoint(origin))<3.5f && GetComponent<NetworkWeapon>().CanReach(c.ClosestPoint(origin),ship.transform)) { reachable=true; break; }
            if(!reachable) return;
            nextPush=Time.time+1.2f;
            Vector3 direction=Vector3.ProjectOnPlane(ship.transform.position-transform.position,Vector3.up).normalized;
            if(ship.Motor.TryManualPush(direction)) PushAudioObserversRpc(transform.position);
        }
        [ObserversRpc(RunLocally=true)]
        void PushAudioObserversRpc(Vector3 point) => GameAudio.Play(SoundCue.Creak,point,.8f);
        void OnGUI()
        {
            if(IsOwner && aimed!=null && player.Motor.InputActive)
                GUI.Box(new Rect(Screen.width/2f-150,Screen.height-180,300,30),"E — оттолкнуть корабль");
        }
    }
}
