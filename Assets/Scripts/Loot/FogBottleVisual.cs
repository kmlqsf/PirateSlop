using UnityEngine;

namespace PirateSlop
{
    public sealed class FogBottleVisual : MonoBehaviour
    {
        public FogCloudVisual Mist;
        public float Density = 22f;
        float animationAge;
        void Awake()
        {
            animationAge = Mathf.Repeat(GetEntityId().GetHashCode() * .381f, 25f);
            if (Mist != null && Mist.InsideBottle) Mist.Density = Mathf.Max(0f, Density);
            UpdateMist();
        }
        void LateUpdate()
        {
            animationAge += Mathf.Min(Time.deltaTime, .05f);
            UpdateMist();
        }
        void UpdateMist()
        {
            if (Mist != null) Mist.SetPersistentAge(animationAge);
        }
    }
}
