# Battle Royale Zone VFX — implementation spec

## Visual objective

The zone must feel dangerous and physically present, but must **not erase the horizon**. The current failure mode is a large opaque/noisy grey mass that reads like a fullscreen texture rather than a world-space storm.

The target is a layered world-space storm curtain built around the gameplay circle:

1. **Storm wall mesh** — translucent cylindrical/annular curtain following the safe-zone radius.
2. **Sea contact band** — a narrow foam/spray ring where the wall intersects the ocean.
3. **Sparse volumetric/VFX particles** — spray, rain streaks, mist and occasional lightning inside the wall volume.
4. **Proximity feedback** — wind/audio/postFX ramp only near the border.
5. **Outside-zone treatment** — stronger fog/desaturation/vignette only when the player is outside.
6. **Gameplay circle logic** remains independent from rendering and drives the VFX through center/radius/progress.

## Important art rules

- No giant opaque background card.
- No fullscreen high-frequency noise.
- The player must be able to see ships, islands and the horizon through the wall from inside the safe zone.
- Use broad low-poly-friendly shapes and muted blue-grey values.
- No cyan neon. No mobile-RPG glow.
- Lightning should be sparse and localized to the wall volume.
- At the ocean intersection, use a thin visible foam line plus mist/spray, not a glowing ring.
- The storm should have parallax: at least two independent noise layers with different world scales/speeds.
- Prefer world-space movement (tangent around the ring + slight vertical rise) over camera-space scrolling.

## Suggested scene hierarchy

BRZoneVisualRoot
- ZoneWallRenderer
- SeaContactRing
- StormMistVFX
- StormSprayVFX
- LightningVFX
- ZoneAudio
- ZonePostFXController
- DebugGizmos

## Rendering approach

### Wall
Generate a ring/cylinder mesh procedurally in Unity so it can follow changing radius and center.
Recommended:
- 96–128 segments
- height 60–120 m depending on map scale
- thickness 8–20 m
- double-sided only if necessary
- vertex colors can encode height fade / inner-vs-outer face

Shader:
- transparent surface
- depth fade / soft intersections
- vertical alpha gradient
- large scrolling noise + detail noise
- low-frequency distortion
- muted blue-grey color
- clamp final alpha so it never becomes a solid wall from the safe side
- optional subtle fresnel, but keep it weak
- alpha should vary spatially; avoid uniform opacity

### Sea contact
Use a narrow annulus mesh at water level:
- 4–8 m width
- animated foam mask
- small vertical offset to avoid z-fighting
- mix foam + spray particles
- follow water height if the current water system exposes it

### Post FX
Do not apply strong fullscreen effects while safely inside the circle.
Drive by signed distance to border:
- > warningDistance inside: 0
- warningDistance → border: gentle wind/vignette/contrast
- outside: stronger cold tint, desaturation, fog, subtle vignette, camera shake only if already used elsewhere

## Gameplay integration

Create a clean data source/interface:
- Zone center (Vector3/XZ)
- Current radius
- Target center/radius
- Phase start/end time
- Shrink progress
- IsPlayerOutside
- DistanceToBorder

If existing BR logic already owns these values, use it instead of duplicating state.

Visuals must interpolate smoothly and must not be authoritative for damage.

## Networking

If multiplayer/networking exists:
- zone state/damage stays server authoritative
- clients interpolate visual center/radius
- do not network particles
- only replicate phase state that is already required by gameplay

## Debug tooling

Add an inspector/debug component:
- show current/target radius
- show distance to border
- phase slider/simulation
- toggle wall / foam / particles / postFX / lightning independently
- gizmos for current and target circles
- runtime sliders for wall opacity, height, foam width, warning distance

## Acceptance criteria

- From 200–600 m away, the storm boundary is clearly visible.
- From inside safe zone, world silhouettes/horizon remain readable through the wall.
- The zone looks like a world-space ring, not a camera overlay.
- Border intersection with the sea reads clearly.
- Visual intensity ramps as the player approaches/crosses the border.
- No strong fullscreen effect when far from the border.
- No large opaque grey texture wall.
- Current gameplay shrink timing and damage behavior remain intact unless a bug is discovered.
- Provide a fallback path if VFX Graph is unavailable.
- Keep all new assets/scripts under a dedicated BR zone folder.
