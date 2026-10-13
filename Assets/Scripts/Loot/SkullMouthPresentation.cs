using UnityEngine;
using PirateSlop.Networking;

namespace PirateSlop
{
    public sealed class SkullMouthPresentation : MonoBehaviour
    {
        GameObject sign;

        public void Initialize(NetworkSkullEvent altar)
        {
            var catalog = altar.Catalog;
            if (catalog == null || catalog.SkullInstructionSign == null) return;
            sign = Instantiate(catalog.SkullInstructionSign);
            sign.name = "SkullInstructionSign";
            sign.transform.position = altar.Mouth.position - altar.Mouth.right * 32f - Vector3.up * 4f + altar.Mouth.forward * 12f;
            var towardMouth = Vector3.ProjectOnPlane(altar.Mouth.position - sign.transform.position, Vector3.up).normalized;
            sign.transform.rotation = Quaternion.LookRotation(Vector3.Cross(towardMouth, Vector3.up), Vector3.up);
            AimArrow(altar.Mouth.position);
            sign.transform.SetParent(altar.RotatingRoot, true);
        }

        void AimArrow(Vector3 mouth)
        {
            var destination = sign.transform.InverseTransformPoint(mouth);
            var tip = new Vector3(3.8f, 1.7f, .35f);
            float originalAngle = Mathf.Atan2(1.155f, 1.115f) * Mathf.Rad2Deg;
            float angle = 0f;
            for (int i = 0; i < 5; i++)
            {
                var turnedTip = Quaternion.AngleAxis(angle, Vector3.forward) * tip;
                var direction = destination - turnedTip;
                angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - originalAngle;
            }
            var turn = Quaternion.AngleAxis(angle, sign.transform.forward);
            foreach (var part in sign.GetComponentsInChildren<Transform>())
            {
                if (part.name != "CurvedArrow" && part.name != "ArrowHead") continue;
                part.SetParent(sign.transform, true);
                part.position = sign.transform.position + turn * (part.position - sign.transform.position);
                part.rotation = turn * part.rotation;
            }
        }

        public void Present(bool active)
        {
            if (sign != null && sign.activeSelf != active) sign.SetActive(active);
        }

        void OnDestroy()
        {
            if (sign != null) Destroy(sign);
        }
    }
}
