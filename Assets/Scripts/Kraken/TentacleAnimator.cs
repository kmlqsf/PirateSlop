using System;
using UnityEngine;
using PirateSlop.Networking;

namespace PirateSlop
{
    public sealed class TentacleAnimator : MonoBehaviour
    {
        [SerializeField] SkinnedMeshRenderer meshRenderer;
        [SerializeField] Transform[] bones;
        [SerializeField] float idleFrequency = 1.2f;
        [SerializeField] float attackDuration = 1.7f;
        Vector3[] restPositions, restDirections;
        Quaternion[] restRotations;
        readonly Vector3[] curve = new Vector3[65];
        readonly float[] arcLengths = new float[65];
        readonly Vector3[] waveOffsets = new Vector3[65];
        NetworkShip ship;
        float phase, lean, curl, sway;
        bool isAttacking, impactTriggered;
        double attackStartTime;
        double visualClock;
        bool clockReady;
        Vector3 attackTargetLocal;
        Action impactCallback;
        Func<double> clockSource;
        float idleMotionScale=1f;
        bool directionalStrikes;
        float strikeHeading;

        public bool IsAttacking => isAttacking;
        public double AttackStartedAt => attackStartTime;
        public Vector3 AttackTarget => ship != null ? ship.transform.TransformPoint(attackTargetLocal) : attackTargetLocal;
        public Vector3 TipPosition => bones != null && bones.Length > 0 ? bones[^1].position : transform.position;
        public double Clock => clockSource != null ? clockSource() : ship != null && ship.IsSpawned ? ship.TimeManager.TicksToTime(ship.TimeManager.Tick) : Time.timeAsDouble;
        public float Duration => attackDuration;
        public bool IsStriking => isAttacking && visualClock>=attackStartTime+attackDuration*.32f && visualClock<=attackStartTime+attackDuration*.6f;
        public void SetIdleMotion(float scale) => idleMotionScale=Mathf.Clamp(scale,1f,5f);
        public void SetDirectionalStrikes(bool enabled) => directionalStrikes=enabled;
        public float Reach => restPositions != null && restPositions.Length > 1 ? Vector3.Distance(transform.TransformPoint(restPositions[0]),transform.TransformPoint(restPositions[^1])) : 0f;
        public void SetClockSource(Func<double> source) { clockSource=source; clockReady=false; }
        public void SetTarget(NetworkShip target) => ship=target;

        void Awake()
        {
            if (meshRenderer == null) meshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
            if (bones == null || bones.Length < 2) return;
            restPositions = new Vector3[bones.Length];
            restRotations = new Quaternion[bones.Length];
            restDirections = new Vector3[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                restPositions[i] = transform.InverseTransformPoint(bones[i].position);
                restRotations[i] = Quaternion.Inverse(transform.rotation) * bones[i].rotation;
                Vector3 direction = i+1 < bones.Length ? bones[i+1].position-bones[i].position : bones[i].position-bones[i-1].position;
                restDirections[i] = transform.InverseTransformDirection(direction.normalized);
            }
        }

        public void Configure(NetworkShip owner, System.Random random)
        {
            ship = owner;
            clockReady = false;
            phase = (float)random.NextDouble()*Mathf.PI*2;
            lean = Mathf.Lerp(-2.4f,2.4f,(float)random.NextDouble());
            curl = Mathf.Lerp(1.5f,4.5f,(float)random.NextDouble());
            sway = Mathf.Lerp(.65f,1.35f,(float)random.NextDouble());
        }

        public void TriggerAttack(Vector3 targetPosition, Action onImpact, double startedAt = -1)
        {
            if (isAttacking) return;
            isAttacking = true;
            attackStartTime = startedAt >= 0 ? startedAt : Clock;
            attackTargetLocal = ship != null ? ship.transform.InverseTransformPoint(targetPosition) : targetPosition;
            Vector3 strikeDirection = Vector3.ProjectOnPlane(targetPosition-transform.position,Vector3.up);
            strikeHeading = directionalStrikes && strikeDirection.sqrMagnitude>.0001f ? Vector3.SignedAngle(transform.forward,strikeDirection,Vector3.up) : 0;
            impactCallback = onImpact;
            impactTriggered = false;
        }

        public void CancelAttack() { isAttacking = false; impactCallback = null; }
        void LateUpdate()
        {
            double now = Clock;
            if (!clockReady) { visualClock = now; clockReady = true; }
            else
            {
                visualClock += Time.deltaTime;
                double correction = (now - visualClock) * (1 - Math.Exp(-3 * Time.deltaTime));
                visualClock += Math.Clamp(correction, -Time.deltaTime * .25, Time.deltaTime * .25);
            }
            EvaluatePose(visualClock);
        }

        public void EvaluatePose(double now)
        {
            if (restPositions == null || bones.Length < 2) return;
            Vector3 right = transform.right, forward = transform.forward;
            Quaternion rotation = transform.rotation;
            Vector3 start = transform.TransformPoint(restPositions[0]);
            float length = Vector3.Distance(start,transform.TransformPoint(restPositions[^1]));
            double time = now*idleFrequency+phase;
            Vector3 idleTip = start+Vector3.up*(length*.87f)+right*(lean+(float)Math.Sin(time*.55)*sway*idleMotionScale)+forward*(curl+(float)Math.Sin(time*.7)*sway*idleMotionScale);
            Vector3 tip = idleTip;
            Vector3 strikeForward = forward;
            float progress = isAttacking ? Mathf.Clamp01((float)(now-attackStartTime)/attackDuration) : 0;
            if (isAttacking)
            {
                Vector3 aim = AttackTarget;
                if (directionalStrikes)
                {
                    Vector3 direction = Vector3.ProjectOnPlane(aim-start,Vector3.up);
                    if (direction.sqrMagnitude > .0001f)
                    {
                        strikeHeading += Mathf.DeltaAngle(strikeHeading,Vector3.SignedAngle(forward,direction,Vector3.up));
                        strikeForward = Quaternion.AngleAxis(strikeHeading,Vector3.up)*forward;
                    }
                }
                Vector3 windup = start+Vector3.up*(length*.94f)-strikeForward*(length*.16f)+right*lean;
                if (progress < .32f) tip = Vector3.Lerp(idleTip,windup,Mathf.SmoothStep(0,1,progress/.32f));
                else if (progress < .52f) { float t=(progress-.32f)/.2f; tip=Vector3.Lerp(windup,aim,t*t); }
                else if (progress < .6f) tip = aim;
                else tip = Vector3.Lerp(aim,idleTip,Mathf.SmoothStep(0,1,(progress-.6f)/.4f));
            }
            Vector3 offset = tip-start;
            if (offset.magnitude > length*.98f) tip=start+offset.normalized*(length*.98f);
            float strikeBlend = directionalStrikes && isAttacking ? Mathf.SmoothStep(0,1,Mathf.Min(progress/.32f,(1-progress)/.4f)) : 0;
            Vector3 bendForward = Quaternion.AngleAxis(strikeHeading*strikeBlend,Vector3.up)*forward;
            Vector3 bend = Vector3.ProjectOnPlane(-bendForward,(tip-start).normalized);
            if (bend.sqrMagnitude < .000001f) bend=Vector3.ProjectOnPlane(right,(tip-start).normalized);
            bend.Normalize();
            float idleBlend = !isAttacking ? 1f : progress < .32f ? 1f-Mathf.SmoothStep(0,1,progress/.32f) : progress > .6f ? Mathf.SmoothStep(0,1,(progress-.6f)/.4f) : 0f;
            float amplitude = sway*.7f*Mathf.Clamp(length/24f,1f,1.7f)*idleMotionScale*idleBlend;
            for (int i=0; i<waveOffsets.Length; i++)
            {
                float t=i/(float)(waveOffsets.Length-1);
                float envelope=Mathf.Sin(t*Mathf.PI);
                waveOffsets[i]=envelope*amplitude*(right*(float)Math.Sin(t*Math.PI*3.2-time*.8)
                    +forward*(.5f*(float)Math.Sin(t*Math.PI*4.2+time*.63+phase)));
            }
            float low=0, high=length;
            Vector3 middle=(start+tip)*.5f;
            for (int iteration=0; iteration<12; iteration++)
            {
                float height=(low+high)*.5f;
                float arc=BuildCurve(start,middle+bend*height,tip);
                if (arc < length) low=height; else high=height;
            }
            float total=BuildCurve(start,middle+bend*((low+high)*.5f),tip);
            int cursor=1;
            Vector3 previous=start;
            for (int i=0; i<bones.Length; i++)
            {
                float distance=total*i/(bones.Length-1);
                while (cursor<curve.Length-1 && arcLengths[cursor]<distance) cursor++;
                float blend=Mathf.InverseLerp(arcLengths[cursor-1],arcLengths[cursor],distance);
                Vector3 point=Vector3.Lerp(curve[cursor-1],curve[cursor],blend);
                Vector3 direction=i==bones.Length-1 ? point-previous : curve[Mathf.Min(cursor+1,curve.Length-1)]-point;
                if (direction.sqrMagnitude < .0001f) direction=Vector3.up;
                bones[i].position=point;
                bones[i].rotation=Quaternion.FromToRotation(rotation * restDirections[i],direction.normalized)*rotation*restRotations[i];
                previous=point;
            }
            if (isAttacking && !impactTriggered && progress >= .52f)
            {
                impactTriggered=true;
                impactCallback?.Invoke();
            }
            if (isAttacking && progress >= 1) CancelAttack();
        }

        float BuildCurve(Vector3 start, Vector3 control, Vector3 end)
        {
            arcLengths[0]=0;
            for (int i=0; i<curve.Length; i++)
            {
                float t=i/(float)(curve.Length-1), s=1-t;
                curve[i]=s*s*start+2*s*t*control+t*t*end+waveOffsets[i];
                if (i>0) arcLengths[i]=arcLengths[i-1]+Vector3.Distance(curve[i-1],curve[i]);
            }
            return arcLengths[^1];
        }
    }
}
