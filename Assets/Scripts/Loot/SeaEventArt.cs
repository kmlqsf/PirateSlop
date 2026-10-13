using UnityEngine;

namespace PirateSlop
{
    public sealed class SeaEventArt : ScriptableObject
    {
        public GameObject Raft;
        public GameObject CaptureStation;
        public GameObject CaptureBuoy;
        public GameObject RopeShackle;
        public GameObject RopeGrip;
        public Material Hemp;
        public Material FlagInk;
        public Vector3[] ChestRopeSurface, ChestRopeNormals;
        public float ChestRopeMinY, ChestRopeMaxY;
    }
}
