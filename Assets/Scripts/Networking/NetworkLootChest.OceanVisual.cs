using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkLootChest
    {
        void CreateOceanVisual()
        {
            visualCreated = true;
            chestCollider = GetComponent<Collider>();
            chestRenderers = GetComponentsInChildren<Renderer>(true);
            eventVisual = new GameObject("SeaLootObjective");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(eventVisual, gameObject.scene);
            eventVisual.transform.position = eventPoint.Value;
            var wood = chestRenderers[0].sharedMaterial;
            markerMaterial = new Material(wood);
            markerMaterial.color = new Color(1, .7f, .15f);
            if (markerMaterial.HasProperty("_BaseColor")) markerMaterial.SetColor("_BaseColor", new Color(1, .7f, .15f));
            if (Kind == SeaLootKind.Raft)
            {
                for (int i = 0; i < 7; i++)
                    Piece("RaftLog", PrimitiveType.Cube, new Vector3((i - 3) * .55f, .2f, 0), new Vector3(.52f, .45f, 4), wood, true);
                Piece("Crossbeam", PrimitiveType.Cube, new Vector3(0, -.08f, -1.2f), new Vector3(4, .2f, .25f), wood, false);
                Piece("Crossbeam", PrimitiveType.Cube, new Vector3(0, -.08f, 1.2f), new Vector3(4, .2f, .25f), wood, false);
            }
            else if (Kind == SeaLootKind.Capture)
            {
                ring = eventVisual.AddComponent<LineRenderer>();
                ring.sharedMaterial = markerMaterial;
                ring.useWorldSpace = false;
                ring.loop = true;
                ring.widthMultiplier = .7f;
                ring.positionCount = 128;
                for (int i = 0; i < ring.positionCount; i++)
                {
                    float angle = i * Mathf.PI * 2 / ring.positionCount;
                    ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * Catalog.CaptureRadius, .4f, Mathf.Sin(angle) * Catalog.CaptureRadius));
                }
            }
            else if (Kind == SeaLootKind.Sunken)
            {
                Piece("Buoy", PrimitiveType.Cylinder, new Vector3(0, .3f, 0), new Vector3(.7f, .6f, .7f), markerMaterial, false);
                rope = eventVisual.AddComponent<LineRenderer>();
                rope.sharedMaterial = wood;
                rope.useWorldSpace = false;
                rope.widthMultiplier = .06f;
                rope.positionCount = 2;
                rope.SetPosition(0, Vector3.zero);
                rope.SetPosition(1, Vector3.down * Mathf.Max(3, Catalog.SunkenDepth));
            }
        }

        void Piece(string label, PrimitiveType shape, Vector3 position, Vector3 scale, Material material, bool solid)
        {
            var piece = GameObject.CreatePrimitive(shape);
            piece.name = label;
            piece.transform.SetParent(eventVisual.transform, false);
            piece.transform.localPosition = position;
            piece.transform.localScale = scale;
            piece.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) { piece.GetComponent<Collider>().enabled = false; Destroy(piece.GetComponent<Collider>()); }
        }

        void OnGUI()
        {
            if (!IsSpawned || Kind == SeaLootKind.None || SessionController.MenuOpen) return;
            var camera = Camera.main;
            if (camera == null || !camera.isActiveAndEnabled) return;
            var viewer = camera.GetComponentInParent<NetworkPlayer>();
            if (viewer == null || !viewer.IsOwner || viewer.Motor.IsDead) return;
            Vector3 point = phase.Value == SeaLootState.Ready ? transform.position : eventPoint.Value;
            float distance = Vector3.Distance(camera.transform.position, point);
            if (distance > 1200 || (phase.Value == SeaLootState.Ready && distance > 100)) return;
            Vector3 screen = camera.WorldToScreenPoint(point + Vector3.up * 4);
            if (screen.z <= 0) return;
            string label = phase.Value == SeaLootState.Ready ? "Ящик с припасами" : Kind == SeaLootKind.Capture ? "Зона захвата" : Kind == SeaLootKind.Raft ? "Плот с припасами" : "Подводный тайник";
            if (Kind == SeaLootKind.Capture && phase.Value == SeaLootState.Locked)
                label += $" · {Mathf.RoundToInt(Progress * 100)}%" + (contested.Value ? " · оспаривается" : captureShip.Value != 0 ? $" · корабль {captureShip.Value}" : "");
            PirateHudStyle.Panel(new Rect(screen.x - 170, Screen.height - screen.y, 340, 28), $"{label} · {Mathf.RoundToInt(distance)} м");
        }
    }
}
