# Audio sources

All downloaded recordings below are published under CC0 1.0: https://creativecommons.org/publicdomain/zero/1.0/

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
