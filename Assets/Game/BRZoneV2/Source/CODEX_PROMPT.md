# TASK: Rebuild the Battle Royale shrinking-zone system visuals in Unity

You have MCP access to the Unity project and Blender. Work directly in the existing project.

The current battle-royale boundary is visually unacceptable: it appears as a huge opaque/blurred storm texture that destroys the horizon, depth, and readability of the low-poly pirate world. Replace it with a professional world-space storm boundary and preserve or improve the existing shrinking-zone gameplay mechanic.

## First: inspect before changing anything

Use the Unity MCP to inspect:
1. Which render pipeline is in use (Built-in / URP / HDRP).
2. Existing zone/shrink/damage scripts and where zone center/radius/phase timing come from.
3. Existing networking solution, if any.
4. Existing water implementation and whether water height can be queried.
5. Existing post-processing / volumes.
6. Current storm VFX/material/shader objects that produce the ugly effect.
7. Existing audio hooks for zone warning/damage.
8. Target scene and prefab organization.

Do not create a second competing gameplay zone manager if one already exists. Reuse the authoritative data source.

Before implementation, briefly report what you found and the concrete implementation plan. Then execute the plan.

## Target visual direction

Use the reference files included in the supplied BR Zone VFX Kit.

The zone should read as a **large translucent turbulent storm curtain in world space**, following the battle-royale circle. It must feel threatening without becoming an opaque wall.

Core visual layers:
- Translucent storm wall mesh around the circle.
- Narrow foam/spray band where the wall meets the ocean.
- Sparse mist/rain/spray particles contained around the wall.
- Occasional lightning inside the storm volume.
- Near-border wind/audio/postFX ramp.
- Stronger outside-zone grading/fog only after crossing the boundary.

Art direction:
- muted blue-grey storm colors
- low-poly-friendly broad forms
- no neon cyan
- no mobile-RPG glow
- no giant camera-facing texture card
- no fullscreen high-frequency noise
- horizon, ships and islands should remain visible through the boundary from inside the safe zone

## Recommended wall implementation

Prefer a procedural Unity ring/cylindrical mesh rather than Blender unless the current project architecture strongly favors authored meshes.

Suggested starting values:
- 96–128 segments
- wall height ~90 m
- thickness ~12 m
- radius driven directly by gameplay zone radius
- world-space noise scrolling around/tangent to the ring plus a slower vertical drift
- vertical alpha gradient
- depth fade / soft intersections
- two noise scales (large + detail)
- safe-side effective opacity mostly in the 0.22–0.48 range, never a solid sheet

If URP/HDRP is present, use Shader Graph where appropriate. If Shader Graph/VFX Graph is not installed or not appropriate, create a performant hand-written shader/particle fallback.

## Sea contact

Create a thin annulus at water height:
- 4–8 m width
- animated foam mask
- subtle mist/spray
- no bright glowing circle
- update position/radius with the gameplay zone
- if water height changes, bind to the project's water-height source

## Proximity feedback

Drive effects by signed distance from the player to the circle boundary.

Start with:
- warning distance: ~85 m
- strong proximity effects: inside ~28 m
- far inside safe zone: no fullscreen postFX
- close to border: gentle wind, spray, subtle vignette / contrast shift
- outside: colder/desaturated grade, mild fog, stronger wind/audio, damage feedback

Do not make the screen unreadable.

## Lightning

Lightning must be sparse:
- average interval roughly 5–12 seconds
- localized inside the storm wall
- no repeated full-screen white flashes
- keep screen flash extremely small or omit it
- prioritize silhouette/lightning visibility inside the wall instead

## Gameplay / shrinking mechanic

Preserve the current shrink phases and damage behavior unless bugs are found.

The visual layer must consume:
- current center
- current radius
- target center
- target radius
- phase start/end time
- shrink progress
- player distance to border / outside state

If these values are currently duplicated or scattered, introduce a small clean read-only interface/data component, but keep the authoritative gameplay state where it belongs.

For multiplayer:
- server remains authoritative for zone state/damage
- clients interpolate center/radius for visuals
- do not network particles or cosmetic noise state

## Debug / tuning tools

Add a debug component or inspector with:
- current and target circle gizmos
- current/target radius readout
- distance to border
- simulate phase / scrub shrink progress
- independent toggles for wall, foam, particles, postFX, lightning
- runtime tuning fields for wall height, opacity, warning distance, foam width, particle density

Use sensible serialized defaults based on `ZoneTuning.json`.

## Performance requirements

Avoid excessive transparent overdraw and fullscreen passes.
Target the new zone effect to stay near a ~2 ms GPU budget on the project's target quality level if profiler access is available.
Prefer one primary wall material, one sea-contact material, and controlled particle counts.
Do not spawn thousands of CPU particles around the full circumference if a cheaper solution exists.

## Deliverables inside the Unity project

Create a dedicated folder, for example:
`Assets/Game/BRZoneV2/`

Include:
- runtime scripts
- shaders / Shader Graphs
- materials
- VFX/particle prefabs
- zone visual prefab
- debug component
- short README explaining setup, dependencies and tuning fields

Replace/disable the existing ugly storm visual while keeping the previous assets available until the new system is verified.

## Validation

Test in Play Mode:
1. Player well inside safe zone.
2. Player 80 m from boundary.
3. Player 20 m from boundary.
4. Player crossing outside.
5. Zone actively shrinking.
6. Zone center moving, if supported.
7. Looking toward the wall over open ocean.
8. Looking toward ships/islands through the wall.
9. Low and high quality settings if the project supports them.

Final result must satisfy:
- storm boundary is obvious at distance
- horizon remains readable
- sea intersection looks intentional
- intensity grows as player approaches
- outside zone feels dangerous
- no giant opaque grey wall
- no broken gameplay timing/damage
- no unnecessary duplicated systems

Use Blender only if a custom authored mesh is genuinely beneficial. A procedural Unity ring is preferred for maintainability.
