using UnityEngine;

public class AutoSetupHelmAndWater : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Setup()
    {
        // 1. Ищем корабль
        GameObject ship = GameObject.Find("SM_PirateSloop");
        if (ship != null)
        {
            // Проверяем, есть ли штурвал, если нет - создаем пустой объект штурвала на палубе
            Transform helmTransform = ship.transform.Find("SteeringWheel");
            GameObject helmObj;
            if (helmTransform == null)
            {
                helmObj = new GameObject("SteeringWheel");
                helmObj.transform.SetParent(ship.transform);
                // Ставим на корму корабля (примерные координаты относительно шлюпа)
                helmObj.transform.localPosition = new Vector3(0f, 1.5f, -4f);
            }
            else
            {
                helmObj = helmTransform.gameObject;
            }

            // Вешаем HelmInteraction если его еще нет
            if (helmObj.GetComponent<HelmInteraction>() == null)
            {
                helmObj.AddComponent<HelmInteraction>();
            }

            // Связываем с ShipController
            ShipController sc = ship.GetComponent<ShipController>();
            if (sc != null)
            {
                // Убедимся, что WheelInteraction на корабле ссылается на этот штурвал или заменен на HelmInteraction
            }
        }
    }
}
