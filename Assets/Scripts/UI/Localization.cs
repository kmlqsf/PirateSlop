using System;
using System.Collections.Generic;
using UnityEngine;

namespace PirateSlop
{
    public enum GameLanguage { Russian, English }

    public static class Localization
    {
        [Serializable] sealed class Entry { public string ru, en; }
        [Serializable] sealed class Table { public Entry[] entries; }

        static bool loaded;
        static GameLanguage language;
        static Dictionary<string, string> english;
        public static readonly HashSet<string> Missing = new();
        public static event Action Changed;

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            int saved = PlayerPrefs.GetInt("Language", -1);
            if (saved >= 0 && saved <= 1) language = (GameLanguage)saved;
            else language = Application.systemLanguage is SystemLanguage.Russian or SystemLanguage.Ukrainian or SystemLanguage.Belarusian ? GameLanguage.Russian : GameLanguage.English;
        }

        public static GameLanguage Current
        {
            get { Load(); return language; }
            set
            {
                Load();
                if (language == value) return;
                language = value;
                PlayerPrefs.SetInt("Language", (int)value);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        static void LoadEnglish()
        {
            english = new Dictionary<string, string>(StringComparer.Ordinal);
            var asset = Resources.Load<TextAsset>("Localization/en");
            if (asset == null) return;
            var table = JsonUtility.FromJson<Table>(asset.text);
            if (table?.entries == null) return;
            foreach (var entry in table.entries)
                if (!string.IsNullOrEmpty(entry.ru) && !string.IsNullOrEmpty(entry.en)) english[entry.ru] = entry.en;
        }

        public static string T(string russian)
        {
            if (string.IsNullOrEmpty(russian)) return russian;
            Load();
            if (language == GameLanguage.Russian) return russian;
            if (english == null) LoadEnglish();
            if (english.TryGetValue(russian, out var translated)) return translated;
            if (Application.isEditor) Missing.Add(russian);
            return russian;
        }

        public static string F(string russianFormat, params object[] args) => string.Format(T(russianFormat), args);
    }
}
