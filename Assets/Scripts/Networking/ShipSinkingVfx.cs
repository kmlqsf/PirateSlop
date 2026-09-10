using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed class ShipSinkingVfx : MonoBehaviour
    {
        NetworkShip ship;
        float nextSplash;
        int sample;

        void Awake() => ship = GetComponent<NetworkShip>();

        void Update()
        {
            if (!ship.IsClientInitialized || !ship.IsSinking || Application.isBatchMode || Time.time < nextSplash) return;
            nextSplash = Time.time + .25f;
            var ocean = OceanSurface.Instance;
            var camera = Camera.main;
            if (ocean == null || camera == null || (camera.transform.position - transform.position).sqrMagnitude > 180f * 180f) return;
            float along = ((sample / 2) % 7) / 6f * 2f - 1f;
            float side = (sample++ & 1) == 0 ? -1f : 1f;
            Vector3 point = transform.TransformPoint(new Vector3(side * ship.HullHalfExtents.x, 3f, along * ship.HullHalfExtents.y));
            float water = ocean.Height(point);
            if (Mathf.Abs(point.y - water) > 7f) return;
            point.y = water + .05f;
            CombatVfx.Splash(point, .7f);
        }
    }
}
