using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class SessionController
    {
        Vector2 voiceScroll;
        void DrawVoiceMenu(float x, float y, float width)
        {
            MenuText(new Rect(x, y, width, 32), "ГОЛОС РЯДОМ");
            PirateVoiceChat.VoiceEnabled = MenuToggle(x, y + 48, width, PirateVoiceChat.VoiceEnabled, "Включить голосовой чат");
            PirateVoiceChat.Volume = MenuVolume(x, y + 100, width, "Громкость голосов", PirateVoiceChat.Volume, "VoiceVolume");
            if (MenuAction(x, y + 155, width, PirateVoiceChat.VoiceActivation ? "Режим: активация голосом" : "Режим: по кнопке V"))
            {
                PirateVoiceChat.VoiceActivation = !PirateVoiceChat.VoiceActivation;
                PlayerPrefs.Save();
            }
            if (PirateVoiceChat.VoiceActivation)
            {
                MenuText(new Rect(x, y + 210, width, 28), $"Порог микрофона: {PirateVoiceChat.ActivationThresholdDb:F0} дБ", true);
                PirateVoiceChat.ActivationThresholdDb = GUI.HorizontalSlider(new Rect(x, y + 245, width, 20), PirateVoiceChat.ActivationThresholdDb, -60f, -20f, menuSlider, menuThumb);
                MenuText(new Rect(x, y + 267, width, 28), "Левее — реагирует на более тихий голос", true);
            }
            var voice = PirateVoiceChat.Instance;
            MenuText(new Rect(x, y + 305, width, 55), voice != null ? voice.Status : "Войдите в сессию, чтобы подключить голос.", true);
            string instruction = PirateVoiceChat.VoiceActivation ? "Говорите — микрофон включается автоматически." : "Удерживайте V, чтобы говорить.";
            MenuText(new Rect(x, y + 365, width, 85), instruction + "\nГолос слышен до 60 м и затихает с расстоянием.\nВ меню и при потере фокуса микрофон выключен.", true);
            if (voice != null && MenuAction(x, y + 455, width, "Перезапустить микрофон")) voice.Retry();
            if (MenuAction(x, y + 515, width, "Назад")) { PlayerPrefs.Save(); menuPage = 3; }
            if (voice == null) return;
            float right = x + width + 48f;
            float listWidth = Mathf.Min(400f, Screen.width / Mathf.Min(Screen.height / 900f, Screen.width / 1000f) - right - 32f);
            MenuText(new Rect(right, y, listWidth, 35), "ИГРОКИ РЯДОМ");
            var members = voice.Participants;
            voiceScroll = GUI.BeginScrollView(new Rect(right, y + 48, listWidth, 335), voiceScroll,
                new Rect(0, 0, listWidth - 22, Mathf.Max(335, members.Count * 50)));
            for (int i = 0; i < members.Count; i++)
            {
                var member = members[i];
                string name = member.DisplayName + (member.IsSelf ? " · вы" : member.SpeechDetected && !member.IsMuted ? " · говорит" : "");
                PirateHudStyle.Label(new Rect(0, i * 50, listWidth - 130, 44), name, PirateHudStyle.Paper, false, TextAnchor.MiddleLeft);
                if (!member.IsSelf && PirateHudStyle.Button(new Rect(listWidth - 126, i * 50 + 8, 100, 30), member.IsMuted ? "Включить" : "Заглушить")) voice.ToggleMute(member);
            }
            GUI.EndScrollView();
        }
    }
}
