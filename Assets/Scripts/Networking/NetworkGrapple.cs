using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkWeapon
    {
        readonly SyncVar<NetworkObject> grapple = new();
        readonly SyncVar<NetworkObject> grappleShip = new();
        readonly SyncVar<Vector3> grappleNormal = new(Vector3.up);
        readonly SyncVar<bool> grappleAttached = new();
        LineRenderer grappleLine;
        NetworkObject shownGrapple;
        float grappleShownAt;
        Vector3 grappleStart;
        bool releasedGrapple;
        public bool GrappleActive => grapple.Value != null && grapple.Value.IsSpawned && grappleAttached.Value && !releasedGrapple && Time.time >= grappleShownAt + .6f;
        public Vector3 GrapplePoint => grapple.Value.transform.position;
        public Rigidbody GrappleBody => grappleShip.Value != null ? grappleShip.Value.GetComponent<Rigidbody>() : null;
        public Vector3 GrappleNormal => grappleShip.Value != null ? grappleShip.Value.transform.TransformDirection(grappleNormal.Value) : grappleNormal.Value;
        public void ThrowGrapple(Vector3 direction)
        {
            if (!IsServerInitialized || !CanHandleBall() || inventory.EquipmentAt(inventory.SelectedSlot) != InventoryItem.GrapplingHook) return;
            if (grapple.Value != null && grapple.Value.IsSpawned) return;
            if (!float.IsFinite(direction.sqrMagnitude) || direction.sqrMagnitude < .5f || DropPrefabs.Length <= 17 || DropPrefabs[17] == null) return;
            Vector3 origin = transform.position + Vector3.up * 1.55f;
            RaycastHit nearest = default;
            float distance = 35;
            foreach (var hit in Physics.RaycastAll(origin,direction.normalized,distance,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform)) continue;
                if (hit.distance < distance) { nearest=hit; distance=hit.distance; }
            }
            if (nearest.collider == null) return;
            if (nearest.collider.GetComponentInParent<NetworkFish>() != null || nearest.collider.GetComponentInParent<CombatHealth>() != null) return;
            var support = nearest.collider.GetComponentInParent<NetworkShip>();
            if (nearest.rigidbody != null && support == null) return;
            if (!ConsumeEquipment(inventory.SelectedSlot,InventoryItem.GrapplingHook)) return;
            Vector3 point=nearest.point+nearest.normal*.12f;
            var hook=Instantiate(DropPrefabs[17],point,Quaternion.LookRotation(nearest.normal));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(hook.gameObject,gameObject.scene);
            hook.Place(support != null ? support.NetworkObject : null,point,hook.transform.rotation);
            ServerManager.Spawn(hook.NetworkObject);
            grapple.Value=hook.NetworkObject;
            grappleShip.Value=support != null ? support.NetworkObject : null;
            grappleNormal.Value=support != null ? support.transform.InverseTransformDirection(nearest.normal) : nearest.normal;
            grappleAttached.Value=true;
            releasedGrapple=false;
        }
        public void ReleaseGrapple()
        {
            if (IsServerInitialized) grappleAttached.Value=false;
            if (IsOwner) releasedGrapple=true;
        }
        [ServerRpc] void ReleaseGrappleServerRpc() => grappleAttached.Value=false;
        [ServerRpc]
        void RecoverGrappleServerRpc()
        {
            if (grapple.Value == null || !grapple.Value.IsSpawned) return;
            var motor = GetComponent<AdvancedPlayerController>();
            if (motor.IsDead || motor.LocomotionLocked || !CanAddItem(InventoryItem.GrapplingHook)) return;
            Vector3 eye = transform.position + Vector3.up * 1.5f;
            Vector3 delta = GrapplePoint - eye;
            if (delta.magnitude > 3f) return;
            foreach (var hit in Physics.RaycastAll(eye, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) && !hit.transform.IsChildOf(grapple.Value.transform)) return;
            var pickup = grapple.Value.GetComponent<NetworkFish>();
            if (pickup == null || !pickup.Take()) return;
            AddItem(InventoryItem.GrapplingHook);
            grappleAttached.Value = false;
            grapple.Value = null;
        }
        void UpdateGrapple()
        {
            if (!IsSpawned) return;
            bool exists=grapple.Value != null && grapple.Value.IsSpawned;
            if (shownGrapple != grapple.Value)
            {
                shownGrapple=grapple.Value; grappleShownAt=Time.time; releasedGrapple=false;
                grappleStart=transform.position+Vector3.up*1.5f;
            }
            if (IsServerInitialized && (!exists || GetComponent<AdvancedPlayerController>().IsDead)) grappleAttached.Value=false;
            if (IsOwner && exists && GetComponent<AdvancedPlayerController>().InputActive && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                RecoverGrappleServerRpc();
            if (IsOwner && GrappleActive && GetComponent<AdvancedPlayerController>().InputActive && Keyboard.current != null &&
                (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.qKey.wasPressedThisFrame))
            { releasedGrapple=true; ReleaseGrappleServerRpc(); }
            if (grappleLine == null)
            {
                var rope=new GameObject("GrappleRope"); rope.transform.SetParent(transform,false);
                grappleLine=rope.AddComponent<LineRenderer>();
                grappleLine.sharedMaterial=Resources.Load<Material>("HookRope");
                grappleLine.positionCount=16; grappleLine.widthMultiplier=.025f;
                grappleLine.generateLightingData=true; grappleLine.numCapVertices=3;
            }
            grappleLine.enabled=exists && grappleAttached.Value && !releasedGrapple;
            if (!grappleLine.enabled) return;
            Vector3 start=transform.position+Vector3.up*1.15f, end=GrapplePoint;
            float flight=Mathf.Clamp01((Time.time-grappleShownAt)/.6f);
            end=Vector3.Lerp(grappleStart,end,flight)+Vector3.up*(Mathf.Sin(flight*Mathf.PI)*1.5f);
            if (grapple.Value.transform.childCount > 0) grapple.Value.transform.GetChild(0).position=end;
            for(int i=0;i<16;i++)
            {
                float t=i/15f;
                grappleLine.SetPosition(i,Vector3.Lerp(start,end,t)+Vector3.down*(Mathf.Sin(t*Mathf.PI)*.12f));
            }
            if (IsOwner && !SessionController.MenuOpen)
                grappleLine.widthMultiplier=.025f;
        }
        void OnGUI()
        {
            if (!IsOwner || !GrappleActive || !GetComponent<AdvancedPlayerController>().InputActive) return;
            PirateHudStyle.Panel(new Rect(Screen.width*.5f-340,Screen.height-180,680,32), "W/S — вверх/вниз · A/D — вбок · Space/Q — отпустить · E рядом — забрать");
        }
    }
}
