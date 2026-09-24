using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed class BotCombatPosition
    {
        readonly NetworkPlayer player;
        readonly DeckRoute route;
        float nextPlan, nextProbe, progressAt;
        Vector3 anchor;
        bool edgeClear;
        float strafeTimeOffset, nextJumpTime;
        public string Status { get; private set; } = "Удерживает позицию";
        public bool Moving => route.Searching || route.Ready;
        public BotCombatPosition(NetworkPlayer player)
        {
            this.player = player;
            route = new DeckRoute(player, SessionController.Instance.Config.BotMotion);
            strafeTimeOffset = Random.Range(0f, 100f);
            nextJumpTime = Time.time + Random.Range(2f, 5f);
        }
        public void Clear() { route.Clear(); Status = "Удерживает позицию"; }
        bool Safe(Vector3 point)
        {
            if (!route.ClearAt(point, true)) return false;
            for (int i = 0; i < 4; i++)
            {
                float angle = i * Mathf.PI * .5f;
                var edge = point + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * .45f;
                if (!route.Ground(edge, out var support) || Mathf.Abs(support.y - point.y) > .25f) return false;
            }
            return true;
        }
        float Score(Vector3 local, Vector3 enemy, NetworkPlayer target, bool close, bool cover)
        {
            var feet = player.Ship.transform.TransformPoint(local);
            float distance = Vector3.Distance(feet + Vector3.up, enemy);
            bool blocked = FirearmTrace.Cast(player.gameObject, feet + Vector3.up * 1.65f, enemy, out var hit) &&
                hit.collider.GetComponentInParent<NetworkPlayer>() != target;
            bool wall = blocked && hit.collider.GetComponentInParent<NetworkPlayer>() == null;
            float range = close ? Mathf.Abs(distance - 1.7f) * 3f : Mathf.Abs(distance - 14f) * .15f + Mathf.Max(0, 6f - distance) * 3f;
            return range + (cover ? wall ? -25f : 10f : blocked ? 20f : 0f);
        }
        public void Plan(Vector3 enemy, NetworkPlayer target, bool close, bool cover)
        {
            if (Time.time < nextPlan || route.Searching || player.Ship == null) return;
            nextPlan = Time.time + 1.5f;
            var ship = player.Ship;
            route.Configure(ship);
            var local = ship.transform.InverseTransformPoint(player.transform.position);
            if (!route.Ground(local, out var start)) return;
            float current = Score(start, enemy, target, close, cover);
            float best = current - 1f;
            Vector3 goal = default;
            bool found = false;
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI * .25f;
                var near = start + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (close ? 3f : 2.5f);
                if (!route.Ground(near, out var point, 1.5f) || !Safe(point)) continue;
                float score = Score(point, enemy, target, close, cover) + Vector3.Distance(start, point) * .2f;
                if (score >= best) continue;
                best = score; goal = point; found = true;
            }
            if (!found)
            {
                if (current < 10f) Clear();
                return;
            }
            route.Begin(ship, start, goal); anchor = start; progressAt = Time.time; nextProbe = 0;
            Status = cover ? "Ищет укрытие для перезарядки" : close ? "Сближается по палубе" : "Меняет позицию для стрельбы";
        }
        public void Move(ref PlayerCommand command)
        {
            if (route.Searching) return;
            if (route.Failed) { Clear(); Status = "Позиция недоступна; остаётся на палубе"; return; }
            if (!route.Ready) return;
            var ship = player.Ship;
            var local = ship.transform.InverseTransformPoint(player.transform.position);
            if ((local - anchor).sqrMagnitude > .09f) { anchor = local; progressAt = Time.time; }
            if (route.Advance(local)) { Clear(); return; }
            if (Time.time - progressAt > 4f) { Clear(); Status = "Проход перекрыт; перемещение отменено"; return; }
            var target = route.Waypoint;
            if (Time.time >= nextProbe)
            {
                nextProbe = Time.time + .15f;
                var probe = Vector3.MoveTowards(local, target, .5f);
                edgeClear = route.Ground(probe, out var ground) && route.Edge(local, ground, true) && route.ClearAt(ground, true);
            }
            if (!edgeClear) return;
            var world = ship.transform.TransformVector(target - local); world.y = 0;
            var relative = Quaternion.Inverse(Quaternion.Euler(0, command.Yaw, 0)) * world.normalized;
            
            float strafe = Mathf.Sin(Time.time * 3.5f + strafeTimeOffset) * 0.75f;
            command.Move = Vector2.ClampMagnitude(new Vector2(relative.x + strafe, relative.z), 1f);
            
            bool routeJump = player.Motor.IsGrounded && target.y - local.y > .28f;
            bool randomJump = player.Motor.IsGrounded && Time.time >= nextJumpTime;
            if (randomJump) nextJumpTime = Time.time + Random.Range(2f, 4.5f);
            command.Jump = routeJump || randomJump;
        }
    }
}
