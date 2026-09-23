using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed class BotMaintenanceAction : IBotAction, IBotOutsideWork
    {
        readonly NetworkPlayer player;
        readonly PlayerInventory inventory;
        readonly NetworkWeapon weapon;
        readonly SimpleCannon cannon;
        readonly ShipDamageSection section;
        readonly int fragment;
        Vector3 localRepairPoint;
        BotStationAction step;
        BotHullWaterRepairAction outside;
        public bool RequiresShipWait => outside != null && outside.State == BotActionState.Running;
        bool supplying;
        int previousSlot;
        public BotActionState State { get; private set; } = BotActionState.Running;
        public string Status => outside?.Status ?? step?.Status ?? "Подготовка палубных работ";
        public string Failure => outside?.Failure ?? step?.Failure ?? "Нет";

        public BotMaintenanceAction(NetworkPlayer player, SimpleCannon cannon)
        {
            this.player = player; this.cannon = cannon;
            inventory = player.GetComponent<PlayerInventory>(); weapon = player.GetComponent<NetworkWeapon>();
        }
        public BotMaintenanceAction(NetworkPlayer player, ShipDamageSection section, int fragment, Vector3 point)
            : this(player, (SimpleCannon)null)
        {
            this.section = section; this.fragment = fragment;
            localRepairPoint = player.Ship.transform.InverseTransformPoint(point);
        }
        public static int MalletSlot(PlayerInventory inventory)
        {
            for (int i = 0; i < PlayerInventory.SlotCount; i++) if (inventory.HasMallet(i)) return i;
            return -1;
        }
        public bool Begin(out string reason)
        {
            previousSlot = inventory.SelectedSlot;
            supplying = cannon != null && inventory.BallCount(PlayerInventory.AmmoSlot) == 0;
            return StartStep(out reason);
        }
        bool StartStep(out string reason)
        {
            var station = new WorkStation(this);
            step = new BotStationAction(player, SessionController.Instance.Config.BotMotion, station,
                section != null ? "Восстановить повреждение своего корабля настоящим молотком" : "Доставить боеприпас и зарядить свободную пушку");
            if (step.Begin(out reason)) return true;
            if (TryOutside(out reason)) return true;
            State = BotActionState.Failed; weapon.SelectServerSlot(previousSlot); return false;
        }
        bool TryOutside(out string reason)
        {
            reason = step.Failure;
            if (section == null || fragment < 0 || outside != null || Time.time < player.NextBotWaterRepair ||
                section.Owner.Definition(section.SectionId).Type != ShipSectionType.Hull) return false;
            outside = new BotHullWaterRepairAction(player, section, fragment);
            bool ready = outside.Begin(out reason);
            if (!ready) player.NextBotWaterRepair = Time.time + 20f;
            return ready;
        }
        public PlayerCommand Tick(float delta)
        {
            if (outside != null)
            {
                var result = outside.Tick(delta);
                State = outside.State;
                if (State == BotActionState.Failed || State == BotActionState.Cancelled) player.NextBotWaterRepair = Time.time + 20f;
                if (State != BotActionState.Running) weapon.SelectServerSlot(previousSlot);
                return result;
            }
            var command = step.Tick(delta);
            if (step.State == BotActionState.Running) return command;
            if (step.State == BotActionState.Failed && TryOutside(out _)) return command;
            if (step.State == BotActionState.Succeeded && supplying)
            {
                supplying = false; StartStep(out _); return command;
            }
            State = step.State; weapon.SelectServerSlot(previousSlot); return command;
        }
        public void Cancel(string reason)
        {
            if (State != BotActionState.Running) return;
            outside?.Cancel(reason); step?.Cancel(reason); State = BotActionState.Cancelled; weapon.SelectServerSlot(previousSlot);
        }

        sealed class WorkStation : IBotShipStation, IBotApproachConstraint, IBotApproachRange
        {
            readonly BotMaintenanceAction job;
            readonly bool supply;
            bool acquired, done;
            float nextStrike, lastProgress;
            public WorkStation(BotMaintenanceAction job) { this.job = job; supply = job.supplying; }
            bool Repair => job.section != null;
            public float ApproachRadius => Repair ? 3.4f : 1.7f;
            NetworkCannon Network => job.cannon != null ? job.cannon.Network : null;
            public string Name => Repair ? job.fragment < 0 ? "Ремонт мачты" : job.section.Owner.Definition(job.section.SectionId).Type == ShipSectionType.Helm ? "Ремонт штурвала" : job.section.Owner.Definition(job.section.SectionId).Type == ShipSectionType.Capstan ? "Ремонт шпиля" : "Ремонт корпуса" :
                supply ? "Подбор ядер" : $"Зарядка пушки {job.cannon.Index + 1}";
            public Vector3 Position => Repair ? job.player.Ship.transform.TransformPoint(job.localRepairPoint) :
                supply ? Network.Crate.Supply.transform.position : job.cannon.transform.position + Vector3.up;
            public bool Complete => done || Repair && (job.fragment < 0 ?
                !job.section.Owner.MastRepairPoint(job.section.SectionId, out _) :
                (job.section.RemovedFragments & (1UL << job.fragment)) == 0);
            public bool Available => job.player.Ship != null && !job.player.Ship.IsSinking &&
                (Repair ? job.section.Owner != null && job.section.Owner.gameObject == job.player.Ship.gameObject :
                Network != null && Network.gameObject == job.player.Ship.gameObject && job.cannon.gameObject.activeInHierarchy &&
                (!supply || Network.Crate != null && Network.Crate.Supply != null));
            public bool Busy => !Repair && (job.cannon.IsIgnited || Network.HasBoarding(job.cannon.Index) || !done && job.cannon.IsLoaded);
            public void Validate() { }
            public bool Owned(NetworkPlayer player) => acquired && (Complete || !Busy && Time.time - lastProgress < 3f);
            public bool AllowsApproach(Vector3 worldPosition)
            {
                if (!Repair) return true;
                var eye = worldPosition + Vector3.up * 1.5f;
                var point = RepairPoint(eye);
                var delta = point - eye;
                return delta.magnitude <= 3.5f && !FirearmTrace.Cast(job.player.gameObject, eye, point - delta.normalized * .08f, out _);
            }
            Vector3 RepairPoint(Vector3 eye)
            {
                if (job.fragment < 0) return Position;
                var anchor = job.section.RepairTransform(job.fragment);
                return anchor.TransformPoint(job.section.RepairBounds(job.fragment).ClosestPoint(anchor.InverseTransformPoint(eye)));
            }
            public bool Acquire(NetworkPlayer player)
            {
                if (!Available || Busy) return false;
                if (Complete) { acquired = true; return true; }
                if (Repair)
                {
                    if (!job.weapon.SelectServerSlot(MalletSlot(job.inventory))) return false;
                    job.localRepairPoint = player.Ship.transform.InverseTransformPoint(RepairPoint(player.transform.position + Vector3.up * 1.5f));
                    acquired = true; lastProgress = Time.time; Work(player, 0); return true;
                }
                done = supply ? job.weapon.TryStoreBall(Network.NetworkObject) :
                    job.weapon.SelectServerSlot(PlayerInventory.AmmoSlot) && job.weapon.TryLoadBall(Network.NetworkObject, job.cannon.Index);
                acquired = done; lastProgress = Time.time; return done;
            }
            public void Work(NetworkPlayer player, float delta)
            {
                if (!Repair || Complete || Time.time < nextStrike) return;
                nextStrike = Time.time + .55f;
                if (player.GetComponent<NetworkHullRepair>().TryRepair(job.section.Owner.NetworkObject,
                    job.section.SectionId, job.fragment, job.localRepairPoint)) lastProgress = Time.time;
            }
            public void Release(NetworkPlayer player) { acquired = false; }
        }
    }
}
