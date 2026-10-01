using UnityEngine;

public class WaterGridGenerator : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void RemoveLegacyBuoys()
    {
        for (int x = -60; x <= 60; x += 15)
            for (int z = -60; z <= 60; z += 15)
            {
                var buoy = GameObject.Find($"WaterBuoy_{x}_{z}");
                if (buoy != null) Object.Destroy(buoy);
            }
    }
}
