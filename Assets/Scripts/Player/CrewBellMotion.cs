using UnityEngine;

namespace PirateSlop
{
    public sealed class CrewBellMotion : MonoBehaviour
    {
        public PirateSlop.Networking.NetworkCrewBell Holder;
        Transform shell, clapper, ball, rope, knot;
        Vector3 shellRest, clapperRest, ballRest, ropeRest, knotRest, ropeScale;
        Quaternion shellRotation, clapperRotation, ballRotation, ropeRotation;
        float pull, target, rang = -20f;
        BoxCollider gripCollider;
        public Vector3 GripPoint => knot != null ? knot.position : transform.TransformPoint(new Vector3(0, -.51f, 0));
        void Awake()
        {
            foreach (var child in GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "BellShell") shell = child;
                if (child.name == "BellClapper") clapper = child;
                if (child.name == "BellClapperBall") ball = child;
                if (child.name == "BellPullRope") rope = child;
                if (child.name == "BellRopeKnot") knot = child;
            }
            var grip = transform.Find("BellRopeGrip");
            if (grip != null) gripCollider = grip.GetComponent<BoxCollider>();
            if (shell != null) { shellRest = shell.localPosition; shellRotation = shell.localRotation; }
            if (clapper != null) { clapperRest = clapper.localPosition; clapperRotation = clapper.localRotation; }
            if (ball != null) { ballRest = ball.localPosition; ballRotation = ball.localRotation; }
            if (rope != null) { ropeRest = rope.localPosition; ropeScale = rope.localScale; ropeRotation = rope.localRotation; }
            if (knot != null) knotRest = knot.localPosition;
        }
        public void SetPull(float value) { target = Mathf.Clamp01(value); }
        public void Ring() { target = 0; rang = Time.time; }
        void LateUpdate()
        {
            pull = Mathf.Lerp(pull, target, 1 - Mathf.Exp(-18 * Time.deltaTime));
            float age = Time.time - rang;
            float swing = Mathf.Sin(age * 12) * Mathf.Exp(-age * 1.8f) * 24f;
            float angle = -pull * 18 + swing;
            Rotate(shell, shellRest, shellRotation, new Vector3(0, .22f, 0), angle);
            float clapperAngle = angle + Mathf.Sin(age * 12 - .8f) * Mathf.Exp(-age * 2f) * 18;
            Rotate(clapper, clapperRest, clapperRotation, new Vector3(0, .16f, 0), clapperAngle);
            Rotate(ball, ballRest, ballRotation, new Vector3(0, .16f, 0), clapperAngle);
            float down = pull * .38f;
            float sway = Mathf.Sin(age * 10) * Mathf.Exp(-age * 2f) * .07f;
            if (rope != null)
            {
                rope.localPosition = ropeRest + new Vector3(sway * .5f, -down * .5f, 0);
                rope.localScale = new Vector3(ropeScale.x, ropeScale.y, ropeScale.z * (1 + down / .36f));
                rope.localRotation = Quaternion.Euler(0, 0, -sway * 80) * ropeRotation;
            }
            if (knot != null) knot.localPosition = knotRest + new Vector3(sway, -down, 0);
            if (gripCollider != null) { gripCollider.center = new Vector3(sway * .5f, -down * .5f, 0); gripCollider.size = new Vector3(.19f, .38f + down, .19f); }
        }
        static void Rotate(Transform part, Vector3 rest, Quaternion rotation, Vector3 pivot, float angle)
        {
            if (part == null) return;
            var q = Quaternion.Euler(angle, 0, 0);
            part.localPosition = pivot + q * (rest - pivot); part.localRotation = q * rotation;
        }
    }
}
