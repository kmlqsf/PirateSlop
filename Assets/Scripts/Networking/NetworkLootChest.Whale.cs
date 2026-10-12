using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
namespace PirateSlop.Networking
{
    public sealed partial class NetworkLootChest
    {
        readonly SyncVar<NetworkObject> whaleSupport = new();
        readonly SyncVar<bool> whaleAttached = new();
        public WhaleLootPoint Whale => whaleSupport.Value != null ? whaleSupport.Value.GetComponent<WhaleLootPoint>() : null;
        public bool OnWhale => whaleAttached.Value;
        public void PlaceOnWhale(WhaleLootPoint whale)
        {
            kind.Value = SeaLootKind.FloatingReward; phase.Value = SeaLootState.Ready;
            eventPoint.Value = transform.position;
            whaleSupport.Value = whale.NetworkObject; whaleAttached.Value = true;
        }
        public bool CanReachWhaleChest(NetworkWeapon actor)
        {
            var whale=Whale;
            if(actor==null || whale==null) return false;
            var origin=WhaleHarpoonPin.ReachOrigin(actor);
            if(Vector3.Distance(origin,transform.position)>5f) return false;
            var passenger=actor.GetComponent<ShipDeckPassenger>();
            bool onBack=passenger!=null && passenger.Support!=null && passenger.Support.GetComponent<WhaleLootPoint>()==whale
                || Vector3.Dot(origin-whale.ChestSocket.position,whale.transform.up)>-.25f;
            foreach(var collider in GetComponentsInChildren<Collider>())
            {
                if(!collider.enabled || collider.isTrigger) continue;
                var target=collider.bounds.ClosestPoint(origin);
                var delta=target-origin;
                bool blocked=false;
                foreach(var hit in Physics.RaycastAll(origin,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore))
                {
                    if(hit.transform.IsChildOf(actor.transform) || hit.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<AdvancedPlayerController>()!=null) continue;
                    if(onBack && hit.collider.GetComponentInParent<WhaleLootPoint>()==whale) continue;
                    if(hit.distance<delta.magnitude-.05f) { blocked=true; break; }
                }
                if(!blocked) return true;
            }
            return false;
        }
        bool UpdateWhaleChest()
        {
            if (!OnWhale) return false;
            var whale = Whale;
            if (whale != null && whale.ChestSocket != null) transform.SetPositionAndRotation(whale.ChestSocket.position, whale.ChestSocket.rotation);
            return true;
        }
        string WhaleHint => Whale == null ? "Раненый кит" : Whale.ChestAvailable ? "E — открыть · F — забрать сундук" : Whale.State == WhalePOIState.Diving ? "Кит погружается!" : $"Сними по одному гарпуну из каждой связки: {Whale.FreedPairs}/3";
    }
}

