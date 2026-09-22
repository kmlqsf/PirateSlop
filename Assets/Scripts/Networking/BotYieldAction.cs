using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed class BotYieldAction : IBotAction
    {
        static readonly float[] Angles = { 90f, -90f, 45f, -45f, 0f };
        readonly NetworkPlayer player, requester;
        readonly DeckRoute route;
        Vector3 goal;
        float deadline, reachedAt = -1;
        public BotActionState State { get; private set; } = BotActionState.Running;
        public string Status => "Уступает проход БОТ" + requester.BotNumber;
        public string Failure => "Нет";
        public BotYieldAction(NetworkPlayer player, NetworkPlayer requester)
        {
            this.player = player; this.requester = requester;
            route = new DeckRoute(player, SessionController.Instance.Config.BotMotion);
        }
        public bool Begin(out string reason)
        {
            reason = Status;
            route.Configure(player.Ship);
            var origin = player.Ship.transform.InverseTransformPoint(player.transform.position);
            var away = Vector3.ProjectOnPlane(origin - player.Ship.transform.InverseTransformPoint(requester.transform.position), Vector3.up).normalized;
            if (away.sqrMagnitude < .1f) away = Vector3.right;
            for (int ring = 1; ring <= 2; ring++)
                foreach (float angle in Angles)
                {
                    var candidate = origin + Quaternion.Euler(0, angle, 0) * away * (ring * .9f);
                    if (!route.Ground(candidate, out candidate) || !route.ClearAt(candidate, true) || !route.Edge(origin, candidate)) continue;
                    goal = candidate; deadline = Time.time + 3f;
                    SessionController.Instance.RecordBotEvent(player.BotNumber, Status);
                    return true;
                }
            State = BotActionState.Failed;
            return false;
        }
        public PlayerCommand Tick(float delta)
        {
            var command = new PlayerCommand { Yaw = player.transform.eulerAngles.y };
            if (player.Ship == null || player.Motor.IsDead || player.Motor.IsSwimming || player.Motor.IsClimbing ||
                player.Motor.LocomotionLocked || player.Passenger.Ship != player.Ship.Body || Time.time >= deadline)
            { State = BotActionState.Succeeded; return command; }
            var origin = player.Ship.transform.InverseTransformPoint(player.transform.position);
            var offset = goal - origin; offset.y = 0;
            if (offset.sqrMagnitude < .04f || reachedAt >= 0)
            {
                if (reachedAt < 0) reachedAt = Time.time;
                if (Time.time - reachedAt > .8f) State = BotActionState.Succeeded;
                return command;
            }
            var probe = origin + offset.normalized * Mathf.Min(.35f, offset.magnitude);
            if (!route.Ground(probe, out probe) || !route.Edge(origin, probe, true))
            { State = BotActionState.Failed; return command; }
            var direction = player.Ship.transform.TransformDirection(offset.normalized);
            var relative = Quaternion.Inverse(Quaternion.Euler(0, command.Yaw, 0)) * direction;
            command.Move = new Vector2(relative.x, relative.z);
            return command;
        }
        public void Cancel(string reason) { State = BotActionState.Cancelled; }
    }
}
