# Spyglass

Source: `Spyglass.blend`. Unity prefab: `Assets/Prefabs/Props/Spyglass.prefab`.

Faceted brass and dark wood from the project style palette, recessed opaque teal lenses. 4,128 triangles. Maximum diameter 0.104 m. Extended length 0.627 m; folded length 0.289 m.

The prefab is saved extended. The objective faces Unity +Z. The root and sliding pivots have identity rotations and unit scale. Animate only local Z on the nested transforms:

| Transform | Extended local Z | Folded local Z |
|---|---:|---:|
| Body/Slide_01 | -0.139 m | -0.014 m |
| Body/Slide_01/Slide_02 | -0.126 m | -0.014 m |
| Body/Slide_01/Slide_02/Slide_03 | -0.115 m | -0.014 m |

The hollow tubes retain overlap when extended. Collars remain outside their parent tubes when folded. Keep this hierarchy and do not combine moving meshes. No animation clips or gameplay behavior are included. The collider covers the fixed barrel only.

Blender slide travel uses local +Z; Unity import converts this to local -Z. Export selected objects as FBX, Forward -Z, Up Y, Apply Transform disabled, animation disabled. Keep the source root orientation; Unity imports it with identity rotation. The Unity importer uses Bake Axis Conversion.

Review appearance and manually move the three pivots between the listed endpoints in Prefab Mode.
