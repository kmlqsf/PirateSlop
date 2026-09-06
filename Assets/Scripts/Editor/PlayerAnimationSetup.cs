using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PirateSlop.EditorTools
{
    public static class PlayerAnimationSetup
    {
        const string Folder = "Assets/Animations/Player";
        const string ControllerPath = Folder + "/PiratePlayer.controller";
        const string CharacterPath = "Assets/Prefabs/Characters/PirateCharacter.prefab";
        const string NetworkPath = "Assets/Prefabs/Networking/NetworkPlayer.prefab";

        [MenuItem("PirateSlop/Configure Player Animations")]
        public static void Configure()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            var player = GameObject.Find("PlayerCharacter");
            if (player == null) throw new InvalidOperationException("Open SampleScene first.");
            var avatar = AssetDatabase.LoadAllAssetsAtPath("Assets/Models/Characters/Pirate/PirateCharacter.fbx").OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isValid) throw new InvalidOperationException("Valid pirate Avatar required.");
            string[] names = { "Idle", "Running", "FastRun", "CrouchIdle", "CrouchedWalking", "Jumping", "RunningSlide" };
            var clips = names.Select((name, index) => PrepareClip(name, index < 5)).ToArray();
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var sm = controller.layers[0].stateMachine;
            foreach (var state in sm.states) sm.RemoveState(state.state);
            foreach (var transition in sm.anyStateTransitions) sm.RemoveAnyStateTransition(transition);
            foreach (var tree in AssetDatabase.LoadAllAssetsAtPath(ControllerPath).OfType<BlendTree>()) UnityEngine.Object.DestroyImmediate(tree, true);
            controller.parameters = Array.Empty<AnimatorControllerParameter>();
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Crouched", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Sliding", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            var locomotion = sm.AddState("Locomotion");
            locomotion.motion = Blend(controller, "Locomotion", new[] { clips[0], clips[1], clips[2] }, new[] { 0f, 5f, 8f });
            var crouch = sm.AddState("Crouch");
            crouch.motion = Blend(controller, "Crouch", new[] { clips[3], clips[4] }, new[] { 0f, 2.5f });
            var motor = new SerializedObject(player.GetComponent<AdvancedPlayerController>());
            float airTime = 2 * Mathf.Sqrt(2 * motor.FindProperty("jumpHeight").floatValue / -motor.FindProperty("gravity").floatValue);
            var jump = sm.AddState("Jump"); jump.motion = clips[5]; jump.speed = clips[5].length / Mathf.Max(.01f, airTime);
            var slide = sm.AddState("Slide"); slide.motion = clips[6]; slide.speed = clips[6].length / Mathf.Max(.01f, motor.FindProperty("slideDuration").floatValue);
            sm.defaultState = locomotion;
            var states = new[] { locomotion, crouch, jump, slide };
            foreach (var from in states)
                foreach (var to in states)
                {
                    if (from == to) continue;
                    var transition = from.AddTransition(to);
                    transition.hasExitTime = false; transition.hasFixedDuration = true; transition.duration = .08f;
                    Condition(transition, "Grounded", to != jump);
                    if (to != jump) Condition(transition, "Sliding", to == slide);
                    if (to == locomotion || to == crouch) Condition(transition, "Crouched", to == crouch);
                }
            ConfigurePrefab(CharacterPath, avatar, controller, false);
            ConfigurePrefab(NetworkPath, avatar, controller, true);
            Undo.RegisterFullObjectHierarchyUndo(player, "Configure player animations");
            ConfigurePlayer(player, avatar, controller);
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(player.scene);
            EditorSceneManager.SaveScene(player.scene);
            AssetDatabase.SaveAssets();
            Debug.Log("PLAYER_ANIMATIONS_CONFIGURED: SampleScene and NetworkPlayer, seven clips.");
        }
        static AnimationClip PrepareClip(string name, bool loop)
        {
            var source = AssetDatabase.LoadAllAssetsAtPath(Folder + "/" + name + ".fbx").OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
            string path = Folder + "/" + name + ".anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, path); }
            EditorUtility.CopySerialized(source, clip); clip.name = name;
            PirateAnimationRetargeter.Bake(Folder + "/" + name + ".fbx", source, clip, name == "Jumping");
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop; AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }
        static BlendTree Blend(AnimatorController controller, string name, AnimationClip[] clips, float[] thresholds)
        {
            var tree = new BlendTree { name = name, blendType = BlendTreeType.Simple1D, blendParameter = "Speed", useAutomaticThresholds = false };
            for (int i = 0; i < clips.Length; i++) tree.AddChild(clips[i], thresholds[i]);
            AssetDatabase.AddObjectToAsset(tree, controller); return tree;
        }
        static void Condition(AnimatorStateTransition transition, string name, bool value) => transition.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, name);
        static void ConfigurePrefab(string path, Avatar avatar, AnimatorController controller, bool player)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (player) ConfigurePlayer(root, avatar, controller);
                else ConfigureAnimator(root.GetComponent<Animator>(), avatar, controller);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        static void ConfigurePlayer(GameObject player, Avatar avatar, AnimatorController controller)
        {
            if (player.GetComponent<PlayerAnimatorDriver>() == null) player.AddComponent<PlayerAnimatorDriver>();
            var animator = player.GetComponentInChildren<Animator>(true);
            ConfigureAnimator(animator, avatar, controller);
            var visibility = animator.GetComponent<FirstPersonModelVisibility>();
            if (visibility == null) visibility = animator.gameObject.AddComponent<FirstPersonModelVisibility>();
            visibility.Configure(player.GetComponentInChildren<Camera>(true));
            if (PrefabUtility.IsPartOfPrefabInstance(animator)) PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
        }
        static void ConfigureAnimator(Animator animator, Avatar avatar, AnimatorController controller)
        {
            if (animator == null) throw new InvalidOperationException("Pirate Animator missing.");
            animator.avatar = avatar; animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false; animator.enabled = true;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
    }
}
