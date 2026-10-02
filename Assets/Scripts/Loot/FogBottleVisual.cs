using UnityEngine;

namespace PirateSlop
{
    public sealed class FogBottleVisual : MonoBehaviour
    {
        public FogCloudVisual Mist;
        void Awake() => UpdateMist();
        void Update() => UpdateMist();
        void UpdateMist()
        {
            if (Mist != null) Mist.SetPersistentAge(Time.time);
        }
    }
}
