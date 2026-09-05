using UnityEngine;

public class WaterGridGenerator : MonoBehaviour
{
    private void Start()
    {
        CreateGrid();
    }

    private void CreateGrid()
    {
        // Создаем процедурную сетку на воде для визуального отслеживания движения
        GameObject gridObj = new GameObject("WaterGridLines");
        gridObj.transform.position = new Vector3(0, 0.05f, 0);
        
        LineRenderer lr = gridObj.AddComponent<LineRenderer>();
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = new Color(1f, 1f, 1f, 0.3f);
        lr.endColor = new Color(1f, 1f, 1f, 0.3f);
        lr.startWidth = 0.5f;
        lr.endWidth = 0.5f;

        int size = 100;
        int spacing = 10;
        int pointsCount = (size * 2 + 1) * 2 * 2;
        
        // Простой маркер сетки из квадратов
        // Вместо сложного кодинга процедурных мешей сделаем несколько ориентиров-буев
        for (int x = -50; x <= 50; x += 20)
        {
            for (int z = -50; z <= 50; z += 20)
            {
                GameObject buoys = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                buoys.name = "WaterBuoy";
                buoys.transform.position = new Vector3(x, 0.5f, z);
                buoys.transform.localScale = new Vector3(1f, 1f, 1f);
                buoys.GetComponent<Renderer>().material.color = Color.yellow;
                Object.Destroy(buoys.GetComponent<Collider>()); // Без физики
            }
        }
    }
}
