# Audio sources

The recordings in the table below are published under CC0 1.0: https://creativecommons.org/publicdomain/zero/1.0/

| Files | Author | Source |
|---|---|---|
| Foley/*.ogg | Kenney | https://kenney.nl/assets/rpg-audio |
| Foley/wheel_oak.wav | Joseph SARDIN | https://bigsoundbank.com/squeaking-parquet-6-s3056.html |
| Naval/cannon_*.ogg, Naval/ship_*.ogg | Thimras | https://opengameart.org/content/battle-at-sea |
| Naval/pistol_black_powder.wav | kurt | https://opengameart.org/node/70780 |
| Ambience/Wind*.ogg | IgnasD | https://opengameart.org/content/wind |
| Ambience/ocean.ogg | jasinski; submitted by qubodup | https://opengameart.org/content/beach-ocean-waves |

Pistol: black-powder shot lowered in pitch, filtered and layered with a short low-frequency body from Thimras's cannon recording, softly saturated and faded. Wheel: oak friction recording lowered in pitch, filtered and faded. Ocean: FLAC converted to Ogg with a crossfaded loop seam. Other clips retain source recordings; Unity applies mono import for effects and playback volume/pitch variation.

GameAudioBank in Assets/Resources controls clip assignments, cue volume, audible distance, master/effects/ambience/interface levels. GameAudio owns a pool of 32 spatial effects and two local ambience sources. Combat events use existing local/observer playback; movement and control foley follow replicated object state. Ship damage/destruction and collisions use server observer events. No gameplay or listening pass has been performed for this initial integration.

Storm/EvilStorm.wav and Storm/ThunderA.mp3, ThunderB.mp3, ThunderC.mp3 were supplied by the project owner from `game sounds` on 2026-09-27. The storm loop has a two-second seam crossfade. Thunder filenames supplied were `49053354-thunder-307513.mp3`, `tanweraman-thunder-strike-wav-321628.mp3`, and `u_vrs223ln83-loud-thunder-439064.mp3`; a duplicate copy of the second file was omitted. License and original source URLs were not supplied and should be recorded before redistribution.

Sea/SeaWaves.wav, Sea/ShipWind.ogg, Sea/ShipCreakA.mp3, Sea/ShipCreakB.mp3, Sea/ShipLeatherA.mp3, and Sea/ShipLeatherB.mp3 were supplied by the project owner from `game sounds/sea` on 2026-09-27. Original filenames: `mixkit-close-sea-waves-loop-1195.wav`, `soundreality-wind-blowing-457954.mp3`, `dragon-studio-floorboard-creak-02-499644.mp3`, `dragon-studio-heavy-wooden-creaking-sfx-515253.mp3`, `oxidvideos-rope-amp-leather-tension-449628.mp3`, and `oxidvideos-rope-amp-leather-tension-2-449631.mp3`. Sea waves and wind were looped with 1.5-second and 2-second seam crossfades respectively; wind was encoded to Ogg Vorbis. License and source URLs should be recorded before redistribution.

Wheel/WheelTurn01.wav through WheelTurn04.wav were cut from the four distinct squeaks in the owner-supplied `game sounds/wheel/o0specter0o-squeaky-wooden-wheel-chair-607908.mp3`, with short edge fades and 9 dB gain. Wheel/WheelRope01.mp3 through WheelRope09.mp3 and WheelRope14.mp3 came from the corresponding `floraphonic-rope-tighten-knot-*-1997*.mp3` files in the same folder. Wheel idle reuses Sea/ShipLeatherA.mp3 and Sea/ShipLeatherB.mp3, which the owner also supplied in `game sounds/wheel`. Source licenses and URLs should be recorded before redistribution.
# Cannonball audio (user supplied)

The cannon fire, fuse, dispenser, rolling, drop, and held elemental cannonball recordings came from `C:\Users\K\Desktop\game sounds\ядро`. `CannonballDispense.wav` plays at double speed. The long rolling recording was split into 12 randomized clips. `CannonFuse.wav` contains the beginning of the source and stops with the in-game fuse. `FireHeld.wav` and `PushHeld.wav` are shortened loops. Original creator and license details were not supplied.
`CannonFireLong01-03.wav` are extended-tail edits of the three supplied cannon fire recordings.
`CannonballDropTight.wav` trims the leading silence from the supplied cannonball drop recording to align its onset with deck contact.

# Lockpicking audio

Lockpick/PickMove01-03.wav derives from Tegurd, https://opengameart.org/content/lockpicking-sound (CC0). Lockpick/LockTurn.wav, LockJam01-02.wav and LockSuccess.wav derive from Cough-E, https://opengameart.org/content/door-lock-sounds (CC0). Lockpick/PickBreak.wav adapts Kenney RPG Audio metalClick.ogg; the start cue reuses metalLatch.ogg (CC0). Editing details are in Lockpick/SOURCE.md.
