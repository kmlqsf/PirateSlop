using PirateSlop.Networking;
using UnityEngine;

namespace PirateSlop
{
    [DefaultExecutionOrder(95)]
    public sealed class HandMortarVisual : MonoBehaviour
    {
        public Transform Trigger, Hammer, Frizzen, Wick, Ember, StrikePoint, Muzzle;
        NetworkEquipment equipment;
        AdvancedPlayerController motor;
        Quaternion triggerRest, hammerRest, frizzenRest;
        bool view, strikePending;
        float previousAge = 10;

        void Awake()
        {
            equipment = GetComponentInParent<NetworkEquipment>();
            motor = GetComponentInParent<AdvancedPlayerController>();
            view = motor != null && transform.IsChildOf(motor.PlayerCamera.transform);
            triggerRest = Trigger.localRotation;
            hammerRest = Hammer.localRotation;
            frizzenRest = Frizzen.localRotation;
        }

        void LateUpdate()
        {
            if (equipment == null || !equipment.HandMortar || equipment.MortarSettings == null) { Ember.gameObject.SetActive(false); return; }
            float age = equipment.MortarShotAge;
            float ignition = equipment.MortarSettings.IgnitionSeconds;
            bool burning = age >= .065f && age < ignition && equipment.IsBusy && !equipment.IsReloading;
            Wick.gameObject.SetActive(equipment.LoadedRounds > 0 || age < ignition && equipment.IsBusy && !equipment.IsReloading);
            Ember.gameObject.SetActive(burning);
            if (age < .3f && age < previousAge - .001f) strikePending = true;
            previousAge = age;
            float press = age < .045f ? Mathf.SmoothStep(0, 1, age / .045f) : 1f - Mathf.SmoothStep(0, 1, (age - .045f) / .18f);
            Trigger.localRotation = triggerRest * Quaternion.Euler(18f * press, 0, 0);
            float strike = age < .065f ? Mathf.SmoothStep(0, 1, age / .065f) : 1f - Mathf.SmoothStep(0, 1, (age - .065f) / .22f);
            Hammer.localRotation = hammerRest * Quaternion.Euler(-40f * strike, 0, 0);
            float open = age < .065f ? 0 : age < .11f ? Mathf.SmoothStep(0, 1, (age - .065f) / .045f) : 1f - Mathf.SmoothStep(0, 1, (age - .11f) / .17f);
            Frizzen.localRotation = frizzenRest * Quaternion.Euler(-22f * open, 0, 0);
            if (!strikePending || age < .065f) return;
            strikePending = false;
            bool first = equipment.IsOwner && motor.PlayerCamera.enabled && !motor.IsThirdPerson;
            if (!equipment.Active || view != first) return;
            FirearmVfx.FlintlockStrike(StrikePoint.position, (transform.right + transform.up * .4f).normalized, .8f);
        }
    }
}
