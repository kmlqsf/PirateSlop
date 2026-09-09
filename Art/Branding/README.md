# PirateSlop branding

- `../../Assets/Branding/PirateSlopIcon.png`: master application icon, assigned to Unity Player Settings as the default icon. Standalone inherits it. It takes effect in the next Windows build; existing executables are unchanged.
- `PirateSlopLogo.png`: wooden title artwork master.
- `Steam/shortcut_icon_256.png`: Steam shortcut icon, 256 × 256 PNG.
- `Steam/app_icon_184.jpg`: Steam community/client app icon, 184 × 184 JPG.

The project currently initializes Steam with AppID 480 (Spacewar). Local icons cannot replace Spacewar's name or shared Steam artwork. To show PirateSlop to friends, use a dedicated PirateSlop Steamworks application, configure its name and upload these client icons under that application's graphical assets. The current AppID guard in SteamParty.cs and the 480 written by SteamTestBuild.cs must be updated together when that AppID is available. Do not substitute an unrelated game's AppID. No Steamworks settings were published in this task.

Steam specifications: https://partner.steamgames.com/doc/store/assets
AppID explanation: https://partner.steamgames.com/doc/sdk/api/example

Generated with the built-in image_gen tool using the two user-supplied reference images. Technical Steam-size exports preserve the square master composition.

## Generation prompts

Icon: Create a polished square 1024x1024 Windows game application icon for PirateSlop using the attached wooden pirate skull as reference. Single frontal wooden skull, bold red bandana, one broad dark iron eyepatch, large black eye socket, warm ivory wood teeth. Simplify strongly for legibility at 32 pixels, fewer large facets, crisp thick dark silhouette, premium stylized low-poly game rendering. Center skull fills 86 percent of square. Deep teal solid rounded square background with subtle light at center, clean corners, no text, no letters, no extra symbols, no border, no mockup. Keep the reference's wooden salvaged pirate personality but make clean iconic design.

Logo: Create a polished wide landscape game logo artwork for PirateSlop, 1536x1024. Reference 1 is inspiration for wooden lettering; reference 2 for wooden skull motif. Exact text only 'PIRATE SLOP' in two huge very readable centered lines with PIRATE above SLOP, hand crafted warm timber block letters reinforced with a few iron rivets, stylized low-poly 3D shading, broad clear shapes, cream highlights and dark brown outline. Small red-bandana wooden pirate skull crest centered above the lettering, short rope accent and modest ship wheel behind lower letters. Background full bleed deep teal with very subtle broad ocean swirls, warm amber rim light. Clean cohesive premium indie pirate game identity, not overly busy, no small tagline, no extra text, no watermark, no mockup. All emblem and letters contained in central 75 percent width and 85 percent height with generous clean teal margins, suitable for Steam library branding.
