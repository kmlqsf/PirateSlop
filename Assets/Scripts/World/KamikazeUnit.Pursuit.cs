using UnityEngine;

namespace PirateSlop.World
{
    public sealed partial class KamikazeUnit
    {
        Vector3 leapStart, leapEnd, leapVelocity;
        float leapElapsed, leapDuration, leapArc;
        bool leapRequested;

        void RequestLeap()
        {
            Chasing = true;
            leapRequested = true;
            if (agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
            if (!Event.TakeLeapBudget()) return;
            leapStart = transform.position; leapEnd = target.transform.position;
            leapDuration = Mathf.Max(.2f, Event.LeapSeconds); leapElapsed = 0f;
            leapArc = Event.LeapArcHeight(leapStart, leapEnd);
            leapVelocity = Vector3.zero;
            agent.enabled = false; Leaping = true; leapRequested = false;
        }

        void ServerLeapStep()
        {
            if (Event.IsPursuitTarget(target)) leapEnd = target.transform.position;
            float elapsed = Mathf.Min(leapElapsed + Time.deltaTime, leapDuration);
            int steps = Mathf.Clamp(Mathf.CeilToInt((elapsed - leapElapsed) / .025f), 1, 4);
            float deltaTime = (elapsed - leapElapsed) / steps;
            for (int step = 0; step < steps; step++)
            {
                leapElapsed = Mathf.Min(leapElapsed + deltaTime, leapDuration);
                float fraction = leapElapsed / leapDuration;
                var point = Vector3.Lerp(leapStart, leapEnd, fraction) + Vector3.up * (Mathf.Sin(fraction * Mathf.PI) * leapArc);
                var previous = transform.position;
                if (Event.TraceLeap(previous, point, out var hit))
                {
                    var direction = (point - previous).normalized;
                    transform.position = previous + direction * Mathf.Max(0f, hit.distance - .03f);
                    Event.QueueExplosion(this);
                    return;
                }
                transform.position = point;
                leapVelocity = (point - previous) / Mathf.Max(.0001f, deltaTime);
                var facing = Vector3.ProjectOnPlane(leapEnd - point, Vector3.up);
                if (facing.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(facing);
                if (Event.IsPursuitTarget(target) && (BlastPoint - (target.transform.position + Vector3.up * .9f)).sqrMagnitude <= Event.DetonationDistance * Event.DetonationDistance &&
                    Event.ClearLine(BlastPoint, target.transform.position + Vector3.up * .9f, target.transform))
                {
                    Event.QueueExplosion(this);
                    return;
                }
            }
            if (leapElapsed >= leapDuration - .0001f) Event.QueueExplosion(this);
        }
    }
}
