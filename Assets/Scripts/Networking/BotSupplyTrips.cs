using System;
using System.Collections.Generic;
using PirateSlop.World;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class BotCaptain
    {
        sealed class Expedition
        {
            public LocationRecord Island;
            public Vector3 Approach, Shore;
            public NetworkPlayer Runner;
            public float NextVisit, Deadline;
            public bool Anchored, Returning;
        }
        static readonly Dictionary<NetworkShip, Expedition> expeditions = new();
        Expedition trip;
        ShipLadder boardingLadder;
        NetworkFish looseGoal;
        NetworkLootChest chestGoal;
        readonly HashSet<UnityEngine.Object> skippedLoot = new();
        float lootDeadline, nextLootSearch;
        int tripStage, lootTaken;
        public bool OnSupplyTrip => trip != null;
        void CancelTrip()
        {
            if (trip != null && trip.Runner == player)
            { trip.Runner = null; trip.Island = null; trip.NextVisit = Time.time + 30f; trip.Anchored = false; }
            trip = null; boardingLadder = null; looseGoal = null; chestGoal = null; tripStage = 0;
        }
        ShipLadder BoardingLadder(NetworkShip ship)
        {
            foreach (var ladder in ShipLadder.Active)
                if (ladder.Body == ship.Body && !ladder.RopeClimb && ship.transform.InverseTransformPoint(ladder.transform.position).y < 0f) return ladder;
            return null;
        }
        bool NavigateSupply(NetworkShip ship, float safe)
        {
            if (!expeditions.TryGetValue(ship, out var mission))
            { mission = new Expedition { NextVisit = Time.time + 8f }; expeditions[ship] = mission; }
            if (mission.Runner != null && (!mission.Runner.IsSpawned || mission.Runner.Motor.IsDead))
            { mission.Runner = null; mission.Island = null; mission.Anchored = false; mission.NextVisit = Time.time + 25f; }
            bool danger = Enemy(ship, 150f) != null || Horizontal(ship.transform.position) > safe;
            if (mission.Island != null && (danger || Time.time > mission.Deadline))
            {
                mission.Returning = true;
                if (mission.Runner == null) { mission.Island = null; mission.Anchored = false; mission.NextVisit = Time.time + 35f; route = null; }
            }
            if (mission.Runner != null || mission.Anchored)
            {
                if (Time.time > mission.Deadline + 90f)
                {
                    mission.Island = null; mission.Anchored = false; mission.Runner = null;
                    mission.NextVisit = Time.time + 45f; route = null; return false;
                }
                rudder = 0f; deployment = 0f; return true;
            }
            if (mission.Island == null && !danger && Time.time >= mission.NextVisit && BoardingLadder(ship) != null)
            {
                mission.NextVisit = Time.time + 30f;
                var world = ProceduralWorld.Instance;
                var items = UnityEngine.Object.FindObjectsByType<NetworkFish>(FindObjectsSortMode.None);
                LocationRecord selected = null;
                float best = float.PositiveInfinity;
                foreach (var island in world.Layout.Locations)
                {
                    if (Horizontal(island.Position) + island.Radius > safe) continue;
                    bool supplies = false;
                    foreach (var item in items)
                        if (item.Available && (item.CurrentItem == InventoryItem.Rum || item.CurrentItem == InventoryItem.Cannon) && FlatDistance(item.transform.position, island.Position) < island.Radius)
                        { supplies = true; break; }
                    if (!supplies) continue;
                    float score = FlatDistance(ship.transform.position, island.Position);
                    if (score < best) { selected = island; best = score; }
                }
                if (selected != null)
                {
                    WorldPoint approach = null; best = float.PositiveInfinity;
                    foreach (var point in world.Points("ship_approach"))
                    {
                        float distance = FlatDistance(point.Position, selected.Position);
                        if (distance < best && world.CanSail(point.Position, point.Yaw)) { approach = point; best = distance; }
                    }
                    if (approach != null && best < selected.Radius + 180f)
                    {
                        Vector3 towardWater = (approach.Position - selected.Position).normalized;
                        Vector3 shore = selected.Position;
                        bool landing = false;
                        for (float radius = selected.Radius * 1.1f; radius > selected.Radius * .2f; radius -= 2f)
                        {
                            Vector3 p = selected.Position + towardWater * radius;
                            p.y = world.GroundHeight(p);
                            if (p.y < world.Layout.SeaLevel + .25f || p.y > world.Layout.SeaLevel + 1.2f) continue;
                            shore = p; landing = true; break;
                        }
                        if (landing)
                        {
                            try { route = SessionController.Instance.BotRoutes.FindPath(ship.transform.position, approach.Position); }
                            catch (InvalidOperationException) { route = null; }
                            if (route != null)
                            {
                                mission.Island = selected; mission.Approach = approach.Position; mission.Shore = shore;
                                mission.Returning = false; mission.Deadline = Time.time + 240f;
                                waypoint = 1;
                            }
                        }
                    }
                }
            }
            if (mission.Island == null) return false;
            float remaining = FlatDistance(ship.transform.position, mission.Approach);
            if (remaining < 35f)
            {
                deployment = 0f; rudder = 0f;
                if (ship.Motor.Speed < .3f) { mission.Anchored = true; mission.Deadline = Time.time + 240f; }
                return true;
            }
            if (route != null && waypoint < route.Count && FlatDistance(ship.transform.position, route[waypoint]) < 20f) waypoint++;
            Vector3 goal = route != null && waypoint < route.Count ? route[waypoint] : mission.Approach;
            Vector3 delta = goal - ship.transform.position;
            float desired = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
            float turn = Mathf.DeltaAngle(ship.transform.eulerAngles.y, desired);
            rudder = Mathf.Clamp(turn / 30f, -1f, 1f);
            deployment = remaining < 65f ? .2f : Mathf.Abs(turn) > 70f ? .08f : .65f;
            if (!ClearCourse(ship, ship.transform.position, ship.transform.forward, ship.transform.eulerAngles.y, 30f)) deployment = 0f;
            return true;
        }
        bool TryStartTrip(NetworkShip ship)
        {
            if (!expeditions.TryGetValue(ship, out var mission) || !mission.Anchored || mission.Returning || mission.Runner != null || mission.Island == null) return false;
            boardingLadder = BoardingLadder(ship);
            if (boardingLadder == null || player.GetComponent<PlayerInventory>().EmptySlot() < 0) return false;
            trip = mission; trip.Runner = player; tripStage = lootTaken = 0;
            skippedLoot.Clear(); nextLootSearch = 0f; navigation.Clear(); return true;
        }
        PlayerCommand Toward(Vector3 target, bool swim = false)
        {
            Vector3 delta = target - player.transform.position;
            return new PlayerCommand { Yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg, Move = Vector2.up, Rise = swim, Sprint = swim };
        }
        PlayerCommand ReturnAboard(NetworkShip ship)
        {
            var ladder = BoardingLadder(ship);
            if (ladder == null) return new PlayerCommand { Yaw = player.transform.eulerAngles.y, Rise = true };
            Vector3 local = ladder.transform.InverseTransformPoint(player.transform.position);
            if (player.Motor.IsClimbing || (Mathf.Abs(local.x) < ladder.HalfWidth && local.z < 1.5f && local.z > 0f))
                return new PlayerCommand { Yaw = ladder.transform.eulerAngles.y + 180f, Move = Vector2.up };
            Vector3 point = ladder.transform.TransformPoint(new Vector3(0f, 1f, .7f));
            if (player.Motor.IsSwimming) return Toward(point, true);
            return navigation.Move(player, point);
        }
        PlayerCommand SupplyTrip(NetworkShip ship)
        {
            var motor = player.Motor;
            motor.SetLocomotionLocked(false);
            if (boardingLadder == null || trip.Island == null) { CancelTrip(); return ReturnAboard(ship); }
            if (trip.Returning && tripStage < 3)
            {
                tripStage = player.Passenger.Ship == ship.Body && !motor.IsSwimming && !motor.IsClimbing ? 5 : motor.IsClimbing || motor.IsSwimming ? 4 : 3;
                navigation.Clear();
            }
            var idle = new PlayerCommand { Yaw = player.transform.eulerAngles.y };
            if (tripStage == 0)
            {
                var top = boardingLadder.transform.TransformPoint(new Vector3(0, boardingLadder.Height + .1f, -.6f));
                var local = boardingLadder.transform.InverseTransformPoint(player.transform.position);
                if (motor.IsSwimming || local.y < 1f) { tripStage = 1; navigation.Clear(); return idle; }
                if (motor.IsClimbing || FlatDistance(player.transform.position, top) < 1.2f)
                    return new PlayerCommand { Yaw = boardingLadder.transform.eulerAngles.y, Move = Vector2.up, Pitch = 70f, Crouch = true };
                return navigation.Move(player, top, ship.transform);
            }
            if (tripStage == 1)
            {
                if (!motor.IsSwimming && motor.IsGrounded && player.Passenger.Ship == null)
                { tripStage = 2; navigation.Clear(); nextLootSearch = 0f; return idle; }
                return Toward(trip.Shore, true);
            }
            if (tripStage == 2)
            {
                var weapon = player.GetComponent<NetworkWeapon>();
                if (lootTaken >= 6 || Time.time > trip.Deadline - 60f || player.GetComponent<PlayerInventory>().EmptySlot() < 0)
                { tripStage = 3; navigation.Clear(); return idle; }
                if (looseGoal != null && !looseGoal.Available) looseGoal = null;
                if (looseGoal == null && chestGoal == null && Time.time >= nextLootSearch)
                {
                    nextLootSearch = Time.time + 1f; float best = float.PositiveInfinity;
                    foreach (var item in UnityEngine.Object.FindObjectsByType<NetworkFish>(FindObjectsSortMode.None))
                    {
                        if (!item.Available || skippedLoot.Contains(item) || !weapon.CanAddItem(item.CurrentItem) || FlatDistance(item.transform.position, trip.Island.Position) > trip.Island.Radius) continue;
                        float score = FlatDistance(player.transform.position, item.transform.position) + (item.CurrentItem == InventoryItem.Rum ? 0f : 25f);
                        if (score < best) { best = score; looseGoal = item; }
                    }
                    if (looseGoal == null)
                        foreach (var chest in UnityEngine.Object.FindObjectsByType<NetworkLootChest>(FindObjectsSortMode.None))
                        {
                            if (!chest.IsSpawned || skippedLoot.Contains(chest) || FlatDistance(chest.transform.position, trip.Island.Position) > trip.Island.Radius) continue;
                            bool useful = false;
                            for (int i = 0; i < chest.SlotCount; i++) if (weapon.CanAddItem(chest.ItemAt(i))) useful = true;
                            float score = FlatDistance(player.transform.position, chest.transform.position);
                            if (useful && score < best) { best = score; chestGoal = chest; }
                        }
                    lootDeadline = Time.time + 30f; navigation.Clear();
                    if (looseGoal == null && chestGoal == null) { tripStage = 3; return idle; }
                }
                if (looseGoal != null || chestGoal != null)
                {
                    var target = looseGoal != null ? looseGoal.transform : chestGoal.transform;
                    if (Time.time > lootDeadline)
                    { skippedLoot.Add(looseGoal != null ? (UnityEngine.Object)looseGoal : chestGoal); looseGoal = null; chestGoal = null; navigation.Clear(); return idle; }
                    bool taken = looseGoal != null ? weapon.BotTakeLoose(looseGoal) : weapon.BotLoot(chestGoal);
                    if (taken) { lootTaken++; looseGoal = null; chestGoal = null; navigation.Clear(); return idle; }
                    return navigation.Move(player, target.position);
                }
                return idle;
            }
            if (tripStage == 3)
            {
                if (motor.IsSwimming) { tripStage = 4; navigation.Clear(); return idle; }
                if (FlatDistance(player.transform.position, trip.Shore) < 2f)
                    return Toward(boardingLadder.transform.position, true);
                return navigation.Move(player, trip.Shore);
            }
            if (tripStage == 4)
            {
                var local = boardingLadder.transform.InverseTransformPoint(player.transform.position);
                if (!motor.IsClimbing && !motor.IsSwimming && player.Passenger.Ship == ship.Body && local.y >= boardingLadder.Height - .3f)
                { tripStage = 5; navigation.Clear(); return idle; }
                return ReturnAboard(ship);
            }
            var shelf = ship.GetComponentInChildren<RumShelf>();
            Vector3 deposit = shelf != null ? shelf.transform.position : ship.transform.TransformPoint(SessionController.Instance.Config.PlayerLocalSpawn);
            if (Vector3.Distance(player.transform.position, deposit) > 2.8f) return navigation.Move(player, deposit, ship.transform);
            player.GetComponent<NetworkWeapon>().BotUnload(ship);
            trip.NextVisit = Time.time + 45f; trip.Runner = null; trip.Island = null; trip.Anchored = false;
            trip = null; tripStage = 0; looseGoal = null; chestGoal = null; navigation.Clear();
            return idle;
        }
    }
}
