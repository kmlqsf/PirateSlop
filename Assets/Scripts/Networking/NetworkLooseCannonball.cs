using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace PirateSlop.Networking
{
    [RequireComponent(typeof(Cannonball))]
    public sealed class NetworkLooseCannonball : NetworkBehaviour
    {
        readonly SyncVar<Vector3> position = new();
        readonly SyncVar<Quaternion> rotation = new(Quaternion.identity);
        readonly SyncVar<NetworkObject> platform = new();
        readonly SyncVar<int> holder = new(-1);
        Cannonball ball;
        bool localHolding;
        float nextSync, lastHold;
        public bool IsHeld => holder.Value >= 0;
        void Awake() { ball = GetComponent<Cannonball>(); }
        public override void OnStartServer()
        {
            base.OnStartServer();
            position.Value = transform.position; rotation.Value = transform.rotation;
            ball.Release();
            PublishPose();
        }
        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!IsServerInitialized) ball.Body.isKinematic = true;
        }
        NetworkWeapon Player(NetworkConnection sender)
        {
            var player = sender != null ? SessionController.Instance.GetPlayer(sender.ClientId) : null;
            if (player == null || player.Motor.IsDead || player.Motor.IsSwimming || player.Motor.IsClimbing || player.Motor.LocomotionLocked) return null;
            var fishing = player.GetComponent<NetworkFishing>();
            if (fishing != null && (fishing.IsFishing || fishing.IsEating || fishing.CarryingCatch)) return null;
            return player.GetComponent<NetworkWeapon>();
        }
        public void RequestHold(bool holding, Vector3 point)
        {
            if (!IsSpawned || !IsClientInitialized) { localHolding = false; if (ball != null) ball.Held = false; return; }
            localHolding = holding;
            HoldServerRpc(holding, point);
        }
        [ServerRpc(RequireOwnership = false)]
        void HoldServerRpc(bool holding, Vector3 point, NetworkConnection sender = null)
        {
            var player = Player(sender);
            if (sender == null) return;
            if (holder.Value >= 0 && holder.Value != sender.ClientId) { RejectTargetRpc(sender); return; }
            if (!holding)
            {
                if (holder.Value == sender.ClientId) Release();
                return;
            }
            if (player == null || !float.IsFinite(point.sqrMagnitude) || !player.CanReach(point, transform) ||
                (holder.Value == -1 && Vector3.Distance(point, transform.position) > 1f))
            { RejectTargetRpc(sender); return; }
            if (!ball.Body.isKinematic) { ball.Body.linearVelocity = Vector3.zero; ball.Body.angularVelocity = Vector3.zero; }
            holder.Value = sender.ClientId; lastHold = Time.time;
            ball.Held = true; ball.AttachToPlatform(null); platform.Value = null; ball.Body.isKinematic = true;
            ball.GetComponent<Collider>().enabled = false;
            ball.Body.position = point; position.Value = point;
        }
        [TargetRpc]
        void RejectTargetRpc(NetworkConnection sender) { localHolding = false; ball.Held = false; }
        void Release()
        {
            holder.Value = -1; localHolding = false; ball.Held = false;
            ball.GetComponent<Collider>().enabled = true; ball.Release();
        }
        public void Store() { if (IsSpawned && IsClientInitialized) StoreServerRpc(); }
        [ServerRpc(RequireOwnership = false)]
        void StoreServerRpc(NetworkConnection sender = null)
        {
            var player = Player(sender);
            if (player == null || (IsHeld && holder.Value != sender.ClientId) || !player.CanReach(transform.position, transform)) return;
            if (player.AddItem(GetComponent<NetworkFish>().CurrentItem)) GetComponent<NetworkFish>().Take();
        }
        public void Load(NetworkObject ship, int index)
        {
            if (IsSpawned && IsClientInitialized && ship != null && ship.IsSpawned)
                LoadServerRpc(ship, index, ship.transform.InverseTransformPoint(transform.position));
        }
        public override void OnStopClient()
        {
            localHolding = false;
            if (ball != null) ball.Held = false;
            base.OnStopClient();
        }
        [ServerRpc(RequireOwnership = false)]
        void LoadServerRpc(NetworkObject ship, int index, Vector3 localPoint, NetworkConnection sender = null)
        {
            var player = Player(sender);
            if (player == null || holder.Value != sender.ClientId || ship == null) return;
            var cannon = ship.GetComponent<NetworkCannon>();
            if (cannon == null || cannon.Crate == null || index < 0 || index >= cannon.Crate.Cannons.Count) return;
            if (!float.IsFinite(localPoint.sqrMagnitude)) return;
            Vector3 point = ship.transform.TransformPoint(localPoint);
            if (Vector3.Distance(player.transform.position, point) > 5f || !cannon.Crate.Cannons[index].CanLoadFrom(point)) return;
            if (cannon.LoadInventoryBall(player, index, GetComponent<NetworkFish>().CurrentItem)) GetComponent<NetworkFish>().Take();
        }
        void FixedUpdate()
        {
            if (!IsServerInitialized) return;
            if (IsHeld)
            {
                var player = SessionController.Instance.GetPlayer(holder.Value);
                if (player == null || player.Motor.IsDead || Time.time - lastHold > .5f) Release();
            }
            if (Time.time < nextSync) return;
            nextSync = Time.time + .05f;
            PublishPose();
        }
        void PublishPose()
        {
            var support = ball.PlatformBody;
            platform.Value = support != null ? support.GetComponent<NetworkObject>() : null;
            position.Value = platform.Value != null ? support.transform.InverseTransformPoint(ball.Body.position) : ball.Body.position;
            rotation.Value = platform.Value != null ? Quaternion.Inverse(support.rotation) * ball.Body.rotation : ball.Body.rotation;
        }
        void LateUpdate()
        {
            if (!IsSpawned || IsServerInitialized || localHolding) return;
            ball.Held = IsHeld; ball.GetComponent<Collider>().enabled = !IsHeld;
            if (platform.Value != null && !IsHeld)
            {
                transform.SetPositionAndRotation(platform.Value.transform.TransformPoint(position.Value), platform.Value.transform.rotation * rotation.Value);
                return;
            }
            transform.SetPositionAndRotation(Vector3.Lerp(transform.position, position.Value, 1f - Mathf.Exp(-25f * Time.deltaTime)),
                Quaternion.Slerp(transform.rotation, rotation.Value, 1f - Mathf.Exp(-25f * Time.deltaTime)));
        }
    }
}
