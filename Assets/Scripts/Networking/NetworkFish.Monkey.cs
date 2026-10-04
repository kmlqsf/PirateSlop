using System.Linq;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using PirateSlop.Ships;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkFish
    {
        readonly SyncVar<NetworkObject> monkeyCarrier = new();
        Vector3 carriedCenter;
        bool carriedCenterReady, monkeyWasCarrying;
        public bool MonkeyCarried => monkeyCarrier.Value != null;
        public NetworkObject MonkeyCarrier => monkeyCarrier.Value;
        public NetworkShip GroundShip
        {
            get
            {
                var ball = GetComponent<NetworkLooseCannonball>();
                if (ball != null) return GetComponent<Cannonball>().PlatformBody != null ? GetComponent<Cannonball>().PlatformBody.GetComponent<NetworkShip>() : null;
                return platform.Value != null ? platform.Value.GetComponent<NetworkShip>() : resolvedPlatform;
            }
        }
        public bool MonkeyCanCollect => Available && !MonkeyCarried && !airborne && (GetComponent<NetworkLooseCannonball>() == null || !GetComponent<NetworkLooseCannonball>().IsHeld && !GetComponent<Cannonball>().Held);

        public bool TryMonkeyCarry(NetworkShip ship, Vector3 hand)
        {
            if (!IsServerInitialized || ship == null || !ship.IsServerInitialized || ship.IsSinking || !MonkeyCanCollect || GroundShip != ship || Vector3.Distance(transform.position, hand) > 1.25f) return false;
            monkeyCarrier.Value = ship.NetworkObject;
            carriedCenterReady = false;
            airborne = false; velocity = Vector3.zero; visualReady = false;
            return true;
        }

        public void ReleaseMonkeyCarry(NetworkObject support, Vector3 point, Quaternion orientation)
        {
            if (!IsServerInitialized || !MonkeyCarried) return;
            monkeyCarrier.Value = null; monkeyWasCarrying = false;
            airborne = false; velocity = Vector3.zero; nextFlop = Time.time + Random.Range(10f, 30f);
            Place(support, point, orientation);
            var ball = GetComponent<Cannonball>();
            if (ball != null && GetComponent<NetworkLooseCannonball>() != null)
            {
                ball.Held = false; ball.Body.position = point; ball.Body.rotation = orientation;
                ball.Release();
                if (support != null) ball.AttachToPlatform(support.GetComponent<Rigidbody>());
            }
        }

        bool PresentMonkeyCarry()
        {
            if (!IsSpawned) return false;
            if (!MonkeyCarried)
            {
                if (monkeyWasCarrying)
                {
                    monkeyWasCarrying = false;
                    var oldBall = GetComponent<Cannonball>();
                    if (oldBall != null && GetComponent<NetworkLooseCannonball>() != null) oldBall.Held = false;
                }
                return false;
            }
            var monkey = monkeyCarrier.Value.GetComponent<ShipMonkey>();
            if (monkey == null || monkey.Visual == null) return true;
            if (!carriedCenterReady)
            {
                var renderers = GetComponentsInChildren<Renderer>().Where(r => r is MeshRenderer || r is SkinnedMeshRenderer).ToArray();
                if (renderers.Length > 0)
                {
                    var bounds = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                    carriedCenter = transform.InverseTransformPoint(bounds.center);
                }
                carriedCenterReady = true;
            }
            Quaternion facing = monkey.Visual.rotation * Quaternion.Euler(0f, CurrentItem == InventoryItem.Fish ? 90f : 0f, 0f);
            Vector3 point = monkey.HeldPoint - facing * Vector3.Scale(carriedCenter, transform.lossyScale);
            transform.SetPositionAndRotation(point, facing);
            var ball = GetComponent<Cannonball>();
            if (ball != null && GetComponent<NetworkLooseCannonball>() != null)
            {
                ball.Held = true; ball.Body.isKinematic = true;
                ball.Body.position = point; ball.Body.rotation = facing;
            }
            monkeyWasCarrying = true;
            if (IsServerInitialized && Item == InventoryItem.Fish) expires += Time.deltaTime;
            return true;
        }
    }
}
