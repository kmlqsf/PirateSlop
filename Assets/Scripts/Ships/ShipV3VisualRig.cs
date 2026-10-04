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
        float applied = float.NaN;
        public void Apply(float amount)
        {
            if (Target == null || Poses == null || Poses.Length < 2) return;
            if (applied == amount) return;
            applied = amount;
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
        int[] furlShapes;
        int[][] rigShapes;
        bool[] rigHasHalf;
        float[] appliedRig;
        float appliedChain = float.NaN;
        Vector3 appliedAnchor, appliedDrum;
        ShipV3RenderBudget budget;
        float nextVisualUpdate, lastVisualUpdate;
        static readonly Unity.Profiling.ProfilerMarker marker = new("Ships.VisualRig");
        readonly System.Collections.Generic.List<Vector3> chainPoints = new(96);

        void Awake()
        {
            ship = GetComponent<NetworkShip>(); sails = GetComponent<SailSystem>();
            budget = GetComponent<ShipV3RenderBudget>(); lastVisualUpdate = Time.time;
            deploy = new float[Sails.Length];
            furlShapes = new int[Sails.Length];
            for (int i = 0; i < Sails.Length; i++)
            {
                var mesh = Sails[i] != null ? Sails[i].sharedMesh : null;
                furlShapes[i] = mesh != null ? mesh.GetBlendShapeIndex("Furled") : -1;
                if (furlShapes[i] < 0 && mesh != null) furlShapes[i] = mesh.GetBlendShapeIndex("Deployed.Furled");
            }
            int count = RigMeshes != null ? RigMeshes.Length : 0;
            rigShapes = new int[count][]; rigHasHalf = new bool[count]; appliedRig = new float[count];
            for (int i = 0; i < count; i++)
            {
                appliedRig[i] = float.NaN;
                var mesh = RigMeshes[i] != null ? RigMeshes[i].sharedMesh : null;
                if (mesh == null) continue;
                rigHasHalf[i] = mesh.GetBlendShapeIndex("Half") >= 0;
                rigShapes[i] = new int[mesh.blendShapeCount];
                for (int shape = 0; shape < mesh.blendShapeCount; shape++)
                {
                    string name = mesh.GetBlendShapeName(shape);
                    rigShapes[i][shape] = name == "Furled" ? -1 : name == "Half" ? -2 : -3;
                    if (name.StartsWith("Deploy_") && int.TryParse(name.Substring(7), out int target)) rigShapes[i][shape] = target;
                }
            }
            if (Rudder != null) rudderRest = Rudder.localRotation;
        }

        void LateUpdate()
        {
            if (ship == null || !ship.IsSpawned) return;
            if (Time.time < nextVisualUpdate) return;
            nextVisualUpdate = Time.time + (budget != null ? budget.VisualInterval : 0f);
            float delta = Mathf.Max(0f, Time.time - lastVisualUpdate);
            lastVisualUpdate = Time.time;
            using var sample = marker.Auto();
            for (int i = 0; i < deploy.Length; i++)
            {
                deploy[i] = Mathf.MoveTowards(deploy[i], sails.Tension(i), delta * 1.5f);
                if (Sails[i] != null && Sails[i].sharedMesh != null)
                {
                    int shape = furlShapes[i];
                    float weight = (1f - deploy[i]) * 100f;
                    if (shape >= 0 && Sails[i].GetBlendShapeWeight(shape) != weight) Sails[i].SetBlendShapeWeight(shape, weight);
                    bool visible = sails.Efficiency(i) > 0f;
                    if (Sails[i].enabled != visible) Sails[i].enabled = visible;
                }
            }
            if (RigMeshes != null)
                for (int i = 0; i < RigMeshes.Length; i++)
                {
                    var renderer = RigMeshes[i];
                    if (renderer == null) continue;
                    float amount = deploy[RigSailIndices[i]];
                    if (appliedRig[i] == amount || rigShapes[i] == null) continue;
                    appliedRig[i] = amount;
                    for (int shape = 0; shape < rigShapes[i].Length; shape++)
                    {
                        int kind = rigShapes[i][shape];
                        float weight = kind == -1 ? (1f - amount) * 100f : 0f;
                        if (rigHasHalf[i])
                            weight = kind == -2 ? (1f - Mathf.Abs(amount * 2f - 1f)) * 100f : kind == -1 ? Mathf.Max(0f, 1f - amount * 2f) * 100f : 0f;
                        if (kind >= 0) weight = Mathf.Max(0f, 1f - Mathf.Abs(amount * 100f - kind) / 25f) * 100f;
                        renderer.SetBlendShapeWeight(shape, weight);
                    }
                }
            float progress = ship.Capstan != null ? ship.Capstan.Progress : ship.AnchorRaiseProgress;
            paidOut = Mathf.MoveTowards(paidOut, (1f - progress) * 6f, delta * (ship.AnchorDropped ? 3.6f : 2.3f));
            foreach (var motion in Motions)
                motion.Apply(motion.SailIndex < 0 ? paidOut / 6f : deploy[motion.SailIndex]);
            if (Rudder != null) Rudder.localRotation = rudderRest * Quaternion.AngleAxis(-ship.Motor.Capture().Rudder * 32f, RudderAxis);
            UpdateChain();
        }

        void UpdateChain()
        {
            if (ChainLinks == null || ChainLinks.Length == 0 || Drum == null || AnchorEye == null || ChainRoute == null) return;
            var anchor = transform.InverseTransformPoint(AnchorEye.position);
            var drum = transform.InverseTransformPoint(Drum.position);
            if (appliedChain == paidOut && (appliedAnchor - anchor).sqrMagnitude < .000001f && (appliedDrum - drum).sqrMagnitude < .000001f) return;
            appliedChain = paidOut; appliedAnchor = anchor; appliedDrum = drum;
            float wound = Mathf.Max(.63f, 6.63f - paidOut);
            var points = chainPoints;
            points.Clear();
            int turns = 80;
            Vector3 towardOutlet = ChainRoute.Length > 0 ? ChainRoute[0] - drum : Vector3.forward;
            float distanceToOutlet = new Vector2(towardOutlet.x, towardOutlet.z).magnitude;
            float endAngle = Mathf.Atan2(towardOutlet.z, towardOutlet.x)
                + Mathf.Acos(Mathf.Clamp(DrumRadius / Mathf.Max(DrumRadius, distanceToOutlet), -1f, 1f));
            for (int i = 0; i <= turns; i++)
            {
                float distance = wound * i / turns;
                float angle = endAngle - (wound - distance) / DrumRadius;
                points.Add(drum + Vector3.right * (Mathf.Cos(angle) * DrumRadius) + Vector3.forward * (Mathf.Sin(angle) * DrumRadius)
                    + Vector3.up * (.07f + .085f * distance / (Mathf.PI * 2f * DrumRadius)));
            }
            foreach (var point in ChainRoute) points.Add(point);
            points.Add(anchor);
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
                link.SetPositionAndRotation(transform.TransformPoint(Vector3.Lerp(points[segment - 1], points[segment], (at - previous) / Mathf.Max(.001f, delta.magnitude))),
                    transform.rotation * Quaternion.LookRotation(delta.normalized, Vector3.up) * Quaternion.Euler(0, 0, i % 2 * 90f));
            }
        }
    }
}
