using System;
using UnityEngine;
using PirateSlop.Networking;

namespace PirateSlop.Ships
{
    [Serializable]
    public struct ShipV3Pose
    {
        public Vector3 Position, Scale;
        public Quaternion Rotation;
    }

    [Serializable]
    public sealed class ShipV3Motion
    {
        public Transform Target;
        public int SailIndex;
        public ShipV3Pose[] Poses;
        public void Apply(float amount)
        {
            if (Target == null || Poses == null || Poses.Length < 2) return;
            float frame = Mathf.Clamp01(amount) * (Poses.Length - 1);
            int index = Mathf.Min(Mathf.FloorToInt(frame), Poses.Length - 2);
            float blend = frame - index;
            Target.localPosition = Vector3.Lerp(Poses[index].Position, Poses[index + 1].Position, blend);
            Target.localRotation = Quaternion.Slerp(Poses[index].Rotation, Poses[index + 1].Rotation, blend);
            Target.localScale = Vector3.Lerp(Poses[index].Scale, Poses[index + 1].Scale, blend);
        }
    }

    [DefaultExecutionOrder(40)]
    public sealed class ShipV3VisualRig : MonoBehaviour
    {
        public SkinnedMeshRenderer[] Sails;
        public SkinnedMeshRenderer[] RigMeshes;
        public int[] RigSailIndices;
        public ShipV3Motion[] Motions;
        public Transform Rudder;
        public Vector3 RudderAxis = Vector3.up;
        public Transform[] ChainLinks;
        public Transform ChainHawse, AnchorEye, Drum;
        public Vector3[] ChainRoute;
        public float ChainLength = 12.346f, DrumRadius = .441f;
        SailSystem sails;
        NetworkShip ship;
        Quaternion rudderRest;
        float[] deploy;
        float paidOut;
        readonly System.Collections.Generic.List<Vector3> chainPoints = new(96);

        void Awake()
        {
            ship = GetComponent<NetworkShip>(); sails = GetComponent<SailSystem>();
            deploy = new float[Sails.Length];
            if (Rudder != null) rudderRest = Rudder.localRotation;
        }

        void LateUpdate()
        {
            if (ship == null || !ship.IsSpawned) return;
            for (int i = 0; i < deploy.Length; i++)
            {
                deploy[i] = Mathf.MoveTowards(deploy[i], sails.Tension(i), Time.deltaTime * 1.5f);
                if (Sails[i] != null && Sails[i].sharedMesh != null)
                {
                    int shape = Sails[i].sharedMesh.GetBlendShapeIndex("Furled");
                    if (shape < 0) shape = Sails[i].sharedMesh.GetBlendShapeIndex("Deployed.Furled");
                    if (shape >= 0) Sails[i].SetBlendShapeWeight(shape, (1f - deploy[i]) * 100f);
                    Sails[i].enabled = sails.Efficiency(i) > 0f;
                }
            }
            if (RigMeshes != null)
                for (int i = 0; i < RigMeshes.Length; i++)
                {
                    var renderer = RigMeshes[i];
                    if (renderer == null) continue;
                    float amount = deploy[RigSailIndices[i]];
                    var mesh = renderer.sharedMesh;
                    for (int shape = 0; shape < mesh.blendShapeCount; shape++)
                    {
                        string name = mesh.GetBlendShapeName(shape);
                        float weight = name == "Furled" ? (1f - amount) * 100f : 0f;
                        if (mesh.GetBlendShapeIndex("Half") >= 0)
                            weight = name == "Half" ? (1f - Mathf.Abs(amount * 2f - 1f)) * 100f : name == "Furled" ? Mathf.Max(0f, 1f - amount * 2f) * 100f : 0f;
                        if (name.StartsWith("Deploy_") && int.TryParse(name.Substring(7), out int target))
                            weight = Mathf.Max(0f, 1f - Mathf.Abs(amount * 100f - target) / 25f) * 100f;
                        renderer.SetBlendShapeWeight(shape, weight);
                    }
                }
            float progress = ship.Capstan != null ? ship.Capstan.Progress : ship.AnchorRaiseProgress;
            paidOut = Mathf.MoveTowards(paidOut, (1f - progress) * 6f, Time.deltaTime * (ship.AnchorDropped ? 3.6f : 2.3f));
            foreach (var motion in Motions)
                motion.Apply(motion.SailIndex < 0 ? paidOut / 6f : deploy[motion.SailIndex]);
            if (Rudder != null) Rudder.localRotation = rudderRest * Quaternion.AngleAxis(-ship.Motor.Capture().Rudder * 32f, RudderAxis);
            UpdateChain();
        }

        void UpdateChain()
        {
            if (ChainLinks == null || ChainLinks.Length == 0 || Drum == null || AnchorEye == null || ChainRoute == null) return;
            float wound = Mathf.Max(.63f, 6.63f - paidOut);
            var points = chainPoints;
            points.Clear();
            int turns = 80;
            Vector3 towardOutlet = ChainRoute.Length > 0 ? transform.TransformPoint(ChainRoute[0]) - Drum.position : transform.forward;
            float distanceToOutlet = new Vector2(Vector3.Dot(towardOutlet, transform.right), Vector3.Dot(towardOutlet, transform.forward)).magnitude;
            float endAngle = Mathf.Atan2(Vector3.Dot(towardOutlet, transform.forward), Vector3.Dot(towardOutlet, transform.right))
                + Mathf.Acos(Mathf.Clamp(DrumRadius / Mathf.Max(DrumRadius, distanceToOutlet), -1f, 1f));
            for (int i = 0; i <= turns; i++)
            {
                float distance = wound * i / turns;
                float angle = endAngle - (wound - distance) / DrumRadius;
                points.Add(Drum.position + transform.right * (Mathf.Cos(angle) * DrumRadius) + transform.forward * (Mathf.Sin(angle) * DrumRadius)
                    + transform.up * (.07f + .085f * distance / (Mathf.PI * 2f * DrumRadius)));
            }
            foreach (var point in ChainRoute) points.Add(transform.TransformPoint(point));
            points.Add(AnchorEye.position);
            float length = 0f;
            for (int i = 1; i < points.Count; i++) length += Vector3.Distance(points[i - 1], points[i]);
            float at = 0f;
            int segment = 1;
            float previous = 0f;
            for (int i = 0; i < ChainLinks.Length; i++)
            {
                at = i * .085f + Mathf.Repeat(paidOut, .085f);
                var link = ChainLinks[i];
                if (link == null) continue;
                bool visible = at <= length;
                if (link.gameObject.activeSelf != visible) link.gameObject.SetActive(visible);
                if (!visible) continue;
                while (segment < points.Count - 1 && previous + Vector3.Distance(points[segment - 1], points[segment]) < at)
                { previous += Vector3.Distance(points[segment - 1], points[segment]); segment++; }
                Vector3 delta = points[segment] - points[segment - 1];
                link.SetPositionAndRotation(Vector3.Lerp(points[segment - 1], points[segment], (at - previous) / Mathf.Max(.001f, delta.magnitude)),
                    Quaternion.LookRotation(delta.normalized, transform.up) * Quaternion.Euler(0, 0, i % 2 * 90f));
            }
        }
    }
}
