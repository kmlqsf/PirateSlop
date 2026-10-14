using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkLootChest
    {
        SeaEventArt seaArt;

        void CreateOceanVisual()
        {
            visualCreated = true;
            chestCollider = GetComponent<Collider>();
            chestRenderers = GetComponentsInChildren<Renderer>(true);
            if (Kind == SeaLootKind.Capture) captureChestColliders = GetComponentsInChildren<Collider>(true);
            eventVisual = new GameObject("SeaLootObjective");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(eventVisual, gameObject.scene);
            eventVisual.transform.position = eventPoint.Value;
            seaArt = Resources.Load<SeaEventArt>("SeaEvents/SeaEventArt");
            if (Kind == SeaLootKind.Raft)
            {
                raftPoseKnown = false;
                raftBody = eventVisual.AddComponent<Rigidbody>();
                raftBody.isKinematic = true;
                raftBody.useGravity = false;
                eventVisual.AddComponent<RaftPlatform>().Chest = this;
                Instantiate(seaArt.Raft, eventVisual.transform, false);
            }
            else if (Kind == SeaLootKind.Capture)
            {
                CreateCaptureStation();
            }
            else if (Kind == SeaLootKind.Sunken)
            {
                CreateSunkenRope();
                CreateSunkenGulls();
            }
            else if (Kind == SeaLootKind.Shark)
            {
                swarm = eventVisual.AddComponent<SharkSwarmVisual>();
                swarm.Initialize(this);
            }
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (!DeveloperMenu.Available || !DeveloperMenu.ShowLootEventLabels) return;
            if (!IsSpawned || Kind == SeaLootKind.None || SessionController.MenuOpen || InTow) return;
            var camera = Camera.main;
            if (camera == null || !camera.isActiveAndEnabled) return;
            var viewer = camera.GetComponentInParent<NetworkPlayer>();
            if (viewer == null || !viewer.IsOwner || viewer.Motor.IsDead) return;
            bool isSharkObjective = Kind == SeaLootKind.Shark && !Opened && carrier.Value == null;
            Vector3 point = (phase.Value == SeaLootState.Ready && !isSharkObjective) ? transform.position : eventPoint.Value;
            float distance = Vector3.Distance(camera.transform.position, point);
            Vector3 screen = camera.WorldToScreenPoint(point + Vector3.up * 4);
            if (screen.z <= 0) return;
            string label = Kind == SeaLootKind.Platforming ? "Лестница контрабандистов" : InSeagullWreck ? "Чайка с ключом" : InFort ? $"Пушечный пролом · входы {Fort?.Hits(0) ?? 0}/{Fort?.HitsToBreach ?? 6} · {Fort?.Hits(1) ?? 0}/{Fort?.HitsToBreach ?? 6}" : Kind == SeaLootKind.SternCargo ? "Груз за кормой" : InRigging ? "Сундук в такелаже" : InClam ? "Раковина с сундуком" : Kind == SeaLootKind.FloatingReward ? "Сундук из черепа"
                : Kind == SeaLootKind.Shark
                ? (Opened || carrier.Value != null ? "Ящик с припасами" : SharksDistracted ? "Ящик с припасами" : "Стая акул")
                : phase.Value == SeaLootState.Ready
                    ? "Ящик с припасами"
                    : Kind == SeaLootKind.Capture
                        ? "Зона захвата"
                        : Kind == SeaLootKind.Raft
                            ? "Плот с припасами"
                            : "Подводный тайник";
            if (Kind == SeaLootKind.Capture && phase.Value == SeaLootState.Locked)
                label += $" · {Mathf.RoundToInt(Progress * 100)}%" + (contested.Value ? " · оспаривается" : captureShip.Value != 0 ? $" · корабль {captureShip.Value}" : "");
            PirateHudStyle.Panel(new Rect(screen.x - 170, Screen.height - screen.y, 340, 28), $"{label} · {Mathf.RoundToInt(distance)} м");
        }
    }
}
