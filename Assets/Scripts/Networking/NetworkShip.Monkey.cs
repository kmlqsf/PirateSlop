using FishNet.Object.Synchronizing;
using PirateSlop.Ships;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkShip
    {
        readonly SyncVar<ShipMonkeyPose> monkeyPose = new();
        readonly SyncVar<bool> monkeyVisible = new(true);
        ShipMonkey monkey;
        float nextMonkeyPublish;

        public void MonkeySound(SoundCue cue, Vector3 point) { if (IsServerInitialized) MonkeySoundObserversRpc(cue, point); }

        [FishNet.Object.ObserversRpc(RunLocally = true)]
        void MonkeySoundObserversRpc(SoundCue cue, Vector3 point) => GameAudio.Play(cue, point, .6f);

        void BeginMonkey()
        {
            monkey = GetComponent<ShipMonkey>();
            if (monkey == null) return;
            monkey.Begin();
            nextMonkeyPublish = 0f;
            if (IsServerInitialized) monkeyPose.Value = monkey.Capture();
        }

        void UpdateMonkey()
        {
            if (monkey == null || !IsServerInitialized && !IsClientInitialized) return;
            if (IsServerInitialized)
            {
                if (!IsSinking) monkey.Simulate(Time.deltaTime);
                else monkey.StopActivities();
                if (Time.unscaledTime < nextMonkeyPublish) return;
                nextMonkeyPublish = Time.unscaledTime + monkey.SnapshotInterval;
                monkeyPose.Value = monkey.Capture();
                monkeyVisible.Value = monkey.Visual != null && monkey.Visual.gameObject.activeSelf;
            }
            else
            {
                if (monkeyPose.Value.Sequence == 0) return;
                if (!monkeyVisible.Value) { if (monkey.Visual != null) monkey.Visual.gameObject.SetActive(false); return; }
                monkey.Receive(monkeyPose.Value);
                monkey.PresentRemote();
            }
        }
    }
}
