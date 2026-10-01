using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkLootChest
    {
        readonly Transform[] sunkenGulls = new Transform[2];
        readonly Transform[] gullWings = new Transform[4];
        Material gullWhite, gullDark;
        Mesh gullWingMesh;

        void CreateSunkenGulls()
        {
            gullWhite = new Material(markerMaterial);
            gullWhite.SetColor("_BaseColor", new Color(.92f, .94f, .93f));
            gullDark = new Material(markerMaterial);
            gullDark.SetColor("_BaseColor", new Color(.18f, .21f, .23f));
            gullWingMesh = new Mesh { name = "SeagullWing" };
            gullWingMesh.vertices = new[] { Vector3.zero, new Vector3(.45f, 0, .16f), new Vector3(1.05f, 0, -.02f), new Vector3(.9f, 0, -.2f), new Vector3(.25f, 0, -.23f) };
            gullWingMesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 0, 3, 4 };
            gullWingMesh.RecalculateNormals();
            gullWingMesh.RecalculateBounds();
            for (int i = 0; i < sunkenGulls.Length; i++)
            {
                var bird = new GameObject("SunkenChestSeagull").transform;
                bird.SetParent(eventVisual.transform, false);
                sunkenGulls[i] = bird;
                GullPart(bird, "Body", new Vector3(0, 0, 0), new Vector3(.23f, .2f, .65f), gullWhite);
                GullPart(bird, "Head", new Vector3(0, .09f, .3f), Vector3.one * .19f, gullWhite);
                GullPart(bird, "Beak", new Vector3(0, .06f, .42f), new Vector3(.065f, .06f, .18f), gullDark);
                GullPart(bird, "Tail", new Vector3(0, 0, -.37f), new Vector3(.22f, .04f, .27f), gullDark);
                for (int side = 0; side < 2; side++)
                {
                    var wing = new GameObject("Wing").transform;
                    wing.SetParent(bird, false);
                    wing.localScale = new Vector3(side == 0 ? -1 : 1, 1, 1);
                    wing.gameObject.AddComponent<MeshFilter>().sharedMesh = gullWingMesh;
                    wing.gameObject.AddComponent<MeshRenderer>().sharedMaterial = gullWhite;
                    gullWings[i * 2 + side] = wing;
                }
            }
            UpdateSunkenGulls();
        }

        void GullPart(Transform parent, string label, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            part.name = label;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            var collider = part.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
        }

        void UpdateSunkenGulls()
        {
            float time = OceanSurface.Instance != null ? OceanSurface.Instance.WaveTime : Time.time;
            for (int i = 0; i < sunkenGulls.Length; i++)
            {
                var bird = sunkenGulls[i];
                if (bird == null) continue;
                bool show = phase.Value != SeaLootState.Ready;
                bird.gameObject.SetActive(show);
                if (!show) continue;
                float angle = time * (.38f - i * .05f) + i * Mathf.PI + eventPoint.Value.x * .01f;
                float radius = 6f + i * 2f;
                var center = eventPoint.Value;
                if (OceanSurface.Instance != null) center.y = OceanSurface.Instance.Height(center);
                bird.position = center + new Vector3(Mathf.Cos(angle) * radius, 10f + i * 2f + Mathf.Sin(time * .7f + i) * .6f, Mathf.Sin(angle) * radius);
                bird.rotation = Quaternion.LookRotation(new Vector3(-Mathf.Sin(angle), 0, Mathf.Cos(angle))) * Quaternion.Euler(0, 0, -12f);
                float flap = Mathf.Sin(time * 5f + i * 2f) * 18f;
                gullWings[i * 2].localRotation = Quaternion.Euler(0, 0, -flap);
                gullWings[i * 2 + 1].localRotation = Quaternion.Euler(0, 0, flap);
            }
        }
    }
}
