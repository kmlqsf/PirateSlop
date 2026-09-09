using UnityEngine;
using FishNet.Object;
using PirateSlop.Networking;

namespace PirateSlop
{
    public enum BulletSurfaceKind : byte { Stone, Wood, Metal, Sand, Flesh }
    public sealed class BulletSurface : MonoBehaviour
    {
        public BulletSurfaceKind Kind;
        public bool Marks = true;
        public static void Describe(RaycastHit hit,ref FirearmShot shot)
        {
            var surface=hit.collider.GetComponentInParent<BulletSurface>();
            shot.Surface=surface!=null?surface.Kind:Guess(hit.collider);
            shot.LeaveMark=shot.Surface!=BulletSurfaceKind.Flesh && (surface==null || surface.Marks);
            var anchor=hit.collider.GetComponentInParent<NetworkObject>();
            if(anchor!=null)
            {
                shot.Anchor=anchor;
                shot.LocalEnd=anchor.transform.InverseTransformPoint(hit.point);
                shot.LocalNormal=anchor.transform.InverseTransformDirection(hit.normal);
                var ship=anchor.GetComponent<NetworkShip>();shot.ShipId=ship!=null?ship.ParticipantId.Value:0;
            }
        }
        static BulletSurfaceKind Guess(Collider collider)
        {
            if(collider.GetComponentInParent<CombatHealth>()!=null) return BulletSurfaceKind.Flesh;
            if(collider.GetComponentInParent<SimpleCannon>()!=null || collider.GetComponentInParent<Cannonball>()!=null) return BulletSurfaceKind.Metal;
            string name=collider.name.ToLowerInvariant();
            if(name.Contains("metal") || name.Contains("iron") || name.Contains("barrel") && name.Contains("cannon")) return BulletSurfaceKind.Metal;
            if(collider.GetComponentInParent<NetworkShip>()!=null || name.Contains("wood") || name.Contains("plank") || name.Contains("crate") || name.Contains("tree")) return BulletSurfaceKind.Wood;
            if(name.Contains("sand") || name.Contains("terrain") || name.Contains("beach")) return BulletSurfaceKind.Sand;
            return BulletSurfaceKind.Stone;
        }
    }
}
