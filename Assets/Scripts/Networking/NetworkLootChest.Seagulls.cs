using PirateSlop.World;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkLootChest
    {
        readonly SeagullVisual[] sunkenGulls = new SeagullVisual[2];

        void CreateSunkenGulls()
        {
            for (int i = 0; i < sunkenGulls.Length; i++)
            {
                sunkenGulls[i] = SeagullVisual.Create(eventVisual.transform, i * 113 + Mathf.RoundToInt(eventPoint.Value.x));
                sunkenGulls[i].name = "SunkenChestSeagull";
            }
            UpdateSunkenGulls();
        }

        void UpdateSunkenGulls()
        {
            float time = OceanSurface.Instance != null ? OceanSurface.Instance.WaveTime : Time.time;
            for (int i = 0; i < sunkenGulls.Length; i++)
            {
                var bird = sunkenGulls[i];
                if (bird == null) continue;
                bool show = phase.Value != SeaLootState.Ready;
                bird.gameObject.SetActive(show);
                if (!show) continue;
                float angle = time * (.38f - i * .05f) + i * Mathf.PI + eventPoint.Value.x * .01f;
                float radius = 6f + i * 2f;
                var center = eventPoint.Value;
                if (OceanSurface.Instance != null) center.y = OceanSurface.Instance.Height(center);
                bird.transform.position = center + new Vector3(Mathf.Cos(angle) * radius, 10f + i * 2f + Mathf.Sin(time * .7f + i) * .6f, Mathf.Sin(angle) * radius);
                bird.transform.rotation = Quaternion.LookRotation(new Vector3(-Mathf.Sin(angle), 0, Mathf.Cos(angle))) * Quaternion.Euler(0, 0, -12f);
                bird.Animate(time, SeagullVisual.FlightPower(time, i * 113), 0, 0);
            }
        }
    }
}
