using MetaVoiceChat.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PirateSlop
{
    public sealed class PirateVoiceInputFilter : VcInputFilter
    {
        internal PirateVoiceChat Voice;
        float speechUntil;
        bool previousActivation;

        protected override void Filter(int index, ref float[] samples)
        {
            bool activation = PirateVoiceChat.VoiceActivation;
            if (activation != previousActivation) speechUntil = 0f;
            previousActivation = activation;
            bool transmit = Voice != null && Voice.CanTransmit && samples != null && samples.Length > 0;
            if (transmit && activation)
            {
                double energy = 0;
                foreach (float sample in samples) energy += sample * sample;
                float rms = Mathf.Sqrt((float)(energy / samples.Length));
                float threshold = Mathf.Pow(10f, PirateVoiceChat.ActivationThresholdDb / 20f);
                if (rms >= threshold) speechUntil = Time.unscaledTime + .3f;
                transmit = Time.unscaledTime < speechUntil;
            }
            else if (transmit) transmit = Keyboard.current != null && Keyboard.current.bKey.isPressed;
            else speechUntil = 0f;
            if (Voice != null) Voice.SetTransmitting(transmit);
            if (!transmit) samples = null;
        }
    }
}
