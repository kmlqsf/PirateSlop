using UnityEngine;

namespace PirateSlop
{
    public sealed class RepairReveal : MonoBehaviour
    {
        MeshRenderer target;
        Mesh mesh;
        Material[] materials;
        float started;
        bool hiddenBefore;
        public static void Show(GameObject root)
        {
            if (!Application.isPlaying || root == null || !root.activeInHierarchy) return;
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null || renderer.GetComponent<RepairReveal>() != null) continue;
                var effect = renderer.gameObject.AddComponent<RepairReveal>();
                effect.target = renderer; effect.mesh = filter.sharedMesh; effect.materials = renderer.sharedMaterials;
                effect.hiddenBefore = renderer.forceRenderingOff; effect.started = Time.time;
                renderer.forceRenderingOff = true;
            }
        }
        void LateUpdate()
        {
            if (target == null || !target.gameObject.activeInHierarchy || Time.time - started >= .24f) { Destroy(this); return; }
            if (hiddenBefore) return;
            float scale = Mathf.Lerp(.82f,1f,Mathf.SmoothStep(0,1,(Time.time-started)/.24f));
            Vector3 center = mesh.bounds.center;
            var matrix = transform.localToWorldMatrix * Matrix4x4.Translate(center) * Matrix4x4.Scale(Vector3.one * scale) * Matrix4x4.Translate(-center);
            for (int i=0;i<Mathf.Min(mesh.subMeshCount,materials.Length);i++)
                Graphics.DrawMesh(mesh,matrix,materials[i],gameObject.layer,null,i,null,UnityEngine.Rendering.ShadowCastingMode.Off,false);
        }
        void OnDisable() { if (target != null) target.forceRenderingOff = hiddenBefore; }
        void OnDestroy() { if (target != null) target.forceRenderingOff = hiddenBefore; }
    }
}
