using PirateSlop.Harpoon;
using UnityEngine;

namespace PirateSlop.Ships
{
    [DefaultExecutionOrder(50)]
    public sealed class ShipV3HarpoonVisual : MonoBehaviour
    {
        public HarpoonGun Gun;
        public Transform Reel, ReelExit, Guide, Knot;
        public Vector3 ReelAxis = Vector3.right;
        public float Radius = .13f;
        public LineRenderer Rope;
        Quaternion reelRest;
        float length, angle;
        void Awake() { if (Reel != null) reelRest = Reel.localRotation; }
        void LateUpdate()
        {
            if (Gun == null || Reel == null || ReelExit == null || Guide == null || Rope == null) return;
            var projectile = Gun.ActiveProjectile;
            Vector3 end = projectile != null ? projectile.KnotPosition : Knot.position;
            float next = projectile != null ? (projectile.IsAttached ? projectile.CurrentCableLength : Vector3.Distance(Guide.position, end)) : 0f;
            angle += (next - length) / Mathf.Max(.02f, Radius) * Mathf.Rad2Deg;
            length = next;
            Reel.localRotation = reelRest * Quaternion.AngleAxis(angle, ReelAxis);
            int count = 32;
            Rope.positionCount = count;
            for (int i = 0; i < 8; i++) Rope.SetPosition(i, Vector3.Lerp(ReelExit.position, Guide.position, i / 7f));
            float distance = Vector3.Distance(Guide.position, end);
            float sag = projectile != null && projectile.IsAttached ? Mathf.Clamp((next - distance) * .25f, .03f, 4f) : projectile != null ? .12f : .01f;
            for (int i = 8; i < count; i++)
            {
                float t = (i - 8f) / (count - 9f);
                Rope.SetPosition(i, Vector3.Lerp(Guide.position, end, t) - Vector3.up * Mathf.Sin(t * Mathf.PI) * sag);
            }
        }
    }
}
