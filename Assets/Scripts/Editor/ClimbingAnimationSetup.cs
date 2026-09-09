using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace PirateSlop.EditorTools
{
    public static class ClimbingAnimationSetup
    {
        [MenuItem("PirateSlop/Configure Climbing Animation")]
        public static void Configure()
        {
            const string sourcePath = "Assets/Models/Characters/Pirate/LadderClimb.fbx";
            const string clipPath = "Assets/Animations/Player/LadderClimb.anim";
            var source = AssetDatabase.LoadAllAssetsAtPath(sourcePath).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, clipPath);
            }
            PirateAnimationRetargeter.Bake(sourcePath, source, clip, false);
            clip.name = "LadderClimb";
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Animations/Player/PiratePlayer.controller");
            if (!controller.parameters.Any(p => p.name == "ClimbSpeed"))
                controller.AddParameter("ClimbSpeed", AnimatorControllerParameterType.Float);
            var machine = controller.layers[0].stateMachine;
            var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "LadderClimb") ?? machine.AddState("LadderClimb");
            state.motion = clip;
            state.speedParameter = "ClimbSpeed";
            state.speedParameterActive = true;
            EditorUtility.SetDirty(clip);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }
    }
}
