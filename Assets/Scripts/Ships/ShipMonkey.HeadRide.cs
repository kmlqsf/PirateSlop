using FishNet.Object;
using PirateSlop.Networking;
using UnityEngine;

namespace PirateSlop.Ships
{
    public sealed partial class ShipMonkey
    {
        public Vector2 HeadRideInterval = new(120f, 240f);
        public Vector2 HeadRideDuration = new(8f, 15f);
        NetworkPlayer headPassenger;
        NetworkObject shownHeadPassenger;
        Transform passengerHead, seatPelvis;
        float nextHeadRide, nextHeadCheck, headGoalRefresh, headTime, headDuration;
        Vector3 headFrom, headTo;
        int headPhase, headLandingNode;
        bool HeadTask => task == TaskKind.GoHead || task == TaskKind.HeadRide;
        NetworkObject HeadRider => headPhase >= 3 && headPhase <= 6 && headPassenger != null ? headPassenger.NetworkObject : null;

        void BeginHeadRide()
        {
            headPassenger = null; headPhase = 0; nextHeadCheck = 0f;
            ScheduleHeadRide();
        }

        void ScheduleHeadRide()
        {
            float minimum = Mathf.Max(20f, HeadRideInterval.x);
            nextHeadRide = Time.time + Random.Range(minimum, Mathf.Max(minimum, HeadRideInterval.y));
        }

        bool HeadPassengerValid(NetworkPlayer player) => activityShip != null && !activityShip.IsSinking &&
            activityShip.TeamId.Value > 0 && player != null && player.TeamId.Value == activityShip.TeamId.Value &&
            Aboard(player) && !player.Motor.IsDowned && !player.Motor.IsFrozen && !player.Motor.IsSwimming && !player.Motor.IsClimbing;

        Vector3 HeadSeat(NetworkPlayer player)
        {
            var capsule = player.GetComponent<CharacterController>();
            return capsule != null ? capsule.transform.TransformPoint(capsule.center + Vector3.up * (capsule.height * .5f + .035f))
                : player.transform.position + player.transform.up * 1.8f;
        }

        bool TryStartHeadTrip()
        {
            if (Time.time < nextHeadRide || Time.time < nextHeadCheck || HasCarriedItem || attacker != null || jumpPhase != 0 || headPhase != 0) return false;
            nextHeadCheck = Time.time + 5f;
            FindActivityReachability();
            NetworkPlayer selected = null; int goal = -1; float best = 64f;
            foreach (var player in NetworkPlayer.Active)
            {
                if (!HeadPassengerValid(player) || !player.Motor.IsGrounded || player.Motor.PlanarSpeed > 2f) continue;
                int node = DeckNear(player.transform.position, 1.3f, true);
                if (node < 0) continue;
                float score = (player.transform.position - transform.TransformPoint(position)).sqrMagnitude;
                if (score < best) { selected = player; goal = node; best = score; }
            }
            if (selected == null) return false;
            headPassenger = selected; ScheduleHeadRide(); SetTask(TaskKind.GoHead, goal);
            headGoalRefresh = Time.time + .5f; activityDeadline = Time.time + 25f;
            return true;
        }

        bool UpdateHeadTrip()
        {
            if (!HeadPassengerValid(headPassenger)) { CancelActivities(); return false; }
            if (Time.time >= headGoalRefresh)
            {
                headGoalRefresh = Time.time + .5f; FindActivityReachability();
                int goal = DeckNear(headPassenger.transform.position, 1.3f, true);
                if (goal < 0) { CancelActivities(); return false; }
                if (goal != activityGoal)
                {
                    activityGoal = goal; pause = 0f;
                    if (target < 0) { path.Clear(); pathIndex = 0; }
                    else if (path.Count > pathIndex + 1) path.RemoveRange(pathIndex + 1, path.Count - pathIndex - 1);
                }
            }
            if (target >= 0) return false;
            Vector3 feet = transform.TransformPoint(position), offset = headPassenger.transform.position - feet;
            if (Vector3.ProjectOnPlane(offset, transform.up).sqrMagnitude > 1.4f * 1.4f || Mathf.Abs(Vector3.Dot(offset, transform.up)) > .45f) return false;
            if (!headPassenger.Motor.IsGrounded || headPassenger.Motor.PlanarSpeed > 2f) { SetMotion(ShipMonkeyMotion.Idle); return true; }
            Vector3 destination = transform.InverseTransformPoint(HeadSeat(headPassenger));
            if (!HeadArcClear(position, destination, headPassenger.transform)) { CancelActivities(); return false; }
            path.Clear(); pathIndex = 0; wantsRest = looking = false; animationSpeed = 1f;
            task = TaskKind.HeadRide; headPhase = 1; headTime = 0f; headFrom = position;
            headDuration = Mathf.Clamp(Vector3.Distance(position, destination) / JumpSpeed, .45f, .9f);
            Face(headPassenger.transform.position, 10f); SetMotion(ShipMonkeyMotion.JumpStart);
            return true;
        }

        Vector3 HeadArc(Vector3 from, Vector3 to, float t) => Vector3.Lerp(from, to, t) + Vector3.up * (4f * Mathf.Max(.35f, JumpArcHeight * .6f) * t * (1f - t));

        bool HeadArcClear(Vector3 from, Vector3 to, Transform ignore)
        {
            Vector3 previous = from;
            for (int i = 1; i <= 10; i++)
            {
                Vector3 next = HeadArc(from, to, i / 10f);
                if (!ClearJump(transform.TransformPoint(previous) + transform.up * .3f, transform.TransformPoint(next) + transform.up * .3f, ignore)) return false;
                previous = next;
            }
            return true;
        }

        bool UpdateHeadRide(float delta)
        {
            if (headPhase == 0) return false;
            if (headPhase <= 6)
            {
                TryStartDefense();
                if (!DefenseTask) PrioritizeAid();
                if (HeadTask && (!HeadPassengerValid(headPassenger) || attacker != null)) CancelActivities();
            }
            headTime += delta; animationSpeed = 1f; looking = false;
            if (headPhase == 1)
            {
                if (headTime >= .2f) { headPhase = 2; headTime = 0f; SetMotion(ShipMonkeyMotion.JumpAir); }
            }
            else if (headPhase == 2)
            {
                Vector3 destination = transform.InverseTransformPoint(HeadSeat(headPassenger));
                Vector3 next = HeadArc(headFrom, destination, Mathf.Clamp01(headTime / headDuration));
                if ((destination - headFrom).sqrMagnitude > 16f || !ClearJump(transform.TransformPoint(position) + transform.up * .3f, transform.TransformPoint(next) + transform.up * .3f, headPassenger.transform))
                { CancelActivities(); return true; }
                position = next;
                if (headTime >= headDuration) { headPhase = 3; headTime = 0f; SetMotion(ShipMonkeyMotion.JumpLand); }
            }
            else if (headPhase <= 6)
            {
                perched = true;
                position = transform.InverseTransformPoint(HeadSeat(headPassenger)) - Vector3.up * .2f;
                rotation = Quaternion.Inverse(transform.rotation) * Quaternion.Euler(0f, headPassenger.transform.eulerAngles.y, 0f);
                if (headPhase == 3 && headTime >= .27f) { headPhase = 4; headTime = 0f; SetMotion(ShipMonkeyMotion.SitDown); }
                else if (headPhase == 4 && headTime >= .7f)
                {
                    headPhase = 5; headTime = 0f;
                    float minimum = Mathf.Max(1f, HeadRideDuration.x);
                    headDuration = Random.Range(minimum, Mathf.Max(minimum, HeadRideDuration.y));
                    SetMotion(ShipMonkeyMotion.Sit);
                }
                else if (headPhase == 5 && headTime >= headDuration) { headPhase = 6; headTime = 0f; SetMotion(ShipMonkeyMotion.StandUp); }
                else if (headPhase == 6 && headTime >= .7f) CancelActivities();
            }
            else if (headPhase == 7)
            {
                perched = false; SetMotion(ShipMonkeyMotion.JumpAir);
                if (headLandingNode >= 0 && Nodes[headLandingNode].Available) headTo = Point(headLandingNode);
                position = HeadArc(headFrom, headTo, Mathf.Clamp01(headTime / headDuration));
                if (headTime >= headDuration) { headPhase = 8; headTime = 0f; SetMotion(ShipMonkeyMotion.JumpLand); }
            }
            else if (headTime >= .27f)
            {
                headPhase = 0; headPassenger = null;
                if (headLandingNode >= 0 && Nodes[headLandingNode].Available) current = headLandingNode;
                else Recover();
                SetMotion(ShipMonkeyMotion.Idle); pause = .5f;
            }
            return true;
        }

        void ResetHeadRide()
        {
            if (headPhase == 0) { headPassenger = null; return; }
            if (headPhase >= 7) return;
            headFrom = Visual != null ? Visual.localPosition : position;
            headLandingNode = -1; float best = float.PositiveInfinity;
            for (int i = 0; i < Nodes.Length; i++)
            {
                if (!Nodes[i].Available || Nodes[i].Surface != ShipMonkeySurface.Deck) continue;
                float score = (Point(i) - headFrom).sqrMagnitude;
                if (score >= best || !HeadArcClear(headFrom, Point(i), headPassenger != null ? headPassenger.transform : null)) continue;
                best = score; headLandingNode = i;
            }
            if (headLandingNode < 0) headLandingNode = DeckNear(transform.TransformPoint(position));
            if (headLandingNode < 0) headLandingNode = current;
            headTo = Point(headLandingNode);
            headDuration = Mathf.Clamp(Vector3.Distance(headFrom, headTo) / JumpSpeed, .4f, 1f);
            headPhase = 7; headTime = 0f; path.Clear(); pathIndex = 0; target = -1;
            SetMotion(ShipMonkeyMotion.JumpAir);
        }

        void PresentHeadRide()
        {
            NetworkObject rider = initialized ? HeadRider : remote.HeadRider;
            if ((!initialized && !remoteInitialized) || Visual == null || rider == null) return;
            if (shownHeadPassenger != rider)
            {
                shownHeadPassenger = rider; passengerHead = null;
                foreach (var bone in rider.GetComponentsInChildren<Transform>(true))
                    if (bone.name == "mixamorig:HeadTop_End") { passengerHead = bone; break; }
            }
            var player = rider.GetComponent<NetworkPlayer>();
            if (player == null) return;
            Vector3 seat = passengerHead != null ? passengerHead.position + rider.transform.up * .035f : HeadSeat(player);
            Visual.rotation = Quaternion.Euler(0f, rider.transform.eulerAngles.y, 0f);
            if (seatPelvis == null)
                foreach (var bone in Visual.GetComponentsInChildren<Transform>(true))
                    if (bone.name == "Pelvis") { seatPelvis = bone; break; }
            if (seatPelvis != null && (Motion == ShipMonkeyMotion.Sit || Motion == ShipMonkeyMotion.SitDown || Motion == ShipMonkeyMotion.StandUp))
                Visual.position += seat - seatPelvis.position;
            else Visual.position = seat;
        }
    }
}
