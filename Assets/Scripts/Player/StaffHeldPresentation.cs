using UnityEngine;
using UnityEngine.Rendering;
using PirateSlop.Networking;

namespace PirateSlop
{
    [DefaultExecutionOrder(90)]
    public sealed class StaffHeldPresentation : MonoBehaviour
    {
        Material crystal;
        MeshRenderer crystalRenderer;
        MaterialPropertyBlock properties;
        LineRenderer arc;
        float phase;
        int theme;

        public void Configure(Transform socket, StaffSettings settings, Material arcMaterial)
        {
            theme = settings.Item == InventoryItem.FireStaff ? 0 : settings.Item == InventoryItem.IceStaff ? 1 : 2;
            phase = Random.value * 20f;
            var model = socket.parent.Find("Model");
            var source = model != null ? model.GetComponent<MeshRenderer>() : null;
            var mesh = model != null ? model.GetComponent<MeshFilter>() : null;
            if (source != null && mesh != null)
            {
                var glow = new GameObject("CrystalSurfaceGlow");
                glow.transform.SetParent(model, false);
                glow.AddComponent<MeshFilter>().sharedMesh = mesh.sharedMesh;
                crystalRenderer = glow.AddComponent<MeshRenderer>();
                crystalRenderer.shadowCastingMode = ShadowCastingMode.Off;
                crystalRenderer.receiveShadows = false;
                crystal = new Material(Resources.Load<Shader>("StaffHeldCrystal")) { hideFlags = HideFlags.HideAndDontSave };
                crystal.SetTexture("_BaseMap", source.sharedMaterial.GetTexture("_BaseMap"));
                crystal.SetFloat("_Theme", theme);
                crystalRenderer.sharedMaterial = crystal;
                properties = new MaterialPropertyBlock();
            }
            if (theme == 2)
            {
                var sparks = new GameObject("CrystalArc");
                sparks.transform.SetParent(transform, false);
                arc = sparks.AddComponent<LineRenderer>();
                arc.useWorldSpace = false;
                arc.sharedMaterial = arcMaterial;
                arc.positionCount = 9;
                arc.widthMultiplier = .003f;
                arc.numCapVertices = 2;
                arc.shadowCastingMode = ShadowCastingMode.Off;
                arc.receiveShadows = false;
            }
            Evaluate(0);
        }

        void LateUpdate() => Evaluate(Time.time + phase);

        public void Evaluate(float time)
        {
            if (crystalRenderer != null)
            {
                properties.SetMatrix("_HeadFromWorld", transform.worldToLocalMatrix);
                properties.SetFloat("_Pulse", .82f + .12f * Mathf.Sin(time * 1.7f) + .06f * Mathf.Sin(time * 4.3f));
                crystalRenderer.SetPropertyBlock(properties);
            }
            if (arc == null) return;
            float cycle = time % 2.3f;
            float strength = cycle < .18f ? Mathf.Sin(cycle / .18f * Mathf.PI) : 0f;
            arc.startColor = arc.endColor = new Color(.75f, .55f, 1f, strength * .65f);
            if (strength <= 0f) return;
            int seed = Mathf.FloorToInt(time / 2.3f);
            for (int i = 0; i < 9; i++)
            {
                float t = i / 8f;
                float envelope = Mathf.Sin(t * Mathf.PI);
                arc.SetPosition(i, new Vector3(Mathf.Lerp(-.075f, .07f, t), .015f + Mathf.Sin(i * 13.7f + seed) * .023f * envelope, -.065f + Mathf.Sin(i * 7.3f + seed) * .016f * envelope));
            }
        }

        void OnDestroy()
        {
            if (crystalRenderer != null)
            {
                if (Application.isPlaying) Destroy(crystalRenderer.gameObject); else DestroyImmediate(crystalRenderer.gameObject);
            }
            if (crystal != null)
            {
                if (Application.isPlaying) Destroy(crystal); else DestroyImmediate(crystal);
            }
        }
    }
}
