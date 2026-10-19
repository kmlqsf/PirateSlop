#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace PirateSlop.Experimental
{
    public sealed class UpperDroppedWeapon : NetworkBehaviour
    {
        public Transform Barrel, Traverse;
        readonly SyncVar<float> elevation = new(3f), traverse = new();
        Quaternion barrelRest, traverseRest;
        void Awake()
        {
            if (Barrel != null) barrelRest = Barrel.localRotation;
            if (Traverse != null) traverseRest = Traverse.localRotation;
        }
        public void SetPose(float pitch, float yaw) { elevation.Value = pitch; traverse.Value = yaw; }
        public override void OnStartClient()
        {
            if (Barrel != null) Barrel.localRotation = Quaternion.AngleAxis(3f - elevation.Value, Vector3.right) * barrelRest;
            if (Traverse != null) Traverse.localRotation = Quaternion.AngleAxis(traverse.Value, Vector3.up) * traverseRest;
        }
    }
}
#endif
