using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed class BotBucketAction : IBotAction
    {
        readonly NetworkPlayer player;
        readonly NetworkShip ship;
        readonly ShipFlooding flooding;
        float nextActionTime;
        int phase;
        public BotActionState State { get; private set; } = BotActionState.Running;
        public string Status { get; private set; } = "Откачка воды из трюма";
        public string Failure { get; private set; } = "Нет";

        public BotBucketAction(NetworkPlayer player)
        {
            this.player = player;
            ship = player.Ship;
            flooding = ship != null ? ship.GetComponent<ShipFlooding>() : null;
        }

        public bool Begin(out string reason)
        {
            reason = "Откачка воды ведром";
            if (!player.IsServerInitialized || !player.IsBot.Value || ship == null || ship.IsSinking || flooding == null)
            {
                State = BotActionState.Failed;
                reason = "Корабль недоступен для откачки воды";
                return false;
            }
            nextActionTime = Time.time + 1.2f;
            return true;
        }

        public PlayerCommand Tick(float delta)
        {
            var command = new PlayerCommand { Yaw = player.transform.eulerAngles.y };
            if (ship == null || ship.IsSinking || flooding == null || player.Motor.IsDead)
            {
                State = BotActionState.Failed;
                Failure = "Корабль затонул или бот погиб";
                return command;
            }
            if (flooding.Level <= 0.05f)
            {
                State = BotActionState.Succeeded;
                Status = "Вода из трюма откачана";
                return command;
            }

            if (Time.time >= nextActionTime)
            {
                nextActionTime = Time.time + 1.4f;
                if (phase == 0)
                {
                    flooding.SetLevel(Mathf.Max(0f, flooding.Level - .08f));
                    phase = 1;
                    Status = "Зачерпнул ведро воды; выливает за борт";
                }
                else
                {
                    phase = 0;
                    Status = "Черпает воду в трюме";
                }
                SessionController.Instance.RecordBotEvent(player.BotNumber, Status);
            }
            return command;
        }

        public void Cancel(string reason)
        {
            State = BotActionState.Cancelled;
            Failure = reason;
        }
    }
}
