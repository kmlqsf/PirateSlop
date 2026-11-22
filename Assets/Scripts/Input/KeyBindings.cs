using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace PirateSlop
{
    public enum GameAction { Forward, Back, Left, Right, Jump, Sprint, Crouch, Use, Cancel, Reload, Carry, Drop, Kick, Upgrades, Voice, Map }

    public static class KeyBindings
    {
        static readonly Key[] defaults =
        {
            Key.W, Key.S, Key.A, Key.D, Key.Space, Key.LeftShift, Key.C, Key.E, Key.Q, Key.R, Key.F, Key.G, Key.X, Key.V, Key.B, Key.M
        };
        static readonly string[] names =
        {
            "Вперёд", "Назад", "Влево", "Вправо", "Прыжок", "Бег", "Присесть / подкат", "Взаимодействие", "Отпустить / отмена",
            "Перезарядка / переброс", "Нести / кости", "Бросить предмет", "Пинок", "Улучшения", "Голос (рация)", "Карта"
        };
        static readonly Key[] current = new Key[defaults.Length];
        static bool loaded;

        public static int Count => defaults.Length;
        public static string Name(GameAction action) => Localization.T(names[(int)action]);

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            for (int i = 0; i < defaults.Length; i++)
            {
                current[i] = defaults[i];
                string saved = PlayerPrefs.GetString("Bind_" + (GameAction)i, "");
                if (!string.IsNullOrEmpty(saved) && Enum.TryParse(saved, out Key key) && key != Key.None) current[i] = key;
            }
        }

        public static Key Get(GameAction action) { Load(); return current[(int)action]; }

        public static void Set(GameAction action, Key key)
        {
            Load();
            if (key == Key.None || key == Key.Escape) return;
            int index = (int)action;
            for (int i = 0; i < current.Length; i++)
                if (i != index && current[i] == key)
                {
                    current[i] = current[index];
                    PlayerPrefs.SetString("Bind_" + (GameAction)i, current[i].ToString());
                }
            current[index] = key;
            PlayerPrefs.SetString("Bind_" + action, key.ToString());
            PlayerPrefs.Save();
        }

        public static void Reset()
        {
            Load();
            for (int i = 0; i < defaults.Length; i++) { current[i] = defaults[i]; PlayerPrefs.DeleteKey("Bind_" + (GameAction)i); }
            PlayerPrefs.Save();
        }

        public static string Label(GameAction action)
        {
            var keyboard = Keyboard.current;
            var key = Get(action);
            if (keyboard != null)
            {
                string display = keyboard[key].displayName;
                if (!string.IsNullOrEmpty(display)) return display.ToUpperInvariant();
            }
            return key.ToString();
        }

        public static KeyControl Control(Keyboard keyboard, GameAction action) => keyboard[Get(action)];

        public static Key Forward => Get(GameAction.Forward);
        public static Key Back => Get(GameAction.Back);
        public static Key Left => Get(GameAction.Left);
        public static Key Right => Get(GameAction.Right);
        public static Key Jump => Get(GameAction.Jump);
        public static Key Sprint => Get(GameAction.Sprint);
        public static Key Crouch => Get(GameAction.Crouch);
        public static Key Use => Get(GameAction.Use);
        public static Key Cancel => Get(GameAction.Cancel);
        public static Key Reload => Get(GameAction.Reload);
        public static Key Carry => Get(GameAction.Carry);
        public static Key Drop => Get(GameAction.Drop);
        public static Key Kick => Get(GameAction.Kick);
        public static Key Upgrades => Get(GameAction.Upgrades);
        public static Key Voice => Get(GameAction.Voice);
        public static Key Map => Get(GameAction.Map);
    }
}
