using UnityEngine;

namespace PirateSlop
{
    public abstract class OceanHeightSource : MonoBehaviour
    {
        public abstract float HeightOffset(Vector3 position, float time);
    }
}
