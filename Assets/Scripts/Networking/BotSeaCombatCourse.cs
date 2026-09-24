using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed class BotSeaCombatCourse
    {
        readonly NetworkShip[] candidates = new NetworkShip[3];
        SimpleCannon battery;
        float nextScan, nextBattery;
        Vector3 course;
        string reason;
        bool active;
        float range, clearanceRange, firingRange, closingSpeed, observedAt;
        NetworkShip observedTarget;
        public float SailDemand(NetworkShip ship, float turnError)
        {
            float turn = Mathf.Abs(turnError);
            if (!active || turn >= 60f) return 0f;
            float demand;
            if (range < clearanceRange) demand = 1f;
            else if (range - Mathf.Max(0f, closingSpeed) * 6f < clearanceRange + 10f) demand = 0f;
            else if (battery != null && battery.IsIgnited) demand = 0f;
            else if (range > firingRange + 25f) demand = 1f;
            else
            {
                float distanceError = Mathf.Abs(range - firingRange);
                demand = Mathf.Clamp01(distanceError / 25f);
                if (Mathf.Abs(ship.Motor.Speed) > 2f && distanceError < 12f) demand = 0f;
            }
            demand *= 1f - Mathf.Clamp01(turn / 60f) * .3f;
            return Mathf.Round(Mathf.Clamp01(demand) * 10f) / 10f;
        }
        public NetworkShip Target { get; private set; }
        public void Clear() { Target = null; battery = null; active = false; nextScan = 0; }
        bool Usable(SimpleCannon cannon) => cannon != null && cannon.gameObject.activeInHierarchy &&
            (!cannon.IsLoaded || BotCannonAmmoPolicy.Supported(cannon.LoadedAmmo)) &&
            Time.time - cannon.LastHumanControlTime >= SessionController.Instance.Config.BotMotion.HumanStationGrace;

        public bool TryCourse(NetworkShip ship, NetworkPlayer observer, float safe, out Vector3 point, out string description)
        {
            if (Target != null && Target.IsSinking) { Clear(); }
            if (Time.time >= nextScan)
            {
                nextScan = Time.time + 1f; active = false;
                if (observer == null || observer.Motor.IsDead || observer.Passenger.Ship != ship.Body)
                { Clear(); point = default; description = null; return false; }
                if (Target == null || !BotCannonStation.Visible(observer, Target))
                {
                    Target = null;
                    for (int i = 0; i < candidates.Length; i++) candidates[i] = null;
                    foreach (var candidate in NetworkShip.ActiveShips)
                    {
                        if (candidate == null || candidate == ship || candidate.IsSinking || candidate.TeamId.Value == ship.TeamId.Value) continue;
                        float distance = (candidate.transform.position - ship.transform.position).sqrMagnitude;
                        if (distance > 160f * 160f) continue;
                        for (int i = 0; i < candidates.Length; i++)
                            if (candidates[i] == null || distance < (candidates[i].transform.position - ship.transform.position).sqrMagnitude)
                            {
                                for (int j = candidates.Length - 1; j > i; j--) candidates[j] = candidates[j - 1];
                                candidates[i] = candidate; break;
                            }
                    }
                    foreach (var candidate in candidates)
                        if (candidate != null && BotCannonStation.Visible(observer, candidate)) { Target = candidate; break; }
                }
                var network = ship.GetComponent<NetworkCannon>();
                if (Target != null && network != null && network.Crate != null)
                {
                    var offset = Target.transform.position - ship.transform.position; offset.y = 0;
                    float bearing = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
                    if (!Usable(battery) || Time.time >= nextBattery)
                    {
                        nextBattery = Time.time + 4f;
                        float best = float.PositiveInfinity;
                        foreach (var cannon in network.Crate.Cannons)
                        {
                            if (!Usable(cannon) || network.HasBoarding(cannon.Index)) continue;
                            var local = ship.transform.InverseTransformDirection(cannon.transform.forward);
                            float desired = bearing - Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
                            float score = Mathf.Abs(Mathf.DeltaAngle(ship.transform.eulerAngles.y, desired)) -
                                (cannon.IsLoaded ? 25f : 0f) - (cannon == battery ? 20f : 0f);
                            if (score >= best) continue;
                            best = score; battery = cannon;
                        }
                        if (float.IsPositiveInfinity(best)) battery = null;
                    }
                    if (Usable(battery))
                    {
                        var local = ship.transform.InverseTransformDirection(battery.transform.forward);
                        float yaw = bearing - Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
                        var heading = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
                        float distance = offset.magnitude;
                        var flood = ship.GetComponent<ShipFlooding>();
                        float hpFraction = flood != null ? 1f - flood.Level : 1f;
                        float clearance = Mathf.Max(40f, ship.CollisionRadius + Target.CollisionRadius + 10f);
                        float desiredRange = Mathf.Max(85f, clearance + 20f);
                        bool closeWeapon = battery.LoadedAmmo == InventoryItem.PushCannonball || battery.LoadedAmmo == InventoryItem.BoomerangCannonball || battery.LoadedAmmo == InventoryItem.BoardingHook;
                        if (hpFraction > .5f && closeWeapon) desiredRange = Mathf.Clamp(clearance + 5f, 40f, 60f);
                        else if (hpFraction < .3f) desiredRange = 175f;
                        else if (battery.LoadedAmmo == InventoryItem.BoomerangCannonball)
                            desiredRange = Mathf.Max(clearance + 5f, Mathf.Min(85f, battery.LaunchSpeed * 1.15f * 1.4f));
                        closingSpeed = observedTarget == Target && Time.time - observedAt < 2.5f ?
                            Mathf.Clamp((range - distance) / Mathf.Max(.1f, Time.time - observedAt), -20f, 20f) : 0f;
                        observedTarget = Target; observedAt = Time.time;
                        range = distance; clearanceRange = clearance; firingRange = desiredRange;
                        var toUs = (ship.transform.position - Target.transform.position).normalized;
                        if (Mathf.Abs(Vector3.Dot(toUs, Target.transform.right)) > .35f)
                        {
                            var saferSide = Vector3.Dot(toUs, Target.transform.forward) >= 0 ? Target.transform.forward : -Target.transform.forward;
                            heading = (heading + saferSide * .6f).normalized;
                        }
                        heading = distance < clearance ? -offset.normalized :
                            (heading + offset.normalized * Mathf.Clamp((distance - desiredRange) / 40f, -.7f, .7f)).normalized;
                        course = ship.transform.position + heading * 60f;
                        if (hpFraction < .3f && PirateSlop.World.ProceduralWorld.Instance != null)
                        {
                            var islands = PirateSlop.World.ProceduralWorld.Instance.Layout.Locations;
                            if (islands != null)
                            {
                                float bestCover = float.PositiveInfinity;
                                Vector3 coverPoint = course;
                                foreach (var island in islands)
                                {
                                    var toIsland = island.Position - Target.transform.position;
                                    var toShipPos = ship.transform.position - Target.transform.position;
                                    if (Vector3.Dot(toIsland.normalized, toShipPos.normalized) > .5f && toIsland.sqrMagnitude < toShipPos.sqrMagnitude)
                                    {
                                        var behindIsland = island.Position + (island.Position - Target.transform.position).normalized * (island.Radius + 30f);
                                        float d = Vector3.Distance(ship.transform.position, behindIsland);
                                        if (d < bestCover) { bestCover = d; coverPoint = behindIsland; }
                                    }
                                }
                                if (!float.IsPositiveInfinity(bestCover))
                                {
                                    course = coverPoint;
                                    reason = $"Отступление в укрытие за остров: LoS перекрыт; дальность {distance:F0} м";
                                }
                            }
                        }
                        active = new Vector2(course.x, course.z).magnitude < safe;
                        if (reason == null || !reason.StartsWith("Отступление"))
                            reason = $"Боевой курс: команда {Target.TeamId.Value}, пушка {battery.Index + 1}, {distance:F0} м; " +
                                (distance < clearance ? "увеличить дистанцию" : "вывести цель в сектор пушки");
                    }
                }
                if (!active) Target = null;
            }
            point = course; description = reason;
            return active && Target != null && !Target.IsSinking;
        }
    }
}
