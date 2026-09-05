using UnityEngine;

public class WaterGridGenerator : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        // Создаем буи-ориентиры на воде при старте игры
        if (GameObject.Find("WaterBuoy_0_0") != null) return;

        for (int x = -60; x <= 60; x += 15)
        {
            for (int z = -60; z <= 60; z += 15)
            {
                GameObject buoy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                buoy.name = $"WaterBuoy_{x}_{z}";
                buoy.transform.position = new Vector3(x, 0.5f, z);
                buoy.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
                
                // Красим в яркий красный/желтый цвет
                var renderer = buoy.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = new Color(1f, 0.5f, 0f);
                }
                
                // Убираем коллайдер, чтобы корабль не цеплялся за них
                Collider col = buoy.GetComponent<Collider>();
                if (col != null) Object.Destroy(col);
            }
        }
    }
}
