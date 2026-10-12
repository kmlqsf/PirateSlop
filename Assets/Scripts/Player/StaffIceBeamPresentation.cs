using UnityEngine;

namespace PirateSlop
{
    public sealed class StaffIceBeamPresentation : MonoBehaviour
    {
        ParticleSystem[] streams;
        ParticleSystem.Particle[][] buffers;
        float length = 30f;
        public void Initialize(params ParticleSystem[] particles)
        {
            streams = particles;
            buffers = new ParticleSystem.Particle[streams.Length][];
            for (int i = 0; i < streams.Length; i++) buffers[i] = new ParticleSystem.Particle[streams[i].main.maxParticles];
        }
        public void SetLength(float value)
        {
            length = Mathf.Max(0f, value);
            if (streams == null) return;
            foreach (var particles in streams)
            {
                var main = particles.main;
                main.startLifetime = Mathf.Max(.01f, length / 45f);
            }
        }
        void LateUpdate() => Trim();
        public void Trim()
        {
            if (streams == null) return;
            for (int i = 0; i < streams.Length; i++)
            {
                int count = streams[i].GetParticles(buffers[i]);
                bool changed = false;
                for (int particle = 0; particle < count; particle++) if (buffers[i][particle].position.z > length)
                {
                    buffers[i][particle].remainingLifetime = 0f;
                    changed = true;
                }
                if (changed) streams[i].SetParticles(buffers[i], count);
            }
        }
    }
}
