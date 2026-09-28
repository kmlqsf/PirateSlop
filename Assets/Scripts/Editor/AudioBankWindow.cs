using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PirateSlop.EditorTools
{
    public sealed class AudioBankWindow : EditorWindow
    {
        enum Page { Mix, Ambience, Music, Weapons, Movement, Ship, Fishing, Items, Other }

        const string BankPath = "Assets/Resources/GameAudioBank.asset";
        static readonly Page[] pages = (Page[])Enum.GetValues(typeof(Page));
        GameAudioBank bank;
        Page page;
        string search = "";
        Vector2 scroll;

        [MenuItem("PirateSlop/Audio Bank")]
        static void Open()
        {
            GetWindow<AudioBankWindow>("Audio Bank");
        }

        void OnEnable()
        {
            bank = AssetDatabase.LoadAssetAtPath<GameAudioBank>(BankPath);
            EditorApplication.projectChanged += Repaint;
        }

        void OnDisable()
        {
            EditorApplication.projectChanged -= Repaint;
        }

        void OnGUI()
        {
            if (bank == null) bank = AssetDatabase.LoadAssetAtPath<GameAudioBank>(BankPath);
            if (bank == null)
            {
                EditorGUILayout.HelpBox("GameAudioBank.asset не найден в Assets/Resources.", MessageType.Error);
                return;
            }

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Game Audio Bank", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Найти asset", EditorStyles.toolbarButton))
            {
                Selection.activeObject = bank;
                EditorGUIUtility.PingObject(bank);
            }
            EditorGUILayout.EndHorizontal();

            if (EditorApplication.isPlaying)
                EditorGUILayout.HelpBox("Для изменения банка выйдите из Play Mode. Во время игры пользовательские настройки громкости могут перекрывать значения банка.", MessageType.Info);

            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                var serialized = new SerializedObject(bank);
                serialized.Update();
                EditorGUILayout.BeginHorizontal();
                DrawSidebar();
                scroll = EditorGUILayout.BeginScrollView(scroll);
                switch (page)
                {
                    case Page.Mix: DrawMix(serialized); break;
                    case Page.Ambience: DrawAmbience(serialized); break;
                    case Page.Music: DrawMusic(serialized); break;
                    default: DrawCues(serialized); break;
                }
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndHorizontal();
                if (serialized.ApplyModifiedProperties()) AssetDatabase.SaveAssets();
            }
        }

        void DrawSidebar()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(150));
            foreach (var item in pages)
            {
                if (GUILayout.Toggle(page == item, Title(item), "Button", GUILayout.Height(28)) && page != item)
                {
                    page = item;
                    scroll = Vector2.zero;
                    GUI.FocusControl(null);
                }
            }
            EditorGUILayout.EndVertical();
        }

        static string Title(Page item)
        {
            switch (item)
            {
                case Page.Mix: return "Общая громкость";
                case Page.Ambience: return "Окружение";
                case Page.Music: return "Музыка";
                case Page.Weapons: return "Оружие";
                case Page.Movement: return "Персонаж";
                case Page.Ship: return "Корабль";
                case Page.Fishing: return "Рыбалка";
                case Page.Items: return "Предметы / UI";
                default: return "Прочее";
            }
        }

        static void Field(SerializedObject serialized, string name, string label)
        {
            var property = serialized.FindProperty(name);
            if (property != null) EditorGUILayout.PropertyField(property, new GUIContent(label), true);
        }

        static void DrawMix(SerializedObject serialized)
        {
            EditorGUILayout.LabelField("Общая громкость", EditorStyles.boldLabel);
            Field(serialized, "Master", "Общая");
            Field(serialized, "Effects", "Эффекты");
            Field(serialized, "Ambience", "Окружение");
            Field(serialized, "Interface", "Интерфейс");
            Field(serialized, "Music", "Музыка");
            EditorGUILayout.HelpBox("Это базовые значения для игры. Настройки громкости игрока могут их перекрывать.", MessageType.None);
        }

        static void DrawAmbience(SerializedObject serialized)
        {
            EditorGUILayout.LabelField("Море и корабль", EditorStyles.boldLabel);
            Field(serialized, "Ocean", "Петля моря");
            Field(serialized, "OceanLevel", "Громкость моря");
            Field(serialized, "Wind", "Петля ветра");
            Field(serialized, "WindLevel", "Громкость ветра на полном ходу");
            Field(serialized, "DeckCreaks", "Петля палубы");
            Field(serialized, "DeckLevel", "Громкость петли палубы");
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Шторм и вода", EditorStyles.boldLabel);
            Field(serialized, "Storm", "Петля шторма");
            Field(serialized, "StormLevel", "Громкость шторма");
            Field(serialized, "StormThunder", "Варианты грома");
            Field(serialized, "ThunderLevel", "Громкость грома");
            Field(serialized, "RainLevel", "Громкость дождя");
            Field(serialized, "UnderwaterLevel", "Подводные пузыри");
            Field(serialized, "FloodingLevel", "Вода внутри корабля");
            EditorGUILayout.HelpBox("Ветер зависит от скорости корабля. Дождь создаётся кодом, поэтому у него нет файла для замены.", MessageType.None);
        }

        static void DrawMusic(SerializedObject serialized)
        {
            EditorGUILayout.LabelField("Музыка", EditorStyles.boldLabel);
            Field(serialized, "MainMenuMusic", "Меню");
            Field(serialized, "MainMenuMusicLevel", "Громкость меню");
            Field(serialized, "SailingMusic", "Плавание");
            Field(serialized, "SailingMusicLevel", "Громкость плавания");
            Field(serialized, "CombatMusic", "Бой");
            Field(serialized, "CombatMusicLevel", "Громкость боя");
        }

        void DrawCues(SerializedObject serialized)
        {
            EditorGUILayout.LabelField(Title(page), EditorStyles.boldLabel);
            search = EditorGUILayout.TextField("Поиск", search);
            var entries = serialized.FindProperty("Entries");
            var present = new HashSet<SoundCue>();
            int shown = 0;
            for (int i = 0; i < entries.arraySize; i++)
            {
                var element = entries.GetArrayElementAtIndex(i);
                var cue = (SoundCue)element.FindPropertyRelative("Cue").enumValueIndex;
                present.Add(cue);
                if (Category(cue) != page || !Matches(cue)) continue;
                shown++;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(ObjectNames.NicifyVariableName(cue.ToString()), EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(element.FindPropertyRelative("Clips"), new GUIContent("Клипы"), true);
                EditorGUILayout.PropertyField(element.FindPropertyRelative("Volume"), new GUIContent("Громкость"));
                EditorGUILayout.PropertyField(element.FindPropertyRelative("Distance"), new GUIContent("Дальность, м"));
                EditorGUILayout.EndVertical();
            }
            foreach (SoundCue cue in Enum.GetValues(typeof(SoundCue)))
            {
                if (present.Contains(cue) || Category(cue) != page || !Matches(cue)) continue;
                shown++;
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                GUILayout.Label(cue + " — нет записи в банке");
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Добавить", GUILayout.Width(85)))
                {
                    serialized.ApplyModifiedProperties();
                    AddCue(cue);
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndHorizontal();
            }
            if (shown == 0) EditorGUILayout.HelpBox("В этой категории пока нет звуков.", MessageType.Info);
        }

        bool Matches(SoundCue cue)
        {
            return string.IsNullOrWhiteSpace(search) || cue.ToString().IndexOf(search.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
        }

        void AddCue(SoundCue cue)
        {
            Undo.RecordObject(bank, "Add audio cue");
            var list = new List<GameAudioBank.Entry>(bank.Entries ?? Array.Empty<GameAudioBank.Entry>());
            list.Add(new GameAudioBank.Entry { Cue = cue, Clips = Array.Empty<AudioClip>(), Volume = .5f, Distance = 25f });
            bank.Entries = list.ToArray();
            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssets();
        }

        static Page Category(SoundCue cue)
        {
            switch (cue)
            {
                case SoundCue.Pistol: case SoundCue.Cannon: case SoundCue.Knife: case SoundCue.Reload:
                case SoundCue.ReloadReady: case SoundCue.DryFire: case SoundCue.Impact: case SoundCue.HitConfirm:
                case SoundCue.Musket: case SoundCue.DoubleBarrel: case SoundCue.BulletWood: case SoundCue.BulletMetal:
                case SoundCue.BulletStone: case SoundCue.BulletFlesh: case SoundCue.HookThrow:
                case SoundCue.HookTension: case SoundCue.HookRelease: case SoundCue.CannonFuse:
                case SoundCue.CannonballDispense: case SoundCue.CannonballRoll: case SoundCue.CannonballDrop:
                case SoundCue.FireCannonballHeld: case SoundCue.IceCannonballHeld: case SoundCue.PushCannonballHeld: return Page.Weapons;
                case SoundCue.Footstep: case SoundCue.Jump: case SoundCue.Land: case SoundCue.Slide:
                case SoundCue.Hurt: case SoundCue.Death: case SoundCue.Respawn: case SoundCue.FootstepWood:
                case SoundCue.FootstepWoodRun: case SoundCue.FootstepStone: case SoundCue.WaterSplash:
                case SoundCue.UnderwaterBubbles: case SoundCue.AirWarning: return Page.Movement;
                case SoundCue.Wheel: case SoundCue.Sail: case SoundCue.Barrel: case SoundCue.Load:
                case SoundCue.ShipHit: case SoundCue.ShipDeath: case SoundCue.ShipCollision:
                case SoundCue.Splash: case SoundCue.Creak: case SoundCue.ShipBell:
                case SoundCue.WheelReverseRope: case SoundCue.WheelIdleLeather: return Page.Ship;
                case SoundCue.FishingCast: case SoundCue.FishingBite: case SoundCue.FishingReel:
                case SoundCue.FishingCatch: case SoundCue.FishDrop: case SoundCue.FishEat:
                case SoundCue.FishingEscape: return Page.Fishing;
                case SoundCue.Pickup: case SoundCue.Place: case SoundCue.Select: case SoundCue.ChestOpen:
                case SoundCue.ChestClose: case SoundCue.SwordEquip: case SoundCue.SwordSheathe:
                case SoundCue.BottleOpen: case SoundCue.BottleClose: case SoundCue.PufferThrow:
                case SoundCue.PufferBurst: case SoundCue.SwordfishThrow: case SoundCue.SwordfishStick:
                case SoundCue.PufferWarning: case SoundCue.HolyFlash: return Page.Items;
                default: return Page.Other;
            }
        }
    }
}
