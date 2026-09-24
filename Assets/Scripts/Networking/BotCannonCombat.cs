using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed class BotCannonStation : IBotShipStation
    {
        readonly SimpleCannon cannon;
        readonly NetworkShip ship, target;
        readonly RaycastHit[] hits = new RaycastHit[32];
        readonly Collider[] overlaps = new Collider[32];
        float nextScan, seenAt = -100f, started;
        Vector3 observed, velocity;
        bool acquired, fired, wasLoaded, blockedAim;
        public bool CannotEngage => acquired && blockedAim && Time.time - started > 2f;
        BotBoomerangShot boomerang;
        public string Reason { get; private set; } = "Наблюдение цели";
        public string Name => "Артиллерист: " + Reason;
        public Vector3 Position => cannon.transform.position + Vector3.up;
        public bool Available => cannon != null && cannon.gameObject.activeInHierarchy && ship != null && !ship.IsSinking &&
            (fired || target != null && !target.IsSinking) && cannon.Network != null;
        public bool Busy => cannon.Operator != null || cannon.RemoteOccupied;
        public bool Complete => acquired && !cannon.IsIgnited && (fired && !cannon.IsLoaded || target == null || target.IsSinking || (cannon.IsLoaded && Time.time - started > 14f) || Time.time - seenAt > 3f);
        public BotCannonStation(SimpleCannon cannon, NetworkShip ship, NetworkShip target)
        { this.cannon = cannon; this.ship = ship; this.target = target; }
        public void Validate() { }
        public bool Owned(NetworkPlayer player) => cannon != null && cannon.Operator == player.Motor;
        public bool Acquire(NetworkPlayer player)
        {
            if (!cannon.TakeControl(player.Motor)) return false;
            acquired = true; wasLoaded = cannon.IsLoaded; started = seenAt = Time.time; observed = target.transform.position + Vector3.up * 3f;
            return true;
        }
        public void Release(NetworkPlayer player) { if (Owned(player)) cannon.ReleaseControl(); }
        public static bool Visible(NetworkPlayer player, NetworkShip target)
        {
            if (player.GetComponent<NetworkHolyGrenadeHands>()?.BotBlinded == true) return false;
            if (target == null || target.IsSinking || target.TeamId.Value == player.TeamId.Value) return false;
            var eye = BotCombatMemory.Eye(player);
            var goal = target.transform.position + Vector3.up * 3f;
            if ((goal - eye).sqrMagnitude > 160f * 160f) return false;
            for (int i = 0; i < 3; i++)
            {
                var point = goal + (i == 0 ? Vector3.zero : target.transform.forward * (i == 1 ? 7f : -7f) + Vector3.up * 3f);
                if (!FirearmTrace.Cast(player.gameObject, eye, point, out var hit) || hit.collider.GetComponentInParent<NetworkShip>() == target) return true;
            }
            return false;
        }
        bool Solve(out Vector3 direction, out float duration)
        {
            if (!cannon.IsMortar && cannon.LoadedAmmo == InventoryItem.BoomerangCannonball)
                return BotBoomerangShot.Solve(cannon, ship, observed, velocity, out direction, out duration);
            direction = default; duration = 0;
            float speed = cannon.LaunchSpeed * 1.15f;
            float delay = cannon.FuseSeconds * (cannon.IsIgnited ? 1f - cannon.FuseProgress : 1f);
            var inherited = ship.Motor.CannonPointVelocity(cannon.Muzzle.position);
            var origin = cannon.ShotPosition + inherited * delay;
            float factor = 1f, distanceFactor = 0f, best = float.PositiveInfinity;
            var gravityVelocity = Vector3.zero; var gravityPosition = Vector3.zero;
            const float dt = .1f;
            float damping = Mathf.Exp(-.015f * dt);
            Vector3 previousRequired = default;
            for (int step = 1; step <= (cannon.IsMortar ? 120 : 60); step++)
            {
                float previousFactor = factor;
                factor *= damping; distanceFactor += (previousFactor + factor) * (.5f * dt);
                var previousGravity = gravityVelocity;
                gravityVelocity = CannonShotDamage.StepVelocity(gravityVelocity, dt); gravityPosition += (previousGravity + gravityVelocity) * (.5f * dt);
                float time = step * dt;
                var required = (observed + velocity * (delay + time) - origin - gravityPosition) / distanceFactor - inherited;
                var sampleRequired = required;
                if (step > 1 && (previousRequired.magnitude - speed) * (required.magnitude - speed) <= 0f)
                {
                    float low = 0f, high = 1f;
                    bool descending = previousRequired.magnitude > required.magnitude;
                    for (int iteration = 0; iteration < 10; iteration++)
                    {
                        float middle = (low + high) * .5f;
                        bool above = Vector3.Lerp(previousRequired, required, middle).magnitude > speed;
                        if (above == descending) low = middle; else high = middle;
                    }
                    float fraction = (low + high) * .5f;
                    sampleRequired = Vector3.Lerp(previousRequired, required, fraction);
                    time = (step - 1 + fraction) * dt;
                }
                previousRequired = required;
                var local = cannon.transform.InverseTransformDirection(sampleRequired.normalized);
                float elevation = Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;
                float traverse = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
                if (elevation < cannon.MinElevation || elevation > cannon.MaxElevation || Mathf.Abs(traverse) > cannon.MaxTraverse) continue;
                float error = Mathf.Abs(sampleRequired.magnitude - speed);
                if (error >= best) continue;
                best = error; direction = sampleRequired.normalized; duration = time;
            }
            return best < .65f;
        }
        bool ClearArc(Vector3 direction, float duration)
        {
            var previous = cannon.ShotPosition;
            int occupied = Physics.OverlapSphereNonAlloc(previous, cannon.ProjectileRadius + .1f, overlaps, ~0, QueryTriggerInteraction.Ignore);
            if (occupied == overlaps.Length) return false;
            for (int i = 0; i < occupied; i++) if (!overlaps[i].transform.IsChildOf(cannon.transform)) return false;
            var speed = direction * (cannon.LaunchSpeed * 1.15f) + ship.Motor.CannonPointVelocity(cannon.Muzzle.position);
            for (float t = 0; t < duration; t += .1f)
            {
                var previousSpeed = speed;
                speed = CannonShotDamage.StepVelocity(speed, .1f);
                var next = previous + (previousSpeed + speed) * .05f;
                int count = Physics.SphereCastNonAlloc(previous, cannon.ProjectileRadius + .1f, (next - previous).normalized,
                    hits, Vector3.Distance(previous, next), ~0, QueryTriggerInteraction.Ignore);
                if (count == hits.Length) return false;
                for (int i = 0; i < count; i++)
                {
                    if (hits[i].transform.IsChildOf(cannon.transform)) continue;
                    var body = hits[i].collider.GetComponentInParent<NetworkShip>();
                    if (body == target) continue;
                    var allyPlayer = hits[i].collider.GetComponentInParent<NetworkPlayer>();
                    if (allyPlayer != null && allyPlayer.TeamId.Value == ship.TeamId.Value && Vector3.Distance(cannon.ShotPosition, hits[i].point) > 12f) continue;
                    return false;
                }
                if (OceanSurface.Instance != null && next.y < OceanSurface.Instance.Height(next)) return false;
                foreach (var other in NetworkShip.ActiveShips)
                {
                    if (other == null || other == ship || other.TeamId.Value != ship.TeamId.Value) continue;
                    var predicted = other.transform.position + other.Motor.CannonPointVelocity(other.transform.position) * (t + cannon.FuseSeconds);
                    if (Vector3.Distance(next, predicted + Vector3.up * 2f) < other.CollisionRadius + 3f) return false;
                }
                previous = next;
            }
            return true;
        }
        bool SafeEffect(NetworkPlayer player, float flight)
        {
            if (!BotCannonAmmoPolicy.Supported(cannon.LoadedAmmo)) return false;
            if (cannon.LoadedAmmo == InventoryItem.Cannonball && !cannon.IsMortar) return true;
            float duration = flight + cannon.FuseSeconds;
            var impact = observed + velocity * duration;
            float radius = target.CollisionRadius + (cannon.IsMortar ? CannonAmmo.MortarBlastRadius(cannon.LoadedAmmo) + 3f : BotCannonAmmoPolicy.SafetyRadius(cannon.LoadedAmmo));
            if (cannon.LoadedAmmo == InventoryItem.BoardingHook && (!SessionController.Instance.BotBoardingAdvantage(ship, target) || Vector3.Distance(ship.transform.position, observed) > 55f || Mathf.Abs(ship.Motor.Speed) > 2f)) return false;
            if (SessionController.Instance.BotAllyNear(impact, radius, player)) return false;
            foreach (var other in NetworkShip.ActiveShips)
            {
                if (other == null || other.IsSinking || other.TeamId.Value != ship.TeamId.Value) continue;
                var predicted = other.transform.position + other.Motor.CannonPointVelocity(other.transform.position) * duration;
                if (Vector3.Distance(predicted, impact) < radius + other.CollisionRadius) return false;
            }
            return true;
        }
        public void Work(NetworkPlayer player, float delta)
        {
            if (!Owned(player)) return;
            cannon.TakeControl(player.Motor);
            if (cannon.IsLoaded && !wasLoaded) started = Time.time;
            wasLoaded = cannon.IsLoaded;
            if (cannon.IsIgnited) { Reason = "фитиль горит; наведение зафиксировано"; return; }
            if (Time.time < nextScan) return;
            nextScan = Time.time + .2f;
            if (!Visible(player, target)) { Reason = "цель скрылась; огонь запрещён"; return; }
            var point = target.transform.position + Vector3.up * 3f;
            var targetDestruction = target.GetComponent<ShipDestruction>();
            if (targetDestruction != null && targetDestruction.Sections != null)
            {
                float bestSectionDist = float.PositiveInfinity;
                Vector3 candidatePoint = point;
                foreach (var sec in targetDestruction.Sections)
                {
                    if (sec == null || sec.RemovedFragments == ulong.MaxValue) continue;
                    if (targetDestruction.Definition(sec.SectionId).Type != ShipSectionType.Hull) continue;
                    for (int frag = 0; frag < Mathf.Min(64, sec.RepairCount); frag++)
                    {
                        if ((sec.RemovedFragments & (1UL << frag)) != 0) continue;
                        var tForm = sec.RepairTransform(frag);
                        if (tForm == null) continue;
                        var p = tForm.TransformPoint(sec.RepairBounds(frag).center);
                        float d = Vector3.Distance(cannon.transform.position, p);
                        if (d < bestSectionDist) { bestSectionDist = d; candidatePoint = p; }
                        break;
                    }
                }
                if (!float.IsPositiveInfinity(bestSectionDist)) point = candidatePoint;
            }
            float elapsed = Time.time - seenAt;
            velocity = elapsed > .1f && elapsed < 1.5f ? Vector3.ClampMagnitude((point - observed) / elapsed, 20f) : Vector3.zero;
            observed = point; seenAt = Time.time;
            blockedAim = false;
            if (!Solve(out var direction, out float flight)) { blockedAim = true; Reason = "цель вне дальности или углов наведения"; return; }
            var local = cannon.transform.InverseTransformDirection(direction);
            float elevation = Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;
            float traverse = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            float dist = Vector3.Distance(cannon.transform.position, point);
            float spreadScale = Mathf.Clamp01((dist - 15f) / 100f);
            elevation += Mathf.Sin(Time.time * 1.7f + cannon.Index * 3.1f) * 1.5f * spreadScale;
            traverse += Mathf.Cos(Time.time * 1.9f + cannon.Index * 2.3f) * 1.5f * spreadScale;
            cannon.Aim(player.Motor, Mathf.MoveTowards(cannon.Elevation, elevation, 9f), Mathf.MoveTowardsAngle(cannon.Traverse, traverse, 12f));
            cannon.Network.PublishBotAim(cannon);
            if (Vector3.Angle(cannon.Muzzle.forward, direction) > 1.5f) { Reason = "наведение с упреждением"; return; }
            if (!cannon.IsLoaded || cannon.IsLoading || cannon.CooldownRemaining > 0f) { Reason = "ожидание готовности пушки"; return; }
            if (cannon.IsIgnited) { Reason = "фитиль горит"; return; }
            float volleyDelay = (cannon.Index % 4) * .15f;
            if (Time.time - started < 1f + volleyDelay) { Reason = "синхронизация бортового залпа"; return; }
            if (!cannon.IsMortar && cannon.LoadedAmmo == InventoryItem.BoomerangCannonball)
            {
                boomerang ??= new BotBoomerangShot();
                if (!boomerang.Clear(player, cannon, ship, target, cannon.Muzzle.forward))
                { blockedAim = true; Reason = "прямой или обратный путь ядра небезопасен"; return; }
            }
            else if (!ClearArc(cannon.Muzzle.forward, flight)) { blockedAim = true; Reason = "траектория небезопасна"; return; }
            if (!SafeEffect(player, flight)) { blockedAim = true; Reason = "эффект ядра опасен для своего экипажа или союзников"; return; }
            if (cannon.Network.TryFire(player, cannon.Index)) { fired = true; started = Time.time; Reason = "поджёг фитиль: " + BotCannonAmmoPolicy.Purpose(cannon.LoadedAmmo); }
        }
    }
}
