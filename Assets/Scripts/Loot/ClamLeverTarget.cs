using System.Collections.Generic;
using PirateSlop.Networking;
using UnityEngine;

namespace PirateSlop
{
    public sealed class ClamLeverTarget : MonoBehaviour
    {
        public static readonly List<ClamLeverTarget> Active = new();
        public NetworkClamEvent Owner;
        public int Side;
        public Transform HandlePivot, Grip, LiftArm, HookSocket;
        public Transform[] LiftSections;
        public float SectionLength;
        public Vector3 HookRestPoint;
        public float HookLiftWeight;
        public Transform BoardingPoint;
        public Transform LiftEnd;
        public float SectionOverlap=.12f;
        void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
        void OnDisable() { Active.Remove(this); }

        public bool Aim(Ray ray, out float distance)
        {
            distance = float.PositiveInfinity;
            for (int i = 0; i <= 8; i++)
            {
                var point = Vector3.Lerp(HandlePivot.position, Grip.position, i / 8f);
                float along = Vector3.Dot(point - ray.origin, ray.direction);
                if (along < 0 || along > 3f) continue;
                if ((point - ray.GetPoint(along)).sqrMagnitude < .25f * .25f) distance = Mathf.Min(distance, along);
            }
            return !float.IsPositiveInfinity(distance);
        }

        public bool CanReach(NetworkWeapon actor)
        {
            if (actor == null || Grip == null) return false;
            var eye = actor.transform.position + Vector3.up * 1.65f;
            var delta = Grip.position - eye;
            if (delta.sqrMagnitude > 2.8f * 2.8f) return false;
            foreach (var hit in Physics.RaycastAll(eye, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(actor.transform) && !hit.transform.IsChildOf(Owner.transform)) return false;
            return true;
        }

        public void Animate()
        {
            SetHandle(Owner.HandlePull(Side));
            HookSocket.localPosition = HookRestPoint + Vector3.up * (Owner.LipHeight(Side) * HookLiftWeight);
            var delta = HookSocket.position - LiftArm.position;
            if (delta.sqrMagnitude < .001f) return;
            LiftArm.rotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
            float length = SectionLength;
            float spacing = Mathf.Clamp((delta.magnitude-length)/(LiftSections.Length-1),0,length*(1-SectionOverlap));
            for (int i = 0; i < LiftSections.Length; i++)
            {
                LiftSections[i].localPosition = Vector3.up * (length * .5f + i * spacing);
                var scale = LiftSections[i].localScale; scale.y = length; LiftSections[i].localScale = scale;
            }
            if(LiftEnd!=null) LiftEnd.SetPositionAndRotation(HookSocket.position,LiftArm.rotation);
        }

        public void SetHandle(float pull) => HandlePivot.localRotation = Quaternion.Euler(-65f * Mathf.Clamp01(pull), 0, 0);
        public string Hint => Owner.SuccessfulPairs >= Owner.RequiredPairs
            ? Owner.ChestAvailable ? "Раковина открыта · подлезьте вприсядку к сундуку" : "Раковина открывается"
            : Owner.Cooldown(Side) > 0 ? $"Рычаг возвращается: {Owner.Cooldown(Side):F1} с · {Owner.SuccessfulPairs}/{Owner.RequiredPairs}"
            : Owner.Hold(Side).Actor != null ? "Ручку держит другой игрок"
            : $"ЛКМ — взяться · мышь вниз — нажать · E — нажать · {Owner.SuccessfulPairs}/{Owner.RequiredPairs}";
    }
}
