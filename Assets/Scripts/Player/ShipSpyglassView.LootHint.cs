using PirateSlop.Networking;
using UnityEngine;
using UnityEngine.Rendering;

namespace PirateSlop
{
    public sealed partial class ShipSpyglassView
    {
        [SerializeField, Min(.1f)] float lootBeamWidth = 1f;
        [SerializeField, Min(10f)] float lootBeamHeight = 300f;
        NetworkLootChest lootHint;
        NetworkSkullEvent skullHint;
        GameObject lootBeam;
        MeshRenderer lootBeamRenderer;
        Mesh lootBeamMesh;
        Material lootBeamMaterial;

        void UpdateLootHint()
        {
            NetworkLootChest nearest = null;
            float nearestDistance = float.PositiveInfinity;
            foreach (var chest in NetworkLootChest.ClientChests)
            {
                if (chest == null || !chest.LootHintEligible) continue;
                var offset = chest.LootHintPoint - player.transform.position;
                offset.y = 0f;
                float distance = offset.sqrMagnitude;
                if (distance >= nearestDistance) continue;
                nearestDistance = distance;
                nearest = chest;
            }
            lootHint = nearest;
            skullHint = null;
            foreach (var altar in NetworkSkullEvent.ClientEvents)
            {
                if (altar == null || !altar.LootHintEligible) continue;
                var offset = altar.LootHintPoint - player.transform.position;
                offset.y = 0f;
                if (offset.sqrMagnitude >= nearestDistance) continue;
                nearestDistance = offset.sqrMagnitude;
                skullHint = altar;
                lootHint = null;
            }
            if (lootHint == null && skullHint == null) { HideLootBeam(); return; }
            if (lootBeam == null) CreateLootBeam();
            lootBeam.transform.position = (skullHint != null ? skullHint.LootHintPoint : lootHint.LootHintPoint) - Vector3.up * 1.85f;
            lootBeam.transform.localScale = new Vector3(lootBeamWidth, lootBeamHeight, lootBeamWidth);
        }

        void CreateLootBeam()
        {
            lootBeam = new GameObject("SpyglassLootBeam");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lootBeam, gameObject.scene);
            lootBeamMesh = new Mesh { name = "LootBeamColumn" };
            var vertices = new Vector3[16];
            var uv = new Vector2[16];
            var triangles = new int[24];
            var corners = new[] { new Vector3(-.5f, 0, -.5f), new Vector3(.5f, 0, -.5f), new Vector3(.5f, 0, .5f), new Vector3(-.5f, 0, .5f) };
            for (int side = 0; side < 4; side++)
            {
                int v = side * 4, t = side * 6;
                vertices[v] = corners[side];
                vertices[v + 1] = corners[(side + 1) % 4];
                vertices[v + 2] = vertices[v + 1] + Vector3.up;
                vertices[v + 3] = vertices[v] + Vector3.up;
                uv[v] = Vector2.zero; uv[v + 1] = Vector2.right; uv[v + 2] = Vector2.one; uv[v + 3] = Vector2.up;
                triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
                triangles[t + 3] = v; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
            }
            lootBeamMesh.vertices = vertices;
            lootBeamMesh.uv = uv;
            lootBeamMesh.triangles = triangles;
            lootBeamMesh.RecalculateBounds();
            lootBeam.AddComponent<MeshFilter>().sharedMesh = lootBeamMesh;
            lootBeamMaterial = new Material(Resources.Load<Shader>("LootEventBeam"));
            lootBeamRenderer = lootBeam.AddComponent<MeshRenderer>();
            lootBeamRenderer.sharedMaterial = lootBeamMaterial;
            lootBeamRenderer.shadowCastingMode = ShadowCastingMode.Off;
            lootBeamRenderer.receiveShadows = false;
            lootBeamRenderer.allowOcclusionWhenDynamic = false;
            lootBeamRenderer.enabled = false;
        }

        void ShowLootBeam(Camera camera)
        {
            if (lootBeamRenderer == null) return;
            bool visible = player.IsOwner && !player.Motor.IsDead && !RoguelikeUpgradeUI.BlocksInput && (engaged || player.HasUpgrade(UpgradeEffect.Seeker)) && camera == player.Motor.PlayerCamera &&
                ((lootHint != null && lootHint.LootHintEligible) || (skullHint != null && skullHint.LootHintEligible));
            lootBeamRenderer.enabled = visible;
            if (!visible) return;
            Vector3 target = (skullHint != null ? skullHint.LootHintPoint : lootHint.LootHintPoint) - Vector3.up * 1.85f;
            Vector3 offset = target - camera.transform.position;
            float distance = Mathf.Max(.01f, offset.magnitude);
            float limit = Mathf.Max(camera.nearClipPlane * 4f, Mathf.Min(200f, camera.farClipPlane * .25f));
            float scale = Mathf.Min(1f, limit / distance);
            float pixelWidth = distance * 2f * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f) * 2f / Mathf.Max(1, camera.pixelHeight);
            float width = Mathf.Max(lootBeamWidth, pixelWidth) * scale;
            lootBeam.transform.position = camera.transform.position + offset * scale;
            lootBeam.transform.localScale = new Vector3(width, lootBeamHeight * scale, width);
        }

        void HideLootBeam()
        {
            if (lootBeamRenderer != null) lootBeamRenderer.enabled = false;
        }

        void DestroyLootBeam()
        {
            if (lootBeam != null) Destroy(lootBeam);
            if (lootBeamMesh != null) Destroy(lootBeamMesh);
            if (lootBeamMaterial != null) Destroy(lootBeamMaterial);
        }
    }
}
