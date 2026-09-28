using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace PirateSlop.EditorTools
{
    public static class NewPirateAnimationBatch
    {
        const string FbxFolder = "Assets/Models/Characters/NewPirate";
        const string AnimFolder = "Assets/Animations/Player";
        const string ControllerPath = "Assets/Animations/Player/PiratePlayer.controller";

        [InitializeOnLoadMethod]
        static void AutoRunOnLoad()
        {
            if (SessionState.GetBool("NewPirateAnimFix_v3", false)) return;
            SessionState.SetBool("NewPirateAnimFix_v3", true);
            EditorApplication.delayCall += () =>
            {
                Import();
            };
        }

        [MenuItem("PirateSlop/Import All New Pirate Animations")]
        public static void Import()
        {
            try
            {
                Debug.Log("[NewPirateAnimationBatch] Starting animation import and fix...");

                // 1. Process all 13 FBX animations into .anim clips
                // Locomotion & Movement (Stage 1)
                ProcessSeamlessIdle();
                ProcessSabreReady();
                ProcessClip("Jumping Up.fbx", "New_JumpUp.anim", false, inPlace: true);
                ProcessClip("Falling Idle.fbx", "New_FallingIdle.anim", true, inPlace: true);
                // Trim Landing: starts when feet hit ground at 0.366s, dips knees, recovers to standing at 1.0s
                ProcessClip("Landing.fbx", "New_Landing.anim", false, inPlace: true, removeVerticalDrift: false, trimStart: 0.366f, trimEnd: 1.0f);
                ProcessClip("Crouching Idle.fbx", "New_CrouchIdle.anim", true, inPlace: true);
                ProcessClip("Crouched Walking.fbx", "New_CrouchedWalking.anim", true, inPlace: true);
                ProcessClip("Climbing Ladder.fbx", "New_LadderClimbUp.anim", true, inPlace: true, removeVerticalDrift: true, rotateY180: true);
                ProcessClip("Climbing Down Wall.fbx", "New_LadderClimbDown.anim", true, inPlace: true, removeVerticalDrift: true);
                ProcessClip("Treading Water.fbx", "New_TreadWater.anim", true, inPlace: true);
                ProcessClip("Swimming.fbx", "New_Swimming.anim", true, inPlace: true);

                // Melee Combat (Stage 2)
                ProcessClip("Stable Sword Outward Slash.fbx", "New_SwordSlash1.anim", false, inPlace: true);
                ProcessClip("Stable Sword Inward Slash.fbx", "New_SwordSlash2.anim", false, inPlace: true);

                // Firearms (Stage 3)
                ProcessClip("Pistol Aim.fbx", "New_PistolAim.anim", true, inPlace: true);
                ProcessClip("Shooting.fbx", "New_Shooting.anim", false, inPlace: true);

                AssetDatabase.SaveAssets();
                Debug.Log("[NewPirateAnimationBatch] All 13 clips processed successfully.");

                // 2. Configure Animator Controller
                ConfigureController();

                AssetDatabase.SaveAssets();
                Debug.Log("[NewPirateAnimationBatch] Controller configured successfully.");
            }
            catch (Exception ex)
            {
                Debug.LogError("[NewPirateAnimationBatch] Exception: " + ex);
                throw;
            }
        }

        static void ProcessSeamlessIdle()
        {
            string fbxPath = FbxFolder + "/Stop Walking.fbx";
            string animPath = AnimFolder + "/New_Idle.anim";
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            var srcClip = allAssets.OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (srcClip == null) return;
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(animPath);
            if (clip == null) { clip = new AnimationClip { name = "New_Idle" }; AssetDatabase.CreateAsset(clip, animPath); }
            clip.ClearCurves();
            clip.frameRate = 30f;
            float restT = 2.95f, clipDur = 2.0f;
            var bindings = AnimationUtility.GetCurveBindings(srcClip);
            foreach (var b in bindings)
            {
                var srcCurve = AnimationUtility.GetEditorCurve(srcClip, b);
                float restVal = srcCurve.Evaluate(restT);
                if (b.path == "mixamorig:Hips" && (b.propertyName.EndsWith("m_LocalPosition.x") || b.propertyName.EndsWith("m_LocalPosition.z")))
                    restVal = 0f;
                Keyframe[] keys;
                if (b.path.Contains("Spine") && b.propertyName.EndsWith("m_LocalRotation.x"))
                    keys = new[] { new Keyframe(0f, restVal, 0f, 0f), new Keyframe(clipDur * 0.5f, restVal + 0.005f, 0f, 0f), new Keyframe(clipDur, restVal, 0f, 0f) };
                else
                    keys = new[] { new Keyframe(0f, restVal, 0f, 0f), new Keyframe(clipDur, restVal, 0f, 0f) };
                clip.SetCurve(b.path, b.type, b.propertyName, new AnimationCurve(keys));
            }
            clip.EnsureQuaternionContinuity();
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
        }

        static void ProcessSabreReady()
        {
            string fbxPath = FbxFolder + "/Stable Sword Outward Slash.fbx";
            string animPath = AnimFolder + "/New_SabreReady.anim";
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            var srcClip = allAssets.OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (srcClip == null) return;
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(animPath);
            if (clip == null) { clip = new AnimationClip { name = "New_SabreReady" }; AssetDatabase.CreateAsset(clip, animPath); }
            clip.ClearCurves();
            clip.frameRate = 30f;
            float restT = 0.0f, clipDur = 1.5f;
            var bindings = AnimationUtility.GetCurveBindings(srcClip);
            foreach (var b in bindings)
            {
                var srcCurve = AnimationUtility.GetEditorCurve(srcClip, b);
                float restVal = srcCurve.Evaluate(restT);
                if (b.path == "mixamorig:Hips" && (b.propertyName.EndsWith("m_LocalPosition.x") || b.propertyName.EndsWith("m_LocalPosition.z")))
                    restVal = 0f;
                Keyframe[] keys;
                if (b.path.Contains("Spine") && b.propertyName.EndsWith("m_LocalRotation.x"))
                    keys = new[] { new Keyframe(0f, restVal, 0f, 0f), new Keyframe(clipDur * 0.5f, restVal + 0.005f, 0f, 0f), new Keyframe(clipDur, restVal, 0f, 0f) };
                else
                    keys = new[] { new Keyframe(0f, restVal, 0f, 0f), new Keyframe(clipDur, restVal, 0f, 0f) };
                clip.SetCurve(b.path, b.type, b.propertyName, new AnimationCurve(keys));
            }
            clip.EnsureQuaternionContinuity();
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
        }

        static void ProcessClip(string fbxName, string animName, bool loopTime, bool inPlace = true, bool removeVerticalDrift = false, float? trimStart = null, float? trimEnd = null, bool rotateY180 = false)
        {
            string fbxPath = FbxFolder + "/" + fbxName;
            string animPath = AnimFolder + "/" + animName;

            var allAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            var sourceClip = allAssets.OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (sourceClip == null)
            {
                Debug.LogError("No AnimationClip found in " + fbxPath);
                return;
            }

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(animPath);
            if (clip == null)
            {
                clip = new AnimationClip { name = Path.GetFileNameWithoutExtension(animName) };
                AssetDatabase.CreateAsset(clip, animPath);
            }
            clip.ClearCurves();
            clip.frameRate = sourceClip.frameRate;

            float t0 = trimStart ?? 0f;
            float t1 = trimEnd ?? sourceClip.length;
            float dur = t1 - t0;
            var bindings = AnimationUtility.GetCurveBindings(sourceClip);

            foreach (var b in bindings)
            {
                var srcCurve = AnimationUtility.GetEditorCurve(sourceClip, b);
                var keys = srcCurve.keys.Where(k => k.time >= t0 - 0.001f && k.time <= t1 + 0.001f).ToArray();
                if (keys == null || keys.Length == 0)
                {
                    keys = new[] { new Keyframe(0f, srcCurve.Evaluate(t0)), new Keyframe(dur, srcCurve.Evaluate(t1)) };
                }

                bool isHipZ = inPlace && b.path == "mixamorig:Hips" && b.propertyName == "m_LocalPosition.z";
                bool isHipX = inPlace && b.path == "mixamorig:Hips" && b.propertyName == "m_LocalPosition.x";
                bool isHipY = removeVerticalDrift && b.path == "mixamorig:Hips" && b.propertyName == "m_LocalPosition.y";

                float val0 = srcCurve.Evaluate(t0);
                float val1 = srcCurve.Evaluate(t1);
                var newKeys = new Keyframe[keys.Length];
                for (int i = 0; i < keys.Length; i++)
                {
                    float timeNorm = dur > 0.0001f ? (keys[i].time - t0) / dur : 0f;
                    float val = keys[i].value;
                    if (isHipZ || isHipX)
                    {
                        float drift = Mathf.Lerp(val0, val1, timeNorm);
                        val = val - drift;
                    }
                    else if (isHipY)
                    {
                        float drift = (val1 - val0) * timeNorm;
                        val = (val - val0 - drift) + 0.97f;
                    }
                    if (rotateY180 && (isHipX || isHipZ))
                        val = -val;
                    newKeys[i] = new Keyframe(keys[i].time - t0, val, keys[i].inTangent, keys[i].outTangent);
                }
                clip.SetCurve(b.path, b.type, b.propertyName, new AnimationCurve(newKeys));
            }

            if (rotateY180)
            {
                var rot180 = Quaternion.Euler(0, 180, 0);
                var bX = bindings.FirstOrDefault(b => b.path == "mixamorig:Hips" && b.propertyName.EndsWith("m_LocalRotation.x"));
                var bY = bindings.FirstOrDefault(b => b.path == "mixamorig:Hips" && b.propertyName.EndsWith("m_LocalRotation.y"));
                var bZ = bindings.FirstOrDefault(b => b.path == "mixamorig:Hips" && b.propertyName.EndsWith("m_LocalRotation.z"));
                var bW = bindings.FirstOrDefault(b => b.path == "mixamorig:Hips" && b.propertyName.EndsWith("m_LocalRotation.w"));
                if (bX.path != null && bY.path != null && bZ.path != null && bW.path != null)
                {
                    var cX = AnimationUtility.GetEditorCurve(clip, bX);
                    var cY = AnimationUtility.GetEditorCurve(clip, bY);
                    var cZ = AnimationUtility.GetEditorCurve(clip, bZ);
                    var cW = AnimationUtility.GetEditorCurve(clip, bW);
                    var nKX = new Keyframe[cX.length];
                    var nKY = new Keyframe[cY.length];
                    var nKZ = new Keyframe[cZ.length];
                    var nKW = new Keyframe[cW.length];
                    for (int i = 0; i < cX.length; i++)
                    {
                        var q = rot180 * new Quaternion(cX.keys[i].value, cY.keys[i].value, cZ.keys[i].value, cW.keys[i].value);
                        float t = cX.keys[i].time;
                        nKX[i] = new Keyframe(t, q.x); nKY[i] = new Keyframe(t, q.y);
                        nKZ[i] = new Keyframe(t, q.z); nKW[i] = new Keyframe(t, q.w);
                    }
                    clip.SetCurve(bX.path, bX.type, bX.propertyName, new AnimationCurve(nKX));
                    clip.SetCurve(bY.path, bY.type, bY.propertyName, new AnimationCurve(nKY));
                    clip.SetCurve(bZ.path, bZ.type, bZ.propertyName, new AnimationCurve(nKZ));
                    clip.SetCurve(bW.path, bW.type, bW.propertyName, new AnimationCurve(nKW));
                }
            }

            clip.EnsureQuaternionContinuity();
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loopTime;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            Debug.Log($"[NewPirateAnimationBatch] Processed {animName} (dur: {clip.length:F2}s, loop: {loopTime})");
        }

        static void ConfigureController()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                Debug.LogError("Controller not found at " + ControllerPath);
                return;
            }

            if (!controller.parameters.Any(p => p.name == "VerticalSpeed"))
                controller.AddParameter("VerticalSpeed", AnimatorControllerParameterType.Float);

            // Load clips
            var jumpUp = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimFolder + "/New_JumpUp.anim");
            var fallIdle = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimFolder + "/New_FallingIdle.anim");
            var land = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimFolder + "/New_Landing.anim");
            var crouchIdle = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimFolder + "/New_CrouchIdle.anim");
            var crouchWalk = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimFolder + "/New_CrouchedWalking.anim");
            var climbUp = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimFolder + "/New_LadderClimbUp.anim");
            var climbDown = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimFolder + "/New_LadderClimbDown.anim");
            var swim = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimFolder + "/New_Swimming.anim");
            var treadWater = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimFolder + "/New_TreadWater.anim");
            var slash1 = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimFolder + "/New_SwordSlash1.anim");
            var slash2 = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimFolder + "/New_SwordSlash2.anim");
            var pistolAim = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimFolder + "/New_PistolAim.anim");
            var shooting = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimFolder + "/New_Shooting.anim");
            var newIdle = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimFolder + "/New_Idle.anim");
            var sabreReady = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimFolder + "/New_SabreReady.anim");

            // --- 1. Base Layer ---
            var baseSm = controller.layers[0].stateMachine;

            // Crouch BlendTree
            var crouchState = baseSm.states.FirstOrDefault(s => s.state.name == "Crouch").state;
            if (crouchState != null)
            {
                var crouchTree = crouchState.motion as BlendTree;
                if (crouchTree != null)
                {
                    crouchTree.children = new ChildMotion[]
                    {
                        new ChildMotion { motion = crouchIdle, threshold = 0f, timeScale = 1f },
                        new ChildMotion { motion = crouchWalk, threshold = 2.5f, timeScale = 1f }
                    };
                    EditorUtility.SetDirty(crouchTree);
                }
            }

            // Swim & TreadWater
            var swimState = baseSm.states.FirstOrDefault(s => s.state.name == "Swim").state;
            if (swimState != null) { swimState.motion = swim; EditorUtility.SetDirty(swimState); }

            var treadState = baseSm.states.FirstOrDefault(s => s.state.name == "TreadWater").state;
            if (treadState != null) { treadState.motion = treadWater; EditorUtility.SetDirty(treadState); }

            // LadderClimb (Up & Down BlendTree)
            var ladderState = baseSm.states.FirstOrDefault(s => s.state.name == "LadderClimb").state;
            if (ladderState != null)
            {
                var ladderTree = new BlendTree
                {
                    name = "LadderClimbTree",
                    blendType = BlendTreeType.Simple1D,
                    blendParameter = "ClimbSpeed",
                    useAutomaticThresholds = false
                };
                ladderTree.AddChild(climbDown, -1f);
                ladderTree.AddChild(climbUp, 1f);
                if (!controller.parameters.Any(p => p.name == "ClimbMotionSpeed"))
                    controller.AddParameter("ClimbMotionSpeed", AnimatorControllerParameterType.Float);
                ladderState.motion = ladderTree;
                ladderState.speedParameterActive = true;
                ladderState.speedParameter = "ClimbMotionSpeed";
                EditorUtility.SetDirty(ladderState);
            }

            // Jump, Falling, Landing
            var jumpState = baseSm.states.FirstOrDefault(s => s.state.name == "Jump").state;
            if (jumpState != null)
            {
                jumpState.motion = jumpUp;
                jumpState.speed = 1f;
                jumpState.speedParameterActive = false;
                EditorUtility.SetDirty(jumpState);
            }

            var fallingState = baseSm.states.FirstOrDefault(s => s.state.name == "Falling").state;
            if (fallingState == null)
            {
                fallingState = baseSm.AddState("Falling", new Vector3(300, -50, 0));
            }
            fallingState.motion = fallIdle;
            EditorUtility.SetDirty(fallingState);

            var landingState = baseSm.states.FirstOrDefault(s => s.state.name == "Landing").state;
            if (landingState == null)
            {
                landingState = baseSm.AddState("Landing", new Vector3(500, -50, 0));
            }
            landingState.motion = land;
            EditorUtility.SetDirty(landingState);

            var locoState = baseSm.states.FirstOrDefault(s => s.state.name == "Locomotion").state;
            var crouchRef = baseSm.states.FirstOrDefault(s => s.state.name == "Crouch").state;
            var slideRef = baseSm.states.FirstOrDefault(s => s.state.name == "Slide").state;

            // Remove direct ground transitions from Jump (must go to Landing)
            foreach (var tr in jumpState.transitions.ToArray())
            {
                if (tr.destinationState != fallingState && tr.destinationState != landingState)
                    jumpState.RemoveTransition(tr);
            }

            // Jump -> Falling (when airborne time passes or VerticalSpeed <= 0)
            if (!jumpState.transitions.Any(t => t.destinationState == fallingState))
            {
                var t = jumpState.AddTransition(fallingState);
                t.hasExitTime = true;
                t.exitTime = 0.85f;
                t.duration = 0.1f;
                t.hasFixedDuration = true;
            }

            // Jump -> Landing (if hits ground early)
            if (!jumpState.transitions.Any(t => t.destinationState == landingState))
            {
                var t = jumpState.AddTransition(landingState);
                t.hasExitTime = false;
                t.duration = 0.08f;
                t.hasFixedDuration = true;
                t.AddCondition(AnimatorConditionMode.If, 0, "Grounded");
            }

            // Falling -> Landing (when grounded)
            if (!fallingState.transitions.Any(t => t.destinationState == landingState))
            {
                var t = fallingState.AddTransition(landingState);
                t.hasExitTime = false;
                t.duration = 0.08f;
                t.hasFixedDuration = true;
                t.AddCondition(AnimatorConditionMode.If, 0, "Grounded");
            }

            // Landing -> Locomotion (natural finish)
            var toLocoExit = landingState.transitions.FirstOrDefault(t => t.destinationState == locoState && t.hasExitTime);
            if (toLocoExit == null)
            {
                toLocoExit = landingState.AddTransition(locoState);
                toLocoExit.hasExitTime = true;
                toLocoExit.exitTime = 0.7f;
                toLocoExit.duration = 0.12f;
                toLocoExit.hasFixedDuration = true;
                toLocoExit.AddCondition(AnimatorConditionMode.If, 0, "Grounded");
                toLocoExit.AddCondition(AnimatorConditionMode.IfNot, 0, "Crouched");
            }
            // Landing -> Locomotion (instant cancel if player runs)
            var toLocoMove = landingState.transitions.FirstOrDefault(t => t.destinationState == locoState && !t.hasExitTime);
            if (toLocoMove == null)
            {
                toLocoMove = landingState.AddTransition(locoState);
                toLocoMove.hasExitTime = false;
                toLocoMove.duration = 0.08f;
                toLocoMove.hasFixedDuration = true;
                toLocoMove.AddCondition(AnimatorConditionMode.If, 0, "Grounded");
                toLocoMove.AddCondition(AnimatorConditionMode.Greater, 0.5f, "Speed");
            }
            // Landing -> Crouch
            if (crouchRef != null && !landingState.transitions.Any(t => t.destinationState == crouchRef))
            {
                var t = landingState.AddTransition(crouchRef);
                t.hasExitTime = false;
                t.duration = 0.08f;
                t.hasFixedDuration = true;
                t.AddCondition(AnimatorConditionMode.If, 0, "Grounded");
                t.AddCondition(AnimatorConditionMode.If, 0, "Crouched");
            }
            // Landing -> Slide
            if (slideRef != null && !landingState.transitions.Any(t => t.destinationState == slideRef))
            {
                var t = landingState.AddTransition(slideRef);
                t.hasExitTime = false;
                t.duration = 0.08f;
                t.hasFixedDuration = true;
                t.AddCondition(AnimatorConditionMode.If, 0, "Grounded");
                t.AddCondition(AnimatorConditionMode.If, 0, "Sliding");
            }

            // --- 2. SabreCombat Layer ---
            if (controller.layers.Length > 1)
            {
                var sabreSm = controller.layers[1].stateMachine;
                var readyState = sabreSm.states.FirstOrDefault(s => s.state.name == "Ready").state;
                if (readyState != null)
                {
                    // Default back to clean newIdle (layer weight only active during slash)
                    readyState.motion = newIdle;
                    EditorUtility.SetDirty(readyState);
                }

                var slashState = sabreSm.states.FirstOrDefault(s => s.state.name == "Slash").state;
                if (slashState != null)
                {
                    slashState.motion = slash1;
                    foreach (var tr in slashState.transitions)
                    {
                        if (tr.destinationState == readyState)
                        {
                            tr.hasExitTime = true;
                            tr.exitTime = 0.9f;
                            tr.duration = 0.2f;
                            tr.hasFixedDuration = true;
                        }
                    }
                    EditorUtility.SetDirty(slashState);
                }

                var slash2State = sabreSm.states.FirstOrDefault(s => s.state.name == "Slash2").state;
                if (slash2State == null)
                {
                    slash2State = sabreSm.AddState("Slash2", new Vector3(235, 130, 0));
                }
                slash2State.motion = slash2;
                if (readyState != null)
                {
                    var t = slash2State.transitions.FirstOrDefault(tr => tr.destinationState == readyState);
                    if (t == null) t = slash2State.AddTransition(readyState);
                    t.hasExitTime = true;
                    t.exitTime = 0.9f;
                    t.duration = 0.2f;
                    t.hasFixedDuration = true;
                }
                EditorUtility.SetDirty(slash2State);
            }

            // --- 3. ItemActions Layer ---
            if (controller.layers.Length > 2)
            {
                var itemSm = controller.layers[2].stateMachine;
                var musketAim = itemSm.states.FirstOrDefault(s => s.state.name == "MusketAim").state;
                if (musketAim != null) { musketAim.motion = pistolAim; EditorUtility.SetDirty(musketAim); }

                var musketFire = itemSm.states.FirstOrDefault(s => s.state.name == "MusketFire").state;
                if (musketFire != null) { musketFire.motion = shooting; EditorUtility.SetDirty(musketFire); }

                var shotgunAim = itemSm.states.FirstOrDefault(s => s.state.name == "ShotgunAim").state;
                if (shotgunAim != null) { shotgunAim.motion = pistolAim; EditorUtility.SetDirty(shotgunAim); }

                var shotgunFire = itemSm.states.FirstOrDefault(s => s.state.name == "ShotgunFire").state;
                if (shotgunFire != null) { shotgunFire.motion = shooting; EditorUtility.SetDirty(shotgunFire); }
            }

            EditorUtility.SetDirty(controller);
        }
    }
}
