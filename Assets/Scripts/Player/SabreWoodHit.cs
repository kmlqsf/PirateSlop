using UnityEngine;
using FishNet.Object;
using PirateSlop.Networking;
using PirateSlop.Ships;

namespace PirateSlop
{
    public struct SabreWoodHit
    {
        public NetworkObject Anchor;
        public int ShipId, SectionId;
        public Vector3 Point, Normal, Tangent;
    }

    public sealed partial class PirateWeapon
    {
        bool sabreWoodStruck;
        Vector3 sabreEyeLocal;
        void StrikeSabreWood(Vector3 origin)
        {
            if (sabreWoodStruck) return;
            Vector3 eye = transform.TransformPoint(sabreEyeLocal);
            Vector3 bodyEye = transform.position + Vector3.up * (motor.IsCrouched ? .75f : 1.65f);
            if (FirearmTrace.Cast(gameObject, bodyEye, eye, out var eyeBlock))
                eye = eyeBlock.point + (bodyEye - eye).normalized * .02f;
            Vector3 direction = attackDirection.normalized;
            if (!FirearmTrace.Cast(gameObject, eye, eye + direction * (Vector3.Distance(eye, origin) + 2.4f), out var contact)) return;
            if (Vector3.Distance(origin, contact.point) > 2.4f) return;
            if (FirearmTrace.Cast(gameObject, bodyEye, contact.point - contact.normal * .005f, out var obstruction) && (obstruction.point - contact.point).sqrMagnitude > .0025f) return;
            var vortex = contact.collider.GetComponentInParent<NetworkVortexBottle>();
            if (vortex != null) { sabreWoodStruck = vortex.TryBreakFromWeapon(contact.point); return; }
            var fog = contact.collider.GetComponentInParent<NetworkFogBottle>();
            if (fog != null) { sabreWoodStruck = fog.TryBreakFromWeapon(contact.point); return; }
            var section = ShipV3CollisionBatch.ResolveSection(contact.collider, contact.point);
            var ship = contact.collider.GetComponentInParent<NetworkShip>();
            if (section == null || ship == null) return;
            var surface = contact.collider.GetComponentInParent<BulletSurface>();
            if (surface != null && surface.Kind != BulletSurfaceKind.Wood) return;
            Vector3 tangent = Vector3.ProjectOnPlane(Vector3.Cross(Vector3.up, direction) + Vector3.up * .3f, contact.normal).normalized;
            if (tangent.sqrMagnitude < .001f) tangent = Vector3.ProjectOnPlane(transform.up, contact.normal).normalized;
            var impact = new SabreWoodHit
            {
                Anchor = ship.NetworkObject, ShipId = ship.ParticipantId.Value, SectionId = section.SectionId,
                Point = ship.transform.InverseTransformPoint(contact.point),
                Normal = ship.transform.InverseTransformDirection(contact.normal),
                Tangent = ship.transform.InverseTransformDirection(tangent)
            };
            sabreWoodStruck = true;
            if (Networked && network.IsServerInitialized) network.PublishSabreWood(impact);
            else if (!Networked) SabreWoodImpact.Present(impact);
        }
    }
}
