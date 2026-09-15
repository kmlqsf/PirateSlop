using System;
using System.Collections.Generic;
using MetaVoiceChat;
using MetaVoiceChat.Input.Mic;
using MetaVoiceChat.Output.AudioSource;
using MetaVoiceChat.Utils;
using UnityEngine;
using UnityEngine.InputSystem;
using PirateSlop.Networking;

namespace PirateSlop
{
    [DefaultExecutionOrder(-100)]
    public sealed class PirateVoiceChat : MonoBehaviour
    {
        public static PirateVoiceChat Instance { get; private set; }
        static readonly List<PirateVoiceChat> instances = new();
        readonly List<PirateVoiceChat> nearby = new();
        public static bool VoiceEnabled { get => PlayerPrefs.GetInt("VoiceEnabled", 1) != 0; set => PlayerPrefs.SetInt("VoiceEnabled", value ? 1 : 0); }
        public static float Volume { get => PlayerPrefs.GetFloat("VoiceVolume", .8f); set => PlayerPrefs.SetFloat("VoiceVolume", Mathf.Clamp01(value)); }
        public bool IsSelf => player != null && player.IsOwner;
        public bool IsMuted { get; private set; }
        public bool Transmitting { get; private set; }
        public bool SpeechDetected => !IsMuted && Time.unscaledTime - lastSpeech < .25f;
        public string DisplayName => "Пират " + player.ParticipantId.Value;
        public string Status => !VoiceEnabled ? "Голос отключён" : input.Mic == null || !input.Mic.IsRecording ? "Микрофон недоступен" : "V — говорить рядом";
        public IReadOnlyList<PirateVoiceChat> Participants
        {
            get
            {
                nearby.Clear();
                foreach (var voice in instances)
                    if (voice != null && (voice.transform.position - transform.position).sqrMagnitude <= 3600f) nearby.Add(voice);
                return nearby;
            }
        }
        NetworkPlayer player;
        MetaVc codec;
        VcMicAudioInput input;
        AudioSource output;
        bool started;
        float lastSpeech = -10f;
        public static PirateVoiceChat Create(NetworkPlayer player)
        {
            var root = new GameObject("ProximityVoice");
            root.SetActive(false);
            root.transform.SetParent(player.transform, false);
            root.transform.localPosition = Vector3.up * 1.6f;
            var voice = root.AddComponent<PirateVoiceChat>();
            voice.player = player;
            voice.codec = root.AddComponent<MetaVc>();
            voice.codec.config = new VcConfig { complexity = 4 };
            voice.codec.isInputMuted = new MetaSerializableReactiveProperty<bool> { Value = true };
            voice.codec.isOutputMuted = new MetaSerializableReactiveProperty<bool>();
            voice.codec.isDeafened = new MetaSerializableReactiveProperty<bool>();
            voice.codec.isSpeaking = new MetaSerializableReactiveProperty<bool>();
            voice.input = root.AddComponent<VcMicAudioInput>();
            voice.input.metaVc = voice.codec;
            voice.output = root.AddComponent<AudioSource>();
            voice.output.playOnAwake = false;
            voice.output.spatialBlend = 1f;
            voice.output.dopplerLevel = 0f;
            voice.output.minDistance = 5f;
            voice.output.maxDistance = 60f;
            voice.output.rolloffMode = AudioRolloffMode.Linear;
            voice.output.volume = 0f;
            var sink = root.AddComponent<VcAudioSourceOutput>();
            sink.metaVc = voice.codec;
            sink.audioSource = voice.output;
            voice.codec.audioInput = voice.input;
            voice.codec.audioOutput = sink;
            instances.Add(voice);
            if (player.IsOwner) Instance = voice;
            root.SetActive(true);
            return voice;
        }
        void Start()
        {
            if (player == null || !player.IsClientInitialized) return;
            codec.StartClient(player, IsSelf, 900);
            started = true;
        }
        void Update()
        {
            if (!started) return;
            bool talk = IsSelf && VoiceEnabled && Application.isFocused && !player.Motor.IsDead && !SessionController.MenuOpen && !DeveloperMenu.IsOpen && !PlayerInventory.LootWindowOpen && Cursor.lockState == CursorLockMode.Locked && Keyboard.current != null && Keyboard.current.vKey.isPressed;
            Transmitting = talk && input.Mic != null && input.Mic.IsRecording;
            codec.isInputMuted.Value = !Transmitting;
            codec.isDeafened.Value = !VoiceEnabled;
            codec.isOutputMuted.Value = IsMuted;
            bool audible = !IsSelf && !IsMuted && VoiceEnabled && Instance != null && !player.Motor.IsDead && (Instance.transform.position - transform.position).sqrMagnitude <= 3600f;
            output.volume = audible ? Volume * PlayerPrefs.GetFloat("AudioMaster", 1f) : 0f;
        }
        public void Receive(int index, double timestamp, byte[] data)
        {
            if (!started || data == null || data.Length > 900 || index < 0) return;
            try
            {
                codec.ReceiveFrame(index, timestamp, .02f, data);
                if (data.Length > 0) lastSpeech = Time.unscaledTime;
            }
            catch (ArgumentException) { }
            catch (Concentus.OpusException) { }
        }
        public void Retry()
        {
            if (IsSelf && input.Mic != null) input.Mic.StartRecording();
        }
        public void ToggleMute(PirateVoiceChat participant)
        {
            if (!participant.IsSelf) participant.IsMuted = !participant.IsMuted;
        }
        void Mute() { Transmitting = false; if (codec != null) codec.isInputMuted.Value = true; }
        void OnApplicationFocus(bool focus) { if (!focus) Mute(); }
        void OnApplicationPause(bool pause) { if (pause) Mute(); }
        public void Shutdown()
        {
            Mute();
            if (started) { codec.StopClient(); started = false; }
            if (input.Mic != null) input.Mic.StopRecording();
            output.Stop();
            instances.Remove(this);
            if (Instance == this) Instance = null;
        }
        void OnDestroy() => Shutdown();
        void OnGUI()
        {
            if (!IsSelf || Event.current.type != EventType.Repaint || SessionController.MenuOpen || DeveloperMenu.IsOpen) return;
            using var layout = new HudLayout.Scope(true);
            PirateHudStyle.Fill(new Rect(24, 100, 310, 32), new Color(.025f, .065f, .08f, .8f));
            PirateHudStyle.Label(new Rect(34, 100, 290, 32), Transmitting ? "ГОВОРИТЕ · рядом" : Status, Transmitting ? new Color(.46f, .85f, .65f) : PirateHudStyle.Paper, false, TextAnchor.MiddleLeft);
            int row = 0;
            foreach (var participant in Participants)
            {
                if (participant.IsSelf || !participant.SpeechDetected) continue;
                PirateHudStyle.Panel(new Rect(24, 140 + row * 28, 250, 26), participant.DisplayName);
                if (++row == 4) break;
            }
        }
    }
}
