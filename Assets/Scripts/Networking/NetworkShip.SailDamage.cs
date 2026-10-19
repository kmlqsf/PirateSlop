using FishNet.Object.Synchronizing;
using PirateSlop.Ships;
using UnityEngine;

namespace PirateSlop.Networking
{
    public struct SailPuncture
    {
        public byte Sail;
        public ushort Seed;
        public Vector2 UV, AxisU, AxisV;
    }

    public sealed partial class NetworkShip
    {
        readonly SyncList<SailPuncture> sailPunctures = new();
        SailDamageSurface[] sailDamageSurfaces;
        int presentedSailPunctures;
        uint sailDamageSequence;

        void BeginSailDamage()
        {
            var rig = GetComponent<ShipV3VisualRig>();
            if (rig == null || rig.Sails == null) return;
            sailDamageSurfaces = new SailDamageSurface[rig.Sails.Length];
            for (int i = 0; i < rig.Sails.Length; i++)
                if (rig.Sails[i] != null) sailDamageSurfaces[i] = new SailDamageSurface(rig.Sails[i], i);
            presentedSailPunctures = 0;
        }

        public static void PunctureSails(Vector3 start, Vector3 end, float diameter)
        {
            if (!float.IsFinite(diameter) || diameter <= 0f || !float.IsFinite(start.sqrMagnitude) || !float.IsFinite(end.sqrMagnitude)
                || (end - start).sqrMagnitude < .00000001f) return;
            foreach (var ship in ActiveShips)
            {
                if (ship == null || !ship.IsServerInitialized || ship.sailDamageSurfaces == null) continue;
#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
                var upper = ship.GetComponent<PirateSlop.Experimental.UpperDestructionV2>();
#endif
                for (int i = 0; i < ship.sailDamageSurfaces.Length; i++)
                {
#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
                    if (upper != null && upper.IsProtectedSail(i)) continue;
#endif
                    var surface = ship.sailDamageSurfaces[i];
                    if (surface == null) continue;
                    ushort seed = (ushort)(++ship.sailDamageSequence * 40503u + (uint)i * 7919u);
                    surface.Puncture(ship.sailPunctures, (byte)i, start, end, diameter, seed);
                }
            }
        }

        void UpdateSailDamage()
        {
            if (!IsClientInitialized || sailDamageSurfaces == null) return;
            int end = Mathf.Min(sailPunctures.Count, presentedSailPunctures + 256);
            while (presentedSailPunctures < end)
            {
                var hole = sailPunctures[presentedSailPunctures++];
                if (hole.Sail < sailDamageSurfaces.Length) sailDamageSurfaces[hole.Sail]?.Stamp(hole);
            }
            foreach (var surface in sailDamageSurfaces) surface?.Flush();
        }

        void StopSailDamage()
        {
            if (sailDamageSurfaces == null) return;
            foreach (var surface in sailDamageSurfaces) surface?.Dispose();
            sailDamageSurfaces = null;
            presentedSailPunctures = 0;
        }
    }
}
