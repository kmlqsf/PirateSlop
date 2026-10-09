# Slot machine sound sources

Local sources selected explicitly by the user from their Sonniss GDC libraries. Original source files were not modified. Imported derivatives: mono, PCM16, 48000 Hz, cropped and peak-normalized with short fades.

| Asset | Source crop, seconds | Source |
|---|---|---|
| `Assets/Audio/SlotMachine/FishInsert.wav` | 0.260–1.260 | C:\звуки\Sonniss.com - GDC 2018 - Game Audio Bundle\Articulated Sounds - Bones & Blood - Gore Elements\GORE Wriggle tortured squishy slime.wav |
| `Assets/Audio/SlotMachine/LeverPull.wav` | 0.260–0.800 | C:\звуки\Sonniss.com - GDC 2017 - Game Audio Bundle\SoundHolder -  Cameras\camera kodak coloursnap 35 film advance lever fast mono.wav |
| `Assets/Audio/SlotMachine/ReelSpin.wav` | 0.160–5.060 | C:\звуки\Sonniss.com - GDC 2024 - Game Audio Bundle\Justsoundeffects - Steampunk Gadgets\MECHGear_Tiny Rotation 01_JSE_SG_Stereo.wav |
| `Assets/Audio/SlotMachine/ReelStop.wav` | 0.198–0.393 | C:\звуки\Sonniss.com - GDC 2018 - Game Audio Bundle\UberDuo - The Home Barista\Coffee, Grinder, Knob, Turn, Click X3.wav |
| `Assets/Audio/SlotMachine/LeverReturn.wav` | 0.035–0.655 | C:\звуки\Sonniss.com-GDC2026-GameAudioBundle\Sonic Bat - Music Boxes\SBmb_Music Box C Wind Up 006.wav |
| `Assets/Audio/SlotMachine/FishPayout.wav` | 0.000–0.720 | C:\звуки\Sonniss.com - GDC 2018 - Game Audio Bundle\Soundrangers - Hydrology Bubbles and Splashes\mud_splat_heavy_03.wav |
| `Assets/Audio/SlotMachine/PrizePayout.wav` | 0.190–1.790 | C:\звуки\Sonniss.com - GDC 2017 - Game Audio Bundle\The Soundcatcher - Electric Cash Register\Cash Register_SANYO ECR 335_Drawer_Open_Close_1_1.wav |
| `Assets/Audio/SlotMachine/Win.wav` | 0.010–2.730 | https://www.freesoundslibrary.com/wp-content/uploads/2026/06/jackpot-sound-effect.mp3 |

## Jackpot attribution

"Jackpot Sound Effect" by Free Sounds Library, https://www.freesoundslibrary.com/jackpot-sound-effect/ . Licensed under Creative Commons Attribution 4.0 International: https://creativecommons.org/licenses/by/4.0/ . Changes: cropped to 2.72 s, converted to mono WAV, peak normalization, fade-in/out. Used by both SlotWin and SlotJackpot.

SlotSkillPoint references the existing UpgradeAward clips in GameAudioBank. Personal point notification uses the regular RoguelikeUpgradeUI award sound, the same as chest rewards, once.

## Motion-driven lever ratchet

`LeverRatchet.wav` uses camera-lever source 0.393-0.439 s, one isolated mechanical click. Mono PCM16 48 kHz, peak -5 dBFS, 0.5 ms/8 ms fades. `SlotLeverPull` plays once per 6.5 degrees of forward manual travel. The earlier 0.54 s `LeverPull.wav` remains preserved but is no longer assigned.
