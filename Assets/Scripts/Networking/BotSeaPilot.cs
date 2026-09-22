using PirateSlop.World;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed class BotSeaPilot
    {
        WorldRoute route;
        int waypoint, direction;
        float nextRouteSearch;
        float nextLocalSearch;
        int localCandidate;
        readonly BotSeaCombatCourse combat = new();
        public NetworkShip CombatTarget => combat.Target;
        public Vector3? LootDestination { get; set; }
        public bool EscapeZone { get; set; }
        bool wasEscaping;
        static readonly float[] LocalAngles = { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 135f, -135f, 180f };
        public float Rudder { get; private set; }
        public float Sails { get; private set; }
        public float PlannedSails { get; private set; }
        public string Reason { get; private set; } = "Ожидание маршрута";

        static float Distance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        public void Stop(string reason) { PlannedSails = Sails = Rudder = 0; Reason = reason; combat.Clear(); }

        public void Tick(NetworkShip ship, BotMotionSettings settings, bool helmsman, NetworkPlayer observer)
        {
            PlannedSails = Sails = Rudder = 0;
            var world = ProceduralWorld.Instance;
            if (world == null || !world.Ready || ship.IsSinking || ship.Motor.IsFlooded || ship.Motor.IsFrozen)
            { Stop("Плавание недоступно: мир или состояние корабля"); return; }
            var position = ship.transform.position;
            float safe = Mathf.Min(world.Layout.Radius - 40f, SessionController.Instance.SafeRadius(45f) - 30f);
            if (EscapeZone != wasEscaping) { route = null; nextRouteSearch = nextLocalSearch = 0; wasEscaping = EscapeZone; }
            Vector3 combatPoint = default;
            string combatReason = null;
            if (EscapeZone) combat.Clear();
            bool fighting = !EscapeZone && combat.TryCourse(ship, observer, safe, out combatPoint, out combatReason);
            bool looting = !EscapeZone && !fighting && LootDestination.HasValue;
            if (looting && Distance(position, LootDestination.Value) < 18f)
            { Reason = "Стоянка у точки интереса: убрать паруса и отправить сборщика"; return; }
            if (fighting) { route = null; nextRouteSearch = nextLocalSearch = 0; }
            if (route != null && Distance(position, route.Waypoints[waypoint]) < 14f)
            {
                waypoint += direction;
                if (waypoint < 0 || waypoint >= route.Waypoints.Count) { route = null; nextRouteSearch = 0; }
            }
            if (!EscapeZone && !fighting && !looting && route == null && Time.time >= nextRouteSearch)
            {
                nextRouteSearch = Time.time + 5f;
                float best = float.PositiveInfinity;
                foreach (var candidate in world.Layout.Routes)
                {
                    if (candidate.Waypoints.Count < 2) continue;
                    for (int end = 0; end < 2; end++)
                    {
                        int first = end == 0 ? 0 : candidate.Waypoints.Count - 1;
                        var start = candidate.Waypoints[first];
                        var finish = candidate.Waypoints[end == 0 ? candidate.Waypoints.Count - 1 : 0];
                        float distance = Distance(position, start);
                        if (distance > 80f) continue;
                        float radius = new Vector2(finish.x, finish.z).magnitude;
                        float score = distance + Mathf.Max(0, radius - safe) * 8f;
                        var heading = (distance > 14f ? start : candidate.Waypoints[first + (end == 0 ? 1 : -1)]) - position;
                        score += Mathf.Abs(Mathf.DeltaAngle(ship.transform.eulerAngles.y, Mathf.Atan2(heading.x, heading.z) * Mathf.Rad2Deg)) * .3f;
                        if (score >= best) continue;
                        best = score; route = candidate; waypoint = first; direction = end == 0 ? 1 : -1;
                    }
                }
            }
            if (!fighting && !looting && route == null && Time.time >= nextLocalSearch) FindLocalCourse(world, ship, position, safe);
            if (!fighting && !looting && route == null) { Reason = "Проверка свободного курса: готового маршрута нет; паруса пока убрать"; return; }
            var target = fighting ? combatPoint : looting ? LootDestination.Value : route.Waypoints[waypoint];
            var offset = target - position;
            float yaw = ship.transform.eulerAngles.y;
            float desired = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
            float error = Mathf.DeltaAngle(yaw, desired);
            Rudder = Mathf.Clamp(error / 35f, -1f, 1f);
            Reason = EscapeZone ? "Уход от зоны по проверенному курсу" : fighting ? combatReason : looting ? "Подход к точке интереса" : $"Маршрут {route.Id}, точка {waypoint + 1}/{route.Waypoints.Count}";
            float currentRadius = new Vector2(position.x, position.z).magnitude;
            if (new Vector2(target.x, target.z).magnitude > Mathf.Max(safe, currentRadius - 5f))
            { route = null; Reason = "Маршрут ведёт из безопасной зоны; повторный выбор"; return; }
            if (fighting)
                for (int i = 1; i <= 3; i++)
                    if (!world.CanSail(position, Mathf.LerpAngle(yaw, desired, i / 3f)))
                    { Rudder = 0; Reason += "; нет места для разворота, паруса убрать"; return; }
            if (Mathf.Abs(error) > 60f)
            {
                if (!fighting && Mathf.Abs(ship.Motor.Speed) < .1f && ship.GetComponent<SailSystem>().EffectiveDeploy < .02f)
                    PlannedSails = Mathf.Clamp(settings.CruiseSails, .1f, 1f);
                Reason += "; разворот, матросы готовят канаты"; return;
            }
            float horizon = Mathf.Clamp(35f + Mathf.Abs(ship.Motor.Speed) * 12f, 35f, 100f);
            var forward = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            for (int i = 1; i <= 4; i++)
            {
                var probe = position + forward * (horizon * i / 4f);
                if (!world.CanSail(probe, yaw)) { Reason = "Препятствие по курсу; паруса убрать"; return; }
            }
            for (int i = 1; i <= 4; i++)
            {
                float fraction = i / 4f;
                var probe = Vector3.Lerp(position, target, fraction);
                if (!world.CanSail(probe, Mathf.LerpAngle(yaw, desired, fraction)))
                { Reason = "Подход к точке перекрыт; паруса убрать"; return; }
            }
            foreach (var other in NetworkShip.ActiveShips)
            {
                if (other == null || other == ship || other.IsSinking) continue;
                var relative = other.transform.position - position; relative.y = 0;
                if (relative.sqrMagnitude > 150f * 150f || Physics.Linecast(position + Vector3.up * 8f,
                    other.transform.position + Vector3.up * 8f, LayerMask.GetMask("WorldStatic"), QueryTriggerInteraction.Ignore)) continue;
                float along = Vector3.Dot(relative, forward);
                if (along > -15f && along < horizon + 45f && (relative - forward * along).magnitude < 35f)
                { Reason = "Другой корабль в коридоре движения; уступить"; return; }
            }
            PlannedSails = Mathf.Clamp(settings.CruiseSails, .1f, 1f) * (Mathf.Abs(error) > 25f ? .4f : 1f);
            if (looting) PlannedSails *= Mathf.Clamp01((Distance(position, target) - 12f) / 75f);
            if (fighting)
            {
                PlannedSails = combat.SailDemand(ship, error);
                Reason += $"; паруса {PlannedSails:P0} по дистанции, сближению и развороту";
            }
            if (helmsman) Sails = PlannedSails;
            else Reason += "; матросы занимают канаты, ожидание рулевого";
        }

        void FindLocalCourse(ProceduralWorld world, NetworkShip ship, Vector3 position, float safe)
        {
            float radius = new Vector2(position.x, position.z).magnitude;
            float desired = radius > 100f ? Mathf.Atan2(-position.x, -position.z) * Mathf.Rad2Deg : ship.transform.eulerAngles.y;
            float heading = desired + LocalAngles[localCandidate];
            localCandidate = (localCandidate + 1) % LocalAngles.Length;
            nextLocalSearch = Time.time + (localCandidate == 0 ? 5f : .25f);
            var forward = Quaternion.Euler(0, heading, 0) * Vector3.forward;
            float travel = EscapeZone ? Mathf.Clamp(radius - Mathf.Max(5f, safe * .5f), 5f, 60f) : 60f;
            var target = position + forward * travel;
            float targetRadius = new Vector2(target.x, target.z).magnitude;
            if (targetRadius > Mathf.Max(safe, radius - 5f)) return;
            if (!world.CanSail(position, heading)) return;
            for (int i = 1; i <= 8; i++)
                if (!world.CanSail(position + forward * (travel * i / 8f), heading)) return;
            route = new WorldRoute { Id = "локальный проверенный курс" };
            route.Waypoints.Add(position);
            route.Waypoints.Add(target);
            waypoint = 1; direction = 1; localCandidate = 0;
        }
    }
}
