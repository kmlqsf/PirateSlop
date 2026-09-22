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
        NetworkShip avoidanceThreat;
        Vector3 avoidanceCourse;
        float avoidanceUntil;
        bool avoidanceCourseAvailable;
        public bool AvoidingCollision { get; private set; }
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

        public void Stop(string reason) { AvoidingCollision = false; PlannedSails = Sails = Rudder = 0; Reason = reason; combat.Clear(); }

        public void Tick(NetworkShip ship, BotMotionSettings settings, bool helmsman, NetworkPlayer observer)
        {
            PlannedSails = Sails = Rudder = 0;
            var world = ProceduralWorld.Instance;
            if (world == null || !world.Ready || ship.IsSinking || ship.Motor.IsFlooded || ship.Motor.IsFrozen)
            { Stop("Плавание недоступно: мир или состояние корабля"); return; }
            var position = ship.transform.position;
            float safe = Mathf.Min(world.Layout.Radius - 40f, SessionController.Instance.SafeRadius(45f) - 30f);
            if (EscapeZone != wasEscaping) { route = null; nextRouteSearch = nextLocalSearch = 0; wasEscaping = EscapeZone; }
            bool avoiding = TryAvoidShip(world, ship, safe, out var avoidancePoint);
            AvoidingCollision = avoiding;
            if (avoiding && !avoidanceCourseAvailable) { Reason = "Сближение кораблей: убрать паруса, безопасного обхода пока нет"; return; }
            Vector3 combatPoint = default;
            string combatReason = null;
            if (EscapeZone) combat.Clear();
            bool fighting = !EscapeZone && combat.TryCourse(ship, observer, safe, out combatPoint, out combatReason);
            bool looting = !avoiding && !EscapeZone && !fighting && LootDestination.HasValue;
            if (looting && Distance(position, LootDestination.Value) < 18f)
            { Reason = "Стоянка у точки интереса: убрать паруса и отправить сборщика"; return; }
            if (fighting) { route = null; nextRouteSearch = nextLocalSearch = 0; }
            if (route != null && Distance(position, route.Waypoints[waypoint]) < 14f)
            {
                waypoint += direction;
                if (waypoint < 0 || waypoint >= route.Waypoints.Count) { route = null; nextRouteSearch = 0; }
            }
            if (!avoiding && !EscapeZone && !fighting && !looting && route == null && Time.time >= nextRouteSearch)
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
            if (!avoiding && !fighting && !looting && route == null && Time.time >= nextLocalSearch) FindLocalCourse(world, ship, position, safe);
            if (!avoiding && !fighting && !looting && route == null) { Reason = "Проверка свободного курса: готового маршрута нет; паруса пока убрать"; return; }
            var target = avoiding ? avoidancePoint : fighting ? combatPoint : looting ? LootDestination.Value : route.Waypoints[waypoint];
            var offset = target - position;
            float yaw = ship.transform.eulerAngles.y;
            float desired = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
            float error = Mathf.DeltaAngle(yaw, desired);
            Rudder = Mathf.Clamp(error / 35f, -1f, 1f);
            Reason = avoiding ? "Предотвращение столкновения: обход корабля" : EscapeZone ? "Уход от зоны по проверенному курсу" : fighting ? combatReason : looting ? "Подход к точке интереса" : $"Маршрут {route.Id}, точка {waypoint + 1}/{route.Waypoints.Count}";
            float currentRadius = new Vector2(position.x, position.z).magnitude;
            if (new Vector2(target.x, target.z).magnitude > Mathf.Max(safe, currentRadius - 5f))
            { route = null; Reason = "Маршрут ведёт из безопасной зоны; повторный выбор"; return; }
            if (avoiding) Reason = "Предотвращение столкновения: обход корабля с безопасным интервалом";
            if (fighting || avoiding)
                for (int i = 1; i <= 3; i++)
                    if (!world.CanSail(position, Mathf.LerpAngle(yaw, desired, i / 3f)))
                    { Rudder = 0; Reason += "; нет места для разворота, паруса убрать"; return; }
            if (Mathf.Abs(error) > 60f)
            {
                if (!fighting && !avoiding && Mathf.Abs(ship.Motor.Speed) < .1f && ship.GetComponent<SailSystem>().EffectiveDeploy < .02f)
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
            PlannedSails = Mathf.Clamp(settings.CruiseSails, .1f, 1f) * (Mathf.Abs(error) > 25f ? .4f : 1f);
            if (looting) PlannedSails *= Mathf.Clamp01((Distance(position, target) - 12f) / 75f);
            if (fighting)
            {
                PlannedSails = combat.SailDemand(ship, error);
                Reason += $"; паруса {PlannedSails:P0} по дистанции, сближению и развороту";
            }
            if (avoiding) PlannedSails = Mathf.Abs(error) < 20f ? .3f : 0f;
            if (helmsman) Sails = PlannedSails;
            else Reason += "; матросы занимают канаты, ожидание рулевого";
        }

        bool TryAvoidShip(ProceduralWorld world, NetworkShip ship, float safe, out Vector3 point)
        {
            var position = ship.transform.position;
            point = position;
            NetworkShip threat = null;
            float nearest = float.PositiveInfinity;
            var ownVelocity = Vector3.ProjectOnPlane(ship.Motor.CannonPointVelocity(position), Vector3.up);
            foreach (var other in NetworkShip.ActiveShips)
            {
                if (other == null || other == ship || other.IsSinking) continue;
                var offset = Vector3.ProjectOnPlane(other.transform.position - position, Vector3.up);
                if (offset.sqrMagnitude > 240f * 240f) continue;
                var relativeVelocity = Vector3.ProjectOnPlane(other.Motor.CannonPointVelocity(other.transform.position), Vector3.up) - ownVelocity;
                float time = Mathf.Clamp(-Vector3.Dot(offset, relativeVelocity) / Mathf.Max(.01f, relativeVelocity.sqrMagnitude), 0f, 20f);
                float clearance = ship.CollisionRadius + other.CollisionRadius + 18f;
                if ((offset + relativeVelocity * time).sqrMagnitude >= clearance * clearance) continue;
                if (offset.sqrMagnitude >= nearest) continue;
                nearest = offset.sqrMagnitude; threat = other;
            }
            if (threat == null) { avoidanceThreat = null; return false; }
            if (threat == avoidanceThreat && Time.time < avoidanceUntil && Distance(position, avoidanceCourse) > 15f && CourseClearOfShips(ship, avoidanceCourse))
            { point = avoidanceCourse; avoidanceCourseAvailable = true; return true; }
            float best = float.NegativeInfinity;
            for (int option = 0; option < 3; option++)
            {
                float turn = option == 0 ? 65f : option == 1 ? -65f : 180f;
                float yaw = ship.transform.eulerAngles.y + turn;
                var direction = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
                var candidate = position + direction * 55f;
                if (new Vector2(candidate.x, candidate.z).magnitude > Mathf.Max(safe, new Vector2(position.x, position.z).magnitude - 5f)) continue;
                bool clear = true;
                for (int step = 1; step <= 3; step++)
                    if (!world.CanSail(Vector3.Lerp(position, candidate, step / 3f), Mathf.LerpAngle(ship.transform.eulerAngles.y, yaw, step / 3f)))
                    { clear = false; break; }
                if (!clear || !CourseClearOfShips(ship, candidate)) continue;
                float separation = Vector3.ProjectOnPlane(candidate - threat.transform.position -
                    threat.Motor.CannonPointVelocity(threat.transform.position) * 5f, Vector3.up).magnitude;
                float score = separation + (option == 0 ? 20f : 0f);
                if (score <= best) continue;
                best = score; point = candidate;
            }
            avoidanceCourseAvailable = !float.IsNegativeInfinity(best);
            avoidanceThreat = threat; avoidanceCourse = point; avoidanceUntil = Time.time + 30f;
            return true;
        }

        bool CourseClearOfShips(NetworkShip ship, Vector3 destination)
        {
            var position = ship.transform.position;
            var velocity = Vector3.ProjectOnPlane(destination - position, Vector3.up).normalized * Mathf.Max(2f, Mathf.Abs(ship.Motor.Speed));
            foreach (var other in NetworkShip.ActiveShips)
            {
                if (other == null || other == ship || other.IsSinking) continue;
                var offset = Vector3.ProjectOnPlane(other.transform.position - position, Vector3.up);
                if (offset.sqrMagnitude > 240f * 240f) continue;
                var relative = Vector3.ProjectOnPlane(other.Motor.CannonPointVelocity(other.transform.position), Vector3.up) - velocity;
                float clearance = ship.CollisionRadius + other.CollisionRadius + 8f;
                float closing = Vector3.Dot(offset, relative);
                if (offset.sqrMagnitude < clearance * clearance && closing > 0f) continue;
                float time = Mathf.Clamp(-closing / Mathf.Max(.01f, relative.sqrMagnitude), 0f, 10f);
                if ((offset + relative * time).sqrMagnitude < clearance * clearance) return false;
            }
            return true;
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
