using UnityEngine;

namespace PirateSlop
{
    public sealed class SeaSignalRig : MonoBehaviour
    {
        public Transform Flag, Carriage, Counterweight, Winch, Contest;
        public float Low = 1.35f, High = 6.55f, Head = 8f;
        public Material Hemp;
        SeaRopeVisual halyard;
        readonly Vector3[] path = new Vector3[5];
        float previousProgress = -1f;

        public void Show(float progress, bool contested)
        {
            var position = Flag.localPosition;
            position.y = Mathf.Lerp(Low, High, progress);
            Flag.localPosition = position;
            Carriage.localPosition = new Vector3(0, position.y, 0);
            Counterweight.localPosition = new Vector3(-.24f, Mathf.Lerp(High, Low, progress), 0);
            Winch.localRotation = Quaternion.Euler(progress * 1080f, 0, 90f);
            if (Contest != null) Contest.gameObject.SetActive(contested);
            if (halyard == null)
            {
                var root = new GameObject("WorkingHalyard"); root.transform.SetParent(transform, false);
                halyard = root.AddComponent<SeaRopeVisual>(); root.GetComponent<MeshRenderer>().sharedMaterial = Hemp;
            }
            if (Mathf.Abs(progress - previousProgress) < .0005f) return;
            path[0] = Winch.localPosition + Vector3.right * .16f; path[1] = new Vector3(.24f, Head, 0);
            path[2] = new Vector3(-.24f, Head, 0); path[3] = Counterweight.localPosition + Vector3.up * .15f;
            path[4] = Counterweight.localPosition;
            halyard.SetPath(path, path.Length, .022f);
            previousProgress = progress;
        }

        public void Release()
        {
            if (halyard != null) halyard.gameObject.SetActive(false);
            Flag.gameObject.SetActive(false); Carriage.gameObject.SetActive(false);
            Counterweight.gameObject.SetActive(false); Winch.gameObject.SetActive(false);
            if (Contest != null) Contest.gameObject.SetActive(false);
        }
    }
}
