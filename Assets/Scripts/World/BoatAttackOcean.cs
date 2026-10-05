using UnityEngine;
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

        void Awake()
        {
            if (Surface == null) Surface = GetComponent<OceanSurface>();
            if (Water == null) Water = GetComponent<Water>();
            WaterShipFoam.Ensure(gameObject);
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
                displacement.x += wave.Horizontal * wave.Direction.x * cosine;
                displacement.z += wave.Horizontal * wave.Direction.y * cosine;
                displacement.y += wave.Amplitude * sine;
                float derivative = -wave.Horizontal * wave.Frequency * sine;
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
            var target = new Vector2(position.x, position.z);
            var point = target;
            float bestError = float.MaxValue, height = 0f;
            for (int iteration = 0; iteration < 12; iteration++)
            {
                var displacement = Sample(point, time, out var jacobian);
                var residual = point + new Vector2(displacement.x, displacement.z) - target;
                float error = residual.sqrMagnitude;
                if (error < bestError) { bestError = error; height = displacement.y; }
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

        void UpdateSurface()
        {
            if (Water == null || Surface == null) return;
            Water.transform.position = new Vector3(0f, Surface.SeaLevel, 0f);
            Water.waveTime = Surface.WaveTime;
            Water.whirlpoolCenter = Surface.WhirlpoolCenter;
            Water.whirlpoolRadius = Surface.WhirlpoolRadius;
            Water.whirlpoolDepth = Surface.WhirlpoolDepth;
            Water.whirlpoolTwist = Surface.WhirlpoolTwist;
            WaterShipFoam.Apply(Water.RuntimeMaterial);
        }

        void LateUpdate() => UpdateSurface();
    }
}
