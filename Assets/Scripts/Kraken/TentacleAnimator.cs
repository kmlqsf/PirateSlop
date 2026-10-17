using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
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
        static readonly Vector3[] curveWeights = CreateCurveWeights();
        NativeArray<Vector3> nativeWeights, nativeOffsets, nativeCurve;
        NativeArray<float> nativeLengths;
        public static bool BurstCurveEnabled { get; private set; } = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCurveMode() => BurstCurveEnabled = !Array.Exists(Environment.GetCommandLineArgs(), a => a == "-performanceLegacyTentacleCurve");

        void OnDisable() => ReleaseCurveBuffers();
        void OnDestroy() => ReleaseCurveBuffers();

        void ReleaseCurveBuffers()
        {
            if (nativeWeights.IsCreated) nativeWeights.Dispose();
            if (nativeOffsets.IsCreated) nativeOffsets.Dispose();
            if (nativeCurve.IsCreated) nativeCurve.Dispose();
            if (nativeLengths.IsCreated) nativeLengths.Dispose();
        }

        [BurstCompile(FloatMode = FloatMode.Strict, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
        struct CurveJob : IJob
        {
            [ReadOnly] public NativeArray<Vector3> Weights, Offsets;
            [WriteOnly] public NativeArray<Vector3> Curve;
            public NativeArray<float> Lengths;
            public Vector3 Start, Middle, Bend, End;
            public float TargetLength;

            Vector3 Point(int index, Vector3 control)
            {
                var weights = Weights[index];
                return weights.x * Start + weights.y * control + weights.z * End + Offsets[index];
            }

            float Measure(Vector3 control)
            {
                Vector3 previous = Point(0, control);
                float length = 0f;
                for (int i = 1; i < Weights.Length; i++)
                {
                    Vector3 point = Point(i, control);
                    length += Vector3.Distance(previous, point);
                    previous = point;
                }
                return length;
            }

            public void Execute()
            {
                float low = 0f, high = TargetLength;
                for (int iteration = 0; iteration < 12; iteration++)
                {
                    float height = (low + high) * .5f;
                    if (Measure(Middle + Bend * height) < TargetLength) low = height; else high = height;
                }
                Vector3 control = Middle + Bend * ((low + high) * .5f);
                Vector3 previous = Point(0, control);
                Curve[0] = previous;
                Lengths[0] = 0f;
                float length = 0f;
                for (int i = 1; i < Weights.Length; i++)
                {
                    Vector3 point = Point(i, control);
                    length += Vector3.Distance(previous, point);
                    Curve[i] = point;
                    Lengths[i] = length;
                    previous = point;
                }
            }
        }

        float SolveCurve(Vector3 start, Vector3 middle, Vector3 bend, Vector3 tip, float length)
        {
            if (!BurstCurveEnabled)
            {
                float low = 0f, high = length;
                for (int iteration = 0; iteration < 12; iteration++)
                {
                    float height = (low + high) * .5f;
                    if (MeasureCurve(start, middle + bend * height, tip) < length) low = height; else high = height;
                }
                return BuildCurve(start, middle + bend * ((low + high) * .5f), tip);
            }
            if (!nativeWeights.IsCreated)
            {
                nativeWeights = new NativeArray<Vector3>(curveWeights, Allocator.Persistent);
                nativeOffsets = new NativeArray<Vector3>(waveOffsets.Length, Allocator.Persistent);
                nativeCurve = new NativeArray<Vector3>(curve.Length, Allocator.Persistent);
                nativeLengths = new NativeArray<float>(arcLengths.Length, Allocator.Persistent);
            }
            nativeOffsets.CopyFrom(waveOffsets);
            new CurveJob { Weights = nativeWeights, Offsets = nativeOffsets, Curve = nativeCurve, Lengths = nativeLengths,
                Start = start, Middle = middle, Bend = bend, End = tip, TargetLength = length }.Run();
            nativeCurve.CopyTo(curve);
            nativeLengths.CopyTo(arcLengths);
            return arcLengths[^1];
        }

        static Vector3[] CreateCurveWeights()
        {
            var weights = new Vector3[65];
            for (int i = 0; i < weights.Length; i++)
            {
                float t = i / (float)(weights.Length - 1), s = 1f - t;
                weights[i] = new Vector3(s * s, 2f * s * t, t * t);
            }
            return weights;
        }
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
            Vector3 middle=(start+tip)*.5f;
            float total=SolveCurve(start,middle,bend,tip,length);
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
                bones[i].SetPositionAndRotation(point,Quaternion.FromToRotation(rotation * restDirections[i],direction.normalized)*rotation*restRotations[i]);
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
                var weights=curveWeights[i];
                curve[i]=weights.x*start+weights.y*control+weights.z*end+waveOffsets[i];
                if (i>0) arcLengths[i]=arcLengths[i-1]+Vector3.Distance(curve[i-1],curve[i]);
            }
            return arcLengths[^1];
        }

        float MeasureCurve(Vector3 start, Vector3 control, Vector3 end)
        {
            Vector3 previous=curveWeights[0].x*start+curveWeights[0].y*control+curveWeights[0].z*end+waveOffsets[0];
            float length=0f;
            for (int i=1; i<curveWeights.Length; i++)
            {
                var weights=curveWeights[i];
                Vector3 point=weights.x*start+weights.y*control+weights.z*end+waveOffsets[i];
                length+=Vector3.Distance(previous,point);
                previous=point;
            }
            return length;
        }
    }
}
