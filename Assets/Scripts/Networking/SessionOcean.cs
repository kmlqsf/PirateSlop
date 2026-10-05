using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Transporting;

namespace PirateSlop.Networking
{
    public struct TestOceanMessage : IBroadcast
    {
        public float Strength, Steepness, Speed, Anchor, Clock;
    }

    public sealed partial class SessionController
    {
        public bool CanAdjustTestOcean => DeveloperMenu.Available && manager != null && manager.ServerManager.Started && EnvironmentTestActive;

        TestOceanMessage CurrentTestOcean()
        {
            var ocean = OceanSurface.Instance.HeightSource as BoatAttackOcean;
            return new TestOceanMessage { Strength = ocean.WaveStrength, Steepness = ocean.WaveSteepness, Speed = ocean.WaveSpeed, Anchor = ocean.WaveClockAnchor, Clock = ocean.WaveClockValue };
        }

        public void SetTestOcean(float strength, float steepness, float speed)
        {
            if (!CanAdjustTestOcean || OceanSurface.Instance.HeightSource is not BoatAttackOcean ocean) return;
            if (UnityEngine.Mathf.Approximately(strength, ocean.WaveStrength) && UnityEngine.Mathf.Approximately(steepness, ocean.WaveSteepness) && UnityEngine.Mathf.Approximately(speed, ocean.WaveSpeed)) return;
            ocean.SetWaveControls(strength, steepness, speed);
            manager.ServerManager.Broadcast(CurrentTestOcean());
        }

        void SendTestOcean(NetworkConnection connection)
        {
            if (EnvironmentTestActive && OceanSurface.Instance != null && OceanSurface.Instance.HeightSource is BoatAttackOcean)
                manager.ServerManager.Broadcast(connection, CurrentTestOcean());
        }

        void ReceiveTestOcean(TestOceanMessage message, Channel channel)
        {
            if (manager.ServerManager.Started || OceanSurface.Instance == null || OceanSurface.Instance.HeightSource is not BoatAttackOcean ocean) return;
            ocean.ApplyWaveControls(message.Strength, message.Steepness, message.Speed, message.Anchor, message.Clock);
        }
    }
}
