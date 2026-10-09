using PirateSlop.Networking;
using UnityEngine;

namespace PirateSlop.Ships
{
    public sealed partial class ShipMonkey
    {
        public GameObject BatModel;
        [Min(1f)] public float BoardingCooldown = 120f;
        NetworkPlayer boarder;
        float nextBoardingScan, boardingReadyAt, boardingGoalRefresh;
        bool batHit;
        Vector3 batTarget;
        GameObject boardingBat;
        bool DefenseTask => task == TaskKind.PursueBoarder || task == TaskKind.BatStrike;
        bool ShowsBat => remoteInitialized && !initialized ? remote.HoldingBat : DefenseTask;

        void BeginDefense()
        {
            boarder = null; nextBoardingScan = boardingReadyAt = boardingGoalRefresh = 0f;
            batHit = false;
        }

        bool BoarderValid(NetworkPlayer player) => activityShip != null && activityShip.TeamId.Value > 0 &&
            player != null && player.TeamId.Value > 0 && player.TeamId.Value != activityShip.TeamId.Value &&
            Aboard(player) && !player.Motor.IsDowned && !player.Motor.IsFrozen;

        Vector3 BoarderHead
        {
            get
            {
                var capsule = boarder.GetComponent<CharacterController>();
                return capsule != null ? capsule.transform.TransformPoint(capsule.center + Vector3.up * (capsule.height * .5f - .15f))
                    : boarder.transform.position + boarder.transform.up * 1.55f;
            }
        }

        bool BatReach()
        {
            if (!BoarderValid(boarder)) return false;
            Vector3 feet = transform.TransformPoint(position);
            Vector3 offset = boarder.transform.position - feet;
            if (Vector3.ProjectOnPlane(offset, transform.up).sqrMagnitude > 1.1f * 1.1f || Mathf.Abs(Vector3.Dot(offset, transform.up)) > .45f) return false;
            return ClearJump(feet + transform.up * .8f, BoarderHead, boarder.transform);
        }

        void TryStartDefense()
        {
            if (DefenseTask || attacker != null || jumpPhase != 0 || BatModel == null ||
                activityShip.IsSinking || Time.time < boardingReadyAt || Time.time < nextBoardingScan || !Nodes[current].Available) return;
            nextBoardingScan = Time.time + .5f;
            FindActivityReachability();
            NetworkPlayer selected = null; int goal = -1; float best = float.PositiveInfinity;
            foreach (var player in NetworkPlayer.Active)
            {
                if (!BoarderValid(player)) continue;
                int node = DeckNear(player.transform.position, 2f, true);
                if (node < 0) continue;
                float score = (player.transform.position - transform.TransformPoint(position)).sqrMagnitude;
                if (score < best) { selected = player; goal = node; best = score; }
            }
            if (selected == null) return;
            CancelActivities(); boarder = selected; batHit = false;
            SetTask(TaskKind.PursueBoarder, goal); routeRunning = true;
            boardingGoalRefresh = Time.time + .5f;
            batTarget = transform.InverseTransformPoint(BoarderHead);
        }

        bool UpdateDefense(float delta)
        {
            if (task == TaskKind.BatStrike && batHit)
            {
                activityTime += delta;
                if (activityTime >= .7f) CancelActivities();
                return true;
            }
            if (!BoarderValid(boarder)) { CancelActivities(); return false; }
            batTarget = transform.InverseTransformPoint(BoarderHead);
            if (task == TaskKind.PursueBoarder)
            {
                if (Time.time >= boardingGoalRefresh)
                {
                    boardingGoalRefresh = Time.time + .5f;
                    FindActivityReachability();
                    int goal = DeckNear(boarder.transform.position, 2f, true);
                    if (goal < 0) { CancelActivities(); return false; }
                    if (activityGoal != goal)
                    {
                        activityGoal = goal; pause = 0f;
                        if (target < 0) { path.Clear(); pathIndex = 0; }
                        else if (path.Count > pathIndex + 1) path.RemoveRange(pathIndex + 1, path.Count - pathIndex - 1);
                    }
                }
                routeRunning = true;
                if (target >= 0 || !BatReach()) return false;
                path.Clear(); pathIndex = 0; wantsRest = false;
                task = TaskKind.BatStrike; activityTime = 0f; batHit = false;
                Face(BoarderHead, 10f); SetMotion(ShipMonkeyMotion.BatStrike); shownState = -1;
                animationSpeed = 1f;
            }
            Face(BoarderHead, delta); activityTime += delta;
            if (activityTime < .35f) return true;
            if (!BatReach()) { CancelActivities(); nextBoardingScan = Time.time + .75f; return true; }
            Vector3 away = Vector3.ProjectOnPlane(boarder.transform.position - transform.TransformPoint(position), Vector3.up).normalized;
            if (away.sqrMagnitude < .01f) away = Visual.forward;
            boarder.KnockDown(away * 1.5f + Vector3.up, 4f);
            boardingReadyAt = Time.time + BoardingCooldown;
            batHit = true;
            return true;
        }

        void PresentDefense()
        {
            bool active = (initialized || remoteInitialized) && Visual != null && Visual.gameObject.activeInHierarchy && ShowsBat;
            if (!active)
            {
                if (boardingBat != null) boardingBat.SetActive(false);
                return;
            }
            if (Application.isBatchMode || BatModel == null || RightHand == null) return;
            if (boardingBat == null)
            {
                boardingBat = Instantiate(BatModel, Visual); boardingBat.name = "MonkeyBoardingBat";
                foreach (var shape in boardingBat.GetComponentsInChildren<Collider>()) Destroy(shape);
            }
            boardingBat.SetActive(true);
            Vector3 direction = (Visual.up - Visual.forward * .35f).normalized;
            if (Motion == ShipMonkeyMotion.BatStrike)
            {
                float elapsed = initialized ? motionTime : remote.MotionTime + (Time.unscaledTime - receivedAt) * remote.AnimationSpeed;
                Vector3 point = transform.TransformPoint(initialized ? batTarget : remote.BatTarget);
                Vector3 aim = (point - RightHand.position).normalized;
                float swing = Mathf.Clamp01((elapsed - .12f) / .23f);
                direction = Vector3.Slerp(direction, aim, swing);
            }
            boardingBat.transform.SetPositionAndRotation(RightHand.position, Quaternion.LookRotation(direction, Visual.right) * Quaternion.Euler(90f, 0f, 0f));
        }
    }
}
