using PirateSlop.Networking;
using UnityEngine;
using UnityEngine.AI;

namespace PirateSlop.World
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed partial class KamikazeUnit : MonoBehaviour
    {
        public GameObject VisualRoot;
        public Animator Animator;
        public Collider[] Hitboxes = System.Array.Empty<Collider>();
        public float AnimationRunSpeed = 7f;
        public NetworkKamikazeEvent Event { get; private set; }
        public int Index { get; private set; }
        public bool Alive { get; private set; }
        public bool Chasing { get; private set; }
        public bool Queued { get; private set; }
        public bool Leaping { get; private set; }
        public bool HasVisual => VisualRoot != null && VisualRoot.GetComponentInChildren<Renderer>(true) != null;
        public Vector3 BlastPoint => transform.position + Vector3.up * .95f;
        public Vector3 Velocity => Leaping ? leapVelocity : agent != null && agent.enabled && agent.isOnNavMesh ? agent.velocity : Vector3.zero;
        static readonly int speedParameter = UnityEngine.Animator.StringToHash("Speed");
        NavMeshAgent agent;
        CombatHealth target;
        AdvancedPlayerController targetMotor;
        AudioSource scream;
        float wounds, thinkAt, pathAt, snapshotAt, animationSpeed;
        uint sequence;
        int pathRequestedFrame;
        Vector3 fromPoint, toPoint, lastDestination;
        Quaternion fromFacing, toFacing;
        Quaternion visualFacing;
        bool initialized, server, animationReady, exploded, pathRequested;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            agent.enabled = false;
            if (VisualRoot != null) VisualRoot.SetActive(false);
            SetHitboxes(false);
        }

        public void Initialize(NetworkKamikazeEvent owner, int index, bool authoritative)
        {
            Event = owner; Index = index; server = authoritative; initialized = true;
            visualFacing = VisualRoot != null ? VisualRoot.transform.localRotation : Quaternion.identity;
            agent.speed = owner.RunSpeed; agent.acceleration = owner.RunSpeed * 4f;
            agent.angularSpeed = 540f; agent.stoppingDistance = .25f;
            agent.autoRepath = false; agent.autoTraverseOffMeshLink = false;
            agent.agentTypeID = owner.AgentTypeID;
            agent.areaMask = owner.NavigationAreas;
            animationReady = Animator != null && Animator.runtimeAnimatorController != null;
            if (animationReady)
            {
                Animator.applyRootMotion = false;
                animationReady = false;
                foreach (var parameter in Animator.parameters)
                    if (parameter.nameHash == speedParameter && parameter.type == AnimatorControllerParameterType.Float) animationReady = true;
            }
        }

        public bool Activate(Vector3 position, Quaternion facing)
        {
            transform.SetPositionAndRotation(position, facing);
            Alive = true; Queued = Chasing = exploded = Leaping = pathRequested = leapRequested = false; wounds = 0; target = null; targetMotor = null;
            if (VisualRoot != null) VisualRoot.SetActive(true);
            SetHitboxes(true);
            agent.enabled = server;
            if (server && (!agent.isOnNavMesh || !agent.Warp(position)))
            {
                Hide();
                return false;
            }
            thinkAt = Time.time + Index * .025f;
            pathAt = Time.time + Index * .025f;
            lastDestination = Vector3.positiveInfinity;
            return true;
        }

        public void ServerStep()
        {
            if (!server || !Alive || Queued) return;
            if (Time.time >= thinkAt || target != null && !Event.IsPursuitTarget(target))
            {
                thinkAt = Time.time + .45f;
                target = Event.FindTarget(transform.position);
                targetMotor = target != null ? target.GetComponent<AdvancedPlayerController>() : null;
                if (targetMotor != null) agent.speed = Mathf.Max(Event.RunSpeed, targetMotor.SprintTopSpeed * Event.SprintSpeedMultiplier);
                agent.acceleration = agent.speed * 4f;
            }
            if (Leaping) { ServerLeapStep(); return; }
            if (target == null)
            {
                if (agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
                Chasing = leapRequested = false;
                return;
            }
            var destination = target.transform.position;
            if ((BlastPoint - (destination + Vector3.up * .9f)).sqrMagnitude <= Event.DetonationDistance * Event.DetonationDistance &&
                Event.ClearLine(BlastPoint, destination + Vector3.up * .9f, target.transform)) { Event.QueueExplosion(this); return; }
            if (leapRequested || !agent.enabled || !agent.isOnNavMesh || (targetMotor != null && targetMotor.IsSwimming) || !Event.IsTarget(target))
            {
                RequestLeap(); return;
            }
            if (Time.time >= pathAt)
            {
                pathAt = Time.time + .4f;
                if (Event.TryNavigationPoint(destination, 2f, out var point))
                {
                    if ((point - lastDestination).sqrMagnitude >= .25f || !agent.hasPath)
                    {
                        if (agent.SetDestination(point)) { lastDestination = point; pathRequested = true; pathRequestedFrame = Time.frameCount; }
                        else { RequestLeap(); return; }
                    }
                    agent.isStopped = false;
                }
                else { RequestLeap(); return; }
            }
            if (pathRequested && Time.frameCount > pathRequestedFrame && !agent.pathPending && (!agent.hasPath || agent.pathStatus != NavMeshPathStatus.PathComplete)) { RequestLeap(); return; }
            Chasing = !agent.isStopped && (agent.pathPending || agent.hasPath && agent.pathStatus == NavMeshPathStatus.PathComplete);
        }

        public void ReceiveHit(float amount, GameObject attacker)
        {
            if (!initialized || Event == null || !Event.IsServerInitialized || !Event.IsSpawned || !Alive || Queued || !float.IsFinite(amount) || amount <= 0) return;
            wounds += amount;
            if (wounds >= 2f) Event.QueueExplosion(this);
        }

        public bool Queue()
        {
            if (!Alive || Queued) return false;
            Queued = true; Chasing = false;
            Leaping = leapRequested = false; leapVelocity = Vector3.zero;
            if (agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
            SetHitboxes(false);
            return true;
        }

        public void Hide()
        {
            Alive = Chasing = Leaping = leapRequested = false;
            leapVelocity = Vector3.zero;
            if (agent != null) agent.enabled = false;
            SetHitboxes(false);
            if (VisualRoot != null) VisualRoot.SetActive(false);
            if (scream != null) scream.Stop();
        }

        public void MarkExploded()
        {
            exploded = true;
            Hide();
        }

        public KamikazeState Capture(uint revision)
        {
            return new KamikazeState {
                Position = Event.transform.InverseTransformPoint(transform.position),
                Yaw = Mathf.DeltaAngle(Event.transform.eulerAngles.y, transform.eulerAngles.y),
                Speed = Velocity.magnitude,
                Flags = (byte)((Alive && !Queued ? 1 : 0) | (Chasing ? 2 : 0) | (exploded ? 4 : 0) | (Leaping ? 8 : 0)), Sequence = revision
            };
        }

        public void ApplySnapshot(KamikazeState state)
        {
            if (!initialized || server) return;
            if ((state.Flags & 4) != 0) MarkExploded();
            if (exploded) return;
            bool present = (state.Flags & 1) != 0;
            if (!present) { Hide(); return; }
            var point = Event.transform.TransformPoint(state.Position);
            var facing = Event.transform.rotation * Quaternion.Euler(0, state.Yaw, 0);
            if (!Alive)
            {
                transform.SetPositionAndRotation(point, facing);
                Alive = true;
                if (VisualRoot != null) VisualRoot.SetActive(true);
                SetHitboxes(true);
            }
            Chasing = (state.Flags & 2) != 0;
            Leaping = (state.Flags & 8) != 0;
            animationSpeed = state.Speed;
            if (sequence == state.Sequence) return;
            sequence = state.Sequence; snapshotAt = Time.unscaledTime;
            fromPoint = transform.position; fromFacing = transform.rotation;
            toPoint = point; toFacing = facing;
        }

        void LateUpdate()
        {
            if (!initialized || Event == null || !Alive) return;
            if (!server)
            {
                float alpha = Mathf.Clamp01((Time.unscaledTime - snapshotAt) / Event.SnapshotInterval);
                transform.SetPositionAndRotation(Vector3.Lerp(fromPoint, toPoint, alpha), Quaternion.Slerp(fromFacing, toFacing, alpha));
            }
            float speed = Leaping ? 0f : server ? Velocity.magnitude : animationSpeed;
            if (VisualRoot != null) VisualRoot.transform.localRotation = Quaternion.AngleAxis(Leaping ? 15f : 0f, Vector3.right) * visualFacing;
            if (animationReady)
            {
                Animator.SetFloat(speedParameter, speed, .1f, Time.deltaTime);
                Animator.speed = speed > .2f ? Mathf.Clamp(speed / Mathf.Max(1f, AnimationRunSpeed), .2f, 1.8f) : 1f;
            }
            if (Event.IsClientInitialized && !Application.isBatchMode)
                GameAudio.KamikazeLoop(ref scream, Event.Scream, transform, !Queued && Chasing, Event.ScreamVolume, Event.ScreamDistance);
        }

        void SetHitboxes(bool enabled)
        {
            foreach (var collider in Hitboxes) if (collider != null) collider.enabled = enabled;
        }

        void OnDisable() { if (scream != null) scream.Stop(); }
    }
}
