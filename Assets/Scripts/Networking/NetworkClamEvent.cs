using System.Collections.Generic;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using PirateSlop.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PirateSlop.Networking
{
    public struct ClamLeverStroke
    {
        public NetworkObject Actor;
        public double PressedAt, PairedAt;
        public bool Pulled, Leading;
    }

    public struct ClamLeverHold
    {
        public NetworkObject Actor;
        public float Pull;
    }

    public sealed class NetworkClamEvent : NetworkBehaviour
    {
        public static readonly List<NetworkClamEvent> ServerEvents = new();
        public LootCatalog Catalog;
        public Transform ChestSocket;
        public ClamLeverTarget[] Levers;
        public Transform ShellPivot;
        public SkinnedMeshRenderer ShellSkin;
        public MeshCollider ShellCollider;
        [Min(1)] public int RequiredPairs = 5;
        [Range(.3f, 1.5f)] public float PairWindow = .8f;
        [Min(1)] public float LeverCooldown = 5f;
        public float OpenAngle = 60f, OpeningSeconds = 1.1f, FailedLiftHeight = .08f;
        public float PlatformRadius = 11f;
        readonly SyncList<ClamLeverStroke> strokes = new();
        readonly SyncList<ClamLeverHold> holds = new();
        readonly float[] heartbeat = new float[2];
        readonly SyncVar<int> pairs = new();
        readonly SyncVar<double> openingAt = new(-1);
        readonly SyncVar<float> openingFrom = new();
        readonly SyncVar<Vector3> origin = new();
        readonly SyncVar<Quaternion> heading = new(Quaternion.identity);
        NetworkLootChest chest;
        bool spawnChest;
        int waitingSide = -1;
        int attemptSide = -1;
        Mesh collisionMesh, collisionSource;
        Vector3[] collisionRest, collisionVertices, leftLift, rightLift;
        float previousLeft = -1, previousRight = -1;
        public double Clock => Application.isPlaying && IsSpawned && TimeManager != null ? TimeManager.TicksToTime(TimeManager.Tick) : Time.timeAsDouble;
        public int SuccessfulPairs => pairs.Value;
        public bool ChestAvailable => SuccessfulPairs >= RequiredPairs && Clock >= openingAt.Value + OpeningSeconds;
        public ClamLeverStroke Stroke(int side) => side >= 0 && side < strokes.Count ? strokes[side] : new ClamLeverStroke { PressedAt = -1, PairedAt = -1 };
        public ClamLeverHold Hold(int side) => side >= 0 && side < holds.Count ? holds[side] : default;
        public float HandlePull(int side)
        {
            var stroke = Stroke(side);
            float age = (float)(Clock - stroke.PressedAt);
            if (stroke.PressedAt >= 0 && age < LeverCooldown)
                return stroke.Pulled ? 1 - Mathf.SmoothStep(0, 1, age / LeverCooldown)
                    : age < .28f ? Mathf.SmoothStep(0, 1, age / .28f) : 1 - Mathf.SmoothStep(0, 1, (age - .28f) / (LeverCooldown - .28f));
            return Hold(side).Pull;
        }
        public float Cooldown(int side) => Stroke(side).PressedAt < 0 ? 0 : Mathf.Max(0, LeverCooldown - (float)(Clock - Stroke(side).PressedAt));
        public float Opening => openingAt.Value < 0 ? 0 : Mathf.Lerp(openingFrom.Value, OpenAngle * pairs.Value / RequiredPairs, Mathf.SmoothStep(0, 1, (float)(Clock - openingAt.Value) / OpeningSeconds));

        public static NetworkClamEvent SpawnEvent(LootCatalog catalog, NetworkManager manager, Vector3 point, Scene scene)
        {
            if (catalog == null || catalog.ClamEventPrefab == null) return null;
            var clam = Instantiate(catalog.ClamEventPrefab, point, Quaternion.identity);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(clam.gameObject, scene);
            clam.Catalog = catalog;
            manager.ServerManager.Spawn(clam.NetworkObject);
            return clam;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            origin.Value = transform.position; heading.Value = transform.rotation;
            strokes.Clear(); holds.Clear(); pairs.Value = 0; openingAt.Value = -1; waitingSide = attemptSide = -1;
            for (int i = 0; i < 2; i++) { strokes.Add(new ClamLeverStroke { PressedAt = -1, PairedAt = -1 }); holds.Add(default); }
            ServerEvents.Add(this); spawnChest = true;
        }

        void SpawnChest()
        {
            spawnChest = false;
            chest = Instantiate(Catalog.ChestPrefab, ChestSocket.position, ChestSocket.rotation);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(chest.gameObject, gameObject.scene);
            chest.Catalog = Catalog;
            var random = new MapRandom(unchecked((uint)NetworkObject.ObjectId * 2654435761u ^ 0xc1a057e1u));
            chest.Fill(ref random); chest.PlaceInClam(this); ServerManager.Spawn(chest.NetworkObject);
        }

        public string BlockReason(NetworkWeapon actor, int side)
        {
            if (!IsSpawned || actor == null || !actor.IsSpawned || side < 0 || side >= Levers.Length) return "Механизм недоступен";
            if (pairs.Value >= RequiredPairs) return "Раковина уже открыта";
            if (Cooldown(side) > 0) return $"Рычаг возвращается: {Cooldown(side):F1} с";
            if (Hold(side).Actor != null && Hold(side).Actor != actor.NetworkObject) return "Ручку держит другой игрок";
            var motor = actor.GetComponent<AdvancedPlayerController>();
            if (motor == null || motor.IsDead || motor.IsDowned || motor.IsFrozen || motor.IsKnockedBack || motor.IsClimbing) return "Сейчас нельзя воспользоваться рычагом";
            if (motor.IsSwimming) return "Сначала поднимитесь на настил: E";
            var passenger = actor.GetComponent<ShipDeckPassenger>();
            if (!motor.IsGrounded && (passenger == null || !passenger.HasDryDeckSupport())) return "Встаньте на деревянную площадку";
            bool ownGrip = Hold(side).Actor == actor.NetworkObject || actor.GetComponent<ClamPlayerInteraction>()?.IsHolding == true;
            if (motor.LocomotionLocked && !(ownGrip && motor.ShipActivityLocked)) return "Завершите другое действие";
            if (actor.LootHandsBusy || actor.GetComponent<CannonHands>()?.HasHeldBall == true) return "Освободите руки";
            var fishing = actor.GetComponent<PlayerInventory>().Fishing;
            if (fishing != null && (fishing.CarryingCatch || fishing.IsFishing || fishing.IsEating)) return "Освободите руки";
            if (actor.gameObject.scene != gameObject.scene || !Levers[side].CanReach(actor)) return "Подойдите к ручке механизма";
            if (waitingSide == 1 - side && Clock - Stroke(waitingSide).PressedAt <= PairWindow && Stroke(waitingSide).Actor == actor.NetworkObject) return "Второй рычаг должен нажать напарник";
            return null;
        }

        public bool InputLever(NetworkWeapon actor, int side, bool held, float delta, bool begin, out string reason)
        {
            reason = null;
            if (!IsServerInitialized || side < 0 || side >= holds.Count || actor == null) { reason = "Механизм недоступен"; return false; }
            var state = holds[side];
            if (!held)
            {
                if (state.Actor == actor.NetworkObject) { state.Actor = null; holds[side] = state; }
                return true;
            }
            reason = BlockReason(actor, side);
            if (reason != null) return false;
            if (begin)
            {
                if (Hold(1 - side).Actor == actor.NetworkObject) { reason = "Отпустите другую ручку"; return false; }
                state.Actor = actor.NetworkObject;
            }
            else if (state.Actor != actor.NetworkObject) { reason = "Сначала возьмитесь за ручку"; return false; }
            float allowed = begin ? 0 : Mathf.Clamp(Time.time - heartbeat[side], 0, .15f) * 4f;
            heartbeat[side] = Time.time;
            state.Pull = Mathf.MoveTowards(state.Pull, Mathf.Clamp01(delta), allowed); holds[side] = state;
            if (state.Pull >= .999f) Press(actor, side, true);
            return true;
        }

        public void Press(NetworkWeapon actor, int side, bool pulled = false)
        {
            if (IsServerInitialized && actor != null && actor.GetComponent<AdvancedPlayerController>().IsSwimming) { Board(actor, side); return; }
            if (!IsServerInitialized || BlockReason(actor, side) != null) return;
            if (waitingSide >= 0 && (Clock - Stroke(waitingSide).PressedAt > PairWindow || Stroke(waitingSide).Actor == null || !Stroke(waitingSide).Actor.IsSpawned)) waitingSide = -1;
            var other = Stroke(1 - side);
            if (waitingSide == 1 - side && other.Actor == actor.NetworkObject) return;
            bool leading = attemptSide < 0 || Clock - Stroke(attemptSide).PressedAt >= LeverCooldown;
            if (leading) attemptSide = side;
            strokes[side] = new ClamLeverStroke { Actor = actor.NetworkObject, PressedAt = Clock, PairedAt = -1, Pulled = pulled, Leading = leading };
            holds[side] = default;
            if (waitingSide == 1 - side && Clock - other.PressedAt <= PairWindow)
            {
                openingFrom.Value = Opening; openingAt.Value = Clock; pairs.Value++;
                other.PairedAt = Clock; strokes[1 - side] = other;
                var current = strokes[side]; current.PairedAt = Clock; strokes[side] = current;
                waitingSide = attemptSide = -1;
            }
            else waitingSide = leading ? side : -1;
        }

        void Board(NetworkWeapon actor, int side)
        {
            if (!IsSpawned || !actor.IsSpawned || side < 0 || side >= Levers.Length || actor.gameObject.scene != gameObject.scene || !Levers[side].CanReach(actor) || actor.LootHandsBusy) return;
            var motor = actor.GetComponent<AdvancedPlayerController>();
            if (motor.IsDead || motor.IsDowned || motor.IsFrozen || motor.IsKnockedBack || motor.IsClimbing || motor.LocomotionLocked || actor.GetComponent<CannonHands>().HasHeldBall) return;
            var point = Levers[side].BoardingPoint.position;
            foreach (var hit in Physics.OverlapCapsule(point + Vector3.up * .32f, point + Vector3.up * 1.5f, .3f, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(actor.transform)) return;
            actor.GetComponent<NetworkPlayer>().Teleport(new PlayerState { Position = point, Yaw = actor.transform.eulerAngles.y });
            actor.GetComponent<ShipDeckPassenger>().Attach(GetComponent<Rigidbody>());
        }

        public float LipHeight(int side)
        {
            var stroke = Stroke(side);
            if (stroke.PressedAt < 0 || !stroke.Leading) return 0;
            float age = (float)(Clock - stroke.PressedAt);
            float lift = FailedLiftHeight * Mathf.SmoothStep(0, 1, age / .22f);
            return stroke.PairedAt >= 0 ? lift * (1 - Mathf.SmoothStep(0, 1, (float)(Clock - stroke.PairedAt) / .35f))
                : lift * (1 - Mathf.SmoothStep(0, 1, (age - PairWindow) / .25f));
        }

        void AnimateShell()
        {
            ShellPivot.localRotation = Quaternion.Euler(-Opening, 0, 0);
            float left = LipHeight(0), right = LipHeight(1);
            ShellSkin.SetBlendShapeWeight(0, left * 100);
            ShellSkin.SetBlendShapeWeight(1, right * 100);
            if (Mathf.Abs(left - previousLeft) < .00001f && Mathf.Abs(right - previousRight) < .00001f) return;
            if (collisionMesh == null)
            {
                collisionSource = ShellCollider.sharedMesh;
                collisionMesh = Instantiate(collisionSource); collisionMesh.MarkDynamic();
                collisionRest = collisionMesh.vertices; collisionVertices = new Vector3[collisionRest.Length];
                leftLift = new Vector3[collisionRest.Length]; rightLift = new Vector3[collisionRest.Length];
                ShellSkin.sharedMesh.GetBlendShapeFrameVertices(0, 0, leftLift, null, null);
                ShellSkin.sharedMesh.GetBlendShapeFrameVertices(1, 0, rightLift, null, null);
            }
            for (int i = 0; i < collisionVertices.Length; i++) collisionVertices[i] = collisionRest[i] + leftLift[i] * left + rightLift[i] * right;
            collisionMesh.vertices = collisionVertices; collisionMesh.RecalculateBounds();
            ShellCollider.sharedMesh = null; ShellCollider.sharedMesh = collisionMesh;
            previousLeft = left; previousRight = right;
        }

        void Update()
        {
            if (!IsSpawned) return;
            transform.SetPositionAndRotation(origin.Value, heading.Value);
            if (IsServerInitialized && spawnChest) SpawnChest();
            if (IsServerInitialized && waitingSide >= 0 && Clock - Stroke(waitingSide).PressedAt > PairWindow) waitingSide = -1;
            if (IsServerInitialized)
                for (int side = 0; side < holds.Count; side++)
                {
                    var state = holds[side]; var previous = state;
                    if (state.Actor != null && (!state.Actor.IsSpawned || Time.time - heartbeat[side] > .65f || BlockReason(state.Actor.GetComponent<NetworkWeapon>(), side) != null)) state.Actor = null;
                    if (state.Actor == null) state.Pull = Mathf.MoveTowards(state.Pull, 0, Time.deltaTime * 2f);
                    if (state.Actor != previous.Actor || state.Pull != previous.Pull) holds[side] = state;
                }
            AnimateShell();
            foreach (var lever in Levers) lever.Animate();
        }

        void ReleaseCollision()
        {
            if (ShellCollider != null && collisionSource != null) ShellCollider.sharedMesh = collisionSource;
            if (collisionMesh != null) Destroy(collisionMesh);
            collisionMesh = null; previousLeft = previousRight = -1;
        }
        public override void OnStopClient() { ReleaseCollision(); base.OnStopClient(); }

        public override void OnStopServer()
        {
            ServerEvents.Remove(this);
            ReleaseCollision();
            if (chest != null && chest.IsSpawned && chest.InClam) ServerManager.Despawn(chest.NetworkObject);
            base.OnStopServer();
        }
    }
}
