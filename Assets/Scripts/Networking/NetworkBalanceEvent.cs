using System.Collections.Generic;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using PirateSlop.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PirateSlop.Networking
{
    public struct BalanceStoneState
    {
        public NetworkObject Carrier;
        public Vector3 Point, ReleasePoint;
        public double Started;
        public byte Phase;
        public int Slot;
    }

    [DefaultExecutionOrder(45)]
    public sealed class NetworkBalanceEvent : NetworkBehaviour
    {
        public static readonly List<NetworkBalanceEvent> ServerEvents = new();
        public LootCatalog Catalog;
        public Transform Beam, RewardHanger, WeightHanger, ChestSocket, WeightSocket;
        public BalanceEventTarget[] Stones;
        public BalanceEventTarget DepositTarget;
        public float InitialAngle = 27.3f, FinalAngle = -9f, TiltSeconds = 1.6f;
        public const float PickupSeconds = .48f, DepositSeconds = 1.1f, ReleaseFraction = .65f;
        readonly SyncList<BalanceStoneState> stones = new();
        readonly SyncVar<Vector3> origin = new();
        readonly SyncVar<int> deposited = new();
        readonly SyncVar<float> tiltFrom = new();
        readonly SyncVar<double> tiltAt = new(-1);
        readonly Vector3[] velocities = new Vector3[5];
        readonly Vector3[] homes = new Vector3[5];
        readonly Quaternion[] stoneRotations = new Quaternion[5];
        readonly bool[] falling = new bool[5];
        readonly float[] fallenTime = new float[5];
        NetworkLootChest chest;
        bool spawnChest;
        int depositing = -1;
        bool depositCommitted;
        public double Clock => TimeManager != null && IsSpawned ? TimeManager.TicksToTime(TimeManager.Tick) : Time.timeAsDouble;
        public int Deposited => deposited.Value;
        public bool ChestAvailable => Deposited == 5 && Clock >= tiltAt.Value + TiltSeconds;
        public BalanceStoneState Stone(int index) => index >= 0 && index < stones.Count ? stones[index] : default;
        public float LoadedAngle(int count) => count>=5 ? FinalAngle : Mathf.Lerp(InitialAngle,6.5f,Mathf.Clamp(count,0,4)/4f);
        public float Angle => tiltAt.Value < 0 ? InitialAngle : Mathf.Lerp(tiltFrom.Value, LoadedAngle(Deposited), Ease((float)(Clock - tiltAt.Value) / TiltSeconds));
        static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * t * (t * (6 * t - 15) + 10); }

        void Awake()
        {
            for (int i = 0; i < Stones.Length; i++) { homes[i] = Stones[i].transform.localPosition; stoneRotations[i] = Stones[i].transform.localRotation; }
        }

        public static void SpawnEvent(LootCatalog catalog, NetworkManager manager, Vector3 point, Scene scene)
        {
            if (catalog.BalanceEventPrefab == null) return;
            var balance = Instantiate(catalog.BalanceEventPrefab, point, Quaternion.identity);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(balance.gameObject, scene);
            balance.Catalog = catalog;
            manager.ServerManager.Spawn(balance.NetworkObject);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            origin.Value = transform.position;
            deposited.Value = 0; tiltAt.Value = -1; depositing = -1;
            stones.Clear();
            for (int i = 0; i < Stones.Length; i++) stones.Add(new BalanceStoneState { Point = transform.TransformPoint(homes[i]), Started = -1 });
            ServerEvents.RemoveAll(item => item == null || item == this);
            ServerEvents.Add(this); spawnChest = true;
        }

        bool Standing(NetworkWeapon actor)
        {
            if (actor == null || !actor.IsSpawned || actor.gameObject.scene != gameObject.scene) return false;
            var m = actor.GetComponent<AdvancedPlayerController>();
            return m != null && !m.IsDead && !m.IsDowned && !m.IsFrozen && !m.IsKnockedBack && !m.IsSwimming && !m.IsClimbing && !m.IsCrouched && !m.IsSliding && m.IsGrounded && !m.LocomotionLocked;
        }

        public bool CanReach(NetworkWeapon actor, Vector3 point, Transform target)
        {
            if (actor == null || Vector3.Distance(actor.transform.position + Vector3.up * 1.2f, point) >= 2.8f) return false;
            Vector3 from=actor.transform.position+Vector3.up*1.5f,delta=point-from;
            foreach(var hit in Physics.RaycastAll(from,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore))
                if(!hit.collider.transform.IsChildOf(actor.transform) && !hit.collider.transform.IsChildOf(target)) return false;
            return true;
        }

        public bool CanTake(NetworkWeapon actor, int index)
        {
            if (!IsSpawned || !Standing(actor) || index < 0 || index >= stones.Count || Stone(index).Phase != 0 || actor.LootHandsBusy || actor.GetComponent<CannonHands>().HasHeldBall) return false;
            var fishing = actor.GetComponent<PlayerInventory>().Fishing;
            return (fishing == null || (!fishing.HasFish && !fishing.IsFishing && !fishing.IsEating)) && CanReach(actor, Stones[index].transform.position, Stones[index].transform);
        }

        public void Take(NetworkWeapon actor, int index)
        {
            if (!IsServerInitialized || !CanTake(actor, index)) return;
            var state = stones[index]; state.Carrier = actor.NetworkObject; state.Phase = 1; state.Started = Clock;
            state.Point = Stones[index].transform.position; stones[index] = state; falling[index] = false;
            actor.SetBalanceStone(this, index);
        }

        public bool CanDeposit(NetworkWeapon actor)
        {
            return DepositBlockReason(actor) == null;
        }

        public string DepositBlockReason(NetworkWeapon actor)
        {
            int index = actor != null ? actor.BalanceStoneIndex : -1;
            if (!IsSpawned) return "Весы недоступны";
            if (Deposited >= 5) return "Все пять камней уже уложены";
            if (depositing >= 0 || tiltAt.Value >= 0 && Clock < tiltAt.Value + TiltSeconds) return "Дождитесь остановки весов";
            if (!Standing(actor)) return "Для укладки нужно стоять на острове";
            if (actor.BalanceEvent != this || index < 0 || index >= stones.Count || Stone(index).Carrier != actor.NetworkObject || Stone(index).Phase != 1) return "Сначала подберите камень";
            if (Clock < Stone(index).Started + PickupSeconds) return "Поднимаем камень…";
            if (Vector3.Distance(actor.transform.position + Vector3.up * 1.2f, WeightSocket.position) >= 2.8f) return "Подойдите ближе к чаше";
            if (!CanReach(actor, WeightSocket.position, WeightHanger)) return "Путь к чаше перекрыт";
            return null;
        }

        public void Deposit(NetworkWeapon actor)
        {
            if (!IsServerInitialized || !CanDeposit(actor)) return;
            depositing = actor.BalanceStoneIndex;
            depositCommitted = false;
            var state = stones[depositing]; state.Phase = 2; state.Point = CarryPoint(actor); state.Started = Clock; state.Slot = Deposited;
            var entry=WeightSocket.InverseTransformPoint(actor.transform.position);
            entry.x=Mathf.Clamp(entry.x,-.57f,.57f);entry.z=Mathf.Clamp(entry.z,-.43f,.43f);entry.y=.11f;
            state.ReleasePoint=WeightSocket.TransformPoint(entry);
            stones[depositing] = state;
        }

        public void Drop(NetworkWeapon actor)
        {
            if (!IsServerInitialized || actor == null || actor.BalanceEvent != this) return;
            int index = actor.BalanceStoneIndex;
            if (index < 0 || index >= stones.Count || Stone(index).Carrier != actor.NetworkObject || Stone(index).Phase == 2 && Clock >= Stone(index).Started+DepositSeconds*ReleaseFraction) return;
            Release(index, actor);
        }

        void Release(int index, NetworkWeapon actor)
        {
            var state = stones[index];
            state.Point = Stones[index].transform.position;
            state.Carrier = null; state.Phase = 0; state.Started = Clock; stones[index] = state;
            velocities[index] = actor != null ? actor.transform.forward * 1.8f + Vector3.up * .4f : Vector3.zero;
            falling[index] = true; fallenTime[index] = 0;
            if (actor != null) actor.SetBalanceStone(null, -1);
            if (depositing == index) depositing = -1;
        }

        static Vector3 CarryPoint(NetworkWeapon actor)
        {
            var m = actor.GetComponent<AdvancedPlayerController>();
            if (m.IsLocal && !m.IsThirdPerson && m.PlayerCamera != null)
                return m.PlayerCamera.transform.position + actor.transform.forward * .66f + Vector3.down * .32f;
            return actor.transform.TransformPoint(new Vector3(0, 1.3f, .66f));
        }

        Vector3 SlotPoint(int slot)
        {
            Vector3[] offsets = { new(-.52f,.11f,-.24f), new(0,.11f,-.24f), new(.52f,.11f,-.24f), new(-.26f,.11f,.24f), new(.26f,.11f,.24f) };
            return WeightSocket.TransformPoint(offsets[Mathf.Clamp(slot,0,4)]);
        }

        void Update()
        {
            if (!IsSpawned || !IsServerInitialized) return;
            if (spawnChest)
            {
                spawnChest = false;
                chest = Instantiate(Catalog.ChestPrefab, ChestSocket.position, ChestSocket.rotation);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(chest.gameObject, gameObject.scene); chest.Catalog = Catalog;
                var random = new MapRandom(unchecked((uint)NetworkObject.ObjectId * 2654435761u ^ 0xb41a7ceu));
                chest.Fill(ref random); chest.PlaceOnBalance(this); ServerManager.Spawn(chest.NetworkObject);
            }
            for (int i = 0; i < stones.Count; i++)
            {
                var state = stones[i];
                if (state.Carrier != null)
                {
                    var actor = state.Carrier.GetComponent<NetworkWeapon>();
                    var motor = actor.GetComponent<AdvancedPlayerController>();
                    bool interrupted = !state.Carrier.IsSpawned || motor.IsDead || motor.IsDowned || motor.IsSwimming || motor.IsKnockedBack;
                    if (interrupted && !(state.Phase == 2 && depositCommitted))
                    { Release(i, actor); continue; }
                    if (state.Phase == 2 && !depositCommitted && Clock >= state.Started + DepositSeconds * ReleaseFraction)
                    {
                        tiltFrom.Value = Angle; tiltAt.Value = Clock; deposited.Value++;
                        depositCommitted = true;
                        actor.NotifyBalanceStone($"Камень уложен · Весы: {Deposited}/5");
                    }
                    if (state.Phase == 2 && (interrupted || Clock >= state.Started + DepositSeconds))
                    {
                        state.Phase = 3; state.Carrier = null; stones[i] = state;
                        actor.SetBalanceStone(null, -1); depositing = -1;
                    }
                }
                else if (state.Phase == 2 && depositCommitted && depositing == i)
                {
                    state.Phase = 3; state.Carrier = null; stones[i] = state; depositing = -1;
                }
                else if (state.Phase == 1 || state.Phase == 2) Release(i, null);
                if (falling[i]) Fall(i);
            }
        }

        void Fall(int i)
        {
            var state = stones[i]; fallenTime[i] += Time.deltaTime;
            velocities[i] += Vector3.down * (18 * Time.deltaTime);
            var delta = velocities[i] * Time.deltaTime;
            float nearest = delta.magnitude + .02f; RaycastHit ground = default;
            foreach (var hit in Physics.SphereCastAll(state.Point, .23f, delta.normalized, nearest, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(Stones[i].transform) || hit.collider.GetComponentInParent<AdvancedPlayerController>() != null || hit.normal.y < .55f || hit.distance >= nearest) continue;
                nearest = hit.distance; ground = hit;
            }
            state.Point = ground.collider != null ? state.Point + delta.normalized * Mathf.Max(0,nearest-.01f) : state.Point + delta;
            if (ground.collider != null) falling[i] = false;
            if (state.Point.y < origin.Value.y - 2 || fallenTime[i] > 4) { state.Point = transform.TransformPoint(homes[i]); falling[i] = false; }
            stones[i] = state;
        }

        void LateUpdate()
        {
            if (!IsSpawned) return;
            transform.position = origin.Value;
            ApplyPose(Angle);
            for (int i = 0; i < stones.Count; i++)
            {
                var state = stones[i]; var target = Stones[i].transform;
                Vector3 point = state.Point; Quaternion rotation = transform.rotation * stoneRotations[i];
                if (state.Carrier != null && (state.Phase == 1 || state.Phase == 2))
                {
                    var actor = state.Carrier.GetComponent<NetworkWeapon>();
                    float t = Ease((float)(Clock - state.Started) / (state.Phase == 1 ? PickupSeconds : DepositSeconds));
                    if(state.Phase==1) point=Vector3.Lerp(state.Point,CarryPoint(actor),t)+Vector3.up*(.12f*Mathf.Sin(t*Mathf.PI));
                    else
                    {
                        float age=(float)(Clock-state.Started)/DepositSeconds;
                        float place=Ease(age/ReleaseFraction),settle=Ease((age-ReleaseFraction)/(1-ReleaseFraction));
                        point=age<ReleaseFraction ? Vector3.Lerp(state.Point,state.ReleasePoint,place)+Vector3.up*(.1f*Mathf.Sin(place*Mathf.PI)) : Vector3.Lerp(state.ReleasePoint,SlotPoint(state.Slot),settle);
                    }
                    if (state.Phase == 1 && t >= 1) point += Vector3.up * (Mathf.Sin((float)Clock * 7.5f) * Mathf.Min(.018f,actor.GetComponent<AdvancedPlayerController>().PlanarSpeed*.004f));
                    rotation = actor.transform.rotation * stoneRotations[i];
                }
                else if (state.Phase == 3) point = SlotPoint(state.Slot);
                target.SetPositionAndRotation(point, rotation);
                foreach (var c in target.GetComponentsInChildren<Collider>()) c.enabled = state.Phase == 0;
            }
        }

        public void ApplyPose(float angle)
        {
            Beam.localRotation = Quaternion.Euler(0,0,angle);
            RewardHanger.localRotation = WeightHanger.localRotation = Quaternion.Euler(0,0,-angle);
        }

        public Transform CarriedVisual(NetworkWeapon actor)
        {
            int i = actor.BalanceStoneIndex;
            return i >= 0 && i < Stones.Length && Stone(i).Carrier == actor.NetworkObject ? Stones[i].transform : null;
        }

        public override void OnStopServer()
        {
            ServerEvents.Remove(this);
            for (int i = 0; i < stones.Count; i++) if (Stone(i).Carrier != null) Stone(i).Carrier.GetComponent<NetworkWeapon>()?.SetBalanceStone(null,-1);
            if (chest != null && chest.IsSpawned && chest.OnBalance) ServerManager.Despawn(chest.NetworkObject);
            base.OnStopServer();
        }
    }
}
