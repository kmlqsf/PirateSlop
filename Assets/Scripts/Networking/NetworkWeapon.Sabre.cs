using FishNet.Object;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkWeapon
    {
        uint sabreWoodSequence, lastSabreWoodSequence;
        public void PublishSabreWood(SabreWoodHit hit)
        {
            if (IsServerInitialized) SabreWoodObserversRpc(++sabreWoodSequence, hit);
        }
        [ObserversRpc(RunLocally = true)]
        void SabreWoodObserversRpc(uint sequence, SabreWoodHit hit)
        {
            if (sequence <= lastSabreWoodSequence) return;
            lastSabreWoodSequence = sequence;
            SabreWoodImpact.Present(hit);
        }
    }
}
