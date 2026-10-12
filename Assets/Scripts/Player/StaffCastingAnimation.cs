using PirateSlop.Networking;
using UnityEngine;

namespace PirateSlop
{
    public static class StaffCastingAnimation
    {
        public struct Pose
        {
            public Vector3 Position, Rotation, Support, Wrist;
        }
        public static Vector3 WorldPosition(Pose pose) => new Vector3(.22f, 1.04f, .34f) + pose.Position * .6f;

        public static Pose FirstPerson(Pose pose)
        {
            pose.Position *= .3f;
            pose.Position.z = Mathf.Abs(pose.Position.z) * 3f;
            pose.Rotation *= .3f;
            return pose;
        }
        public static Pose Idle => new Pose { Support = new Vector3(0, -.22f, 0) };
        static float Ease(float value) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(value));
        public static Pose Blend(Pose from, Pose to, float weight) => new Pose
        {
            Position = Vector3.Lerp(from.Position, to.Position, weight),
            Rotation = Vector3.Lerp(from.Rotation, to.Rotation, weight),
            Support = Vector3.Lerp(from.Support, to.Support, weight),
            Wrist = Vector3.Lerp(from.Wrist, to.Wrist, weight)
        };
        public static Pose Charge(InventoryItem item, float progress)
        {
            progress = Mathf.Clamp01(progress);
            float draw = Ease(progress / .22f), gather = Ease((progress - .22f) / .63f);
            Pose pose = Idle;
            if (item == InventoryItem.FireStaff)
            {
                float arc = Mathf.Clamp01((progress - .22f) / .63f) * Mathf.PI * 2f;
                pose.Position = new Vector3(-.1f, .06f, -.1f) * draw + new Vector3(-.025f, .045f, .035f) * gather;
                pose.Position += new Vector3(Mathf.Sin(arc) * .025f, (1f - Mathf.Cos(arc)) * .018f, 0);
                pose.Rotation = new Vector3(-22f, 12f, -15f) * draw + new Vector3(-7f, -12f, 12f) * gather;
                pose.Support = Vector3.Lerp(pose.Support, new Vector3(-.2f, .05f, .12f), draw);
                pose.Support += new Vector3(Mathf.Sin(arc) * .035f, (1f - Mathf.Cos(arc)) * .035f, 0);
                pose.Wrist = new Vector3(-18f, 0, 22f) * draw;
            }
            else if (item == InventoryItem.LightningStaff)
            {
                pose.Position = new Vector3(-.055f, .16f, -.12f) * draw + new Vector3(-.025f, .04f, .03f) * gather;
                pose.Rotation = new Vector3(-12f, -18f, -5f) * draw + new Vector3(-7f, 10f, 10f) * gather;
                pose.Support = Vector3.Lerp(pose.Support, new Vector3(-.18f, .16f, .07f), draw);
                pose.Wrist = new Vector3(-28f, 0, 30f) * draw;
            }
            else
            {
                pose.Position = new Vector3(-.08f, .06f, -.08f) * draw + new Vector3(-.02f, .035f, .02f) * gather;
                pose.Rotation = new Vector3(-14f, -8f, 10f) * draw + new Vector3(-9f, 12f, -8f) * gather;
                pose.Support = Vector3.Lerp(pose.Support, new Vector3(-.10f, -.13f, .08f), draw);
                pose.Wrist = new Vector3(-8f, 0, 12f) * draw;
            }
            return pose;
        }
        public static Pose Release(InventoryItem item, float elapsed, bool beam)
        {
            if (elapsed < 0) return Idle;
            Pose ready = Charge(item, 1f), stroke = Idle;
            if (item == InventoryItem.IceStaff && beam)
            {
                stroke.Position = new Vector3(-.06f, .015f, .065f);
                stroke.Rotation = new Vector3(-16f, 1f, 2f);
                stroke.Support = new Vector3(-.015f, -.19f, .025f);
                return Blend(ready, stroke, Ease(elapsed / .22f));
            }
            if (item == InventoryItem.IceStaff) return Idle;
            bool lightning = item == InventoryItem.LightningStaff;
            float strikeSeconds = lightning ? .14f : .11f;
            stroke.Position = lightning ? new Vector3(-.02f, -.1f, .08f) : new Vector3(-.04f, -.015f, .1f);
            stroke.Rotation = lightning ? new Vector3(23f, 8f, -13f) : new Vector3(9f, -4f, -9f);
            stroke.Support = lightning ? new Vector3(-.18f, .01f, .08f) : new Vector3(-.16f, -.05f, .12f);
            if (elapsed < strikeSeconds) return Blend(ready, stroke, Ease(elapsed / strikeSeconds));
            return Blend(stroke, Idle, Ease((elapsed - strikeSeconds) / (lightning ? .5f : .4f)));
        }
    }
}
