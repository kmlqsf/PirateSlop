using UnityEngine;

namespace PirateSlop.Networking
{
    public readonly struct BotCannonSite
    {
        public readonly Vector3 Point;
        public readonly Quaternion Rotation;
        public BotCannonSite(Vector3 point, Quaternion rotation) { Point = point; Rotation = rotation; }
    }

    public static class BotCannonPlacement
    {
        static readonly RaycastHit[] hits = new RaycastHit[32];
        static readonly Collider[] overlaps = new Collider[48];
        public static bool Find(NetworkShip ship, Vector3 near, out BotCannonSite site)
        {
            site = default;
            if (ship == null || ship.IsSinking || !float.IsFinite(near.sqrMagnitude) || Mathf.Abs(near.x) < 2f) return false;
            float side = Mathf.Sign(near.x);
            int count = Physics.RaycastNonAlloc(ship.transform.TransformPoint(near + Vector3.up * 2f), Vector3.down,
                hits, 4f, ~0, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return false;
            float height = float.NegativeInfinity;
            foreach (var hit in new System.ArraySegment<RaycastHit>(hits, 0, count))
            {
                if (hit.rigidbody != ship.Body || hit.normal.y < .9f) continue;
                var local = ship.transform.InverseTransformPoint(hit.point);
                if (local.y <= height) continue;
                height = local.y; near = local;
            }
            if (!float.IsFinite(height)) return false;
            site = new BotCannonSite(near + Vector3.up * .02f, Quaternion.Euler(0, side * 90f, 0));
            return Clear(ship, site);
        }

        public static bool Clear(NetworkShip ship, BotCannonSite site)
        {
            if (ship == null || ship.IsSinking) return false;
            var rotation = ship.transform.rotation * site.Rotation;
            var point = ship.transform.TransformPoint(site.Point);
            for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2)
            {
                var foot = point + rotation * new Vector3(x * .65f, 0, z * .85f);
                int support = Physics.RaycastNonAlloc(foot + Vector3.up * .15f, Vector3.down, hits, .3f, ~0, QueryTriggerInteraction.Ignore);
                bool supported = false;
                if (support == hits.Length) return false;
                for (int i = 0; i < support; i++)
                    if (hits[i].rigidbody == ship.Body && hits[i].normal.y > .9f && Mathf.Abs(hits[i].point.y - point.y) < .12f) supported = true;
                if (!supported) return false;
            }
            int count = Physics.OverlapBoxNonAlloc(point + rotation * new Vector3(0, .94f, .12f),
                new Vector3(.8f, .82f, 1.22f), overlaps, rotation, ~0, QueryTriggerInteraction.Ignore);
            return count == 0;
        }
    }

    public sealed class BotInstallCannonAction : IBotAction
    {
        readonly NetworkPlayer player;
        readonly NetworkCannon cannon;
        readonly BotCannonSite site;
        readonly PlayerInventory inventory;
        readonly NetworkWeapon weapon;
        BotStationAction step;
        bool placing;
        int previousSlot;
        public BotActionState State { get; private set; } = BotActionState.Running;
        public string Status => step?.Status ?? "Подготовка установки пушки";
        public string Failure => step?.Failure ?? "Нет";

        public BotInstallCannonAction(NetworkPlayer player, NetworkCannon cannon, BotCannonSite site)
        {
            this.player = player; this.cannon = cannon; this.site = site;
            inventory = player.GetComponent<PlayerInventory>(); weapon = player.GetComponent<NetworkWeapon>();
        }
        int CannonSlot()
        {
            for (int i = 0; i < PlayerInventory.SlotCount; i++) if (inventory.HasCannon(i)) return i;
            return -1;
        }
        public bool Begin(out string reason)
        {
            previousSlot = inventory.SelectedSlot;
            placing = CannonSlot() >= 0;
            return StartStep(out reason);
        }
        bool StartStep(out string reason)
        {
            step = new BotStationAction(player, SessionController.Instance.Config.BotMotion,
                new WorkStation(this, placing), placing ? "Установить имеющуюся пушку на свободном месте своего корабля" : "Взять разобранную пушку для оснащения корабля");
            bool started = step.Begin(out reason);
            if (!started) { State = BotActionState.Failed; weapon.SelectServerSlot(previousSlot); }
            return started;
        }
        public PlayerCommand Tick(float delta)
        {
            var command = step.Tick(delta);
            if (step.State == BotActionState.Running) return command;
            if (step.State == BotActionState.Succeeded && !placing)
            {
                placing = true; StartStep(out _); return command;
            }
            State = step.State;
            weapon.SelectServerSlot(previousSlot);
            return command;
        }
        public void Cancel(string reason)
        {
            if (State != BotActionState.Running) return;
            step?.Cancel(reason); State = BotActionState.Cancelled;
            weapon.SelectServerSlot(previousSlot);
        }

        sealed class WorkStation : IBotShipStation, IBotApproachConstraint
        {
            readonly BotInstallCannonAction job;
            readonly bool placing;
            bool done;
            public WorkStation(BotInstallCannonAction job, bool placing) { this.job = job; this.placing = placing; }
            public string Name => placing ? "Установка пушки" : "Подбор разобранной пушки";
            public Vector3 Position => placing ? job.player.Ship.transform.TransformPoint(job.site.Point) + Vector3.up : job.cannon.Crate.Kit.transform.position + Vector3.up;
            public bool Available => job.cannon != null && job.cannon.Crate != null && job.player.Ship != null &&
                job.cannon.gameObject == job.player.Ship.gameObject && (done || (placing ? job.CannonSlot() >= 0 : job.cannon.Crate.KitAvailable));
            public bool Busy => false;
            public bool Complete => done;
            public void Validate() { }
            public bool Owned(NetworkPlayer player) => done;
            public bool AllowsApproach(Vector3 worldPosition)
            {
                if (!placing) return true;
                var relative = Quaternion.Inverse(job.site.Rotation) * (job.player.Ship.transform.InverseTransformPoint(worldPosition) - job.site.Point);
                return Mathf.Abs(relative.x) > 1.2f || Mathf.Abs(relative.z) > 1.65f;
            }
            public bool Acquire(NetworkPlayer player)
            {
                if (!Available) return false;
                if (!placing)
                {
                    if (!job.weapon.CanReach(Position, job.cannon.Crate.Kit.transform)) return false;
                    done = job.weapon.TryTakeCannon(job.cannon.NetworkObject);
                }
                else
                {
                    int slot = job.CannonSlot();
                    if (!BotCannonPlacement.Clear(player.Ship, job.site) || !job.weapon.SelectServerSlot(slot)) return false;
                    done = job.weapon.TryPlaceCannon(job.cannon.NetworkObject, slot, job.site.Point, job.site.Rotation);
                }
                return done;
            }
            public void Work(NetworkPlayer player, float delta) { }
            public void Release(NetworkPlayer player) { }
        }
    }
}
