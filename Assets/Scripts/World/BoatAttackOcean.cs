using UnityEngine;
using UnityEngine.Rendering;
using WaterSystem;

namespace PirateSlop
{
    [DefaultExecutionOrder(-90)]
    public sealed class BoatAttackOcean : OceanHeightSource
    {
        public Water Water;
        public OceanSurface Surface;
        SpectralWave[] waves;

        struct SpectralWave
        {
            public Vector2 Direction;
            public float Frequency, Speed, Amplitude, Horizontal;
        }


        public float WaveStrength { get; private set; } = 1f;
        public float WaveSteepness { get; private set; } = 1f;
        public float WaveSpeed { get; private set; } = 1f;
        float clockAnchor, clockValue;
        public float WaveClockAnchor => clockAnchor;
        public float WaveClockValue => clockValue;

        public void ApplyWaveControls(float strength, float steepness, float speed, float anchor, float clock)
        {
            SetWaveControls(strength, steepness, speed);
            clockAnchor = anchor;
            clockValue = clock;
        }

        public void SetWaveControls(float strength, float steepness, float speed)
        {
            speed = Mathf.Clamp(speed, .25f, 2f);
            if (!Mathf.Approximately(speed, WaveSpeed))
            {
                float time = Surface != null ? Surface.WaveTime : Time.time;
                clockValue = WaveClock(time);
                clockAnchor = time;
            }
            WaveStrength = Mathf.Clamp(strength, 0f, 2f);
            WaveSteepness = Mathf.Clamp01(steepness);
            WaveSpeed = speed;
        }

        float WaveClock(float time) => clockValue + (time - clockAnchor) * WaveSpeed;

        static readonly int CameraWaterId = Shader.PropertyToID("_BoatAttack_CameraWater");

        void OnEnable() => RenderPipelineManager.beginCameraRendering += UpdateCameraWater;

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= UpdateCameraWater;
            Shader.SetGlobalVector(CameraWaterId, Vector4.zero);
            Shader.SetGlobalFloat("_BoatAttack_UnderwaterPass", 0);
        }

        void UpdateCameraWater(ScriptableRenderContext context, Camera camera)
        {
            Shader.SetGlobalFloat("_BoatAttack_UnderwaterPass", 0);
            if (Surface == null || Surface.HeightSource != this)
            {
                Shader.SetGlobalVector(CameraWaterId, Vector4.zero);
                return;
            }
            var position = camera.transform.position;
            float height = Surface.Height(position);
            float depth = height - position.y;
            Shader.SetGlobalVector(CameraWaterId, new Vector4(depth > 0 ? 1 : 0, height, depth, 1));
        }

        void Awake()
        {
            if (Surface == null) Surface = GetComponent<OceanSurface>();
            if (Water == null) Water = GetComponent<Water>();
            WaterShipFoam.Ensure(gameObject);
            if (GetComponent<UnderwaterEnvironment>() == null) gameObject.AddComponent<UnderwaterEnvironment>();
            CacheWaves();
            UpdateSurface();
        }

        void CacheWaves()
        {
            var source = GerstnerWaves.GetWaveArray(Water.gerstnerData);
            waves = new SpectralWave[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                float angle = source[i].direction * Mathf.Deg2Rad;
                float frequency = 6.28318f / source[i].wavelength;
                waves[i] = new SpectralWave
                {
                    Direction = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)),
                    Frequency = frequency, Speed = Mathf.Sqrt(9.8f * frequency),
                    Amplitude = source[i].amplitude / source.Length,
                    Horizontal = source[i].amplitude > 0f ? .85f / (frequency * source.Length) : 0f
                };
            }
        }

        Vector3 Sample(Vector2 position, float time, out Vector4 jacobian)
        {
            Vector3 displacement = Vector3.zero;
            jacobian = new Vector4(1f, 0f, 0f, 1f);
            foreach (var wave in waves)
            {
                float phase = Vector2.Dot(wave.Direction, position) * wave.Frequency - time * wave.Speed;
                float sine = Mathf.Sin(phase), cosine = Mathf.Cos(phase);
                float horizontal = wave.Horizontal * Mathf.Min(WaveStrength, 1f) * WaveSteepness;
                displacement.x += horizontal * wave.Direction.x * cosine;
                displacement.z += horizontal * wave.Direction.y * cosine;
                displacement.y += wave.Amplitude * WaveStrength * sine;
                float derivative = -horizontal * wave.Frequency * sine;
                jacobian.x += derivative * wave.Direction.x * wave.Direction.x;
                jacobian.y += derivative * wave.Direction.x * wave.Direction.y;
                jacobian.z += derivative * wave.Direction.y * wave.Direction.x;
                jacobian.w += derivative * wave.Direction.y * wave.Direction.y;
            }
            return displacement;
        }

        public override float HeightOffset(Vector3 position, float time)
        {
            if (waves == null) CacheWaves();
            return SolveSurface(position, WaveClock(time), out _) + (WaterShipFoam.Instance != null ? WaterShipFoam.Instance.BowHeight(position) : 0);
        }

        float SolveSurface(Vector3 position, float time, out Vector2 parameter)
        {
            parameter = new Vector2(position.x, position.z);
            var target = new Vector2(position.x, position.z);
            var point = target;
            float bestError = float.MaxValue, height = 0f;
            for (int iteration = 0; iteration < 12; iteration++)
            {
                var displacement = Sample(point, time, out var jacobian);
                var residual = point + new Vector2(displacement.x, displacement.z) - target;
                float error = residual.sqrMagnitude;
                if (error < bestError) { bestError = error; height = displacement.y; parameter = point; }
                if (error < .0001f) break;
                float determinant = jacobian.x * jacobian.w - jacobian.y * jacobian.z;
                var step = Mathf.Abs(determinant) > .01f
                    ? new Vector2(jacobian.w * residual.x - jacobian.y * residual.y,
                        -jacobian.z * residual.x + jacobian.x * residual.y) / determinant
                    : residual * .5f;
                step = Vector2.ClampMagnitude(step, 12f);
                bool improved = false;
                for (int attempt = 0; attempt < 4; attempt++)
                {
                    var candidate = point - step;
                    var offset = Sample(candidate, time, out _);
                    if ((candidate + new Vector2(offset.x, offset.z) - target).sqrMagnitude < error)
                    { point = candidate; improved = true; break; }
                    step *= .5f;
                }
                if (!improved) point -= residual * .2f;
            }
            return height;
        }

        public void SampleFoamSurface(Vector3 position, out float height, out Vector3 velocity, out Vector2 parameter)
        {
            if (waves == null) CacheWaves();
            float time = WaveClock(Surface != null ? Surface.WaveTime : Time.time);
            height = SolveSurface(position, time, out parameter);
            velocity = Vector3.zero;
            foreach (var wave in waves)
            {
                float phase = Vector2.Dot(wave.Direction, parameter) * wave.Frequency - time * wave.Speed;
                float horizontal = wave.Horizontal * Mathf.Min(WaveStrength, 1f) * WaveSteepness * wave.Speed * WaveSpeed * Mathf.Sin(phase);
                velocity.x += horizontal * wave.Direction.x;
                velocity.z += horizontal * wave.Direction.y;
                velocity.y -= wave.Amplitude * WaveStrength * wave.Speed * WaveSpeed * Mathf.Cos(phase);
            }
        }

        public Vector2 FoamHorizontalOffset(Vector2 parameter)
        {
            if (waves == null) CacheWaves();
            var offset = Sample(parameter, WaveClock(Surface != null ? Surface.WaveTime : Time.time), out _);
            return new Vector2(offset.x, offset.z);
        }

        void UpdateSurface()
        {
            if (Water == null || Surface == null) return;
            Water.transform.position = new Vector3(0f, Surface.SeaLevel, 0f);
            Water.waveTime = WaveClock(Surface.WaveTime);
            Water.waveStrength = WaveStrength;
            Water.waveSteepness = WaveSteepness;
            Water.whirlpoolCenter = Surface.WhirlpoolCenter;
            Water.whirlpoolRadius = Surface.WhirlpoolRadius;
            Water.whirlpoolDepth = Surface.WhirlpoolDepth;
            Water.whirlpoolTwist = Surface.WhirlpoolTwist;
            WaterShipFoam.Apply(Water.RuntimeMaterial);
            WaterShipFoam.Apply(Water.RuntimeInfiniteMaterial);
        }

        void LateUpdate() => UpdateSurface();
    }
}
