using UnityEngine;

namespace PirateSlop
{
    [DefaultExecutionOrder(100)]
    public sealed class MenuMonkeyPose : MonoBehaviour
    {
        public Animator Pose;
        public Transform Pelvis;
        public Transform Seat;
        public float SeatOffset = .035f;
        public Transform LeftShin;
        public Transform RightShin;
        public Transform LeftFoot;
        public Transform RightFoot;
        float started;

        void OnEnable()
        {
            started = Time.unscaledTime;
            if (Pose == null) return;
            Pose.applyRootMotion = false;
            Pose.updateMode = AnimatorUpdateMode.UnscaledTime;
            Pose.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            Pose.Play("SitPerch", 0, 0);
        }

        void LateUpdate()
        {
            if (Pose == null || !Pose.isActiveAndEnabled) return;
            float time = Time.unscaledTime - started;
            float amplitude = 17 + Mathf.Sin(time * .47f) * 5;
            float left = Mathf.Sin(time * 2.5f) * amplitude;
            float right = Mathf.Sin(time * 2.35f + 2.1f) * amplitude;
            Swing(LeftShin, left);
            Swing(RightShin, right);
            Swing(LeftFoot, -left * .24f + Mathf.Sin(time * 2.5f - .4f) * 3);
            Swing(RightFoot, -right * .24f + Mathf.Sin(time * 2.35f + 1.7f) * 3);
            if (Pelvis != null && Seat != null)
                transform.position += Seat.position + Seat.up * (SeatOffset * transform.lossyScale.y) - Pelvis.position;
        }

        static void Swing(Transform bone, float angle)
        {
            if (bone != null) bone.localRotation *= Quaternion.Euler(angle, 0, 0);
        }
    }
}
