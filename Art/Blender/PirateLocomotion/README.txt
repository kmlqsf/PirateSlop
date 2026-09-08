Corsair pirate variant, based on the project pirate topology and skeleton.
30 fps, meters, forward -Y in Blender.
Walk: frames 1-37, 1.2 seconds. Run: frames 1-25, 0.8 seconds. Final frame duplicates first.
Both are baked in-place clips. Root remains fixed. Match game speed to cadence to prevent sliding.
Walk reference speed 0.82 m/s; run 2.83 m/s. No root-motion translation.
FBX files include the skinned character and one clip each; BindPose FBX has no animation.
Use CorsairRig for shared avatar mapping when importing to Unity later. No Unity import performed.
Choose action in Blender Dope Sheet / Action Editor; set range 1-36 for walk or 1-24 for run playback.
