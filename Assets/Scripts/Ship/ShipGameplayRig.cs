using UnityEngine;

namespace PirateSlop
{
    /// <summary>
    /// Wires ship gameplay from known mesh names so stations stay data-driven and replaceable.
    /// </summary>
    [RequireComponent(typeof(ShipMotor))]
    public class ShipGameplayRig : MonoBehaviour
    {
        [SerializeField] ShipHandlingConfig handling;
        [SerializeField] string mastColliderName = "COL_Mast";
        [SerializeField] string helmColliderName = "COL_Helm";
        [SerializeField] string helmWheelName = "HelmWheel";
        [SerializeField] SailStation.Mode sailMode = SailStation.Mode.Toggle;

        public void SetHandling(ShipHandlingConfig config) => handling = config;

        void Awake()
        {
            Build();
        }

        public void Build()
        {
            var motor = GetComponent<ShipMotor>();
            if (handling != null) motor.SetHandling(handling);

            Transform mast = FindChild(mastColliderName);
            Transform helm = FindChild(helmColliderName);
            Transform wheel = FindChild(helmWheelName);
            if (wheel != null) motor.SetHelmWheel(wheel);

            if (mast != null)
            {
                var sail = mast.GetComponent<SailStation>();
                if (sail == null) sail = mast.gameObject.AddComponent<SailStation>();
                sail.Configure(motor, sailMode);
                EnsureTrigger(mast.gameObject, new Vector3(mast.position.x, 2.25f, mast.position.z), 1.7f);
            }

            if (helm != null)
            {
                var station = helm.GetComponent<HelmStation>();
                if (station == null) station = helm.gameObject.AddComponent<HelmStation>();
                Transform stand = helm.Find("StandPoint");
                if (stand == null)
                {
                    var go = new GameObject("StandPoint");
                    stand = go.transform;
                    stand.SetParent(helm, false);
                }

                stand.position = new Vector3(helm.position.x, 2.7f, helm.position.z + 0.85f);
                stand.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
                station.Configure(motor, stand);
                EnsureTrigger(helm.gameObject, new Vector3(helm.position.x, 2.85f, helm.position.z), 1.2f);
            }
        }

        static void EnsureTrigger(GameObject host, Vector3 worldPos, float radius)
        {
            const string childName = "UseVolume";
            Transform existing = host.transform.Find(childName);
            GameObject volume = existing != null ? existing.gameObject : new GameObject(childName);
            if (existing == null)
            {
                volume.transform.SetParent(host.transform, false);
                volume.layer = host.layer;
            }

            volume.transform.position = worldPos;
            var sphere = volume.GetComponent<SphereCollider>();
            if (sphere == null) sphere = volume.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = radius;
        }

        Transform FindChild(string name)
        {
            Transform[] all = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == name) return all[i];
            }

            return null;
        }
    }
}
