using System;
using System.Collections.Generic;
using PirateSlop.World;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed class BotCaptain
    {
        readonly NetworkPlayer player;
        readonly System.Random random;
        readonly SailSystem sails;
        List<Vector3> route;
        int waypoint;
        float nextDecision, nextRoute, rudder, deployment, strandedSince;
        Vector3 walkTarget;
        float nextWalkTarget;
        bool hasWalkTarget;
        readonly float[] bearings = { 0f, 25f, -25f, 50f, -50f, 80f, -80f, 120f, -120f, 180f };

        public BotCaptain(NetworkPlayer player)
        {
            this.player = player;
            random = new System.Random(player.ParticipantId.Value);
            sails = player.Ship.GetComponentInChildren<SailSystem>();
        }

        public PlayerCommand Command()
        {
            var motor = player.Motor;
            var ship = player.Ship;
            var command = new PlayerCommand { Yaw = motor.transform.eulerAngles.y };
            if (ship == null) return command;
            var helm = ship.Helm;
            if (motor.IsDead || motor.IsKnockedBack || motor.IsSwimming)
            {
                if (helm.IsControlledBy(motor)) helm.ReleaseControl();
                motor.SetLocomotionLocked(false);
                if (!helm.IsControlling) sails?.SetDeploy(0f);
                return command;
            }
            if (!SessionController.Instance.IsBotHelmsman(player))
            {
                motor.SetLocomotionLocked(false);
                strandedSince = 0f;
                return WalkDeck(ship);
            }
            hasWalkTarget = false;
            if (Time.time >= nextDecision)
            {
                nextDecision = Time.time + .5f + (float)random.NextDouble() * .15f;
                Navigate(ship);
            }
            if (helm.SteerBot(motor, rudder))
            {
                strandedSince = 0f;
                if (player.Passenger.Ship != ship.Body) player.Passenger.Attach(ship.Body);
                motor.SetLocomotionLocked(true);
                command.Yaw = ship.transform.eulerAngles.y;
                sails?.SetDeploy(deployment);
            }
            else
            {
                motor.SetLocomotionLocked(false);
                if (helm.IsControlling) return command;
                sails?.SetDeploy(0f);
                Vector3 target = ship.transform.TransformPoint(SessionController.Instance.Config.PlayerLocalSpawn);
                Vector3 direction = target - motor.transform.position;
                direction.y = 0f;
                if (direction.sqrMagnitude > .2f)
                {
                    command.Yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                    command.Move = Vector2.up;
                }
                if (strandedSince <= 0f) strandedSince = Time.time;
                if (Time.time - strandedSince > 20f) { player.ReturnHome(); strandedSince = Time.time; }
            }
            return command;
        }

        PlayerCommand WalkDeck(NetworkShip ship)
        {
            var motor = player.Motor;
            var command = new PlayerCommand { Yaw = motor.transform.eulerAngles.y };
            if (player.Passenger.Ship != ship.Body) return command;
            if (Time.time >= nextWalkTarget)
            {
                hasWalkTarget = false;
                nextWalkTarget = Time.time + 2f + (float)random.NextDouble() * 3f;
                Vector3 origin = SessionController.Instance.Config.PlayerLocalSpawn;
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    Vector3 local = origin + new Vector3(((float)random.NextDouble() - .5f) * 6f, 0f, ((float)random.NextDouble() - .3f) * 5f);
                    Vector3 target = ship.transform.TransformPoint(local);
                    if (FlatDistance(target, motor.transform.position) < 1f || FlatDistance(target, ship.Helm.transform.position) < 1.2f) continue;
                    if (!DeckPath(ship, motor.transform.position, target)) continue;
                    walkTarget = local; hasWalkTarget = true;
                    break;
                }
            }
            if (!hasWalkTarget) return command;
            Vector3 destination = ship.transform.TransformPoint(walkTarget);
            Vector3 direction = destination - motor.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < .2f)
            {
                hasWalkTarget = false;
                nextWalkTarget = Time.time + 1f + (float)random.NextDouble() * 2f;
                return command;
            }
            Vector3 step = motor.transform.position + direction.normalized * Mathf.Min(.8f, direction.magnitude);
            if (!DeckPath(ship, motor.transform.position, step))
            {
                hasWalkTarget = false; nextWalkTarget = Time.time + .6f;
                return command;
            }
            command.Yaw = Mathf.MoveTowardsAngle(command.Yaw, Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg, 120f * (float)player.TimeManager.TickDelta);
            if (Mathf.Abs(Mathf.DeltaAngle(command.Yaw, Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg)) < 20f) command.Move = Vector2.up * .4f;
            return command;
        }

        bool DeckPath(NetworkShip ship, Vector3 from, Vector3 to)
        {
            float distance = Vector3.Distance(from, to);
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / .4f));
            for (int i = 1; i <= steps; i++)
            {
                Vector3 point = Vector3.Lerp(from, to, i / (float)steps);
                if (!Physics.Raycast(point + Vector3.up * .6f, Vector3.down, out var support, 1.2f, ~0, QueryTriggerInteraction.Ignore)
                    || support.rigidbody != ship.Body || support.normal.y < .7f) return false;
            }
            if (distance < .01f) return true;
            foreach (var hit in Physics.CapsuleCastAll(from + Vector3.up * .5f, from + Vector3.up * 1.5f, .28f,
                         (to - from).normalized, distance, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(player.transform)) return false;
            return true;
        }

        void Navigate(NetworkShip ship)
        {
            var session = SessionController.Instance;
            var world = ProceduralWorld.Instance;
            Vector3 position = ship.transform.position;
            float safe = Mathf.Max(35f, session.SafeRadius(45f) - 45f);
            if (route != null && waypoint < route.Count && FlatDistance(position, route[waypoint]) < 28f) waypoint++;
            bool unsafeGoal = route != null && Horizontal(route[route.Count - 1]) > safe;
            if (Time.time >= nextRoute || route == null || waypoint >= route.Count || unsafeGoal)
            {
                nextRoute = Time.time + 12f;
                route = null;
                for (int attempt = 0; attempt < 5; attempt++)
                {
                    float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                    float radius = safe * (.25f + (float)random.NextDouble() * .4f);
                    Vector3 goal = new Vector3(Mathf.Cos(angle) * radius, world.Layout.SeaLevel, Mathf.Sin(angle) * radius);
                    if (!world.CanSail(goal, 0f)) continue;
                    try { route = session.BotRoutes.FindPath(position, goal); break; }
                    catch (InvalidOperationException) { }
                }
                waypoint = 1;
                if (route == null) nextRoute = Time.time + 3f;
            }
            Vector3 destination = route != null && waypoint < route.Count ? route[waypoint] : Vector3.up * position.y;
            Vector3 toward = destination - position;
            float desired = Mathf.Atan2(toward.x, toward.z) * Mathf.Rad2Deg;
            float yaw = ship.transform.eulerAngles.y;
            float best = float.PositiveInfinity, selected = desired;
            bool found = false;
            foreach (float bearing in bearings)
            {
                float candidate = desired + bearing;
                Vector3 direction = Quaternion.Euler(0f, candidate, 0f) * Vector3.forward;
                if (!ClearCourse(ship, position, direction, candidate, 75f)) continue;
                float score = Mathf.Abs(bearing) + Mathf.Abs(Mathf.DeltaAngle(yaw, candidate)) * .25f;
                score += Mathf.Max(0f, Horizontal(position + direction * 75f) - safe) * 2f;
                if (score >= best) continue;
                best = score; selected = candidate; found = true;
            }
            float turn = Mathf.DeltaAngle(yaw, selected);
            rudder = Mathf.Clamp(turn / 32f, -1f, 1f);
            Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            bool ahead = ClearCourse(ship, position, forward, yaw, 45f);
            deployment = !found || !ahead || Mathf.Abs(turn) > 85f ? 0f : Mathf.Lerp(.85f, .2f, Mathf.Clamp01(Mathf.Abs(turn) / 70f));
            if (!found) { rudder = Mathf.Clamp(Mathf.DeltaAngle(yaw, desired) / 30f, -1f, 1f); nextRoute = Mathf.Min(nextRoute, Time.time + 2f); }
        }

        static bool ClearCourse(NetworkShip ship, Vector3 position, Vector3 direction, float yaw, float distance)
        {
            var world = ProceduralWorld.Instance;
            for (float step = 15f; step <= distance; step += 15f)
            {
                Vector3 point = position + direction * step;
                if (!world.CanSail(point, yaw)) return false;
                foreach (var other in NetworkShip.ActiveShips)
                {
                    if (other == null || other == ship) continue;
                    float separation = FlatDistance(point, other.transform.position);
                    if (separation < 52f && separation < FlatDistance(position, other.transform.position)) return false;
                }
            }
            return true;
        }

        static float Horizontal(Vector3 point) => new Vector2(point.x, point.z).magnitude;
        static float FlatDistance(Vector3 a, Vector3 b) => Horizontal(a - b);
    }
}
