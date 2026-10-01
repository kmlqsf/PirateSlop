using FishNet.Object.Synchronizing;
using FishNet.Object;
using PirateSlop.World;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkLootChest
    {
        readonly SyncVar<float> raftYaw = new();
        readonly SyncVar<bool> raftDetachedState = new();
        readonly SyncVar<float> lockAngle = new();
        readonly SyncVar<float> lockRotation = new();
        readonly SyncVar<float> lockStress = new();
        readonly SyncVar<int> lockPicks = new(3);
        float nextRaftTurn, raftSpeed, lockSecret, lockInputAt, desiredLockAngle;
        bool lockTorque, lockInitialized;
        Rigidbody raftBody;
        public Rigidbody RaftBody => raftBody;
        public float LockAngle => lockAngle.Value;
        public float LockRotation => lockRotation.Value;
        public float LockStress => lockStress.Value;
        public int LockPicks => lockPicks.Value;

        void InitializeRaft()
        {
            var prefab = SessionController.Instance != null ? SessionController.Instance.ShipPrefab : null;
            var ship = prefab != null ? prefab.GetComponent<ShipController>() : null;
            raftSpeed = (ship != null ? ship.MaxSpeed : 10f) * .2f;
            raftYaw.Value = Random.Range(0f, 360f);
            nextRaftTurn = Time.time + Random.Range(20f, 45f);
        }

        void TickRaft()
        {
            float dt = Mathf.Min(Time.deltaTime, .1f);
            var session = SessionController.Instance;
            var world = ProceduralWorld.Instance;
            if (session == null || world == null || !world.Ready) return;
            float radius = session.SafeRadius();
            var point = eventPoint.Value;
            var flat = new Vector3(point.x, 0, point.z);
            float margin = radius - flat.magnitude;
            if (margin <= 150f)
            {
                var inward = flat.sqrMagnitude > .001f ? -flat.normalized : Vector3.forward;
                float target = Mathf.Atan2(inward.x, inward.z) * Mathf.Rad2Deg;
                raftYaw.Value = Mathf.MoveTowardsAngle(raftYaw.Value, target, 100f * dt);
                nextRaftTurn = Time.time + 8f;
            }
            else if (Time.time >= nextRaftTurn)
            {
                raftYaw.Value += Random.Range(-100f, 100f);
                nextRaftTurn = Time.time + Random.Range(20f, 45f);
            }
            var forward = Quaternion.Euler(0, raftYaw.Value, 0) * Vector3.forward;
            var next = point + forward * raftSpeed * dt;
            if (!world.CanSail(next + forward * 8f, raftYaw.Value))
            {
                raftYaw.Value += 100f * dt;
                next = point;
            }
            float safeRadius = Mathf.Max(0f, radius - 108f);
            var safe = Vector3.ClampMagnitude(new Vector3(next.x, 0, next.z), safeRadius);
            eventPoint.Value = new Vector3(safe.x, point.y, safe.z);
            if (!raftDetachedState.Value) anchor.Value = eventPoint.Value + Vector3.up * .45f;
        }

        void UpdateRaftVisual()
        {
            if (eventVisual == null) return;
            var point = eventPoint.Value;
            if (OceanSurface.Instance != null) point.y = OceanSurface.Instance.Height(point);
            float blend = IsServerInitialized ? 1f : 1f - Mathf.Exp(-12f * Time.deltaTime);
            eventVisual.transform.SetPositionAndRotation(Vector3.Lerp(eventVisual.transform.position, point, blend),
                Quaternion.Slerp(eventVisual.transform.rotation, Quaternion.Euler(0, raftYaw.Value, 0), blend));
        }

        void BeginRaftLock()
        {
            if (!lockInitialized) { lockSecret = Random.Range(-70f, 70f); lockInitialized = true; }
            lockRound.Value++;
            lockAngle.Value = desiredLockAngle = 0f;
            lockRotation.Value = lockStress.Value = 0f;
            lockPicks.Value = 3;
            lockTorque = false;
            lockInputAt = Time.time;
            LockpickSoundObserversRpc(SoundCue.LockpickStart);
        }

        public void RaftLockInput(NetworkWeapon player, float angle, bool torque, int round)
        {
            if (!IsServerInitialized || Kind != SeaLootKind.Raft || phase.Value != SeaLootState.Locked ||
                player != worker || round != lockRound.Value || !ValidWork() || !float.IsFinite(angle)) return;
            desiredLockAngle = Mathf.Clamp(angle, -85f, 85f);
            lockTorque = torque;
            lockInputAt = Time.time;
            heartbeat = Time.time + 1.2f;
        }

        public void BotRaftLockInput(NetworkWeapon player)
        {
            if (player == null || player.GetComponent<NetworkPlayer>()?.IsBot.Value != true) return;
            RaftLockInput(player, lockSecret, true, lockRound.Value);
        }

        void TickRaftLock()
        {
            float dt = Mathf.Min(Time.deltaTime, .1f);
            lockAngle.Value = Mathf.MoveTowards(lockAngle.Value, desiredLockAngle, 150f * dt);
            bool torque = lockTorque && Time.time - lockInputAt < .35f;
            float error = Mathf.Abs(lockAngle.Value - lockSecret);
            float limit = error <= 8f ? 90f : Mathf.Clamp(85f - (error - 8f) * 1.3f, 5f, 85f);
            lockRotation.Value = Mathf.MoveTowards(lockRotation.Value, torque ? limit : 0f, (torque ? 60f : 120f) * dt);
            bool blocked = torque && error > 8f && lockRotation.Value >= limit - .1f;
            lockStress.Value = Mathf.Clamp01(lockStress.Value + (blocked ? .6f + error / 100f : -.8f) * dt);
            progress.Value = lockRotation.Value / 90f;
            if (lockRotation.Value >= 89.9f) { LockpickSoundObserversRpc(SoundCue.LockpickSuccess); Unlock(); return; }
            if (lockStress.Value < 1f) return;
            lockPicks.Value--;
            LockpickSoundObserversRpc(SoundCue.LockpickBreak);
            lockRound.Value++;
            lockStress.Value = lockRotation.Value = progress.Value = 0f;
            lockTorque = false;
            if (lockPicks.Value <= 0) StopWork();
        }
        [ObserversRpc(RunLocally = true)]
        void LockpickSoundObserversRpc(SoundCue cue) => GameAudio.Play(cue, transform.position);
    }
}
