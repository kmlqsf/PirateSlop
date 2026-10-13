# Звуки захвата зоны

Источник: SFXMint. Лицензия обоих оригиналов: CC0-1.0, https://creativecommons.org/publicdomain/zero/1.0/ . WAV сохранены без обработки; ослабление выполняется громкостью GameAudioBank и пространственным затуханием.

| Cue | Файл | Источник | SHA256 |
|---|---|---|---|
| CaptureStart | ui-chime-57.wav | https://sfxmint.com/sounds/ui-chime-57 | d68cf04d516332d90fd4ed95f998dc720899af8e5108daf693a0cc38cbc9d89c |
| CaptureContested | instrument-bell-36.wav | https://sfxmint.com/sounds/instrument-bell-36 | f9e69564fcfa9847b7e7fe23fec81925584f26173facc70585ddf193764199fb |
| CaptureVictory | feedback-success-01.wav | https://sfxmint.com/sounds/feedback-success-01 | fdbd1dd500b16c5f90294c72d427f28b468e1f48b0bbb70c2151b68538f83c63 |

CaptureStart: Gentle Two-Note Notification Chime 57, короткий двухнотный сигнал, повторяется каждые6с на всём протяжении захвата. CaptureContested: Deep Church-Style Single Bell Strike 36, низкий колокольный сигнал длительностью2с, повторяется каждые4.5с на всём протяжении спора. Подбор выполнен по метаданным библиотеки; субъективная мягкость и сочетаемость с морем/боем требуют прослушивания пользователем.

Состояние стабилизируется0.5с, затем выделенный источник повторяет соответствующий звук с паузами у центрального буйка. На24буйках нет дублей. При смене состояния, окончании, откате, открытии меню или выходе из слышимости прежний звук прекращается. Позднее подключение и вход в слышимость получают сопровождение текущего состояния. Дальность ограничена120м и радиусом зоны плюс35м. Используются общий Effects/Master, пространственное затухание и плавное нарастание громкости. GameAudioBank.Volume: захват0.18, спор0.22. Оригиналы WAV не изменены.

CaptureVictory: выбран пользователем вариант1 — Triumphant Short Level Up Fanfare 65, 2с, CC0-1.0. Файл feedback-level-up-65.wav, источник https://sfxmint.com/sounds/feedback-level-up-65 , SHA256 1cd4a8fbe7b136b0d4161558c7d743d6fcc9dd9256719ffe9d04900a703fa3d8. Прежний feedback-success-01.wav сохранён как неиспользуемый оригинал. Играет один раз у центра при переходе захвата из Locked в Rising/Ready, для находящихся в слышимости; завершённый ивент при late join не повторяет звук. Volume0.25, Distance120м. До победного сигнала прекращаются повторы захвата/спора. Субъективная оценка и игровое прослушивание остаются пользователю.
