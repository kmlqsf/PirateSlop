using System.Collections.Generic;
using PirateSlop.Ships;
using UnityEngine;

namespace PirateSlop
{
    [DefaultExecutionOrder(95)]
    public sealed class HandLanternVisual : MonoBehaviour
    {
        public static readonly List<HandLanternVisual> Active = new();
        public Transform Swing;
        public Renderer Glass;
        public int GlassSlot = 1;
        public Light Light;
        public bool Held;
        MaterialPropertyBlock block;
        Quaternion rest;
        Vector3 previousPosition, previousVelocity;
        Vector2 angle, angleVelocity;
        bool started, applied, lastLit;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRegistry() => Active.Clear();

        void Awake()
        {
            block = new MaterialPropertyBlock();
            rest = Swing != null ? Swing.localRotation : Quaternion.identity;
        }

        void OnEnable()
        {
            Active.Add(this);
            started = false;
            angle = angleVelocity = Vector2.zero;
            applied = false;
            SetLit(false, false);
        }

        void OnDisable()
        {
            Active.Remove(this);
            if (Light != null) Light.enabled = false;
        }

        public void SetLit(bool lit, bool emitLight)
        {
            if (Light != null)
            {
                Light.enabled = lit && emitLight && isActiveAndEnabled;
                Light.renderMode = LightRenderMode.ForcePixel;
            }
            if (Held && Glass != null) Glass.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (applied && lastLit == lit) return;
            applied = true;
            lastLit = lit;
            if (Glass == null) return;
            block ??= new MaterialPropertyBlock();
            Glass.GetPropertyBlock(block, GlassSlot);
            block.SetColor("_EmissionColor", lit ? ShipV3Features.LanternEmission : Color.black);
            Glass.SetPropertyBlock(block, GlassSlot);
        }

        void LateUpdate()
        {
            if (Light != null && Light.enabled)
                Light.intensity = ShipV3Features.LanternIntensity + .01f * Mathf.Sin(Time.time * 2.1f) + .008f * Mathf.Sin(Time.time * 3.3f);
            if (Swing == null || !Held) return;
            float dt = Mathf.Clamp(Time.deltaTime, .001f, .05f);
            Vector3 point = transform.position;
            Vector3 movement = point - previousPosition;
            Vector3 velocity = started && movement.sqrMagnitude < 9f ? movement / dt : Vector3.zero;
            Vector3 acceleration = started ? Vector3.ClampMagnitude((velocity - previousVelocity) / dt, 12f) : Vector3.zero;
            previousPosition = point;
            previousVelocity = Vector3.Lerp(previousVelocity, velocity, 1f - Mathf.Exp(-12f * dt));
            started = true;
            Vector3 gravity = transform.InverseTransformDirection(Vector3.down - acceleration * .065f);
            Vector2 target = new Vector2(Mathf.Atan2(-gravity.z, -gravity.y), Mathf.Atan2(gravity.x, -gravity.y)) * Mathf.Rad2Deg;
            target.x = Mathf.Clamp(target.x, -24f, 24f);
            target.y = Mathf.Clamp(target.y, -24f, 24f);
            angle.x = Mathf.SmoothDampAngle(angle.x, target.x, ref angleVelocity.x, .23f, 120f, dt);
            angle.y = Mathf.SmoothDampAngle(angle.y, target.y, ref angleVelocity.y, .23f, 120f, dt);
            Swing.localRotation = rest * Quaternion.Euler(angle.x, 0f, angle.y);
        }
    }
}
