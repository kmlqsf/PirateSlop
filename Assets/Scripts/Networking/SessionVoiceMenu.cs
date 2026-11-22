using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class SessionController
    {
        Vector2 voiceScroll;
        void DrawVoiceMenu(float x, float y, float width)
        {
            MenuText(new Rect(x, y, width, 32), L("ГОЛОС РЯДОМ"));
            PirateVoiceChat.VoiceEnabled = MenuToggle(x, y + 48, width, PirateVoiceChat.VoiceEnabled, L("Включить голосовой чат"));
            PirateVoiceChat.Volume = MenuVolume(x, y + 100, width, L("Громкость голосов"), PirateVoiceChat.Volume, "VoiceVolume");
            if (MenuAction(x, y + 155, width, PirateVoiceChat.VoiceActivation ? L("Режим: активация голосом") : Localization.F("Режим: по кнопке {0}", KeyBindings.Label(GameAction.Voice))))
            {
                PirateVoiceChat.VoiceActivation = !PirateVoiceChat.VoiceActivation;
                PlayerPrefs.Save();
            }
            if (PirateVoiceChat.VoiceActivation)
            {
                MenuText(new Rect(x, y + 210, width, 28), Localization.F("Порог микрофона: {0} дБ", PirateVoiceChat.ActivationThresholdDb.ToString("F0")), true);
                PirateVoiceChat.ActivationThresholdDb = GUI.HorizontalSlider(new Rect(x, y + 245, width, 20), PirateVoiceChat.ActivationThresholdDb, -60f, -20f, menuSlider, menuThumb);
                MenuText(new Rect(x, y + 267, width, 28), L("Левее — реагирует на более тихий голос"), true);
            }
            var voice = PirateVoiceChat.Instance;
            MenuText(new Rect(x, y + 305, width, 55), voice != null ? voice.Status : L("Войдите в сессию, чтобы подключить голос."), true);
            string instruction = PirateVoiceChat.VoiceActivation ? L("Говорите — микрофон включается автоматически.") : Localization.F("Удерживайте {0}, чтобы говорить.", KeyBindings.Label(GameAction.Voice));
            MenuText(new Rect(x, y + 365, width, 85), instruction + L("\nГолос слышен до 60 м и затихает с расстоянием.\nВ меню и при потере фокуса микрофон выключен."), true);
            if (voice != null && MenuAction(x, y + 455, width, L("Перезапустить микрофон"))) voice.Retry();
            if (MenuAction(x, y + 515, width, L("Назад"))) { PlayerPrefs.Save(); menuPage = 3; }
            if (voice == null) return;
            float right = x + width + 48f;
            float listWidth = Mathf.Min(400f, Screen.width / Mathf.Min(Screen.height / 900f, Screen.width / 1000f) - right - 32f);
            MenuText(new Rect(right, y, listWidth, 35), L("ИГРОКИ РЯДОМ"));
            var members = voice.Participants;
            voiceScroll = GUI.BeginScrollView(new Rect(right, y + 48, listWidth, 335), voiceScroll,
                new Rect(0, 0, listWidth - 22, Mathf.Max(335, members.Count * 50)));
            for (int i = 0; i < members.Count; i++)
            {
                var member = members[i];
                string name = member.DisplayName + (member.IsSelf ? L(" · вы") : member.SpeechDetected && !member.IsMuted ? L(" · говорит") : "");
                PirateHudStyle.Label(new Rect(0, i * 50, listWidth - 130, 44), name, PirateHudStyle.Paper, false, TextAnchor.MiddleLeft);
                if (!member.IsSelf && PirateHudStyle.Button(new Rect(listWidth - 126, i * 50 + 8, 100, 30), member.IsMuted ? L("Включить") : L("Заглушить"))) voice.ToggleMute(member);
            }
            GUI.EndScrollView();
        }
    }
}
