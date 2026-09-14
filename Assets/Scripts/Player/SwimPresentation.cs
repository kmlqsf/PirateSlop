using UnityEngine;
using UnityEngine.Rendering;

namespace PirateSlop
{
    public sealed class SwimPresentation : MonoBehaviour
    {
        AdvancedPlayerController motor;
        bool swimming, changedFog, previousFog;
        Color previousColor;
        FogMode previousMode;
        float previousDensity;
        float strokeAt, breathWarningAt;
        void Awake() { motor = GetComponent<AdvancedPlayerController>(); }
        void OnEnable() { RenderPipelineManager.beginCameraRendering += BeginCamera; RenderPipelineManager.endCameraRendering += EndCamera; }
        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCamera; RenderPipelineManager.endCameraRendering -= EndCamera;
            RestoreFog();
        }
        bool Underwater => OceanSurface.Instance != null && motor.PlayerCamera != null && motor.PlayerCamera.transform.position.y < OceanSurface.Instance.Height(motor.PlayerCamera.transform.position) - .08f;
        void BeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera != motor.PlayerCamera || !camera.enabled || !Underwater) return;
            previousFog = RenderSettings.fog; previousColor = RenderSettings.fogColor;
            previousMode = RenderSettings.fogMode; previousDensity = RenderSettings.fogDensity;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(.015f, .16f, .2f); RenderSettings.fogDensity = .027f; changedFog = true;
        }
        void EndCamera(ScriptableRenderContext context, Camera camera) { if (camera == motor.PlayerCamera) RestoreFog(); }
        void RestoreFog()
        {
            if (!changedFog) return;
            RenderSettings.fog = previousFog; RenderSettings.fogColor = previousColor;
            RenderSettings.fogMode = previousMode; RenderSettings.fogDensity = previousDensity; changedFog = false;
        }
        void LateUpdate()
        {
            if (motor.IsSwimming && !swimming) GameAudio.Play(SoundCue.Splash, transform.position, .5f);
            if (motor.IsSwimming && !motor.IsDead && motor.PlanarSpeed > .4f && Time.time >= strokeAt)
            { GameAudio.Play(SoundCue.Splash, transform.position, .1f); strokeAt = Time.time + 1.2f; }
            swimming = motor.IsSwimming;
            if (motor.PlayerCamera != null && motor.PlayerCamera.enabled && !motor.IsDead && Underwater && motor.BreathFraction < .25f && Time.time >= breathWarningAt)
            { GameAudio.Play(SoundCue.AirWarning, transform.position, 1f, true); breathWarningAt = Time.time + Mathf.Lerp(.8f, 2.5f, motor.BreathFraction / .25f); }
        }
        void OnGUI()
        {
            if (motor.PlayerCamera == null || !motor.PlayerCamera.enabled || motor.IsDead) return;
            Color old = GUI.color;
            if (Underwater)
            {
                GUI.color = new Color(.015f, .22f, .28f, .22f);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            }
            if (motor.IsSwimming)
            {
                if (motor.BreathFraction < .25f) PirateHudStyle.Panel(new Rect(Screen.width * .5f - 180, Screen.height * .5f + 100f, 360, 32), "МАЛО ВОЗДУХА · Space — всплыть");
                GUI.color = Color.white;
                ContextPrompt.Offer("WASD — плыть · Shift — быстрее · Space — вверх · Ctrl — вниз", 10);
                if (motor.BreathFraction < .99f && GetComponent<PlayerHud>() == null)
                {
                    var rect = new Rect(Screen.width / 2f - 100, Screen.height - 180, 200, 18);
                    GUI.color = new Color(0, 0, 0, .7f); GUI.DrawTexture(rect, Texture2D.whiteTexture);
                    GUI.color = motor.BreathFraction > .25f ? Color.cyan : Color.red;
                    GUI.DrawTexture(new Rect(rect.x + 2, rect.y + 2, 196 * motor.BreathFraction, 14), Texture2D.whiteTexture);
                }
            }
            GUI.color = old;
        }
    }
}
