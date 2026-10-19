# Kamikaze audio sources

Оригинальные записи Serious Sam: The First / Second Encounter, Croteam.

Источник: https://sounds.spriters-resource.com/pc_computer/serioussamfirstsecondencounters/asset/409006/

Архив Beheaded Sirians скачан 19.10.2026 через браузер. Автор публикации Cooper, contributor TGE. Это оригинальные игровые записи, не библиотека CC0; отдельное разрешение на распространение в составе другой игры в источнике не установлено.

| Файл проекта | Файл архива | Формат | Длительность | SHA-256 |
| --- | --- | --- | --- | --- |
| KamikazeScream.wav | Headman/AttackKamikaze.wav | Mono PCM16, 11025 Hz | 0.8005 s | 0361527057d4d807d7da94fe90fbf8ba3c07562bfd059b3ef28c5788cb406e4a |
| KamikazeIdle.wav | Headman/IdleKamikaze.wav | Mono PCM16, 11025 Hz | 0.9533 s | 8ca5845b11bda02dc4d09ecabd8e583daf059bc31fc5303ef64c9ab19963be51 |

Исходники без обработки: Art/Audio/Kamikaze. Импорт: PCM, DecompressOnLoad, PreserveSampleRate, mono. Дублирование AudioClip для каждого врага не требуется. Крик подключён одним пространственным looping AudioSource на живого атакующего врага; код останавливает его при смерти, потере цели и выгрузке объекта. Уровень громкости, затухание, общая громкость группы и шов петли требуют прослушивания в игре.

## Explosion02

Источник: https://sounds.spriters-resource.com/pc_computer/serioussamfirstsecondencounters/asset/408987/

Пакет Weapons скачан19.10.2026 через браузер. В проект перенесён только Weapons/Explosion02.wav как KamikazeExplosion.wav: monoPCM16,44100Hz,2.07031746s,SHA-25632780634beef8118436aa234cc171a8fbe2243835c7bffedb444f73ffa911ad7. Исходная копия Art/Audio/Kamikaze/Explosion02.wav. Происхождение и ограничение прав те же, что у записей выше.

Первичный референс выбора: Croteam-official/Serious-Engine, Sources/EntitiesMP/BasicEffects.es, SOUND_EXPLOSION ссылается на Sounds/Weapons/_Explosion02.wav. В архиве название без начального подчёркивания; полное тождество с файлом установленной версии игры отдельно не проверялось. Включён пространственный one-shot через общий пул голосов GameAudio и настройки Master/Effects.
