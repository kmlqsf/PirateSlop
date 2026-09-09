using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace PirateSlop.EditorTools
{
    public static class CharacterActionImport
    {
        public static AnimationClip Import(string name, bool loop)
        {
            string sourcePath = "Assets/Models/Characters/Pirate/" + name + ".fbx";
            AssetDatabase.ImportAsset(sourcePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (ModelImporter)AssetImporter.GetAtPath(sourcePath);
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.SaveAndReimport();
            var source = AssetDatabase.LoadAllAssetsAtPath(sourcePath).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
            string path = "Assets/Animations/Player/" + name + ".anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, path);
            }
            PirateAnimationRetargeter.Bake(sourcePath, source, clip, false);
            clip.name = name;
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        [MenuItem("PirateSlop/Import Sabre Actions")]
        public static void ImportSabre()
        {
            var ready = Import("SabreReady", true);
            var slash = Import("SabreSlash", false);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Animations/Player/PiratePlayer.controller");
            var layer = controller.layers.First(l => l.name == "SabreCombat");
            var readyState = layer.stateMachine.states.First(s => s.state.name == "Ready").state;
            var slashState = layer.stateMachine.states.First(s => s.state.name == "Slash").state;
            readyState.motion = ready;
            slashState.motion = slash;
            slashState.speed = 1;
            foreach (var transition in slashState.transitions) slashState.RemoveTransition(transition);
            var exit = slashState.AddTransition(readyState);
            exit.hasExitTime = true;
            exit.exitTime = 1;
            exit.hasFixedDuration = true;
            exit.duration = .06f;
            const string playerPath = "Assets/Prefabs/Networking/NetworkPlayer.prefab";
            var player = PrefabUtility.LoadPrefabContents(playerPath);
            var source = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Characters/Pirate/SabreReady.fbx"));
            var target = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Characters/Pirate/PirateCharacter.fbx"));
            source.hideFlags = target.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var sourceBones = source.GetComponentsInChildren<Transform>().ToDictionary(t => t.name);
                var targetBones = target.GetComponentsInChildren<Transform>().ToDictionary(t => t.name);
                Vector3 sourceUp = sourceBones["Head"].position - sourceBones["Hips"].position;
                Vector3 targetUp = targetBones["Head"].position - targetBones["Hips"].position;
                var sourceBasis = Quaternion.LookRotation(Vector3.Cross(sourceBones["Thigh.L"].position - sourceBones["Thigh.R"].position, sourceUp), sourceUp);
                var targetBasis = Quaternion.LookRotation(Vector3.Cross(targetBones["Thigh.L"].position - targetBones["Thigh.R"].position, targetUp), targetUp);
                var alignment = targetBasis * Quaternion.Inverse(sourceBasis);
                float scale = targetUp.magnitude / sourceUp.magnitude;
                var raw = AssetDatabase.LoadAllAssetsAtPath("Assets/Models/Characters/Pirate/SabreReady.fbx").OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
                raw.SampleAnimation(source, 0);
                ready.SampleAnimation(target, 0);
                var socket = sourceBones["WeaponSocket_R"];
                var hand = targetBones["Hand.R"];
                Vector3 displacement = alignment * sourceBones["Root"].position * scale;
                displacement.y = 0;
                var animation = player.GetComponent<SabreAnimation>();
                animation.GripPosition = hand.InverseTransformPoint(alignment * socket.position * scale - displacement);
                animation.GripRotation = Quaternion.Inverse(hand.rotation) * alignment * socket.rotation;
                PrefabUtility.SaveAsPrefabAsset(player, playerPath);
            }
            finally
            {
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(target);
                PrefabUtility.UnloadPrefabContents(player);
            }
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }
    }
}
