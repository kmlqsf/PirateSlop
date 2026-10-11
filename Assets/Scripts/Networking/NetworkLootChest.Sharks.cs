using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace PirateSlop.Networking
{
    public enum SharkMotionState : byte { Patrol, Diving, Waiting, Attacking, Returning, Dispersing }

    public struct SharkMotion
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public SharkMotionState State;
        public int Bite;
    }

    public sealed partial class NetworkLootChest
    {
        public const int SharkCount = 3;
        readonly SyncList<SharkMotion> sharkMotion = new();
        readonly SharkMotion[] sharkSimulation = new SharkMotion[SharkCount];
        readonly float[] sharkModeAt = new float[SharkCount], sharkWaitUntil = new float[SharkCount], sharkLastShot = new float[SharkCount];
        readonly Vector3[] sharkVelocity = new Vector3[SharkCount], sharkDivePoint = new Vector3[SharkCount];
        float sharkClock, sharkAccumulator, nextSharkSnapshot, nextSharkAttack;
        int chargingShark = -1, nextChargingShark;
        bool sharksInitialized, chargingFromDepth;

        public SharkMotion SharkPose(int index)
        {
            if (IsServerInitialized && sharksInitialized) return sharkSimulation[index];
            if (index < sharkMotion.Count) return sharkMotion[index];
            float angle = index * Mathf.PI * 2f / SharkCount;
            return new SharkMotion { Position = EventPoint + new Vector3(Mathf.Cos(angle) * 8.5f, -1.1f - index * .35f, Mathf.Sin(angle) * 8.5f), Rotation = Quaternion.LookRotation(new Vector3(-Mathf.Sin(angle), 0, Mathf.Cos(angle))) };
        }

        void InitializeSharks()
        {
            sharkMotion.Clear();
            for (int i = 0; i < SharkCount; i++)
            {
                float angle = i * Mathf.PI * 2f / SharkCount;
                sharkSimulation[i] = new SharkMotion { Position = EventPoint + new Vector3(Mathf.Cos(angle) * 8.5f, -1.1f - i * .35f, Mathf.Sin(angle) * 8.5f), Rotation = Quaternion.LookRotation(new Vector3(-Mathf.Sin(angle), 0, Mathf.Cos(angle))) };
                sharkLastShot[i] = -1; sharkMotion.Add(sharkSimulation[i]);
            }
            sharksInitialized = true;
        }

        public void ShootShark(int index, GameObject shooter)
        {
            if (!IsServerInitialized || Kind != SeaLootKind.Shark || !sharksInitialized || index < 0 || index >= SharkCount || opened.Value || carrier.Value != null || shooter == null || Time.time - sharkLastShot[index] < .25f) return;
            sharkLastShot[index] = Time.time;
            var pose = sharkSimulation[index]; pose.State = SharkMotionState.Diving; sharkModeAt[index] = Time.time;
            var radial = Vector3.ProjectOnPlane(pose.Position - EventPoint, Vector3.up).normalized;
            if (radial.sqrMagnitude < .01f) radial = pose.Rotation * Vector3.right;
            sharkDivePoint[index] = EventPoint + radial * (9f + index * 1.5f) + Vector3.down * (15f + index * 2f);
            sharkSimulation[index] = pose;
            if (chargingShark == index) { chargingShark = -1; chargingFromDepth = false; }
            nextSharkAttack = Mathf.Max(nextSharkAttack, Time.time + .7f); PublishSharks();
        }

        NetworkPlayer SharkPlayer(float radius)
        {
            NetworkPlayer target = null; float best = radius * radius;
            foreach (var player in NetworkPlayer.Active)
            {
                if (player == null || !player.IsSpawned || player.Motor.IsDead || !player.Motor.IsSwimming) continue;
                var delta = player.transform.position - EventPoint;
                float score = Vector3.ProjectOnPlane(delta, Vector3.up).sqrMagnitude;
                if (score >= best || delta.y < -40f || delta.y > 3f) continue;
                best = score; target = player;
            }
            return target;
        }

        void UpdateSharkBehavior()
        {
            if (!sharksInitialized) InitializeSharks();
            var target = opened.Value || carrier.Value != null || SharksDistracted ? null : SharkPlayer(16f);
            var ambush = target != null && Vector3.ProjectOnPlane(target.transform.position - EventPoint, Vector3.up).sqrMagnitude <= 49f ? target : null;
            sharkTargetPlayer.Value = target != null ? target.NetworkObject : null;
            sharkAccumulator = Mathf.Min(.15f, sharkAccumulator + Time.deltaTime);
            while (sharkAccumulator >= 1f / 30f) { sharkAccumulator -= 1f / 30f; SimulateSharks(target, ambush, 1f / 30f); }
            if (Time.time >= nextSharkSnapshot) { nextSharkSnapshot = Time.time + .1f; PublishSharks(); }
        }

        void SimulateSharks(NetworkPlayer target, NetworkPlayer ambush, float dt)
        {
            sharkClock += dt;
            bool dispersing = opened.Value || carrier.Value != null;
            if (!dispersing && !SharksDistracted && target != null && chargingShark < 0 && Time.time >= nextSharkAttack)
                for (int n = 0; n < SharkCount; n++)
                {
                    int i = (nextChargingShark + n) % SharkCount;
                    if (sharkSimulation[i].State != SharkMotionState.Patrol) continue;
                    StartSharkCharge(i); break;
                }
            for (int i = 0; i < SharkCount; i++)
            {
                var pose = sharkSimulation[i]; float age = Time.time - sharkModeAt[i];
                if (dispersing && pose.State != SharkMotionState.Dispersing)
                {
                    pose.State = SharkMotionState.Dispersing; sharkModeAt[i] = Time.time; age = 0;
                    if (chargingShark == i) { chargingShark = -1; chargingFromDepth = false; }
                }
                Vector3 goal; float speed;
                if (pose.State == SharkMotionState.Diving)
                {
                    goal = sharkDivePoint[i]; speed = 9f;
                    if (age > 1f && (goal - pose.Position).sqrMagnitude < 2.25f)
                    {
                        pose.State = SharkMotionState.Waiting; sharkModeAt[i] = Time.time; sharkWaitUntil[i] = Time.time + 7f;
                    }
                }
                else if (pose.State == SharkMotionState.Waiting)
                {
                    float phase = sharkClock * .22f + i * 2.1f;
                    goal = sharkDivePoint[i] + new Vector3(Mathf.Sin(phase) * 2f, Mathf.Sin(phase * .7f) * .5f, Mathf.Cos(phase) * 2f); speed = 2.2f;
                    if (ambush != null && !chargingFromDepth && !SharksDistracted)
                    {
                        sharkSimulation[i] = pose; StartSharkCharge(i, true); pose = sharkSimulation[i]; age = 0;
                    }
                    else if (Time.time >= sharkWaitUntil[i]) { pose.State = SharkMotionState.Returning; sharkModeAt[i] = Time.time; }
                }
                else if (pose.State == SharkMotionState.Dispersing)
                {
                    var outward = Vector3.ProjectOnPlane(pose.Position - EventPoint, Vector3.up).normalized;
                    goal = pose.Position + outward * 12f + Vector3.down * 8f; speed = 7f;
                }
                else
                {
                    goal = SharkOrbit(i, target); speed = target != null ? 6.5f : SharksDistracted ? 6f : 4.6f;
                    if (pose.State == SharkMotionState.Returning && (goal - pose.Position).sqrMagnitude < 9f) { pose.State = SharkMotionState.Patrol; sharkModeAt[i] = Time.time; }
                }
                if (pose.State == SharkMotionState.Attacking)
                {
                    if (target == null || SharksDistracted || age > 5.5f)
                    {
                        pose.State = SharkMotionState.Returning; sharkModeAt[i] = Time.time;
                        if (chargingShark == i) { chargingShark = -1; chargingFromDepth = false; }
                        nextSharkAttack = Time.time + .55f; goal = SharkOrbit(i, target); speed = 6.5f;
                    }
                    else
                    {
                        var body = target.transform.position + Vector3.up * .9f;
                        var direction = (body - pose.Position).normalized;
                        goal = body - direction * 2.8f; speed = 11f;
                        var nose = pose.Position + pose.Rotation * Vector3.forward * 3.35f;
                        if ((nose - body).sqrMagnitude < 2.25f && Vector3.Dot(pose.Rotation * Vector3.forward, direction) > .6f && Time.time >= nextSharkAttack)
                        {
                            target.GetComponent<CombatHealth>()?.Damage(25f); pose.Bite++; SharkAttackRpc(i, pose.Bite, body);
                            pose.State = SharkMotionState.Returning; sharkModeAt[i] = Time.time;
                            if (chargingShark == i) { chargingShark = -1; chargingFromDepth = false; }
                            nextSharkAttack = Time.time + 1.2f; goal = SharkOrbit(i, target); speed = 7f;
                        }
                    }
                }
                var desired = Vector3.ClampMagnitude((goal - pose.Position) * 2f, speed);
                for (int j = 0; j < SharkCount; j++)
                {
                    if (j == i) continue;
                    var delta = SharkBodyGap(pose.Position + sharkVelocity[i] * .3f, pose.Rotation, sharkSimulation[j].Position + sharkVelocity[j] * .3f, sharkSimulation[j].Rotation);
                    float distance = delta.magnitude;
                    if (distance < 3.8f) desired += (distance > .001f ? delta / distance : (i < j ? Vector3.left : Vector3.right)) * ((3.8f - distance) * 4f);
                }
                sharkVelocity[i] = Vector3.MoveTowards(sharkVelocity[i], Vector3.ClampMagnitude(desired, speed), dt * 18f);
                pose.Position += sharkVelocity[i] * dt;
                float surface = OceanSurface.Instance != null ? OceanSurface.Instance.Height(pose.Position) : EventPoint.y;
                pose.Position.y = Mathf.Min(pose.Position.y, surface - .85f);
                if (sharkVelocity[i].sqrMagnitude > .1f) pose.Rotation = Quaternion.RotateTowards(pose.Rotation, Quaternion.LookRotation(sharkVelocity[i].normalized, Vector3.up), (pose.State == SharkMotionState.Attacking ? 160f : 95f) * dt);
                sharkSimulation[i] = pose;
            }
            for (int pass = 0; pass < 8; pass++) for (int i = 0; i < SharkCount; i++) for (int j = i + 1; j < SharkCount; j++)
            {
                var a = sharkSimulation[i]; var b = sharkSimulation[j]; SeparateSharks(ref a, ref b); sharkSimulation[i] = a; sharkSimulation[j] = b;
            }
        }

        void StartSharkCharge(int i, bool fromDepth = false)
        {
            if (chargingShark >= 0 && chargingShark != i)
            {
                var previous = sharkSimulation[chargingShark]; previous.State = SharkMotionState.Returning;
                sharkSimulation[chargingShark] = previous; sharkModeAt[chargingShark] = Time.time;
            }
            chargingFromDepth = fromDepth;
            chargingShark = i; nextChargingShark = (i + 1) % SharkCount;
            var pose = sharkSimulation[i]; pose.State = SharkMotionState.Attacking; sharkSimulation[i] = pose; sharkModeAt[i] = Time.time;
        }

        Vector3 SharkOrbit(int i, NetworkPlayer target)
        {
            float angle = sharkClock * .52f + i * Mathf.PI * 2f / SharkCount + Mathf.Sin(sharkClock * .37f + i * 1.4f) * .1f;
            float radius = (target != null ? 6.8f : SharksDistracted ? 6.5f : 8.5f) + Mathf.Sin(sharkClock * .63f + i * 1.9f) * .85f + Mathf.Cos(sharkClock * .27f + i) * .45f;
            var center = target != null ? target.transform.position + Vector3.up * .65f : SharksDistracted ? SharkBaitPoint : EventPoint;
            var point = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
            float surface = OceanSurface.Instance != null ? OceanSurface.Instance.Height(point) : EventPoint.y;
            point.y = target != null ? Mathf.Min(surface - 1.1f, center.y - i * .8f + Mathf.Sin(sharkClock * .6f + i) * .65f) : surface - 1.1f - i * .4f - (Mathf.Sin(sharkClock * .55f + i * 2f) + 1f) * .65f;
            return point;
        }

        void PublishSharks()
        {
            for (int i = 0; i < SharkCount; i++) if (i < sharkMotion.Count) sharkMotion[i] = sharkSimulation[i]; else sharkMotion.Add(sharkSimulation[i]);
        }

        public static void SeparateSharks(ref SharkMotion a, ref SharkMotion b)
        {
            var delta = SharkBodyGap(a.Position, a.Rotation, b.Position, b.Rotation); float distance = delta.magnitude;
            if (distance >= 3.1f) return;
            var correction = (distance > .001f ? delta / distance : Vector3.right) * ((3.1f - distance) * .5f); a.Position += correction; b.Position -= correction;
        }

        static Vector3 SharkBodyGap(Vector3 a, Quaternion ar, Vector3 b, Quaternion br)
        {
            var af = ar * Vector3.forward; var bf = br * Vector3.forward;
            var p = a - af * .3f; var q = b - bf * .3f; var u = af * 3.1f; var v = bf * 3.1f; var w = p - q;
            float aa = Vector3.Dot(u, u), bb = Vector3.Dot(u, v), cc = Vector3.Dot(v, v), dd = Vector3.Dot(u, w), ee = Vector3.Dot(v, w), denominator = aa * cc - bb * bb;
            float s = denominator > .0001f ? Mathf.Clamp01((bb * ee - cc * dd) / denominator) : 0f;
            float t = Mathf.Clamp01((bb * s + ee) / cc); s = Mathf.Clamp01((bb * t - dd) / aa); t = Mathf.Clamp01((bb * s + ee) / cc);
            return p + u * s - q - v * t;
        }

        [ObserversRpc(RunLocally = true)]
        void SharkAttackRpc(int index, int sequence, Vector3 point)
        {
            GameAudio.Play(SoundCue.BulletFlesh, point); CombatVfx.Impact(point, Vector3.up, false); if (swarm != null) swarm.Bite(index, sequence);
        }
    }
}
