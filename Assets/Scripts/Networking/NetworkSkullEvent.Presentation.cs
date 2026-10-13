using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkSkullEvent
    {
        GameObject skullBreathVisual;
        SkullMouthPresentation mouthPresentation;
        AudioSource skullBreathAudio;
        byte observedBreathPhase;
        Vector3 shownBreathEnd;

        void CreateSkullPresentation()
        {
            if (Application.isBatchMode || mouthPresentation != null) return;
            mouthPresentation = gameObject.AddComponent<SkullMouthPresentation>();
            mouthPresentation.Initialize(this);
        }

        void UpdateSkullPresentation()
        {
            if (Application.isBatchMode) return;
            if (mouthPresentation == null) CreateSkullPresentation();
            if (breathPhase.Value != observedBreathPhase)
            {
                observedBreathPhase = breathPhase.Value;
                if (BreathWarning) GameAudio.Play(SoundCue.SkullGrowl, Mouth.position);
                if (Breathing) shownBreathEnd = breathEndpoint.Value;
            }
            var ear = SpatialAudioTone.FindListener();
            bool nearby = ear != null && (ear.transform.position - Mouth.position).sqrMagnitude < 260f * 260f && !SessionController.MenuOpen;
            mouthPresentation.Present(!Cleared);
            if (Breathing && nearby)
            {
                if (skullBreathVisual == null) skullBreathVisual = StaffVfx.FireBreath();
                skullBreathVisual.SetActive(true);
                var end = Vector3.Lerp(shownBreathEnd, breathEndpoint.Value, 1f - Mathf.Exp(-15f * Time.deltaTime));
                shownBreathEnd = end;
                StaffVfx.UpdateFireBreath(skullBreathVisual, Mouth.position + Mouth.forward * 3f, end, breathContact.Value);
                GameAudio.Loop(ref skullBreathAudio, SoundCue.SkullBreath, Mouth, 1f);
            }
            else
            {
                if (skullBreathVisual != null)
                {
                    foreach (var particles in skullBreathVisual.GetComponentsInChildren<ParticleSystem>()) particles.Clear();
                    skullBreathVisual.SetActive(false);
                }
                GameAudio.StopLoop(skullBreathAudio);
            }
        }

        void ClearSkullPresentation()
        {
            StaffAudio.Stop(ref skullBreathAudio);
            if (skullBreathVisual != null) Destroy(skullBreathVisual);
            if (mouthPresentation != null) Destroy(mouthPresentation);
        }
    }
}
