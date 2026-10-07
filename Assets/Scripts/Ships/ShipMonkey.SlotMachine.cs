using UnityEngine;

namespace PirateSlop.Ships
{
    public sealed partial class ShipMonkey
    {
        public Vector2 SlotInterval = new(180f, 300f);
        ShipSlotMachine activitySlot;
        float nextSlot, nextSlotCheck;
        int slotSpinSequence;

        void BeginSlotActivity()
        {
            activitySlot = null; nextSlotCheck = 0f;
            ScheduleSlotActivity();
        }

        void ScheduleSlotActivity()
        {
            float minimum = Mathf.Max(180f, SlotInterval.x);
            nextSlot = Time.time + Random.Range(minimum, Mathf.Max(minimum, SlotInterval.y));
        }

        bool TryStartSlotTrip()
        {
            if (Time.time < nextSlot || Time.time < nextSlotCheck || HasCarriedItem) return false;
            nextSlotCheck = Time.time + 5f;
            var machine = activityShip.GetComponentInChildren<ShipSlotMachine>();
            if (machine == null || machine.Settings == null || machine.Ship.SlotState.Phase != 0) return false;
            FindActivityReachability();
            Vector3 goalPoint = machine.transform.TransformPoint(new Vector3(-.635f, 0, .78f));
            int goal = -1; float best = float.PositiveInfinity;
            for (int i = 0; i < Nodes.Length; i++)
            {
                if (!activityReachable[i] || !Nodes[i].Available || Nodes[i].Surface != ShipMonkeySurface.Deck) continue;
                Vector3 point = transform.TransformPoint(Point(i));
                Vector3 local = machine.transform.InverseTransformPoint(point);
                if (Mathf.Abs(local.y) > .25f || local.z < .5f || Vector3.Distance(point + transform.up * .6f, machine.LeverGrip.position) > 2f) continue;
                float score = (point - goalPoint).sqrMagnitude;
                if (score < best) { best = score; goal = i; }
            }
            if (goal < 0) return false;
            activitySlot = machine; ScheduleSlotActivity(); SetTask(TaskKind.GoSlot, goal);
            return true;
        }

        bool UpdateSlotActivity(float delta)
        {
            if (activitySlot == null || !activitySlot.gameObject.activeInHierarchy || activityShip.IsSinking)
            { CancelActivities(); return false; }
            var state = activityShip.SlotState;
            if (task == TaskKind.GoSlot)
            {
                if (state.Phase != 0) { CancelActivities(); return false; }
                if (target >= 0 || current != activityGoal) return false;
                Face(activitySlot.LeverGrip.position, 10f);
                if (!activityShip.BeginMonkeySlotTurn(this, activitySlot)) { CancelActivities(); return false; }
                task = TaskKind.SlotPull; animationSpeed = 1f; SetMotion(ShipMonkeyMotion.Work);
                return true;
            }
            if (task == TaskKind.SlotPull)
            {
                Face(activitySlot.LeverGrip.position, delta);
                if (!activityShip.PullMonkeySlotLever(this, delta / .8f)) { CancelActivities(); return false; }
                if (activityShip.SlotState.Phase == 2)
                {
                    slotSpinSequence = activityShip.SlotState.Sequence;
                    task = TaskKind.SlotWait; SetMotion(ShipMonkeyMotion.Idle);
                }
                return true;
            }
            if (state.Phase == 0 || state.Sequence > slotSpinSequence + 1)
            { CancelActivities(); return true; }
            Face(activitySlot.transform.TransformPoint(new Vector3(0, 1.01f, .35f)), delta);
            SetMotion(ShipMonkeyMotion.Idle); animationSpeed = 1f;
            return true;
        }

        void ResetSlotActivity()
        {
            if (activitySlot != null && activityShip != null) activityShip.ReleaseMonkeySlot(this);
            activitySlot = null;
        }

        bool SlotLinkClear(int from, int to)
        {
            if (task != TaskKind.GoSlot || activitySlot == null) return true;
            var start = activitySlot.transform.InverseTransformPoint(transform.TransformPoint(Point(from)));
            var end = activitySlot.transform.InverseTransformPoint(transform.TransformPoint(Point(to)));
            var bounds = new Bounds(new Vector3(0, .25f, 0), new Vector3(1.5f, .75f, 1.15f));
            if (bounds.Contains(end)) return false;
            if (bounds.Contains(start)) return true;
            var delta = end - start;
            return !bounds.IntersectRay(new Ray(start, delta.normalized), out float distance) || distance > delta.magnitude;
        }
    }
}
