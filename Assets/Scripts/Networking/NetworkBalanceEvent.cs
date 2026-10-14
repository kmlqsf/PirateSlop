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
        public Transform BuriedSand;
        public ParticleSystem RevealDust;
        public float ShoreHeight = 3.3657f;
        public float InitialAngle = 13f, FinalAngle = -9f, TiltSeconds = 1.6f;
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
        readonly bool[] canLandInBowl = new bool[5];
        readonly float[] fallenTime = new float[5];
        NetworkLootChest chest;
        bool spawnChest;
        int depositing = -1;
        bool depositCommitted;
        AudioSource mechanismAudio;
        int heardDeposited;
        bool heardComplete;
        static readonly Vector3[] SlotOffsets = { new(-.52f,.25f,-.24f), new(0,.25f,-.24f), new(.52f,.25f,-.24f), new(-.26f,.25f,.24f), new(.26f,.25f,.24f) };
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
            return m != null && !m.IsDead && !m.IsDowned && !m.IsFrozen && !m.IsKnockedBack && !m.IsSwimming && !m.IsClimbing && !m.LocomotionLocked;
        }

        public bool CanReach(NetworkWeapon actor, Vector3 point, Transform target)
        {
            if (actor == null || Vector3.Distance(actor.transform.position + Vector3.up * 1.2f, point) >= 4.3f) return false;
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
            if (!IsServerInitialized) return;
            if (!CanTake(actor, index)) { actor?.NotifyBalanceStone("Камень недоступен: подойдите ближе и освободите руки."); return; }
            var state = stones[index]; state.Carrier = actor.NetworkObject; state.Phase = 1; state.Started = Clock;
            state.Point = Stones[index].transform.position; stones[index] = state; falling[index] = false;
            actor.SetBalanceStone(this, index);
            canLandInBowl[index] = false;
            StoneSoundRpc(SoundCue.BalancePickup, state.Point);
        }

        public bool CanDeposit(NetworkWeapon actor)
        {
            return DepositBlockReason(actor) == null;
        }

        public string DepositBlockReason(NetworkWeapon actor) => DepositBlockReason(actor, WeightSocket.position);
        public string DepositBlockReason(NetworkWeapon actor, Vector3 point)
        {
            int index = actor != null ? actor.BalanceStoneIndex : -1;
            if (!IsSpawned) return "Весы недоступны";
            if (Deposited >= 5) return "Все пять камней уже уложены";
            if (depositing >= 0 || tiltAt.Value >= 0 && Clock < tiltAt.Value + TiltSeconds) return "Дождитесь остановки весов";
            if (!Standing(actor)) return "Для укладки нужно стоять на острове";
            if (actor.BalanceEvent != this || index < 0 || index >= stones.Count || Stone(index).Carrier != actor.NetworkObject || Stone(index).Phase != 1) return "Сначала подберите камень";
            if (Clock < Stone(index).Started + PickupSeconds) return "Поднимаем камень…";
            if (!float.IsFinite(point.sqrMagnitude)) return "Наведите камень на верх чаши";
            var local = WeightSocket.InverseTransformPoint(point);
            if (Mathf.Abs(local.x) > .85f || Mathf.Abs(local.z) > .68f || local.y < -.08f || local.y > .4f) return "Наведите камень на верх чаши";
            if (Vector3.Distance(actor.transform.position + Vector3.up * 1.2f, point) >= 2.8f) return "Подойдите ближе к чаше";
            if (!CanReach(actor, point, WeightHanger)) return "Путь к чаше перекрыт";
            return null;
        }

        public void Deposit(NetworkWeapon actor, Vector3 point)
        {
            if (!IsServerInitialized || DepositBlockReason(actor, point) != null) return;
            depositing = actor.BalanceStoneIndex;
            depositCommitted = false;
            var state = stones[depositing]; state.Phase = 2; state.Point = CarryPoint(actor); state.Started = Clock; state.Slot = FreeSlot(point);
            var entry=WeightSocket.InverseTransformPoint(point);
            entry.x=Mathf.Clamp(entry.x,-.57f,.57f);entry.z=Mathf.Clamp(entry.z,-.43f,.43f);entry.y=.25f;
            state.ReleasePoint=WeightSocket.TransformPoint(entry);
            stones[depositing] = state;
        }

        public void MoveStone(NetworkWeapon actor, Vector3 point)
        {
            if (!IsServerInitialized || actor == null || actor.BalanceEvent != this || !Standing(actor) || !float.IsFinite(point.sqrMagnitude)) return;
            int index = actor.BalanceStoneIndex;
            if (index < 0 || index >= stones.Count) return;
            var state = stones[index];
            if (state.Carrier != actor.NetworkObject || state.Phase != 1) return;
            if (!CanReach(actor, point, Stones[index].transform)) return;
            state.Point = point; stones[index] = state;
        }

        public void Drop(NetworkWeapon actor, bool place = false)
        {
            if (!IsServerInitialized || actor == null || actor.BalanceEvent != this) return;
            int index = actor.BalanceStoneIndex;
            if (index < 0 || index >= stones.Count || Stone(index).Carrier != actor.NetworkObject || Stone(index).Phase == 2 && Clock >= Stone(index).Started+DepositSeconds*ReleaseFraction) return;
            canLandInBowl[index] = place;
            Release(index, actor);
        }

        void Release(int index, NetworkWeapon actor)
        {
            var state = stones[index];
            if (state.Phase != 1) state.Point = Stones[index].transform.position;
            state.Carrier = null; state.Phase = 0; state.Started = Clock; stones[index] = state;
            velocities[index] = actor != null && !canLandInBowl[index] ? actor.transform.forward * 1.8f + Vector3.up * .4f : Vector3.zero;
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
            return WeightSocket.TransformPoint(SlotOffsets[Mathf.Clamp(slot,0,4)]);
        }

        int FreeSlot(Vector3 point)
        {
            int best = 0; float nearest = float.PositiveInfinity;
            for (int slot = 0; slot < 5; slot++)
            {
                bool used = false;
                for (int i = 0; i < stones.Count; i++) if (stones[i].Phase >= 2 && stones[i].Slot == slot) { used = true; break; }
                float distance = (SlotPoint(slot) - point).sqrMagnitude;
                if (!used && distance < nearest) { best = slot; nearest = distance; }
            }
            return best;
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
                    if (actor == null)
                    {
                        if (state.Phase == 2 && depositCommitted)
                        { state.Phase = 3; state.Carrier = null; stones[i] = state; depositing = -1; }
                        else Release(i, null);
                        continue;
                    }
                    var motor = actor.GetComponent<AdvancedPlayerController>();
                    bool interrupted = !state.Carrier.IsSpawned || motor.IsDead || motor.IsDowned || motor.IsSwimming || motor.IsKnockedBack;
                    if (interrupted && !(state.Phase == 2 && depositCommitted))
                    { Release(i, actor); continue; }
                    if (state.Phase == 2 && !depositCommitted && Clock >= state.Started + DepositSeconds * ReleaseFraction)
                    {
                        tiltFrom.Value = Angle; tiltAt.Value = Clock; deposited.Value++;
                        depositCommitted = true;
                        StoneSoundRpc(SoundCue.BalanceDeposit, state.ReleasePoint);
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
            if (ground.collider != null && canLandInBowl[i] && Deposited < 5 && ground.collider.transform.IsChildOf(WeightHanger))
            {
                state.Slot = FreeSlot(ground.point); state.Phase = 3; state.Point = SlotPoint(state.Slot);
                falling[i] = canLandInBowl[i] = false;
                tiltFrom.Value = Angle; tiltAt.Value = Clock; deposited.Value++;
                StoneSoundRpc(SoundCue.BalanceDeposit, ground.point);
                stones[i] = state;
                return;
            }
            if (ground.collider != null) { falling[i] = false; StoneSoundRpc(SoundCue.BalanceDrop, state.Point); }
            if (state.Point.y < origin.Value.y - 2 || fallenTime[i] > 4 || ground.collider != null && state.Point.y < origin.Value.y + ShoreHeight - 1.2f)
            { state.Point = transform.TransformPoint(homes[i]); falling[i] = false; }
            stones[i] = state;
        }

        void LateUpdate()
        {
            if (!IsSpawned) return;
            transform.position = origin.Value;
            ApplyPose(Angle);
            if (BuriedSand != null)
            {
                float reveal = Deposited < 5 ? 0 : Ease((float)(Clock - tiltAt.Value) / TiltSeconds);
                BuriedSand.localScale = new Vector3(1 + reveal * .15f, 1 - reveal, 1 + reveal * .15f);
                BuriedSand.gameObject.SetActive(reveal < .999f);
            }
            if (IsClientInitialized && !Application.isBatchMode)
            {
                bool moving = tiltAt.Value >= 0 && Clock < tiltAt.Value + TiltSeconds;
                if (moving) GameAudio.Loop(ref mechanismAudio, SoundCue.BalanceMechanism, Beam, .6f);
                else GameAudio.StopLoop(mechanismAudio);
                if (Deposited > heardDeposited)
                {
                    heardDeposited = Deposited;
                    if (Deposited == 5 && RevealDust != null) RevealDust.Play();
                }
                if (ChestAvailable && !heardComplete)
                { heardComplete = true; GameAudio.Play(SoundCue.BalanceReveal, ChestSocket.position); }
            }
            for (int i = 0; i < stones.Count; i++)
            {
                var state = stones[i]; var target = Stones[i].transform;
                Vector3 point = state.Point; Quaternion rotation = transform.rotation * stoneRotations[i];
                if (state.Carrier != null && (state.Phase == 1 || state.Phase == 2))
                {
                    var actor = state.Carrier.GetComponent<NetworkWeapon>();
                    float t = Ease((float)(Clock - state.Started) / (state.Phase == 1 ? PickupSeconds : DepositSeconds));
                    if(state.Phase==1) point = actor.IsOwner && actor.GetComponent<BalanceStonePlayer>() is { } input ? input.DragPoint : state.Point;
                    else
                    {
                        float age=(float)(Clock-state.Started)/DepositSeconds;
                        float place=Ease(age/ReleaseFraction),settle=Ease((age-ReleaseFraction)/(1-ReleaseFraction));
                        point=age<ReleaseFraction ? Vector3.Lerp(state.Point,state.ReleasePoint,place)+Vector3.up*(.1f*Mathf.Sin(place*Mathf.PI)) : Vector3.Lerp(state.ReleasePoint,SlotPoint(state.Slot),settle);
                    }
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

        public override void OnStartClient()
        {
            base.OnStartClient(); heardDeposited = Deposited; heardComplete = ChestAvailable;
        }

        [ObserversRpc(RunLocally = true)]
        void StoneSoundRpc(SoundCue cue, Vector3 point)
        {
            if (IsClientInitialized && !Application.isBatchMode) GameAudio.Play(cue, point);
        }

        public override void OnStopClient() { StaffAudio.Stop(ref mechanismAudio); base.OnStopClient(); }

        public override void OnStopServer()
        {
            ServerEvents.Remove(this);
            for (int i = 0; i < stones.Count; i++) if (Stone(i).Carrier != null) Stone(i).Carrier.GetComponent<NetworkWeapon>()?.SetBalanceStone(null,-1);
            if (chest != null && chest.IsSpawned && chest.OnBalance) ServerManager.Despawn(chest.NetworkObject);
            base.OnStopServer();
        }
    }
}
