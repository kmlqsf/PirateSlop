using PirateSlop.Networking;
using UnityEngine;

namespace PirateSlop.Ships
{
    public sealed partial class ShipMonkey
    {
        [Min(.1f)] public float JumpSpeed = 4f;
        [Min(.1f)] public float JumpArcHeight = .8f;
        NetworkPlayer attacker;
        float aggressionUntil, retaliationCooldown, jumpTime, jumpDuration;
        Vector3 jumpFrom, jumpTo;
        int jumpPhase;
        bool pouncing, returning;

        Vector3 Point(int index)
        {
            var node = Nodes[index];
            return node.Surface == ShipMonkeySurface.Prop && node.Support != null
                ? transform.InverseTransformPoint(node.Support.TransformPoint(node.SupportLocalPoint)) : node.Position;
        }

        void ResetJump()
        {
            attacker = null; jumpPhase = 0; pouncing = returning = false;
            aggressionUntil = retaliationCooldown = 0f;
        }

        public void ReceiveFirearmShot(GameObject shooter)
        {
            var ship = GetComponent<NetworkShip>();
            var player = shooter != null ? shooter.GetComponent<NetworkPlayer>() : null;
            if (!initialized || ship == null || !ship.IsServerInitialized || ship.IsSinking || player == null || !player.IsSpawned || player.Motor == null || player.Motor.IsDead || player.Motor.IsDowned || player.Eliminated.Value) return;
            if (ship.TeamId.Value <= 0 || player.TeamId.Value != ship.TeamId.Value || Time.time < retaliationCooldown || attacker != null || jumpPhase != 0) return;
            CancelActivities();
            attacker = player; aggressionUntil = Time.time + 45f; retaliationCooldown = Time.time + 6f;
            wantsRest = false; restTime = 0f; pause = 0f; routeRunning = true;
            if (Motion == ShipMonkeyMotion.Sit || Motion == ShipMonkeyMotion.SitDown) SetMotion(ShipMonkeyMotion.StandUp);
            if (target < 0) path.Clear();
            else if (path.Count > pathIndex + 1) path.RemoveRange(pathIndex + 1, path.Count - pathIndex - 1);
        }

        bool Attackable() => attacker != null && attacker.IsSpawned && !attacker.Eliminated.Value && attacker.Motor != null && !attacker.Motor.IsDead && !attacker.Motor.IsDowned && attacker.TeamId.Value == GetComponent<NetworkShip>().TeamId.Value && Time.time < aggressionUntil;

        bool ClearJump(Vector3 from, Vector3 to, Transform ignore = null)
        {
            Vector3 delta = to - from;
            foreach (var hit in Physics.SphereCastAll(from, .12f, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(Visual) && (ignore == null || !hit.transform.IsChildOf(ignore))) return false;
            return true;
        }

        void UpdateRetaliation()
        {
            if (attacker == null || jumpPhase != 0) return;
            if (!Attackable()) { attacker = null; return; }
            if (Motion == ShipMonkeyMotion.Sit || Motion == ShipMonkeyMotion.SitDown || Motion == ShipMonkeyMotion.StandUp) return;
            Vector3 goal = transform.InverseTransformPoint(attacker.transform.position + Vector3.up * .65f);
            Vector3 delta = goal - position;
            if (delta.magnitude > 3.5f || Mathf.Abs(delta.y) > 1.6f) return;
            Vector3 from = transform.TransformPoint(position) + transform.up * .35f;
            if (!ClearJump(from, transform.TransformPoint(goal) + transform.up * .35f, attacker.transform)) return;
            path.Clear(); target = -1; wantsRest = false;
            BeginJump(goal, true);
        }

        void BeginJump(Vector3 destination, bool attack)
        {
            jumpFrom = position; jumpTo = destination; pouncing = attack; looking = false;
            jumpTime = 0f; jumpPhase = 1; animationSpeed = 1f;
            jumpDuration = Mathf.Clamp(Vector3.Distance(jumpFrom, jumpTo) / JumpSpeed, .35f, 1.05f);
            Vector3 forward = jumpTo - jumpFrom; forward.y = 0f;
            if (forward.sqrMagnitude > .001f) rotation = Quaternion.LookRotation(forward);
            SetMotion(ShipMonkeyMotion.JumpStart);
        }

        bool UpdateJump(float delta)
        {
            if (jumpPhase == 0) return false;
            if (!pouncing && !returning && target >= 0 && !Nodes[target].Available)
            { jumpPhase = 0; target = -1; path.Clear(); Recover(); return false; }
            jumpTime += delta;
            if (jumpPhase == 1)
            {
                if (!pouncing && !returning && Surface == ShipMonkeySurface.Prop && Nodes[current].Available) position = jumpFrom = Point(current);
                if (jumpTime >= (returning ? .06f : .2f)) { jumpPhase = 2; jumpTime = 0f; SetMotion(ShipMonkeyMotion.JumpAir); }
            }
            else if (jumpPhase == 2)
            {
                if (!pouncing && !returning && target >= 0) jumpTo = Point(target);
                float t = Mathf.Clamp01(jumpTime / jumpDuration);
                float height = Mathf.Max(JumpArcHeight, Mathf.Abs(jumpTo.y - jumpFrom.y) * .25f);
                Vector3 next = Vector3.Lerp(jumpFrom, jumpTo, t) + Vector3.up * (4f * height * t * (1f - t));
                if (pouncing && !ClearJump(transform.TransformPoint(position) + transform.up * .35f, transform.TransformPoint(next) + transform.up * .35f, attacker != null ? attacker.transform : null))
                { ReturnFromAttack(); return true; }
                position = next;
                if (t >= 1f)
                {
                    if (pouncing)
                    {
                        Vector3 contact = transform.TransformPoint(position) + transform.up * .35f;
                        if (Attackable() && Vector3.Distance(contact, attacker.transform.position + Vector3.up) < 1.25f)
                        {
                            Vector3 away = Vector3.ProjectOnPlane(attacker.transform.position - transform.TransformPoint(jumpFrom), Vector3.up).normalized;
                            if (away.sqrMagnitude < .01f) away = transform.TransformDirection(rotation * Vector3.forward);
                            attacker.KnockDown(away * 5f + Vector3.up * 3f, 2.8f);
                        }
                        ReturnFromAttack(); return true;
                    }
                    jumpPhase = 3; jumpTime = 0f; SetMotion(ShipMonkeyMotion.JumpLand);
                }
            }
            else if (jumpTime >= .27f)
            {
                jumpPhase = 0; pouncing = false;
                if (returning) { returning = false; target = -1; path.Clear(); pause = 1f; }
                else { current = target; target = -1; pathIndex++; }
                perched = Surface == ShipMonkeySurface.Rail || Surface == ShipMonkeySurface.Prop;
                SetMotion(ShipMonkeyMotion.Idle); animationSpeed = 1f;
                if (pathIndex < path.Count) StartSegment();
                else pause = wantsRest ? .7f : UnityEngine.Random.Range(PauseMin, PauseMax);
            }
            return true;
        }

        void ReturnFromAttack()
        {
            attacker = null; retaliationCooldown = Time.time + 6f;
            int nearest = -1; float best = float.PositiveInfinity;
            for (int i = 0; i < Nodes.Length; i++)
                if (Nodes[i].Available && Nodes[i].Surface != ShipMonkeySurface.Rigging)
                { float score = (Point(i) - position).sqrMagnitude; if (score < best) { best = score; nearest = i; } }
            path.Clear(); pathIndex = 0; target = -1;
            if (nearest < 0) { jumpPhase = 0; Recover(); return; }
            current = nearest; returning = true; BeginJump(Point(nearest), false);
        }
    }
}
