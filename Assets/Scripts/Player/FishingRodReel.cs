using System.Collections.Generic;
using UnityEngine;

namespace PirateSlop
{
    [DisallowMultipleComponent]
    public sealed class FishingRodReel : MonoBehaviour
    {
        public Material LineMaterial;
        Transform spool;
        Quaternion spoolRest;
        Vector3[] guidePoints;
        FishingRodBend rodBend;
        LineRenderer line;
        float speed, angle;

        void Awake()
        {
            Transform exit = null;
            var guides = new List<Transform>();
            foreach (var part in GetComponentsInChildren<Transform>(true))
            {
                if (part.name == "ReelSpoolPivot") spool = part;
                else if (part.name == "ReelLineExit") exit = part;
                else if (part.name.StartsWith("LineGuide_", System.StringComparison.Ordinal)) guides.Add(part);
            }
            if (spool != null) spoolRest = spool.localRotation;
            guides.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            if (exit == null || guides.Count == 0 || LineMaterial == null) return;
            guidePoints = new Vector3[guides.Count + 1];
            guidePoints[0] = transform.InverseTransformPoint(exit.position);
            for (int i = 0; i < guides.Count; i++) guidePoints[i + 1] = transform.InverseTransformPoint(guides[i].position);
            var go = new GameObject("ReelGuideLine");
            go.transform.SetParent(transform, false);
            line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = LineMaterial;
            line.useWorldSpace = true;
            line.positionCount = guidePoints.Length;
            line.startWidth = line.endWidth = .0018f;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            UpdateLine();
        }

        public void Present(byte phase, bool winding, float delta)
        {
            float wanted = phase == 1 ? -720f : phase == 4 && winding ? 540f : 0f;
            speed = Mathf.MoveTowards(speed, wanted, delta * 5000f);
            angle = Mathf.Repeat(angle + speed * delta, 360f);
            if (spool != null) spool.localRotation = spoolRest * Quaternion.AngleAxis(angle, Vector3.right);
            UpdateLine();
        }

        void LateUpdate() => UpdateLine();

        void UpdateLine()
        {
            if (line == null || guidePoints == null) return;
            if (rodBend == null) rodBend = GetComponent<FishingRodBend>();
            for (int i = 0; i < guidePoints.Length; i++)
                line.SetPosition(i, rodBend != null ? rodBend.BendPoint(guidePoints[i]) : transform.TransformPoint(guidePoints[i]));
        }

        void OnDisable() => speed = 0f;
    }
}
