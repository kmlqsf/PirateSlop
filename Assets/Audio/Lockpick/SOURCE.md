# Lockpicking audio

All source recordings are CC0 1.0: https://creativecommons.org/publicdomain/zero/1.0/

| Imported files | Creator | Source |
| --- | --- | --- |
| PickMove01.wav, PickMove02.wav, PickMove03.wav | Tegurd | https://opengameart.org/content/lockpicking-sound |
| LockTurn.wav, LockJam01.wav, LockJam02.wav, LockSuccess.wav | Cough-E | https://opengameart.org/content/door-lock-sounds |
| PickBreak.wav | Kenney | https://kenney.nl/assets/rpg-audio |

Pick movement uses short excerpts starting at 22.76, 26.22 and 27.69 seconds of lockpicking.wav. LockTurn uses DoorLock.wav; jams use two excerpts of LockedDoorHandleJiggle.wav; success uses UnlockDoor.wav. PickBreak adapts the project's existing metalClick.ogg with increased pitch and a high-pass filter.

Effects are mono 44.1 kHz PCM WAV, trimmed, high-pass filtered, peak adjusted and given short edge fades. Start reuses Foley/metalLatch.ogg from Kenney RPG Audio. GameAudioBank controls six separate Lockpick cues. Movement/turn/jam play locally in the minigame with rate limits; start/break/success are server-confirmed spatial events for nearby observers.
