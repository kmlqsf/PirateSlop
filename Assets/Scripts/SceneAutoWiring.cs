using UnityEngine;

public class SceneAutoWiring : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoSetup()
    {
        // 1. Создаем воду с сеткой и буями
        if (GameObject.Find("OceanWater") == null)
        {
            GameObject water = GameObject.CreatePrimitive(PrimitiveType.Plane);
            water.name = "OceanWater";
            water.transform.position = new Vector3(0, 0, 0);
            water.transform.localScale = new Vector3(60, 1, 60);
            
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.color = new Color(0.1f, 0.4f, 0.6f, 0.8f);
            water.GetComponent<Renderer>().material = mat;
            
            Collider col = water.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            // Создаем буи для отслеживания движения
            for (int x = -50; x <= 50; x += 25)
            {
                for (int z = -50; z <= 50; z += 25)
                {
                    GameObject buoy = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    buoy.name = $"Buoy_{x}_{z}";
                    buoy.transform.position = new Vector3(x, 0.5f, z);
                    buoy.transform.localScale = new Vector3(1.5f, 2f, 1.5f);
                    buoy.GetComponent<Renderer>().material.color = Color.yellow;
                    Collider bCol = buoy.GetComponent<Collider>();
                    if (bCol != null) Object.Destroy(bCol);
                }
            }
        }

        // 2. Настраиваем корабль и штурвал
        GameObject ship = GameObject.Find("SM_PirateSloop");
        if (ship != null)
        {
            ship.tag = "Ship";
            
            if (ship.GetComponent<Rigidbody>() == null)
            {
                Rigidbody rb = ship.AddComponent<Rigidbody>();
                rb.mass = 4000f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            }

            if (ship.GetComponent<ShipController>() == null) ship.AddComponent<ShipController>();
            if (ship.GetComponent<SailSystem>() == null) ship.AddComponent<SailSystem>();

            Transform helmT = ship.transform.Find("Helm");
            GameObject helmObj;
            if (helmT == null)
            {
                helmObj = new GameObject("Helm");
                helmObj.transform.SetParent(ship.transform);
                helmObj.transform.localPosition = new Vector3(0f, 1.2f, -3.5f);
            }
            else
            {
                helmObj = helmT.gameObject;
            }

            if (helmObj.GetComponent<HelmInteraction>() == null)
            {
                helmObj.AddComponent<HelmInteraction>();
            }
        }

        Debug.Log("<color=green>SceneAutoWiring: Fully initialized water grid, ship physics, and helm interaction!</color>");
    }
}
