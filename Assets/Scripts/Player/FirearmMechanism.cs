using UnityEngine;
using PirateSlop.Networking;

namespace PirateSlop
{
    [DefaultExecutionOrder(95)]
    public sealed class FirearmMechanism : MonoBehaviour
    {
        public InventoryItem Item;
        public Transform TriggerPivot;
        public Transform[] Hammers, Frizzens, StrikePoints;
        public float SparkScale = 1;
        PirateWeapon pistol;
        NetworkEquipment equipment;
        FirearmHandling handling;
        AdvancedPlayerController motor;
        PlayerInventory inventory;
        Quaternion triggerRest;
        Quaternion[] hammerRest, frizzenRest;
        float previousAge = float.PositiveInfinity;
        bool strikePending, view;

        void Awake()
        {
            pistol = GetComponentInParent<PirateWeapon>();
            equipment = GetComponentInParent<NetworkEquipment>();
            handling = GetComponentInParent<FirearmHandling>();
            motor = GetComponentInParent<AdvancedPlayerController>();
            inventory = GetComponentInParent<PlayerInventory>();
            view = motor != null && transform.IsChildOf(motor.PlayerCamera.transform);
            triggerRest = TriggerPivot.localRotation;
            hammerRest = new Quaternion[Hammers.Length];
            frizzenRest = new Quaternion[Frizzens.Length];
            for (int i = 0; i < Hammers.Length; i++) hammerRest[i] = Hammers[i].localRotation;
            for (int i = 0; i < Frizzens.Length; i++) frizzenRest[i] = Frizzens[i].localRotation;
        }

        void LateUpdate()
        {
            if (motor == null || handling == null) return;
            bool isPistol = Item == InventoryItem.Pistol;
            if (!isPistol && (equipment == null || equipment.Item != Item)) return;
            float age = isPistol ? pistol.ShotAge : equipment.ShotAge;
            bool loaded = isPistol ? pistol.Loaded : equipment.LoadedRounds >= (Item == InventoryItem.DoubleBarrel ? 2 : 1);
            bool reloading = isPistol ? pistol.Reloading : equipment.IsReloading;
            float reload = isPistol ? pistol.ReloadProgress : equipment.ReloadProgress;
            if (age < .35f && age + .001f < previousAge) strikePending = true;
            previousAge = age;
            float press = age < .045f ? Mathf.SmoothStep(0, 1, age / .045f) : 1 - Mathf.SmoothStep(0, 1, (age - .045f) / .14f);
            TriggerPivot.localRotation = triggerRest * Quaternion.Euler(18 * press, 0, 0);
            float hammer = loaded ? 0 : -12;
            if (age < .065f) hammer = 50 * Mathf.SmoothStep(0, 1, age / .065f);
            else if (age < .24f) hammer = Mathf.Lerp(50, -12, Mathf.SmoothStep(0, 1, (age - .065f) / .175f));
            else if (reloading) hammer = Mathf.Lerp(-12, 0, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.72f, .95f, reload)));
            float frizzen = age < .065f ? 0 : age < .11f ? 22 * Mathf.SmoothStep(0, 1, (age - .065f) / .045f) : 22 * (1 - Mathf.SmoothStep(0, 1, (age - .11f) / .14f));
            for (int i = 0; i < Hammers.Length; i++) Hammers[i].localRotation = hammerRest[i] * Quaternion.Euler(hammer, 0, 0);
            for (int i = 0; i < Frizzens.Length; i++) Frizzens[i].localRotation = frizzenRest[i] * Quaternion.Euler(frizzen, 0, 0);
            if (!strikePending || age < .065f) return;
            strikePending = false;
            bool firstPerson = handling.Local && motor.PlayerCamera.enabled && !motor.IsThirdPerson;
            if (view != firstPerson || isPistol && !inventory.PistolSelected || !isPistol && (!equipment.Active || equipment.Scoped)) return;
            foreach (var point in StrikePoints)
            {
                Vector3 outward = transform.TransformDirection(point.localPosition.x < 0 ? Vector3.left : Vector3.right);
                FirearmVfx.FlintlockStrike(point.position, (outward + transform.up * .4f).normalized, SparkScale);
            }
        }
    }
}
