using System;
using PirateSlop.Networking;
using UnityEngine;

namespace PirateSlop.Performance
{
    [Serializable]
    public sealed class PerformanceCombatSummary
    {
        public string schedule = PerformanceCombatScenario.ScheduleVersion;
        public string limitations = "Local host, scripted controls, no AI or remote clients. Cannon loading bypasses inventory/reach checks; firing, flight, damage and effects use gameplay code. POV follows a participant eye and hides its world mesh; it does not simulate an owned client or first-person weapon rendering. No immunity, repair or replacement of casualties.";
        public int cannonShots, cannonImpacts, firearmShots, firearmImpacts, playerDamageEvents, deaths, cameraFallbacks;
        public int minAlivePlayers = 18;
        public int shipsLost, minAliveShips = 6;
        public float[] firstSinkingSeconds = { -1, -1, -1, -1, -1, -1 };
        public int[] shotsByShip = new int[6], hitsByShip = new int[6];
        public float[] distanceByShip = new float[6];
    }

    public sealed class PerformanceCombatScenario : IDisposable
    {
        public const string ScheduleVersion = "circle-six-c12-p5-v5-attrition";
        public readonly PerformanceCombatSummary Summary = new();
        readonly NetworkShip[] ships = new NetworkShip[6];
        readonly NetworkPlayer[] crew = new NetworkPlayer[18];
        readonly NetworkWeapon[] weapons = new NetworkWeapon[18];
        readonly PirateWeapon[] pistols = new PirateWeapon[18];
        readonly SimpleCannon[] cannons = new SimpleCannon[6];
        readonly Vector3[] lastPositions = new Vector3[6];
        readonly float[] nextCannon = new float[6], nextPistol = new float[18];
        readonly Renderer[][] bodies = new Renderer[18][];
        bool[] hiddenFlags;
        Renderer[] hidden;
        readonly Vector3 center;
        readonly float radius, sail;
        float nextControl;
        bool recording;
        bool povOverviewFallback;
        string failure;

        public PerformanceCombatScenario(Vector3 arena, float arenaRadius, float deploy)
        {
            center = arena; radius = arenaRadius; sail = deploy;
            var ordered = new System.Collections.Generic.List<NetworkShip>();
            foreach (var ship in NetworkShip.ActiveShips)
                if (ship != null && ship.TeamId.Value > 0 && ship.IsSpawned) ordered.Add(ship);
            ordered.Sort((a, b) => a.TeamId.Value.CompareTo(b.TeamId.Value));
            if (ordered.Count != 6) throw new InvalidOperationException("Combat requires exactly six team ships.");
            try
            {
                for (int i = 0; i < 6; i++)
                {
                    var ship = ships[i] = ordered[i];
                    var members = new System.Collections.Generic.List<NetworkPlayer>();
                    foreach (var player in NetworkPlayer.Active)
                        if (player != null && player.IsLoadTestParticipant && player.Ship == ship) members.Add(player);
                    members.Sort((a, b) => a.ParticipantId.Value.CompareTo(b.ParticipantId.Value));
                    if (members.Count != 3) throw new InvalidOperationException("Combat requires three participants per ship.");
                    for (int j = 0; j < 3; j++)
                    {
                        int index = i * 3 + j;
                        crew[index] = members[j]; bodies[index] = members[j].GetComponentsInChildren<Renderer>(true);
                        weapons[index] = members[j].GetComponent<NetworkWeapon>();
                        pistols[index] = members[j].GetComponent<PirateWeapon>();
                        weapons[index].SelectServerSlot(0);
                        nextPistol[index] = 60 + index * .12f;
                    }
                    var network = ship.GetComponent<NetworkCannon>();
                    if (network == null || network.Crate == null) throw new InvalidOperationException("Combat cannon supply missing.");
                    Vector3 local = DeckPoint(ship, 3f, -3f);
                    network.Place(local, Quaternion.Euler(0, 90, 0));
                    cannons[i] = network.Crate.Cannons[network.Crate.Cannons.Count - 1];
                    var ocean = OceanSurface.Instance;
                    if (ocean == null || cannons[i].ShotPosition.y <= ocean.Height(cannons[i].ShotPosition) + .4f) throw new InvalidOperationException("Combat cannon muzzle is below the safe water clearance.");
                    lastPositions[i] = ship.transform.position;
                    nextCannon[i] = 60 + i * .35f;
                }
            }
            catch { Dispose(); throw; }
        }

        static Vector3 DeckPoint(NetworkShip ship, float x, float z)
        {
            Vector3 origin = ship.transform.TransformPoint(new Vector3(x, 18, z));
            float nearest = 30;
            Vector3 result = default;
            bool found = false;
            foreach (var hit in Physics.RaycastAll(origin, -ship.transform.up, 30, ~0, QueryTriggerInteraction.Ignore))
            {
                float height = ship.transform.InverseTransformPoint(hit.point).y;
                if (hit.collider.attachedRigidbody != ship.Body || hit.normal.y < .9f || height < 3 || height > 8 || hit.distance >= nearest || hit.collider.GetComponentInParent<SimpleCannon>() != null) continue;
                nearest = hit.distance; result = ship.transform.InverseTransformPoint(hit.point + hit.normal * .04f); found = true;
            }
            if (!found) throw new InvalidOperationException("Combat deck mount could not be found.");
            return result;
        }

        public void Step(float elapsed, bool measuring)
        {
            if (!measuring) return;
            if (!recording)
            {
                recording = true;
                for (int i = 0; i < 6; i++) lastPositions[i] = ships[i].transform.position;
            }
            if (elapsed < nextControl) return;
            nextControl = elapsed + .1f;
            int alive = 0;
            foreach (var player in crew) if (player != null && !player.Motor.IsDead) alive++;
            Summary.minAlivePlayers = Math.Min(Summary.minAlivePlayers, alive);
            for (int i = 0; i < 6; i++)
            {
                var ship = ships[i];
                if (ship == null)
                {
                    if (Summary.firstSinkingSeconds[i] < 0) failure ??= "Combat ship disappeared without observed sinking: " + i;
                    continue;
                }
                if (ship.IsSinking)
                {
                    if (Summary.firstSinkingSeconds[i] < 0)
                    {
                        Summary.firstSinkingSeconds[i] = elapsed;
                        Summary.shipsLost++;
                        Summary.minAliveShips = 6 - Summary.shipsLost;
                        PerformanceCapture.Event("CombatShipSinking", ship, ship.transform.position, i);
                    }
                    continue;
                }
                var position = ship.transform.position;
                Summary.distanceByShip[i] += Vector3.Distance(position, lastPositions[i]); lastPositions[i] = position;
                var radial = Vector3.ProjectOnPlane(position - center, Vector3.up);
                float angle = Mathf.Atan2(radial.x, radial.z) + .3f;
                var ahead = center + new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * radius;
                Vector3 direction = ahead - position;
                float desired = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                float rudder = Mathf.Clamp(Mathf.DeltaAngle(ship.transform.eulerAngles.y, desired) / 35f, -.8f, .8f);
                ship.Helm.MonkeyAdjust(rudder - ship.Helm.CurrentRudderNormalized);
                ship.GetComponent<SailSystem>().SetDeploy(sail);
                if (elapsed >= 60) ControlCannon(i, elapsed);
            }
            if (elapsed < 60) return;
            for (int i = 0; i < crew.Length; i++)
            {
                var player = crew[i];
                if (player == null || player.Motor.IsDead) continue;
                var target = TargetPlayer(i);
                if (target == null) continue;
                var weapon = weapons[i];
                Vector3 eye = Vector3.up * (player.Motor.IsCrouched ? .75f : 1.65f);
                Vector3 direction = (target.transform.position + Vector3.up * 1.2f - player.transform.position - eye).normalized;
                if (!pistols[i].Loaded)
                {
                    if (!pistols[i].Reloading) weapon.TryAct(1, direction, eye, true);
                    continue;
                }
                if (elapsed < nextPistol[i]) continue;
                if (weapon.TryAct(0, direction, eye, true, 41719 + i * 1000 + (int)(elapsed / 5)))
                {
                    Summary.firearmShots++; nextPistol[i] += 5;
                    if (nextPistol[i] <= elapsed) nextPistol[i] = elapsed + 5;
                }
            }
        }

        NetworkPlayer TargetPlayer(int shooter)
        {
            int team = shooter / 3, member = shooter % 3;
            for (int j = 0; j < 3; j++)
            {
                var candidate = crew[((team + 1) % 6) * 3 + (member + j) % 3];
                if (candidate != null && !candidate.Motor.IsDead) return candidate;
            }
            return null;
        }

        public void AimParticipant(NetworkPlayer player, ref PlayerCommand command)
        {
            int index = Array.IndexOf(crew, player);
            var target = index >= 0 ? TargetPlayer(index) : null;
            if (target == null) return;
            Vector3 movement = player.Ship.transform.TransformDirection(new Vector3(command.Move.x, 0, command.Move.y));
            Vector3 direction = target.transform.position + Vector3.up * 1.2f - player.transform.position - Vector3.up * 1.65f;
            command.Yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            command.Pitch = -Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude) * Mathf.Rad2Deg;
            movement = Quaternion.Inverse(Quaternion.Euler(0, command.Yaw, 0)) * movement;
            command.Move = new Vector2(movement.x, movement.z);
        }

        void ControlCannon(int index, float elapsed)
        {
            var cannon = cannons[index];
            if (cannon == null || !cannon.IsIgnited && elapsed < nextCannon[index]) return;
            var target = ships[(index + 3) % 6];
            if (target == null || target.IsSinking) return;
            NetworkPlayer gunner = null;
            for (int j = 0; j < 3; j++) if (crew[index * 3 + j] != null && !crew[index * 3 + j].Motor.IsDead) { gunner = crew[index * 3 + j]; break; }
            if (gunner == null) return;
            Vector3 point = target.transform.TransformPoint(new Vector3(0, 3.3f, Mathf.Sin(index + elapsed * .13f) * 7));
            if (!Solve(cannon, ships[index], target, point, out var direction)) return;
            Vector3 local = cannon.transform.InverseTransformDirection(direction);
            cannon.SetAim(Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg, Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg);
            cannon.Network.PublishBotAim(cannon);
            if (cannon.IsIgnited) return;
            if (!cannon.IsLoaded) cannon.LoadAmmo(InventoryItem.Cannonball);
            if (cannon.IsLoading || cannon.CooldownRemaining > 0) return;
            cannon.Fire(gunner.gameObject);
            if (cannon.IsIgnited) { nextCannon[index] += 12; if (nextCannon[index] <= elapsed) nextCannon[index] = elapsed + 12; }
        }

        static bool Solve(SimpleCannon cannon, NetworkShip source, NetworkShip target, Vector3 point, out Vector3 result)
        {
            result = default;
            float speed = cannon.LaunchSpeed * 1.15f, factor = 1, distanceFactor = 0, best = .65f;
            float delay = cannon.FuseSeconds * (cannon.IsIgnited ? 1 - cannon.FuseProgress : 1);
            Vector3 inherited = source.Motor.CannonPointVelocity(cannon.Muzzle.position);
            Vector3 velocity = target.Motor.CannonPointVelocity(point);
            Vector3 origin = cannon.ShotPosition + inherited * delay, gravityVelocity = Vector3.zero, gravityPosition = Vector3.zero, previousRequired = Vector3.zero;
            const float dt = .1f;
            for (int step = 1; step <= 60; step++)
            {
                float previousFactor = factor; factor *= Mathf.Exp(-.015f * dt); distanceFactor += (previousFactor + factor) * (.5f * dt);
                var before = gravityVelocity; gravityVelocity = CannonShotDamage.StepVelocity(gravityVelocity, dt); gravityPosition += (before + gravityVelocity) * (.5f * dt);
                var required = (point + velocity * (delay + step * dt) - origin - gravityPosition) / distanceFactor - inherited;
                var sample = required;
                if (step > 1 && (previousRequired.magnitude - speed) * (required.magnitude - speed) <= 0)
                {
                    float lo = 0, hi = 1;
                    bool descending = previousRequired.magnitude > required.magnitude;
                    for (int j = 0; j < 10; j++) { float mid = (lo + hi) * .5f; if ((Vector3.Lerp(previousRequired, required, mid).magnitude > speed) == descending) lo = mid; else hi = mid; }
                    sample = Vector3.Lerp(previousRequired, required, (lo + hi) * .5f);
                }
                previousRequired = required;
                Vector3 local = cannon.transform.InverseTransformDirection(sample.normalized);
                float elevation = Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg, traverse = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
                float error = Mathf.Abs(sample.magnitude - speed);
                if (elevation < cannon.MinElevation || elevation > cannon.MaxElevation || Mathf.Abs(traverse) > cannon.MaxTraverse || error >= best) continue;
                best = error; result = sample.normalized;
            }
            return result.sqrMagnitude > .5f;
        }

        public void Observe(string kind, UnityEngine.Object target)
        {
            if (!recording) return;
            if (kind == "CannonShot" && target is CannonShotDamage shot && shot.Authoritative && shot.Source != null)
            {
                int index = Array.IndexOf(ships, shot.Source.GetComponent<NetworkShip>());
                if (index >= 0) { Summary.cannonShots++; Summary.shotsByShip[index]++; }
            }
            if (kind == "CannonImpact:server" && target is Collider hit)
            {
                int index = Array.IndexOf(ships, hit.GetComponentInParent<NetworkShip>());
                if (index >= 0) { Summary.cannonImpacts++; Summary.hitsByShip[index]++; }
            }
            if (kind == "FirearmImpact:visual") Summary.firearmImpacts++;
            if (target is CombatHealth health && Array.IndexOf(crew, health.GetComponent<NetworkPlayer>()) >= 0)
            {
                if (kind == "PlayerDamage") Summary.playerDamageEvents++;
                if (kind == "PlayerDeath") Summary.deaths++;
            }
        }

        public void PositionCamera(Camera camera, int segment, float local)
        {
            int shot = segment % 3;
            if (shot != 1) RestoreBody();
            NetworkShip ship = null;
            foreach (var candidate in ships) if (candidate != null && !candidate.IsSinking) { ship = candidate; break; }
            int actor = -1;
            if (shot == 1)
                for (int i = 0; i < crew.Length; i++) if (crew[i] != null && !crew[i].Motor.IsDead) { actor = i; break; }
            if (shot == 1 && actor < 0)
            {
                RestoreBody();
                if (!povOverviewFallback) { Summary.cameraFallbacks++; PerformanceCapture.Event("CombatPOV:NoLivingParticipant"); }
                povOverviewFallback = true; shot = 0;
            }
            else povOverviewFallback = false;
            if (shot == 1)
            {
                var player = crew[actor];
                if (hidden != bodies[actor])
                {
                    if (hidden != null) Summary.cameraFallbacks++;
                    RestoreBody(); hidden = bodies[actor]; hiddenFlags = new bool[hidden.Length];
                    for (int i = 0; i < hidden.Length; i++) if (hidden[i] != null) { hiddenFlags[i] = hidden[i].forceRenderingOff; hidden[i].forceRenderingOff = true; }
                    PerformanceCapture.Event("CombatPOV", player);
                }
                foreach (var renderer in hidden) if (renderer != null && !renderer.forceRenderingOff) renderer.forceRenderingOff = true;
                Vector3 eye = player.Motor.PlayerCamera != null ? player.Motor.PlayerCamera.transform.position : player.transform.position + Vector3.up * 1.65f;
                var target = TargetPlayer(actor);
                Vector3 direction = target != null ? target.transform.position + Vector3.up * 1.2f - eye : player.transform.forward;
                camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(direction, Vector3.up));
                return;
            }
            var radial = ship != null ? Vector3.ProjectOnPlane(ship.transform.position - center, Vector3.up) : Vector3.forward * radius;
            float angle = Mathf.Atan2(radial.x, radial.z) + (shot == 0 ? -.7f : .8f) + Mathf.SmoothStep(0, 1, local / 20) * .45f;
            var position = center + new Vector3(Mathf.Sin(angle) * (shot == 0 ? 180 : 235), shot == 0 ? 28 : 65, Mathf.Cos(angle) * (shot == 0 ? 180 : 235));
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(center + Vector3.up * 6 - position, Vector3.up));
        }

        public string ValidateResult()
        {
            if (failure != null) return failure;
            for (int i = 0; i < 6; i++)
            {
                if (Summary.distanceByShip[i] < 30) return "Combat ship did not move sufficiently: " + i;
                if (Summary.shotsByShip[i] < 2 || Summary.hitsByShip[i] < 1) return "Combat cannon fire/hits incomplete for ship " + i;
            }
            if (Summary.firearmShots < 18 || Summary.firearmImpacts < 6 || Summary.playerDamageEvents < 1) return "Combat firearm fire/hits were not established.";
            return null;
        }

        void RestoreBody()
        {
            if (hidden == null) return;
            for (int i = 0; i < hidden.Length; i++) if (hidden[i] != null) hidden[i].forceRenderingOff = hiddenFlags[i];
            hidden = null; hiddenFlags = null;
        }

        public void Dispose() => RestoreBody();
    }
}
