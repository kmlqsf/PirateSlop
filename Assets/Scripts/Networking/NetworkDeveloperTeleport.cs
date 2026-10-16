using System;
using System.Linq;
using FishNet.Connection;
using FishNet.Object;
using PirateSlop.World;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkWeapon
    {
        public void RequestDeveloperTeleportPlaces() => DeveloperTeleportPlacesServerRpc();
        public void DeveloperTeleport(string key) => DeveloperTeleportServerRpc(key);
        bool DeveloperTeleportAllowed => DeveloperMenu.Available && (IsOwner || DeveloperMenu.AllowRemote);

        [ServerRpc]
        void DeveloperTeleportPlacesServerRpc()
        {
            var places = DeveloperTeleportAllowed ? DeveloperTeleportDestinations.Collect(gameObject.scene) : Array.Empty<DeveloperTeleportDestination>();
            DeveloperTeleportPlacesTargetRpc(Owner, places);
            if (!DeveloperTeleportAllowed) DeveloperResultTargetRpc(Owner, "Нужно разрешение хоста.");
        }

        [TargetRpc]
        void DeveloperTeleportPlacesTargetRpc(NetworkConnection connection, DeveloperTeleportDestination[] places) => GetComponent<DeveloperMenu>()?.ReceiveTeleportPlaces(places);

        [ServerRpc]
        void DeveloperTeleportServerRpc(string key)
        {
            if (!DeveloperTeleportAllowed) { DeveloperResultTargetRpc(Owner, "Нужно разрешение хоста."); return; }
            if (Time.time < nextDeveloperCommand) return;
            nextDeveloperCommand = Time.time + .3f;
            var player = GetComponent<NetworkPlayer>();
            var deck = player.Passenger != null ? player.Passenger.Ship : null;
            var ship = deck != null ? deck.GetComponent<NetworkShip>() : player.Ship;
            if (ship == null || !ship.IsSpawned || ship.IsSinking || player.Motor.IsDead) { DeveloperResultTargetRpc(Owner, "Живой игрок или корабль не найден."); return; }
            var place = DeveloperTeleportDestinations.Collect(gameObject.scene).FirstOrDefault(p => p.Key == key);
            if (place.Key == null) { DeveloperResultTargetRpc(Owner, "Место больше недоступно. Обновите список."); return; }
            if (!DeveloperTeleportDestinations.TryFindArrival(place, ship, out var position, out var yaw)) { DeveloperResultTargetRpc(Owner, "Рядом с местом нет свободной воды для корабля."); return; }
            ship.DeveloperTeleport(position, yaw, player);
            DeveloperResultTargetRpc(Owner, "Корабль, игроки и груз перенесены: " + place.Name);
        }
    }

    public sealed partial class NetworkShip
    {
        uint developerTeleportRevision;

        public void DeveloperTeleport(Vector3 position, float yaw, NetworkPlayer requester)
        {
            if (!IsServerInitialized) return;
            var passengers = NetworkPlayer.Active.Where(p => p != null && p.IsSpawned && p.gameObject.scene == gameObject.scene && (p == requester || p.Passenger.Support == Body)).ToArray();
            var passengerPositions = passengers.Select(p => p.transform.position).ToArray();
            var localPositions = passengers.Select(p => p == requester && p.Passenger.Support != Body
                ? transform.InverseTransformPoint(GetComponent<PirateSlop.Ships.ShipV3Features>()?.RespawnPoint != null ? GetComponent<PirateSlop.Ships.ShipV3Features>().RespawnPoint.position : transform.TransformPoint(SessionController.Instance.Config.PlayerLocalSpawn))
                : transform.InverseTransformPoint(p.transform.position)).ToArray();
            var states = passengers.Select(p => p.Motor.Capture()).ToArray();
            var revisions = passengers.Select(p => p.DeveloperTeleportRevision + 1).ToArray();
            var players = passengers.Select(p => p.NetworkObject).ToArray();
            var oldPosition = transform.position;
            var oldRotation = transform.rotation;
            var localAnchor = transform.InverseTransformPoint(Motor.AnchorPoint);
            float yawDelta = Mathf.DeltaAngle(transform.eulerAngles.y, yaw);
            foreach (var player in passengers)
            {
                foreach (var candidate in ActiveShips)
                {
                    if (candidate.Helm.IsControlledBy(player.Motor)) candidate.Helm.ReleaseControl();
                    candidate.GetComponent<SailSystem>()?.ReleasePlayer(player.Motor);
                }
                player.Motor.ActiveCannon?.ReleaseControl();
                var weapon = player.GetComponent<NetworkWeapon>();
                weapon.CancelLootWork();
                var pin = weapon.WhalePin;
                if (pin != null) pin.Owner.InputHarpoon(weapon, pin.Index, false, 0, 0);
                player.GetComponent<NetworkFishing>()?.CancelBotFishing();
            }
            Helm.ReleaseControl();
            var state = Motor.Capture();
            state.Position = position + Vector3.up * (oldPosition.y - ProceduralWorld.Instance.Layout.SeaLevel);
            state.Yaw = yaw; state.Speed = state.Rudder = 0; state.Controlling = false;
            var rotation = Quaternion.Euler(state.Pitch, state.Yaw, state.Bank + state.WaveRoll);
            var matrix = Matrix4x4.TRS(state.Position, rotation, transform.lossyScale);
            for (int i = 0; i < states.Length; i++)
            {
                states[i].Position = matrix.MultiplyPoint3x4(localPositions[i]); states[i].Yaw = Mathf.Repeat(states[i].Yaw + yawDelta, 360f);
                states[i].Locked = states[i].Swimming = states[i].Climbing = states[i].LadderExiting = false;
                states[i].VerticalVelocity = states[i].PlanarSpeed = states[i].SlideTimer = states[i].KnockbackTime = 0;
                states[i].SwimVelocity = states[i].KnockbackVelocity = states[i].KickPushVelocity = Vector3.zero;
            }
            var held = passengers.Select(p => p.GetComponent<CannonHands>()?.HeldBall).Where(b => b != null).ToArray();
            var heldPositions = held.Select(b => b.transform.position).ToArray();
            var heldRotations = held.Select(b => b.transform.rotation).ToArray();
            float[] tensions = null; int[] owners = null;
            sails.CaptureRopes(ref tensions, ref owners);
            DeveloperTeleportObserversRpc(state, matrix.MultiplyPoint3x4(localAnchor), players, states, revisions, tensions, owners, ++developerTeleportRevision);
            for (int i = 0; i < held.Length; i++)
            {
                var owner = Array.FindIndex(passengers, p => p.GetComponent<CannonHands>()?.HeldBall == held[i]);
                var delta = rotation * Quaternion.Inverse(oldRotation);
                var point = states[owner].Position + delta * (heldPositions[i] - passengerPositions[owner]);
                held[i].Body.position = point; held[i].Body.rotation = delta * heldRotations[i];
            }
            if (AnchorDropped) anchorSeabedPoint.Value = Motor.AnchorPoint;
            Physics.SyncTransforms();
        }

        [ObserversRpc(RunLocally = true)]
        void DeveloperTeleportObserversRpc(ShipState state, Vector3 anchorPoint, NetworkObject[] players, PlayerState[] playerStates, uint[] playerRevisions, float[] tensions, int[] owners, uint revision)
        {
            if (revision < developerTeleportRevision) return;
            developerTeleportRevision = revision;
            Motor.Restore(state, null, anchorPoint); Motor.Freeze(0);
            if (!Body.isKinematic) { Body.linearVelocity = Vector3.zero; Body.angularVelocity = Vector3.zero; }
            sails.ApplyRopes(tensions, owners);
            remoteState = state; hasRemoteState = true; remoteDriver = 0;
            for (int i = 0; i < players.Length; i++) players[i]?.GetComponent<NetworkPlayer>()?.ApplyDeveloperTeleport(playerStates[i], NetworkObject, playerRevisions[i]);
            foreach (var item in FindObjectsByType<NetworkFish>(FindObjectsSortMode.None)) item.RefreshDeveloperShipPose(this);
            foreach (var chest in FindObjectsByType<NetworkLootChest>(FindObjectsSortMode.None))
                if (chest.SupportingShip == this || chest.Carrier != null && players.Contains(chest.Carrier)) chest.RefreshDeveloperShipPose();
            Physics.SyncTransforms();
        }
    }

    public sealed partial class NetworkPlayer
    {
        public uint DeveloperTeleportRevision { get; private set; }
        internal bool TryDeveloperBoardShip(NetworkShip ship, out string error)
        {
            error = "Корабль недоступен. Сначала создайте мишень или вернитесь к живому кораблю.";
            if (!DeveloperMenu.Available || !IsServerInitialized || !IsSpawned || motor == null || motor.IsDead ||
                ship == null || !ship.IsSpawned || ship.IsSinking || ship.gameObject.scene != gameObject.scene) return false;
            if (!TryDeveloperDeckPoint(ship, out var point)) { error = "На корабле не найден свободный участок палубы."; return false; }
            var state = motor.Capture();
            state.Position = point; state.Yaw = ship.transform.eulerAngles.y;
            state.Locked = state.Grounded = state.Swimming = state.Climbing = state.LadderExiting = false;
            state.VerticalVelocity = state.PlanarSpeed = state.SlideTimer = state.KnockbackTime = state.JumpBuffer = state.GroundGrace = 0f;
            state.SwimVelocity = state.KnockbackVelocity = state.KickPushVelocity = Vector3.zero;
            var weapon = GetComponent<NetworkWeapon>();
            var pin = weapon != null ? weapon.WhalePin : null;
            if (pin != null) pin.Owner.InputHarpoon(weapon, pin.Index, false, 0, 0);
            GetComponent<NetworkFishing>()?.CancelBotFishing();
            DeveloperBoardShipObserversRpc(state, ship.NetworkObject, ship.transform.InverseTransformPoint(point), DeveloperTeleportRevision + 1);
            foreach (var chest in NetworkLootChest.ServerChests)
                if (chest != null && chest.Carrier == NetworkObject) chest.RefreshDeveloperShipPose();
            error = null;
            return true;
        }

        bool TryDeveloperDeckPoint(NetworkShip ship, out Vector3 point)
        {
            point = default;
            var controller = GetComponent<CharacterController>();
            var features = ship.GetComponent<PirateSlop.Ships.ShipV3Features>();
            var seed = features != null && features.RespawnPoint != null ? ship.transform.InverseTransformPoint(features.RespawnPoint.position)
                : SessionController.Instance.Config.PlayerLocalSpawn;
            float radius = controller.radius;
            Physics.SyncTransforms();
            for (int i = 0; i < 25; i++)
            {
                var offset = i == 0 ? Vector3.zero : Quaternion.Euler(0f, (i - 1) % 8 * 45f, 0f) * Vector3.forward * (((i - 1) / 8 + 1) * 2f);
                var origin = ship.transform.TransformPoint(seed + offset) + Vector3.up * 2f;
                RaycastHit floor = default;
                float distance = 10f;
                foreach (var hit in Physics.RaycastAll(origin, Vector3.down, distance, ~0, QueryTriggerInteraction.Ignore))
                    if (hit.rigidbody == ship.Body && hit.normal.y > .55f && hit.distance < distance)
                    { floor = hit; distance = hit.distance; }
                if (floor.collider == null) continue;
                var candidate = floor.point + Vector3.up * .08f;
                bool clear = true;
                foreach (var obstacle in Physics.OverlapCapsule(candidate + Vector3.up * radius, candidate + Vector3.up * (controller.height - radius), radius, ~0, QueryTriggerInteraction.Ignore))
                    if (!obstacle.transform.IsChildOf(transform)) { clear = false; break; }
                if (!clear) continue;
                point = candidate;
                return true;
            }
            return false;
        }

        [ObserversRpc(RunLocally = true)]
        void DeveloperBoardShipObserversRpc(PlayerState state, NetworkObject platform, Vector3 localPosition, uint revision)
        {
            if (platform == null || revision < DeveloperTeleportRevision) return;
            var hands = GetComponent<CannonHands>();
            var held = hands != null ? hands.HeldBall : null;
            var previousPosition = transform.position;
            float previousYaw = transform.eulerAngles.y;
            motor.BeginFreeze();
            motor.CargoRopeWalking = false;
            state.Position = platform.transform.TransformPoint(localPosition);
            ApplyDeveloperTeleport(state, platform, revision);
            if (held != null)
            {
                var delta = Quaternion.Euler(0f, Mathf.DeltaAngle(previousYaw, state.Yaw), 0f);
                held.Body.position = state.Position + delta * (held.transform.position - previousPosition);
                held.Body.rotation = delta * held.transform.rotation;
            }
            Physics.SyncTransforms();
        }

        public void ApplyDeveloperTeleport(PlayerState state, NetworkObject platform, uint revision)
        {
            if (revision < DeveloperTeleportRevision) return;
            DeveloperTeleportRevision = revision;
            if (IsOwner) ActiveShip?.Capstan?.CancelPush();
            motor.ShipActivityLocked = motor.SailPullLocked = motor.BellPullLocked = false;
            Teleport(state);
            if (IsOwner) motor.SetLookAngles(state.Yaw, motor.AimEuler.x);
            passenger.Attach(platform != null ? platform.GetComponent<Rigidbody>() : null);
            CaptureVisualAnchor();
        }
    }
}
